using System.Text.Json;
using System.Text.Json.Serialization;

namespace MaterialUIHelper.Models.filter;

public class FilterItem
{
    [JsonPropertyName("field")] public string Field { get; set; } = null!;

    [JsonPropertyName("operator")] public string Operator { get; set; } = null!;

    [JsonPropertyName("value")] public JsonElement Value { get; set; }
}