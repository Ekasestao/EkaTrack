using System.Text.Json.Serialization;

namespace EkaTrack.Client.Models;

public class TvmazeShowData
{
    [JsonPropertyName("tvmaze_id")]
    public int? TvmazeId { get; set; }

    [JsonPropertyName("seasons")]
    public List<TvmazeSeasonData> Seasons { get; set; } = [];
}

public class TvmazeSeasonData
{
    [JsonPropertyName("tvmaze_season_id")]
    public int TvmazeSeasonId { get; set; }

    [JsonPropertyName("season_number")]
    public int SeasonNumber { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("episode_count")]
    public int EpisodeCount { get; set; }

    [JsonPropertyName("poster_url")]
    public string? PosterUrl { get; set; }

    [JsonPropertyName("episodes")]
    public List<TvmazeEpisodeData> Episodes { get; set; } = [];
}

public class TvmazeEpisodeData
{
    [JsonPropertyName("tvmaze_episode_id")]
    public long TvmazeEpisodeId { get; set; }

    [JsonPropertyName("season_number")]
    public int SeasonNumber { get; set; }

    [JsonPropertyName("episode_number")]
    public int? EpisodeNumber { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("still_url")]
    public string? StillUrl { get; set; }

    [JsonPropertyName("air_date")]
    public string? AirDate { get; set; }

    [JsonPropertyName("runtime")]
    public int? Runtime { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("vote_average")]
    public double? VoteAverage { get; set; }

    [JsonPropertyName("watched")]
    public bool Watched { get; set; }
}
