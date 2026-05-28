using System.Text.Json.Serialization;

namespace EkaTrack.Client.Models;

public class TmdbMediaItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonIgnore]
    public string Title => TitleRaw ?? NameRaw ?? "";

    [JsonPropertyName("title")]
    public string? TitleRaw { get; set; }

    [JsonPropertyName("name")]
    public string? NameRaw { get; set; }

    [JsonPropertyName("media_type")]
    public string MediaType { get; set; } = "";

    [JsonPropertyName("poster_path")]
    public string? PosterPath { get; set; }

    [JsonPropertyName("vote_average")]
    public double VoteAverage { get; set; }

    [JsonPropertyName("overview")]
    public string Overview { get; set; } = "";

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; set; }

    [JsonPropertyName("first_air_date")]
    public string? FirstAirDate { get; set; }

    [JsonIgnore]
    public string Year =>
        ReleaseDate is { Length: >= 4 } ? ReleaseDate[..4]
        : FirstAirDate is { Length: >= 4 } ? FirstAirDate[..4]
        : "";
}
