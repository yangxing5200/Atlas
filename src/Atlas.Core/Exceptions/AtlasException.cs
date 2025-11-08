namespace Atlas.Core.Exceptions;

/// <summary>
/// Atlas 基础异常
/// </summary>
public class AtlasException : Exception
{
    public AtlasException() { }

    public AtlasException(string message) : base(message) { }

    public AtlasException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// 租户未找到异常
/// </summary>
public class TenantNotFoundException : AtlasException
{
    public Guid TenantId { get; }

    public TenantNotFoundException(Guid tenantId)
        : base($"Tenant with ID '{tenantId}' was not found.")
    {
        TenantId = tenantId;
    }
}

/// <summary>
/// 验证异常
/// </summary>
public class ValidationException : AtlasException
{
    public Dictionary<string, string[]> Errors { get; }

    public ValidationException(Dictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string field, string error)
        : this(new Dictionary<string, string[]>
        {
            { field, new[] { error } }
        })
    {
    }
}