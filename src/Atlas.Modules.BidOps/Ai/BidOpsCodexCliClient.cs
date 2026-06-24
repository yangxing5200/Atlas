using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.Ai;

public interface IBidOpsCodexCliClient
{
    Task<BidOpsCodexCliResult> ExecuteJsonAsync(
        BidOpsCodexCliRequest request,
        CancellationToken ct = default);
}

public sealed record BidOpsCodexCliRequest(
    string Use,
    string Prompt,
    string OutputSchemaJson,
    string BinaryPath,
    string Model,
    string ReasoningEffort,
    string WorkingDirectory,
    int TimeoutSeconds,
    string Sandbox,
    bool SkipGitRepoCheck,
    bool IgnoreUserConfig,
    bool IgnoreRules,
    bool Ephemeral,
    string ApiKey);

public sealed record BidOpsCodexCliResult(
    int ExitCode,
    long ElapsedMilliseconds,
    string Stdout,
    string Stderr,
    string AssistantContent);

public sealed class BidOpsCodexCliClient : IBidOpsCodexCliClient
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly ILogger<BidOpsCodexCliClient> _logger;

    public BidOpsCodexCliClient(ILogger<BidOpsCodexCliClient> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BidOpsCodexCliResult> ExecuteJsonAsync(
        BidOpsCodexCliRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tempRoot = Path.Combine(Path.GetTempPath(), "AtlasBidOpsCodexCli");
        Directory.CreateDirectory(tempRoot);
        var fileStem = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
        var schemaPath = Path.Combine(tempRoot, $"{fileStem}.schema.json");
        var outputPath = Path.Combine(tempRoot, $"{fileStem}.output.json");

        var outputSchemaJson = NormalizeAndValidateOutputSchema(request.OutputSchemaJson);
        await WriteUtf8NoBomAsync(schemaPath, outputSchemaJson, ct);

        var arguments = BuildArguments(request, schemaPath, outputPath);
        var processTarget = ResolveProcessTarget(request.BinaryPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = processTarget.FileName,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        AddArguments(startInfo, processTarget, arguments);
        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
            startInfo.WorkingDirectory = request.WorkingDirectory;

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
            startInfo.Environment["CODEX_API_KEY"] = request.ApiKey;

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _logger.LogDebug(
                "Starting Codex CLI. configuredBinary={ConfiguredBinary}, processFile={ProcessFile}, batchFile={BatchFile}, workingDirectory={WorkingDirectory}.",
                request.BinaryPath,
                processTarget.FileName,
                processTarget.BatchFilePath,
                startInfo.WorkingDirectory);

            if (!process.Start())
                throw new InvalidOperationException("Codex CLI process did not start.");

            await process.StandardInput.WriteAsync(request.Prompt.AsMemory(), ct);
            process.StandardInput.Close();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                TryKill(process);
                throw;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                TryKill(process);
                throw new TimeoutException($"Codex CLI timed out after {request.TimeoutSeconds} seconds.");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            stopwatch.Stop();
            var assistantContent = await ReadAssistantContentAsync(outputPath, stdout, ct);

            return new BidOpsCodexCliResult(
                process.ExitCode,
                stopwatch.ElapsedMilliseconds,
                stdout,
                stderr,
                assistantContent);
        }
        finally
        {
            TryDelete(schemaPath);
            TryDelete(outputPath);
        }
    }

    private static IReadOnlyList<string> BuildArguments(
        BidOpsCodexCliRequest request,
        string schemaPath,
        string outputPath)
    {
        var arguments = new List<string>
        {
            "exec",
            "--sandbox",
            request.Sandbox,
            "--color",
            "never",
            "--output-schema",
            schemaPath,
            "--output-last-message",
            outputPath
        };

        if (request.SkipGitRepoCheck)
            arguments.Add("--skip-git-repo-check");
        if (request.Ephemeral)
            arguments.Add("--ephemeral");
        if (request.IgnoreUserConfig)
            arguments.Add("--ignore-user-config");
        if (request.IgnoreRules)
            arguments.Add("--ignore-rules");
        if (!string.IsNullOrWhiteSpace(request.Model))
        {
            arguments.Add("--model");
            arguments.Add(request.Model);
        }
        if (!string.IsNullOrWhiteSpace(request.ReasoningEffort))
        {
            arguments.Add("--config");
            arguments.Add($"model_reasoning_effort=\"{request.ReasoningEffort}\"");
        }

        arguments.Add("-");
        return arguments;
    }

    private static void AddArguments(
        ProcessStartInfo startInfo,
        CodexProcessTarget processTarget,
        IReadOnlyList<string> arguments)
    {
        if (!string.IsNullOrWhiteSpace(processTarget.BatchFilePath))
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(processTarget.BatchFilePath);
        }

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
    }

    private static CodexProcessTarget ResolveProcessTarget(string binaryPath)
    {
        var resolved = ResolveBinaryPath(binaryPath, Environment.GetEnvironmentVariable("PATH"));
        if (OperatingSystem.IsWindows() && IsBatchFile(resolved))
            return new CodexProcessTarget("cmd.exe", resolved);

        return new CodexProcessTarget(resolved, null);
    }

    private static string ResolveBinaryPath(string binaryPath, string? pathValue)
    {
        var trimmed = string.IsNullOrWhiteSpace(binaryPath) ? "codex" : binaryPath.Trim();
        if (!OperatingSystem.IsWindows())
            return trimmed;

        if (IsPathLike(trimmed))
            return ResolvePathLikeWindowsBinary(trimmed);

        foreach (var directory in SplitPath(pathValue))
        {
            foreach (var extension in new[] { ".cmd", ".bat", ".exe", string.Empty })
            {
                var candidate = Path.Combine(directory, trimmed + extension);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return trimmed;
    }

    private static string ResolvePathLikeWindowsBinary(string binaryPath)
    {
        if (!string.IsNullOrWhiteSpace(Path.GetExtension(binaryPath)))
            return binaryPath;

        foreach (var extension in new[] { ".cmd", ".bat", ".exe", string.Empty })
        {
            var candidate = binaryPath + extension;
            if (File.Exists(candidate))
                return candidate;
        }

        return binaryPath;
    }

    private static bool IsPathLike(string value)
    {
        return Path.IsPathRooted(value) ||
               value.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
               value.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static IEnumerable<string> SplitPath(string? pathValue)
    {
        return (pathValue ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(Directory.Exists);
    }

    private static bool IsBatchFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bat", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ReadAssistantContentAsync(
        string outputPath,
        string stdout,
        CancellationToken ct)
    {
        if (File.Exists(outputPath))
        {
            var output = await File.ReadAllTextAsync(outputPath, Utf8NoBom, ct);
            if (!string.IsNullOrWhiteSpace(output))
                return output.Trim();
        }

        return stdout.Trim();
    }

    private static string NormalizeAndValidateOutputSchema(string outputSchemaJson)
    {
        var normalized = (outputSchemaJson ?? string.Empty).TrimStart('\uFEFF');
        using var _ = JsonDocument.Parse(normalized);
        return normalized;
    }

    private static async Task WriteUtf8NoBomAsync(
        string path,
        string content,
        CancellationToken ct)
    {
        await File.WriteAllTextAsync(path, (content ?? string.Empty).TrimStart('\uFEFF'), Utf8NoBom, ct);
    }

    private void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to kill timed-out Codex CLI process.");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup for temporary schema/output files.
        }
    }

    private sealed record CodexProcessTarget(string FileName, string? BatchFilePath);
}

internal sealed record BidOpsCodexCliSettings(
    string Provider,
    string BinaryPath,
    string Model,
    string ReasoningEffort,
    string WorkingDirectory,
    int TimeoutSeconds,
    string Sandbox,
    int MaxInputCharacters,
    bool SkipGitRepoCheck,
    bool IgnoreUserConfig,
    bool IgnoreRules,
    bool Ephemeral,
    string ApiKey,
    string ApiKeySource);

internal sealed record BidOpsCodexCliSettingsDiagnostics(
    bool Enabled,
    bool UseEnabled,
    string Provider,
    bool SupportedProvider,
    string BinaryPath,
    string WorkingDirectory,
    string Model,
    string ReasoningEffort,
    string ApiKeySource,
    bool HasApiKey);

internal static class BidOpsCodexCliSettingsFactory
{
    private static readonly string[] SupportedSandboxes = ["read-only", "workspace-write", "danger-full-access"];

    public static bool TryCreate(
        IConfiguration configuration,
        BidOpsAiUse use,
        out BidOpsCodexCliSettings settings)
    {
        settings = default!;
        if (!configuration.GetValue<bool>("BidOps:Ai:Enabled") || !IsEnabledForUse(configuration, use))
            return false;

        return TryCreate(configuration, use, providerOverride: null, out settings);
    }

    public static bool TryCreate(
        IConfiguration configuration,
        BidOpsAiUse use,
        string? providerOverride,
        string? modelOverride,
        string? reasoningEffortOverride,
        out BidOpsCodexCliSettings settings)
    {
        settings = default!;
        if (!configuration.GetValue<bool>("BidOps:Ai:Enabled") || !IsEnabledForUse(configuration, use))
            return false;

        var provider = FirstNonEmpty(providerOverride, configuration["BidOps:Ai:Provider"], BidOpsSystemValues.AiProviderCodexCli);
        if (!IsCodexCli(provider))
            return false;

        var binaryPath = FirstNonEmpty(
            configuration["BidOps:CodexCli:BinaryPath"],
            configuration["BidOps:Ai:CodexCliBinaryPath"],
            "codex");
        var model = ResolveModel(configuration, modelOverride);
        var reasoningEffort = ResolveReasoningEffort(configuration, reasoningEffortOverride);
        var workingDirectory = FirstNonEmpty(
            configuration["BidOps:CodexCli:WorkingDirectory"],
            Directory.GetCurrentDirectory());
        var sandbox = NormalizeSandbox(FirstNonEmpty(
            configuration["BidOps:CodexCli:Sandbox"],
            "read-only"));
        var timeoutSeconds = Math.Clamp(
            configuration.GetValue<int?>("BidOps:CodexCli:TimeoutSeconds") ?? 600,
            30,
            3600);
        var maxInputCharacters = Math.Clamp(
            configuration.GetValue<int?>("BidOps:CodexCli:MaxInputCharacters") ??
            configuration.GetValue<int?>("BidOps:Ai:MaxInputCharacters") ??
            24_000,
            4_000,
            120_000);

        settings = new BidOpsCodexCliSettings(
            provider.Trim(),
            binaryPath.Trim(),
            model.Trim(),
            reasoningEffort,
            workingDirectory.Trim(),
            timeoutSeconds,
            sandbox,
            maxInputCharacters,
            configuration.GetValue<bool?>("BidOps:CodexCli:SkipGitRepoCheck") ?? true,
            configuration.GetValue<bool?>("BidOps:CodexCli:IgnoreUserConfig") ?? false,
            configuration.GetValue<bool?>("BidOps:CodexCli:IgnoreRules") ?? true,
            configuration.GetValue<bool?>("BidOps:CodexCli:Ephemeral") ?? true,
            ResolveApiKey(configuration),
            ResolveApiKeySource(configuration));
        return true;
    }

    public static bool TryCreate(
        IConfiguration configuration,
        BidOpsAiUse use,
        string? providerOverride,
        out BidOpsCodexCliSettings settings)
    {
        return TryCreate(
            configuration,
            use,
            providerOverride,
            modelOverride: null,
            reasoningEffortOverride: null,
            out settings);
    }

    public static BidOpsCodexCliSettingsDiagnostics Diagnose(
        IConfiguration configuration,
        BidOpsAiUse use)
    {
        var provider = FirstNonEmpty(configuration["BidOps:Ai:Provider"], BidOpsSystemValues.AiProviderCodexCli);
        var apiKey = ResolveApiKey(configuration);
        return new BidOpsCodexCliSettingsDiagnostics(
            configuration.GetValue<bool>("BidOps:Ai:Enabled"),
            IsEnabledForUse(configuration, use),
            provider.Trim(),
            IsCodexCli(provider),
            FirstNonEmpty(configuration["BidOps:CodexCli:BinaryPath"], configuration["BidOps:Ai:CodexCliBinaryPath"], "codex"),
            FirstNonEmpty(configuration["BidOps:CodexCli:WorkingDirectory"], Directory.GetCurrentDirectory()),
            ResolveModel(configuration, modelOverride: null),
            ResolveReasoningEffort(configuration, reasoningEffortOverride: null),
            ResolveApiKeySource(configuration),
            !string.IsNullOrWhiteSpace(apiKey));
    }

    public static BidOpsCodexCliRequest CreateRequest(
        BidOpsCodexCliSettings settings,
        BidOpsAiUse use,
        string prompt,
        string outputSchemaJson)
    {
        return new BidOpsCodexCliRequest(
            use.ToString(),
            prompt,
            outputSchemaJson,
            settings.BinaryPath,
            settings.Model,
            settings.ReasoningEffort,
            settings.WorkingDirectory,
            settings.TimeoutSeconds,
            settings.Sandbox,
            settings.SkipGitRepoCheck,
            settings.IgnoreUserConfig,
            settings.IgnoreRules,
            settings.Ephemeral,
            settings.ApiKey);
    }

    private static bool IsEnabledForUse(IConfiguration configuration, BidOpsAiUse use)
    {
        return use switch
        {
            BidOpsAiUse.NoticeStaging => configuration.GetValue<bool?>("BidOps:Ai:UseForNoticeStaging") ?? true,
            BidOpsAiUse.OutcomeSuppliers => configuration.GetValue<bool?>("BidOps:Ai:UseForOutcomeSuppliers") ?? true,
            _ => false
        };
    }

    private static bool IsCodexCli(string provider)
    {
        return string.Equals(provider, "CodexCli", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(provider, "CodexCLI", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSandbox(string sandbox)
    {
        return SupportedSandboxes.Contains(sandbox, StringComparer.OrdinalIgnoreCase)
            ? sandbox.Trim()
            : "read-only";
    }

    private static string ResolveApiKey(IConfiguration configuration)
    {
        return FirstNonEmpty(
            configuration["BidOps:CodexCli:ApiKey"],
            configuration["BidOps:Ai:CodexCliApiKey"],
            Environment.GetEnvironmentVariable("CODEX_API_KEY"));
    }

    private static string ResolveModel(IConfiguration configuration, string? modelOverride)
    {
        return FirstNonEmpty(
            modelOverride,
            configuration["BidOps:CodexCli:Model"],
            configuration["BidOps:Ai:CodexCliModel"],
            BidOpsSystemValues.DefaultCodexCliModel);
    }

    private static string ResolveReasoningEffort(IConfiguration configuration, string? reasoningEffortOverride)
    {
        var effort = FirstNonEmpty(
            reasoningEffortOverride,
            configuration["BidOps:CodexCli:ReasoningEffort"],
            configuration["BidOps:Ai:CodexCliReasoningEffort"],
            BidOpsSystemValues.DefaultCodexCliReasoningEffort);

        return NormalizeReasoningEffort(effort);
    }

    private static string NormalizeReasoningEffort(string effort)
    {
        var normalized = effort.Trim();
        if (normalized.Equals("xhight", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("extrahight", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("extra-high", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("extra high", StringComparison.OrdinalIgnoreCase))
        {
            return "xhigh";
        }

        return normalized;
    }

    private static string ResolveApiKeySource(IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration["BidOps:CodexCli:ApiKey"]))
            return "BidOps:CodexCli:ApiKey";

        if (!string.IsNullOrWhiteSpace(configuration["BidOps:Ai:CodexCliApiKey"]))
            return "BidOps:Ai:CodexCliApiKey";

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CODEX_API_KEY")))
            return "CODEX_API_KEY";

        return "SavedCodexCliAuth";
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
}
