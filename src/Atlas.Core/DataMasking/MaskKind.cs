namespace Atlas.Core.DataMasking;

/// <summary>
/// Describes how a sensitive value should be masked when returned from ordinary API responses.
/// </summary>
public enum MaskKind
{
    Default,
    Email,
    Phone,
    IdCard,
    BankCard,
    IpAddress,
    Token,
    Secret,
    Name,
    Address
}
