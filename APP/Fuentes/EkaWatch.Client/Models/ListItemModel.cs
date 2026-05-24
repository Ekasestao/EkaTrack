using System.Text.Json.Serialization;

namespace EkaWatch.Client.Models;

public class ListItemModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("tmdb_id")]
    public int TmdbId { get; set; }

    [JsonPropertyName("media_type")]
    public string MediaType { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    [JsonPropertyName("added_at")]
    public string AddedAt { get; set; } = "";
}
