using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Core.Security;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsAiSettingsService : IBidOpsAiSettingsService
{
    private static readonly string[] SupportedReasoningEfforts = ["minimal", "low", "medium", "high", "xhigh"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string EncryptedSecretPrefix = "enc:v1:";
    private const int MaxRuntimeHttpApiKeyLength = 320;
    private const int AiTokenTestTimeoutSeconds = 30;
    private const int MaxTestResponsePreviewLength = 600;

    private readonly IRepository<BidOpsRuntimeSetting> _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentIdentity _identity;
    private readonly IIdGenerator _idGenerator;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICryptoService? _cryptoService;
    private readonly ILogger<BidOpsAiSettingsService> _logger;

    public BidOpsAiSettingsService(
        IRepository<BidOpsRuntimeSetting> settings,
        IUnitOfWork unitOfWork,
        ICurrentIdentity identity,
        IIdGenerator idGenerator,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<BidOpsAiSettingsService> logger,
        ICryptoService? cryptoService = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _cryptoService = cryptoService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BidOpsAiProviderSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var settings = await FindAiSettingsAsync(tracking: false, ct);
        return BuildDto(settings);
    }

    public async Task<BidOpsAiProviderSettingsDto> SetProviderAsync(
        UpdateBidOpsAiProviderRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var provider = NormalizeProvider(request.Provider);
        var now = DateTime.UtcNow;
        var settings = await FindAiSettingsAsync(tracking: true, ct);
        await UpsertSettingAsync(settings, BidOpsSystemValues.AiProviderRuntimeSettingKey, provider, now, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return BuildDto(settings);
    }

    public async Task<BidOpsAiProviderSettingsDto> SetCodexCliSettingsAsync(
        UpdateBidOpsCodexCliSettingsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await SetCodexCliScenarioSettingsAsync(
            new UpdateBidOpsCodexCliScenarioSettingsRequest
            {
                Scenario = BidOpsCodexCliScenarios.Default,
                Model = request.Model,
                ReasoningEffort = request.ReasoningEffort
            },
            ct);
    }

    public async Task<BidOpsAiProviderSettingsDto> SetCodexCliScenarioSettingsAsync(
        UpdateBidOpsCodexCliScenarioSettingsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scenario = NormalizeCodexCliScenario(request.Scenario);
        var keys = GetCodexCliScenarioKeys(scenario);
        var model = NormalizeModel(request.Model);
        var reasoningEffort = NormalizeReasoningEffort(request.ReasoningEffort, strict: true);
        var now = DateTime.UtcNow;
        var settings = await FindAiSettingsAsync(tracking: true, ct);

        await UpsertSettingAsync(settings, keys.ModelKey, model, now, ct);
        await UpsertSettingAsync(settings, keys.ReasoningEffortKey, reasoningEffort, now, ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return BuildDto(settings);
    }

    public async Task<BidOpsAiProviderSettingsDto> SetDeepSeekTokenAsync(
        UpdateBidOpsDeepSeekTokenRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var apiKey = NormalizeDeepSeekApiKey(request.ApiKey);
        var settings = await FindAiSettingsAsync(tracking: true, ct);
        await UpsertSettingAsync(
            settings,
            BidOpsSystemValues.DeepSeekApiKeyRuntimeSettingKey,
            ProtectSecret(apiKey),
            DateTime.UtcNow,
            ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return BuildDto(settings);
    }

    public async Task<BidOpsDeepSeekTokenTestResultDto> TestDeepSeekTokenAsync(
        TestBidOpsDeepSeekTokenRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var testApiKey = string.IsNullOrWhiteSpace(request.ApiKey)
            ? await GetEffectiveDeepSeekApiKeyAsync(ct)
            : NormalizeDeepSeekApiKey(request.ApiKey);

        if (!BidOpsAiHttpSettingsFactory.TryCreate(
            _configuration,
            BidOpsAiUse.NoticeStaging,
            BidOpsSystemValues.AiProviderDeepSeek,
            testApiKey,
            requireEnabled: false,
            out var settings))
        {
            var diagnostics = BidOpsAiHttpSettingsFactory.Diagnose(
                _configuration,
                BidOpsAiUse.NoticeStaging,
                BidOpsSystemValues.AiProviderDeepSeek,
                testApiKey);
            return new BidOpsDeepSeekTokenTestResultDto
            {
                Provider = diagnostics.Provider,
                StatusCode = 0,
                Message = BuildDeepSeekConnectivityUnavailableMessage(diagnostics)
            };
        }

        var requestBody = new Dictionary<string, object?>
        {
            ["model"] = settings.Model,
            ["temperature"] = 0,
            ["max_tokens"] = 8,
            ["messages"] = new object[]
            {
                new
                {
                    role = "user",
                    content = "ping"
                }
            }
        };
        var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(AiTokenTestTimeoutSeconds));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(BidOpsAiSettingsService));
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

            using var response = await client.SendAsync(httpRequest, timeoutCts.Token);
            var responseText = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            stopwatch.Stop();
            return new BidOpsDeepSeekTokenTestResultDto
            {
                Succeeded = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                Provider = settings.Provider,
                Model = settings.Model,
                Endpoint = FormatEndpointForDto(settings.Endpoint),
                Message = response.IsSuccessStatusCode ? "DeepSeek token test succeeded." : "DeepSeek token test failed.",
                ResponsePreview = Truncate(responseText, MaxTestResponsePreviewLength)
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new BidOpsDeepSeekTokenTestResultDto
            {
                StatusCode = 0,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                Provider = settings.Provider,
                Model = settings.Model,
                Endpoint = FormatEndpointForDto(settings.Endpoint),
                Message = $"DeepSeek token test timed out after {AiTokenTestTimeoutSeconds} seconds."
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            return new BidOpsDeepSeekTokenTestResultDto
            {
                StatusCode = 0,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                Provider = settings.Provider,
                Model = settings.Model,
                Endpoint = FormatEndpointForDto(settings.Endpoint),
                Message = $"DeepSeek token test request failed: {ex.Message}"
            };
        }
    }

    public async Task<BidOpsAiProviderSettingsDto> SetMimoTokenAsync(
        UpdateBidOpsMimoTokenRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var apiKey = NormalizeMimoApiKey(request.ApiKey);
        var settings = await FindAiSettingsAsync(tracking: true, ct);
        await UpsertSettingAsync(
            settings,
            BidOpsSystemValues.MimoApiKeyRuntimeSettingKey,
            ProtectSecret(apiKey),
            DateTime.UtcNow,
            ct);

        await _unitOfWork.SaveChangesAsync(ct);
        return BuildDto(settings);
    }

    public async Task<BidOpsMimoTokenTestResultDto> TestMimoTokenAsync(
        TestBidOpsMimoTokenRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var testApiKey = string.IsNullOrWhiteSpace(request.ApiKey)
            ? await GetEffectiveMimoApiKeyAsync(ct)
            : NormalizeMimoApiKey(request.ApiKey);

        if (!BidOpsAiHttpSettingsFactory.TryCreate(
            _configuration,
            BidOpsAiUse.NoticeStaging,
            BidOpsSystemValues.AiProviderMimo,
            testApiKey,
            requireEnabled: false,
            out var settings))
        {
            var diagnostics = BidOpsAiHttpSettingsFactory.Diagnose(
                _configuration,
                BidOpsAiUse.NoticeStaging,
                BidOpsSystemValues.AiProviderMimo,
                testApiKey);
            return new BidOpsMimoTokenTestResultDto
            {
                Provider = diagnostics.Provider,
                StatusCode = 0,
                Message = BuildMimoConnectivityUnavailableMessage(diagnostics)
            };
        }

        var requestBody = new Dictionary<string, object?>
        {
            ["model"] = settings.Model,
            ["temperature"] = 0,
            ["max_tokens"] = 8,
            ["messages"] = new object[]
            {
                new
                {
                    role = "user",
                    content = "ping"
                }
            }
        };
        var requestJson = JsonSerializer.Serialize(requestBody, JsonOptions);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(AiTokenTestTimeoutSeconds));

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var client = _httpClientFactory.CreateClient(nameof(BidOpsAiSettingsService));
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint)
            {
                Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

            using var response = await client.SendAsync(httpRequest, timeoutCts.Token);
            var responseText = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            stopwatch.Stop();
            return new BidOpsMimoTokenTestResultDto
            {
                Succeeded = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                Provider = settings.Provider,
                Model = settings.Model,
                Endpoint = FormatEndpointForDto(settings.Endpoint),
                Message = response.IsSuccessStatusCode ? "Mimo token test succeeded." : "Mimo token test failed.",
                ResponsePreview = Truncate(responseText, MaxTestResponsePreviewLength)
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new BidOpsMimoTokenTestResultDto
            {
                StatusCode = 0,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                Provider = settings.Provider,
                Model = settings.Model,
                Endpoint = FormatEndpointForDto(settings.Endpoint),
                Message = $"Mimo token test timed out after {AiTokenTestTimeoutSeconds} seconds."
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            return new BidOpsMimoTokenTestResultDto
            {
                StatusCode = 0,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                Provider = settings.Provider,
                Model = settings.Model,
                Endpoint = FormatEndpointForDto(settings.Endpoint),
                Message = $"Mimo token test request failed: {ex.Message}"
            };
        }
    }

    public async Task<string> GetEffectiveProviderAsync(CancellationToken ct = default)
    {
        try
        {
            var setting = await FindSettingAsync(BidOpsSystemValues.AiProviderRuntimeSettingKey, tracking: false, ct);
            if (setting != null && !string.IsNullOrWhiteSpace(setting.SettingValue))
                return NormalizeProvider(setting.SettingValue);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "BidOps AI runtime provider setting could not be read; appsettings provider will be used.");
        }

        return NormalizeProvider(GetConfiguredProvider());
    }

    public async Task<string> GetEffectiveDeepSeekApiKeyAsync(CancellationToken ct = default)
    {
        try
        {
            var setting = await FindSettingAsync(BidOpsSystemValues.DeepSeekApiKeyRuntimeSettingKey, tracking: false, ct);
            var runtimeApiKey = ReadStoredSecret(setting);
            if (!string.IsNullOrWhiteSpace(runtimeApiKey))
                return runtimeApiKey;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "BidOps DeepSeek runtime token could not be read; appsettings or environment token will be used.");
        }

        return ResolveConfiguredDeepSeekApiKey();
    }

    public async Task<string> GetEffectiveMimoApiKeyAsync(CancellationToken ct = default)
    {
        try
        {
            var setting = await FindSettingAsync(BidOpsSystemValues.MimoApiKeyRuntimeSettingKey, tracking: false, ct);
            var runtimeApiKey = ReadStoredSecret(setting);
            if (!string.IsNullOrWhiteSpace(runtimeApiKey))
                return runtimeApiKey;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "BidOps Mimo runtime token could not be read; appsettings or environment token will be used.");
        }

        return ResolveConfiguredMimoApiKey();
    }

    public async Task<BidOpsCodexCliRuntimeSettingsDto> GetEffectiveCodexCliSettingsAsync(
        string scenario = BidOpsCodexCliScenarios.Default,
        CancellationToken ct = default)
    {
        var normalizedScenario = NormalizeCodexCliScenario(scenario);
        try
        {
            var settings = await FindAiSettingsAsync(tracking: false, ct);
            return new BidOpsCodexCliRuntimeSettingsDto
            {
                Scenario = normalizedScenario,
                Model = ResolveCodexCliModel(settings, normalizedScenario),
                ReasoningEffort = ResolveCodexCliReasoningEffort(settings, normalizedScenario)
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "BidOps Codex CLI runtime settings could not be read; appsettings model and reasoning effort will be used.");
            var empty = new Dictionary<string, BidOpsRuntimeSetting>();
            return new BidOpsCodexCliRuntimeSettingsDto
            {
                Scenario = normalizedScenario,
                Model = ResolveCodexCliModel(empty, normalizedScenario),
                ReasoningEffort = ResolveCodexCliReasoningEffort(empty, normalizedScenario)
            };
        }
    }

    private async Task<Dictionary<string, BidOpsRuntimeSetting>> FindAiSettingsAsync(
        bool tracking,
        CancellationToken ct)
    {
        var keys = new[]
        {
            BidOpsSystemValues.AiProviderRuntimeSettingKey,
            BidOpsSystemValues.CodexCliModelRuntimeSettingKey,
            BidOpsSystemValues.CodexCliReasoningEffortRuntimeSettingKey,
            BidOpsSystemValues.CodexCliComplexModelRuntimeSettingKey,
            BidOpsSystemValues.CodexCliComplexReasoningEffortRuntimeSettingKey,
            BidOpsSystemValues.CodexCliManualReparseModelRuntimeSettingKey,
            BidOpsSystemValues.CodexCliManualReparseReasoningEffortRuntimeSettingKey,
            BidOpsSystemValues.CodexCliReviewerPromptModelRuntimeSettingKey,
            BidOpsSystemValues.CodexCliReviewerPromptReasoningEffortRuntimeSettingKey,
            BidOpsSystemValues.DeepSeekApiKeyRuntimeSettingKey,
            BidOpsSystemValues.MimoApiKeyRuntimeSettingKey
        };
        var query = tracking
            ? await _settings.QueryTrackingAsync(ct)
            : await _settings.QueryAsync(ct);

        var rows = await query
            .Where(x => keys.Contains(x.SettingKey))
            .ToListAsync(ct);

        return rows
            .GroupBy(x => x.SettingKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.UpdatedAt ?? y.CreatedAt).First(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<BidOpsRuntimeSetting?> FindSettingAsync(
        string key,
        bool tracking,
        CancellationToken ct)
    {
        var query = tracking
            ? await _settings.QueryTrackingAsync(ct)
            : await _settings.QueryAsync(ct);

        return await query
            .Where(x => x.SettingKey == key)
            .FirstOrDefaultAsync(ct);
    }

    private async Task UpsertSettingAsync(
        IDictionary<string, BidOpsRuntimeSetting> settings,
        string key,
        string value,
        DateTime now,
        CancellationToken ct)
    {
        if (!settings.TryGetValue(key, out var setting))
        {
            setting = new BidOpsRuntimeSetting
            {
                Id = _idGenerator.NextId(),
                SettingKey = key,
                SettingValue = value,
                UpdatedByUserName = _identity.UserName,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _settings.AddAsync(setting, ct);
            settings[key] = setting;
            return;
        }

        setting.SettingValue = value;
        setting.UpdatedByUserName = _identity.UserName;
        setting.UpdatedAt = now;
    }

    private BidOpsAiProviderSettingsDto BuildDto(IReadOnlyDictionary<string, BidOpsRuntimeSetting> settings)
    {
        settings.TryGetValue(BidOpsSystemValues.AiProviderRuntimeSettingKey, out var providerSetting);
        settings.TryGetValue(BidOpsSystemValues.CodexCliModelRuntimeSettingKey, out var modelSetting);
        settings.TryGetValue(BidOpsSystemValues.CodexCliReasoningEffortRuntimeSettingKey, out var reasoningSetting);
        settings.TryGetValue(BidOpsSystemValues.CodexCliComplexModelRuntimeSettingKey, out var complexModelSetting);
        settings.TryGetValue(BidOpsSystemValues.CodexCliComplexReasoningEffortRuntimeSettingKey, out var complexReasoningSetting);
        settings.TryGetValue(BidOpsSystemValues.CodexCliManualReparseModelRuntimeSettingKey, out var manualReparseModelSetting);
        settings.TryGetValue(BidOpsSystemValues.CodexCliManualReparseReasoningEffortRuntimeSettingKey, out var manualReparseReasoningSetting);
        settings.TryGetValue(BidOpsSystemValues.CodexCliReviewerPromptModelRuntimeSettingKey, out var reviewerPromptModelSetting);
        settings.TryGetValue(BidOpsSystemValues.CodexCliReviewerPromptReasoningEffortRuntimeSettingKey, out var reviewerPromptReasoningSetting);
        settings.TryGetValue(BidOpsSystemValues.DeepSeekApiKeyRuntimeSettingKey, out var deepSeekApiKeySetting);
        settings.TryGetValue(BidOpsSystemValues.MimoApiKeyRuntimeSettingKey, out var mimoApiKeySetting);

        var configuredProvider = NormalizeProvider(GetConfiguredProvider());
        var runtimeProvider = providerSetting == null ? string.Empty : NormalizeProvider(providerSetting.SettingValue);
        var effectiveProvider = string.IsNullOrWhiteSpace(runtimeProvider)
            ? configuredProvider
            : runtimeProvider;
        var providerSource = string.IsNullOrWhiteSpace(runtimeProvider) ? "Configuration" : "Runtime";
        var deepSeekModel = ResolveHttpModel(BidOpsSystemValues.AiProviderDeepSeek);
        var mimoModel = ResolveHttpModel(BidOpsSystemValues.AiProviderMimo);
        var codexCliModel = ResolveCodexCliModel(settings, BidOpsCodexCliScenarios.Default);
        var codexCliReasoningEffort = ResolveCodexCliReasoningEffort(settings, BidOpsCodexCliScenarios.Default);
        var runtimeDeepSeekApiKey = ReadStoredSecret(deepSeekApiKeySetting);
        var configuredDeepSeekApiKey = ResolveConfiguredDeepSeekApiKey();
        var effectiveDeepSeekApiKey = FirstNonEmpty(runtimeDeepSeekApiKey, configuredDeepSeekApiKey);
        var deepSeekTokenSource = !string.IsNullOrWhiteSpace(runtimeDeepSeekApiKey)
            ? "Runtime"
            : ResolveConfiguredDeepSeekApiKeySource();
        var runtimeMimoApiKey = ReadStoredSecret(mimoApiKeySetting);
        var configuredMimoApiKey = ResolveConfiguredMimoApiKey();
        var effectiveMimoApiKey = FirstNonEmpty(runtimeMimoApiKey, configuredMimoApiKey);
        var mimoTokenSource = !string.IsNullOrWhiteSpace(runtimeMimoApiKey)
            ? "Runtime"
            : ResolveConfiguredMimoApiKeySource();
        var latestSetting = GetLatestSetting(
            providerSetting,
            modelSetting,
            reasoningSetting,
            complexModelSetting,
            complexReasoningSetting,
            manualReparseModelSetting,
            manualReparseReasoningSetting,
            reviewerPromptModelSetting,
            reviewerPromptReasoningSetting,
            deepSeekApiKeySetting,
            mimoApiKeySetting);

        return new BidOpsAiProviderSettingsDto
        {
            Enabled = _configuration.GetValue<bool>("BidOps:Ai:Enabled"),
            NoticeStagingEnabled = _configuration.GetValue<bool?>("BidOps:Ai:UseForNoticeStaging") ?? true,
            OutcomeSuppliersEnabled = _configuration.GetValue<bool?>("BidOps:Ai:UseForOutcomeSuppliers") ?? true,
            ConfiguredProvider = configuredProvider,
            RuntimeProvider = runtimeProvider,
            EffectiveProvider = effectiveProvider,
            ProviderSource = providerSource,
            EffectiveModel = ResolveEffectiveModel(effectiveProvider, deepSeekModel, mimoModel, codexCliModel),
            ReasoningEffort = effectiveProvider.Equals(BidOpsSystemValues.AiProviderCodexCli, StringComparison.OrdinalIgnoreCase)
                ? codexCliReasoningEffort
                : string.Empty,
            DeepSeekModel = deepSeekModel,
            DeepSeekTokenConfigured = !string.IsNullOrWhiteSpace(effectiveDeepSeekApiKey),
            DeepSeekTokenSource = deepSeekTokenSource,
            DeepSeekTokenMasked = MaskSecret(effectiveDeepSeekApiKey),
            DeepSeekTokenUpdatedAt = deepSeekApiKeySetting?.UpdatedAt ?? deepSeekApiKeySetting?.CreatedAt,
            DeepSeekTokenUpdatedByUserName = deepSeekApiKeySetting?.UpdatedByUserName ?? string.Empty,
            MimoModel = mimoModel,
            MimoTokenConfigured = !string.IsNullOrWhiteSpace(effectiveMimoApiKey),
            MimoTokenSource = mimoTokenSource,
            MimoTokenMasked = MaskSecret(effectiveMimoApiKey),
            MimoTokenUpdatedAt = mimoApiKeySetting?.UpdatedAt ?? mimoApiKeySetting?.CreatedAt,
            MimoTokenUpdatedByUserName = mimoApiKeySetting?.UpdatedByUserName ?? string.Empty,
            CodexCliModel = codexCliModel,
            CodexCliReasoningEffort = codexCliReasoningEffort,
            CodexCliModelSource = modelSetting == null || string.IsNullOrWhiteSpace(modelSetting.SettingValue)
                ? "Configuration"
                : "Runtime",
            CodexCliReasoningEffortSource = reasoningSetting == null || string.IsNullOrWhiteSpace(reasoningSetting.SettingValue)
                ? "Configuration"
                : "Runtime",
            CodexCliScenarios =
            [
                BuildCodexCliScenario(settings, BidOpsCodexCliScenarios.Default),
                BuildCodexCliScenario(settings, BidOpsCodexCliScenarios.Complex),
                BuildCodexCliScenario(settings, BidOpsCodexCliScenarios.ManualReparse),
                BuildCodexCliScenario(settings, BidOpsCodexCliScenarios.ReviewerPrompt)
            ],
            UpdatedAt = latestSetting?.UpdatedAt ?? latestSetting?.CreatedAt,
            UpdatedByUserName = latestSetting?.UpdatedByUserName ?? string.Empty,
            Options =
            [
                BuildDeepSeekOption(deepSeekModel, effectiveDeepSeekApiKey),
                BuildMimoOption(mimoModel, effectiveMimoApiKey),
                BuildCodexCliOption(codexCliModel, codexCliReasoningEffort)
            ]
        };
    }

    private static string ResolveEffectiveModel(
        string provider,
        string deepSeekModel,
        string mimoModel,
        string codexCliModel)
    {
        if (provider.Equals(BidOpsSystemValues.AiProviderCodexCli, StringComparison.OrdinalIgnoreCase))
            return codexCliModel;

        if (provider.Equals(BidOpsSystemValues.AiProviderMimo, StringComparison.OrdinalIgnoreCase))
            return mimoModel;

        return deepSeekModel;
    }

    private BidOpsAiProviderOptionDto BuildDeepSeekOption(string model, string apiKeyOverride)
    {
        var diagnostics = BidOpsAiHttpSettingsFactory.Diagnose(
            _configuration,
            BidOpsAiUse.NoticeStaging,
            BidOpsSystemValues.AiProviderDeepSeek,
            apiKeyOverride);
        var available = diagnostics.Enabled &&
            diagnostics.UseEnabled &&
            diagnostics.SupportedProvider &&
            diagnostics.HasApiKey &&
            diagnostics.HasModel &&
            diagnostics.HasEndpoint;

        return new BidOpsAiProviderOptionDto
        {
            Provider = BidOpsSystemValues.AiProviderDeepSeek,
            Label = "DeepSeek",
            Description = "使用 DeepSeek/OpenAI-compatible HTTP 接口。",
            Model = model,
            Available = available,
            AvailabilityMessage = available
                ? "配置可用"
                : "请检查 BidOps:Ai / BidOps:DeepSeek 的 API Key、Endpoint 和启用状态。"
        };
    }

    private BidOpsAiProviderOptionDto BuildMimoOption(string model, string apiKeyOverride)
    {
        var diagnostics = BidOpsAiHttpSettingsFactory.Diagnose(
            _configuration,
            BidOpsAiUse.NoticeStaging,
            BidOpsSystemValues.AiProviderMimo,
            apiKeyOverride);
        var available = diagnostics.Enabled &&
            diagnostics.UseEnabled &&
            diagnostics.SupportedProvider &&
            diagnostics.HasApiKey &&
            diagnostics.HasModel &&
            diagnostics.HasEndpoint;

        return new BidOpsAiProviderOptionDto
        {
            Provider = BidOpsSystemValues.AiProviderMimo,
            Label = "Mimo",
            Description = "使用小米 Mimo OpenAI-compatible HTTP 接口。",
            Model = model,
            Available = available,
            AvailabilityMessage = available
                ? "配置可用"
                : "请检查运行时 Mimo token、BidOps:Mimo / MIMO_API_KEY 和 BidOps:Ai 启用状态。"
        };
    }

    private static BidOpsAiProviderOptionDto BuildCodexCliOption(string model, string reasoningEffort)
    {
        return new BidOpsAiProviderOptionDto
        {
            Provider = BidOpsSystemValues.AiProviderCodexCli,
            Label = "Codex CLI",
            Description = "通过本机 codex exec 执行结构化抽取，可通过 BidOps:CodexCli 配置模型。",
            Model = model,
            ReasoningEffort = reasoningEffort,
            Available = true,
            AvailabilityMessage = $"当前配置 {model} / {reasoningEffort}。"
        };
    }

    private string ProtectSecret(string value)
    {
        if (_cryptoService == null)
            return value;

        return EncryptedSecretPrefix + _cryptoService.Encrypt(value);
    }

    private string ReadStoredSecret(BidOpsRuntimeSetting? setting)
    {
        if (setting == null || string.IsNullOrWhiteSpace(setting.SettingValue))
            return string.Empty;

        var value = setting.SettingValue.Trim();
        if (!value.StartsWith(EncryptedSecretPrefix, StringComparison.OrdinalIgnoreCase))
            return value;

        if (_cryptoService == null)
        {
            _logger.LogWarning("BidOps runtime AI token is encrypted but no crypto service is configured for this host.");
            return string.Empty;
        }

        var encryptedValue = value[EncryptedSecretPrefix.Length..];
        var decryptedValue = _cryptoService.Decrypt(encryptedValue);
        if (!string.IsNullOrWhiteSpace(decryptedValue))
            return decryptedValue.Trim();

        _logger.LogWarning("BidOps runtime AI token could not be decrypted; configuration fallback will be used.");
        return string.Empty;
    }

    private string ResolveConfiguredDeepSeekApiKey()
    {
        return FirstNonEmpty(
            _configuration["BidOps:Ai:ApiKey"],
            _configuration["BidOps:DeepSeek:ApiKey"],
            Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY"));
    }

    private string ResolveConfiguredDeepSeekApiKeySource()
    {
        if (!string.IsNullOrWhiteSpace(_configuration["BidOps:Ai:ApiKey"]))
            return "BidOps:Ai:ApiKey";

        if (!string.IsNullOrWhiteSpace(_configuration["BidOps:DeepSeek:ApiKey"]))
            return "BidOps:DeepSeek:ApiKey";

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")))
            return "DEEPSEEK_API_KEY";

        return "None";
    }

    private string ResolveConfiguredMimoApiKey()
    {
        return FirstNonEmpty(
            _configuration["BidOps:Mimo:ApiKey"],
            GenericAiValueForProvider(BidOpsSystemValues.AiProviderMimo, "ApiKey"),
            Environment.GetEnvironmentVariable("MIMO_API_KEY"));
    }

    private string ResolveConfiguredMimoApiKeySource()
    {
        if (!string.IsNullOrWhiteSpace(_configuration["BidOps:Mimo:ApiKey"]))
            return "BidOps:Mimo:ApiKey";

        if (!string.IsNullOrWhiteSpace(GenericAiValueForProvider(BidOpsSystemValues.AiProviderMimo, "ApiKey")))
            return "BidOps:Ai:ApiKey";

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MIMO_API_KEY")))
            return "MIMO_API_KEY";

        return "None";
    }

    private static string NormalizeDeepSeekApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AtlasException("DeepSeek token is required.");

        var trimmed = apiKey.Trim();
        if (trimmed.Length > MaxRuntimeHttpApiKeyLength)
            throw new AtlasException($"DeepSeek token cannot exceed {MaxRuntimeHttpApiKeyLength} characters.");

        return trimmed;
    }

    private static string NormalizeMimoApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new AtlasException("Mimo token is required.");

        var trimmed = apiKey.Trim();
        if (trimmed.Length > MaxRuntimeHttpApiKeyLength)
            throw new AtlasException($"Mimo token cannot exceed {MaxRuntimeHttpApiKeyLength} characters.");

        return trimmed;
    }

    private static string MaskSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        return trimmed.Length <= 4 ? "****" : $"****{trimmed[^4..]}";
    }

    private static string BuildMimoUnavailableMessage(BidOpsAiHttpSettingsDiagnostics diagnostics)
    {
        if (!diagnostics.Enabled)
            return "BidOps AI HTTP extraction is disabled by BidOps:Ai:Enabled.";

        if (!diagnostics.UseEnabled)
            return "BidOps AI HTTP extraction is disabled for notice staging.";

        if (!diagnostics.SupportedProvider)
            return "Mimo is not a supported HTTP AI provider in current settings.";

        if (!diagnostics.HasApiKey)
            return "Mimo token is missing.";

        if (!diagnostics.HasModel)
            return "Mimo model is missing.";

        if (!diagnostics.HasEndpoint)
            return "Mimo endpoint is missing.";

        return "Mimo settings are unavailable.";
    }

    private static string BuildDeepSeekConnectivityUnavailableMessage(BidOpsAiHttpSettingsDiagnostics diagnostics)
    {
        if (!diagnostics.SupportedProvider)
            return "DeepSeek is not a supported HTTP AI provider in current settings.";

        if (!diagnostics.HasApiKey)
            return "DeepSeek token is missing.";

        if (!diagnostics.HasModel)
            return "DeepSeek model is missing.";

        if (!diagnostics.HasEndpoint)
            return "DeepSeek endpoint is missing.";

        return "DeepSeek settings are unavailable.";
    }

    private static string BuildMimoConnectivityUnavailableMessage(BidOpsAiHttpSettingsDiagnostics diagnostics)
    {
        if (!diagnostics.SupportedProvider)
            return "Mimo is not a supported HTTP AI provider in current settings.";

        if (!diagnostics.HasApiKey)
            return "Mimo token is missing.";

        if (!diagnostics.HasModel)
            return "Mimo model is missing.";

        if (!diagnostics.HasEndpoint)
            return "Mimo endpoint is missing.";

        return "Mimo settings are unavailable.";
    }

    private static string FormatEndpointForDto(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return endpoint;

        return uri.GetLeftPart(UriPartial.Path);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;

        return value[..maxLength];
    }

    private string GetConfiguredProvider()
    {
        return FirstNonEmpty(_configuration["BidOps:Ai:Provider"], BidOpsSystemValues.AiProviderCodexCli);
    }

    private string ResolveHttpModel(string provider)
    {
        if (provider.Equals(BidOpsSystemValues.AiProviderMimo, StringComparison.OrdinalIgnoreCase))
        {
            return FirstNonEmpty(
                _configuration["BidOps:Mimo:Model"],
                GenericAiValueForProvider(provider, "Model"),
                BidOpsSystemValues.DefaultMimoModel);
        }

        if (provider.Equals(BidOpsSystemValues.AiProviderDeepSeek, StringComparison.OrdinalIgnoreCase))
        {
            return FirstNonEmpty(
                _configuration["BidOps:DeepSeek:Model"],
                _configuration["BidOps:Ai:Model"],
                BidOpsSystemValues.DefaultDeepSeekModel);
        }

        return FirstNonEmpty(_configuration["BidOps:Ai:Model"]);
    }

    private string GenericAiValueForProvider(string provider, string key)
    {
        var configuredProvider = NormalizeProvider(_configuration["BidOps:Ai:Provider"]);
        return configuredProvider.Equals(provider, StringComparison.OrdinalIgnoreCase)
            ? FirstNonEmpty(_configuration[$"BidOps:Ai:{key}"])
            : string.Empty;
    }

    private static BidOpsRuntimeSetting? GetLatestSetting(params BidOpsRuntimeSetting?[] settings)
    {
        return settings
            .Where(x => x != null)
            .OrderByDescending(x => x!.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefault();
    }

    private BidOpsCodexCliScenarioSettingsDto BuildCodexCliScenario(
        IReadOnlyDictionary<string, BidOpsRuntimeSetting> settings,
        string scenario)
    {
        var normalizedScenario = NormalizeCodexCliScenario(scenario);
        var keys = GetCodexCliScenarioKeys(normalizedScenario);
        settings.TryGetValue(keys.ModelKey, out var modelSetting);
        settings.TryGetValue(keys.ReasoningEffortKey, out var reasoningSetting);

        return new BidOpsCodexCliScenarioSettingsDto
        {
            Scenario = normalizedScenario,
            Label = GetCodexCliScenarioLabel(normalizedScenario),
            Description = GetCodexCliScenarioDescription(normalizedScenario),
            Model = ResolveCodexCliModel(settings, normalizedScenario),
            ReasoningEffort = ResolveCodexCliReasoningEffort(settings, normalizedScenario),
            ModelSource = modelSetting == null || string.IsNullOrWhiteSpace(modelSetting.SettingValue)
                ? "Configuration"
                : "Runtime",
            ReasoningEffortSource = reasoningSetting == null || string.IsNullOrWhiteSpace(reasoningSetting.SettingValue)
                ? "Configuration"
                : "Runtime"
        };
    }

    private string ResolveCodexCliModel(
        IReadOnlyDictionary<string, BidOpsRuntimeSetting> settings,
        string scenario)
    {
        var normalizedScenario = NormalizeCodexCliScenario(scenario);
        var keys = GetCodexCliScenarioKeys(normalizedScenario);
        settings.TryGetValue(keys.ModelKey, out var scenarioSetting);
        settings.TryGetValue(BidOpsSystemValues.CodexCliModelRuntimeSettingKey, out var defaultSetting);

        return FirstNonEmpty(
            scenarioSetting?.SettingValue,
            normalizedScenario == BidOpsCodexCliScenarios.Default ? null : defaultSetting?.SettingValue,
            _configuration["BidOps:CodexCli:Model"],
            _configuration["BidOps:Ai:CodexCliModel"],
            BidOpsSystemValues.DefaultCodexCliModel);
    }

    private string ResolveCodexCliReasoningEffort(
        IReadOnlyDictionary<string, BidOpsRuntimeSetting> settings,
        string scenario)
    {
        var normalizedScenario = NormalizeCodexCliScenario(scenario);
        var keys = GetCodexCliScenarioKeys(normalizedScenario);
        settings.TryGetValue(keys.ReasoningEffortKey, out var scenarioSetting);
        var defaultEffort = GetDefaultReasoningEffort(normalizedScenario);
        var effort = normalizedScenario == BidOpsCodexCliScenarios.Default
            ? FirstNonEmpty(
                scenarioSetting?.SettingValue,
                _configuration["BidOps:CodexCli:ReasoningEffort"],
                _configuration["BidOps:Ai:CodexCliReasoningEffort"],
                defaultEffort)
            : FirstNonEmpty(scenarioSetting?.SettingValue, defaultEffort);

        return NormalizeReasoningEffort(effort, strict: false);
    }

    private static string GetCodexCliScenarioLabel(string scenario)
    {
        return NormalizeCodexCliScenario(scenario) switch
        {
            BidOpsCodexCliScenarios.Complex => "复杂件识别",
            BidOpsCodexCliScenarios.ManualReparse => "重解析",
            BidOpsCodexCliScenarios.ReviewerPrompt => "人工提示",
            _ => "普通识别"
        };
    }

    private static string GetCodexCliScenarioDescription(string scenario)
    {
        return NormalizeCodexCliScenario(scenario) switch
        {
            BidOpsCodexCliScenarios.Complex => "附件较多或文本较长的公告、PDF 和结果识别，默认 medium。",
            BidOpsCodexCliScenarios.ManualReparse => "无人工提示的手动重解析，默认 medium。",
            BidOpsCodexCliScenarios.ReviewerPrompt => "带审核人员修正提示的重解析和结果识别，默认 xhigh。",
            _ => "自动采集后的普通公告、PDF 和结果识别，默认 low。"
        };
    }

    private static string GetDefaultReasoningEffort(string scenario)
    {
        return NormalizeCodexCliScenario(scenario) switch
        {
            BidOpsCodexCliScenarios.Complex => BidOpsSystemValues.DefaultCodexCliComplexReasoningEffort,
            BidOpsCodexCliScenarios.ManualReparse => BidOpsSystemValues.DefaultCodexCliManualReparseReasoningEffort,
            BidOpsCodexCliScenarios.ReviewerPrompt => BidOpsSystemValues.DefaultCodexCliReviewerPromptReasoningEffort,
            _ => BidOpsSystemValues.DefaultCodexCliReasoningEffort
        };
    }

    private static string NormalizeCodexCliScenario(string? scenario)
    {
        if (string.IsNullOrWhiteSpace(scenario))
            return BidOpsCodexCliScenarios.Default;

        var trimmed = scenario.Trim();
        if (trimmed.Equals(BidOpsCodexCliScenarios.Default, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("ordinary", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            return BidOpsCodexCliScenarios.Default;
        }

        if (trimmed.Equals(BidOpsCodexCliScenarios.Complex, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("difficult", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("large", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("large-source", StringComparison.OrdinalIgnoreCase))
        {
            return BidOpsCodexCliScenarios.Complex;
        }

        if (trimmed.Equals(BidOpsCodexCliScenarios.ManualReparse, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("reparse", StringComparison.OrdinalIgnoreCase))
        {
            return BidOpsCodexCliScenarios.ManualReparse;
        }

        if (trimmed.Equals(BidOpsCodexCliScenarios.ReviewerPrompt, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("manual", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("prompt", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("human-prompt", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("reviewer", StringComparison.OrdinalIgnoreCase))
        {
            return BidOpsCodexCliScenarios.ReviewerPrompt;
        }

        throw new AtlasException("Unsupported Codex CLI scenario. Use default, complex, manual-reparse, or reviewer-prompt.");
    }

    private static CodexCliScenarioKeys GetCodexCliScenarioKeys(string scenario)
    {
        return NormalizeCodexCliScenario(scenario) switch
        {
            BidOpsCodexCliScenarios.Complex => new CodexCliScenarioKeys(
                BidOpsSystemValues.CodexCliComplexModelRuntimeSettingKey,
                BidOpsSystemValues.CodexCliComplexReasoningEffortRuntimeSettingKey),
            BidOpsCodexCliScenarios.ManualReparse => new CodexCliScenarioKeys(
                BidOpsSystemValues.CodexCliManualReparseModelRuntimeSettingKey,
                BidOpsSystemValues.CodexCliManualReparseReasoningEffortRuntimeSettingKey),
            BidOpsCodexCliScenarios.ReviewerPrompt => new CodexCliScenarioKeys(
                BidOpsSystemValues.CodexCliReviewerPromptModelRuntimeSettingKey,
                BidOpsSystemValues.CodexCliReviewerPromptReasoningEffortRuntimeSettingKey),
            _ => new CodexCliScenarioKeys(
                BidOpsSystemValues.CodexCliModelRuntimeSettingKey,
                BidOpsSystemValues.CodexCliReasoningEffortRuntimeSettingKey)
        };
    }

    internal static string NormalizeProvider(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
            return BidOpsSystemValues.AiProviderCodexCli;

        var trimmed = provider.Trim();
        if (trimmed.Equals(BidOpsSystemValues.AiProviderCodexCli, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("CodexCLI", StringComparison.OrdinalIgnoreCase))
        {
            return BidOpsSystemValues.AiProviderCodexCli;
        }

        if (trimmed.Equals(BidOpsSystemValues.AiProviderDeepSeek, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("OpenAICompatible", StringComparison.OrdinalIgnoreCase))
        {
            return BidOpsSystemValues.AiProviderDeepSeek;
        }

        if (trimmed.Equals(BidOpsSystemValues.AiProviderMimo, StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("XiaomiMimo", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("MiMo", StringComparison.OrdinalIgnoreCase))
        {
            return BidOpsSystemValues.AiProviderMimo;
        }

        throw new AtlasException("Unsupported BidOps AI provider. Use DeepSeek, Mimo, or CodexCli.");
    }

    internal static string NormalizeReasoningEffort(string? effort, bool strict)
    {
        if (string.IsNullOrWhiteSpace(effort))
        {
            if (strict)
                throw new AtlasException("Codex CLI reasoning effort is required.");

            return BidOpsSystemValues.DefaultCodexCliReasoningEffort;
        }

        var normalized = effort.Trim().ToLowerInvariant();
        if (normalized is "xhight" or "extrahight" or "extra-high" or "extra high")
            return "xhigh";

        if (SupportedReasoningEfforts.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            return normalized;

        if (strict)
            throw new AtlasException("Unsupported Codex CLI reasoning effort. Use minimal, low, medium, high, or xhigh.");

        return BidOpsSystemValues.DefaultCodexCliReasoningEffort;
    }

    private static string NormalizeModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new AtlasException("Codex CLI model is required.");

        var trimmed = model.Trim();
        if (trimmed.Length > 100)
            throw new AtlasException("Codex CLI model cannot exceed 100 characters.");

        return trimmed;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }

    private sealed record CodexCliScenarioKeys(
        string ModelKey,
        string ReasoningEffortKey);
}
