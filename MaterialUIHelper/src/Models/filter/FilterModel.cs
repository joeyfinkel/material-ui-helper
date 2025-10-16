using System.Text.Json.Serialization;

namespace MaterialUIHelper.Models.filter;

public class FilterModel
{
    [JsonPropertyName("items")] public IEnumerable<FilterItem> Items { get; set; } = [];

    [JsonPropertyName("logicalOperator")] public string LogicalOperator { get; set; } = "Or";
}