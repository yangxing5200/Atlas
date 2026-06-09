using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Atlas.Exporting.Internal;

internal static class ExportQuerySerializer
{
    private const string SchemaVersion = "v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> IdentityFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "tenantId",
        "currentTenantId",
        "userId",
        "currentUserId",
        "storeId",
        "currentStoreId"
    };

    public static JsonSerializerOptions Options => JsonOptions;

    public static ExportSerializedQuery Serialize<TQuery>(TQuery query, Type providerQueryType)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(providerQueryType);

        var node = JsonSerializer.SerializeToNode(query, query.GetType(), JsonOptions)
            ?? new JsonObject();

        var canonical = Canonicalize(node) ?? new JsonObject();
        var json = canonical.ToJsonString(JsonOptions);

        _ = JsonSerializer.Deserialize(json, providerQueryType, JsonOptions)
            ?? throw new InvalidOperationException($"Cannot deserialize export query as {providerQueryType.FullName}.");

        return new ExportSerializedQuery(
            json,
            ComputeSha256(json),
            providerQueryType.FullName ?? providerQueryType.Name,
            SchemaVersion);
    }

    public static object Deserialize(string json, Type queryType)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("Export query json is empty.");

        return JsonSerializer.Deserialize(json, queryType, JsonOptions)
            ?? throw new InvalidOperationException($"Cannot deserialize export query as {queryType.FullName}.");
    }

    private static JsonNode? Canonicalize(JsonNode? node)
    {
        return node switch
        {
            JsonObject jsonObject => CanonicalizeObject(jsonObject),
            JsonArray jsonArray => CanonicalizeArray(jsonArray),
            JsonValue jsonValue => JsonNode.Parse(jsonValue.ToJsonString(JsonOptions)),
            _ => null
        };
    }

    private static JsonObject CanonicalizeObject(JsonObject source)
    {
        var target = new JsonObject();
        foreach (var property in source.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (IdentityFieldNames.Contains(property.Key))
                continue;

            target[property.Key] = Canonicalize(property.Value);
        }

        return target;
    }

    private static JsonArray CanonicalizeArray(JsonArray source)
    {
        var target = new JsonArray();
        foreach (var item in source)
        {
            target.Add(Canonicalize(item));
        }

        return target;
    }

    private static string ComputeSha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
