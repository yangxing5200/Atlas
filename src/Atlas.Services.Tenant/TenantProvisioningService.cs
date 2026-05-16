using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Data.Abstractions;
using Atlas.Data.Global;
using Atlas.Data.Global.Repositories;
using Atlas.Data.Tenant.Context;
using Atlas.Infrastructure.Common.Tenants;
using Atlas.Messaging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GlobalTenant = Atlas.Core.Entities.Global.Tenant;

namespace Atlas.Services.Tenant;

public sealed class TenantProvisioningService : ITenantProvisioningService
{
    private readonly AtlasGlobalDbContext _globalDbContext;
    private readonly ITenantRepository _tenantRepository;
    private readonly IGlobalUnitOfWork _globalUnitOfWork;
    private readonly ITenantDbContextFactory _tenantDbContextFactory;
    private readonly ITenantCodeGenerator _codeGenerator;
    private readonly IDomainEventPublisher _eventPublisher;
    private readonly ILogger<TenantProvisioningService> _logger;

    public TenantProvisioningService(
        AtlasGlobalDbContext globalDbContext,
        ITenantRepository tenantRepository,
        IGlobalUnitOfWork globalUnitOfWork,
        ITenantDbContextFactory tenantDbContextFactory,
        ITenantCodeGenerator codeGenerator,
        IDomainEventPublisher eventPublisher,
        ILogger<TenantProvisioningService> logger)
    {
        _globalDbContext = globalDbContext;
        _tenantRepository = tenantRepository;
        _globalUnitOfWork = globalUnitOfWork;
        _tenantDbContextFactory = tenantDbContextFactory;
        _codeGenerator = codeGenerator;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<TenantProvisioningResult> ProvisionAsync(
        TenantProvisioningRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var domain = _codeGenerator.NormalizeDomain(request.Domain);
        var eventId = Guid.Empty;
        var eventPublished = false;

        await _globalUnitOfWork.BeginTransactionAsync(ct);
        GlobalTenant tenant;
        try
        {
            if (await TenantDomainExistsAsync(domain, ct))
                throw new InvalidOperationException($"Tenant domain '{domain}' already exists.");

            if (!await DatabaseInstanceExistsAsync(request.DatabaseInstanceId, ct))
                throw new InvalidOperationException($"Database instance '{request.DatabaseInstanceId}' does not exist.");

            tenant = CreateTenant(request, domain);
            await _tenantRepository.AddAsync(tenant, ct);
            await _globalUnitOfWork.SaveChangesAsync(ct);
            await _globalUnitOfWork.CommitAsync(ct);
        }
        catch
        {
            if (_globalUnitOfWork.HasActiveTransaction)
                await _globalUnitOfWork.RollbackAsync(ct);
            throw;
        }

        var headquarters = await CreateHeadquartersStoreAsync(tenant, request, ct);
        var domainEvent = new TenantProvisionedEvent
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            Domain = tenant.Domain,
            HeadquartersStoreId = headquarters.Id
        };
        eventId = domainEvent.EventId;

        await _globalUnitOfWork.BeginTransactionAsync(ct);
        try
        {
            tenant.Status = TenantStatus.Active;
            await _eventPublisher.PublishAsync(domainEvent, ct);
            eventPublished = true;
            await _globalUnitOfWork.SaveChangesAsync(ct);
            await _globalUnitOfWork.CommitAsync(ct);
        }
        catch
        {
            if (_globalUnitOfWork.HasActiveTransaction)
                await _globalUnitOfWork.RollbackAsync(ct);
            throw;
        }

        _logger.LogInformation(
            "Provisioned tenant {TenantId} ({Domain}) with headquarters store {StoreId}",
            tenant.Id,
            tenant.Domain,
            headquarters.Id);

        return new TenantProvisioningResult
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            Domain = tenant.Domain,
            DatabaseInstanceId = tenant.DatabaseInstanceId,
            HeadquartersStoreId = headquarters.Id,
            HeadquartersStoreCode = headquarters.Code,
            HeadquartersStoreName = headquarters.Name,
            EventId = eventId,
            EventPublished = eventPublished
        };
    }

    private async Task<bool> TenantDomainExistsAsync(string domain, CancellationToken ct)
    {
        var query = await _tenantRepository.QueryAsync(ct);
        return await query.Where(t => t.Domain == domain && !t.IsDeleted).AnyAsync(ct);
    }

    private Task<bool> DatabaseInstanceExistsAsync(long databaseInstanceId, CancellationToken ct)
    {
        return _globalDbContext.DatabaseInstances
            .AsNoTracking()
            .AnyAsync(x => x.Id == databaseInstanceId, ct);
    }

    private async Task<Store> CreateHeadquartersStoreAsync(
        GlobalTenant tenant,
        TenantProvisioningRequest request,
        CancellationToken ct)
    {
        var storeCode = string.IsNullOrWhiteSpace(request.HeadquartersCode)
            ? _codeGenerator.GenerateStoreCode(tenant.Domain, "hq")
            : request.HeadquartersCode.Trim().ToUpperInvariant();

        var store = new Store
        {
            TenantId = tenant.Id,
            Code = storeCode,
            Name = string.IsNullOrWhiteSpace(request.HeadquartersName)
                ? $"{tenant.Name} Headquarters"
                : request.HeadquartersName.Trim(),
            Type = StoreType.Headquarters,
            ParentStoreId = null,
            IsActive = true,
            Status = StoreStatus.Active,
            Address = request.Address ?? string.Empty,
            ContactPerson = request.ContactName,
            ContactPhone = request.ContactPhoneNumber,
            Province = request.Province ?? string.Empty,
            City = request.City,
            District = string.Empty
        };

        var tenantDb = await _tenantDbContextFactory.GetDbContextAsync(tenant.Id, ct);
        await using var transaction = await tenantDb.Database.BeginTransactionAsync(ct);
        try
        {
            await tenantDb.Set<Store>().AddAsync(store, ct);
            await tenantDb.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return store;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private static GlobalTenant CreateTenant(TenantProvisioningRequest request, string domain)
    {
        return new GlobalTenant
        {
            Name = request.Name.Trim(),
            BrandName = request.BrandName,
            Address = request.Address,
            PhoneNumber = request.PhoneNumber.Trim(),
            ContactName = request.ContactName.Trim(),
            ContactPhoneNumber = request.ContactPhoneNumber.Trim(),
            ContactEmail = request.ContactEmail,
            Domain = domain,
            TenantType = request.TenantType,
            Province = request.Province,
            City = request.City.Trim(),
            Category = request.Category,
            Status = TenantStatus.Inactive,
            BusinessType = request.BusinessType,
            DatabaseInstanceId = request.DatabaseInstanceId,
            OfficeCount = request.OfficeCount
        };
    }

    private static void ValidateRequest(TenantProvisioningRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Tenant name is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Domain))
            throw new ArgumentException("Tenant domain is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            throw new ArgumentException("Tenant phone number is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ContactName))
            throw new ArgumentException("Tenant contact name is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ContactPhoneNumber))
            throw new ArgumentException("Tenant contact phone number is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.City))
            throw new ArgumentException("Tenant city is required.", nameof(request));
        if (request.DatabaseInstanceId <= 0)
            throw new ArgumentException("Database instance id must be greater than zero.", nameof(request));
        if (request.OfficeCount <= 0)
            throw new ArgumentException("Office count must be greater than zero.", nameof(request));
    }
}
