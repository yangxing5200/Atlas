namespace Atlas.Models.Requests;

public sealed class RevealSensitiveFieldsRequest
{
    public List<string> Fields { get; set; } = new();

    public string Reason { get; set; } = string.Empty;

    public string? TicketNo { get; set; }
}
