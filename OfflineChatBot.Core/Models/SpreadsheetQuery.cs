using System.Text.Json.Serialization;

namespace OfflineChatBot.Models
{
    public sealed class SpreadsheetQuery
    {
        [JsonPropertyName("table")]
        public string Table { get; set; } = string.Empty;

        [JsonPropertyName("operation")]
        public string Operation { get; set; } = string.Empty;

        [JsonPropertyName("column")]
        public string Column { get; set; } = string.Empty;

        [JsonPropertyName("limit")]
        public int? Limit { get; set; }

        [JsonPropertyName("filters")]
        public List<QueryFilter> Filters { get; set; } = new List<QueryFilter>();
    }

    public sealed class QueryFilter
    {
        [JsonPropertyName("column")]
        public string Column { get; set; } = string.Empty;

        [JsonPropertyName("equals")]
        public string EqualTo { get; set; } = string.Empty;

        [JsonPropertyName("contains")]
        public string Contains { get; set; } = string.Empty;
    }
}
