namespace Atlas.Models.Responses;

public sealed class RevealSensitiveFieldsResponse
{
    public string EntityType { get; set; } = string.Empty;

    public long EntityId { get; set; }

    public Dictionary<string, string?> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public DateTime RevealedAt { get; set; }
}
