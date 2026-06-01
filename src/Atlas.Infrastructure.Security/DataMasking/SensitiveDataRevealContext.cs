namespace Atlas.Infrastructure.Security.DataMasking;

public sealed class SensitiveDataRevealContext
{
    public string Module { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public long EntityId { get; set; }

    public IReadOnlyCollection<string> Fields { get; set; } = Array.Empty<string>();

    public string Reason { get; set; } = string.Empty;

    public string? TicketNo { get; set; }

    public string RequiredPermission { get; set; } = string.Empty;
}
