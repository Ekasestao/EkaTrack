using System.Text.Json.Serialization;

namespace EkaWatch.Client.Models;

public class ListInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("list_type")]
    public string ListType { get; set; } = "";

    [JsonPropertyName("item_count")]
    public int ItemCount { get; set; }

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = "";

    [JsonPropertyName("first_item_poster")]
    public string? FirstItemPoster { get; set; }
}
