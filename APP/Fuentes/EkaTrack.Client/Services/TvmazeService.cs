using System.Net.Http.Json;
using System.Text.Json;
using EkaTrack.Client.Models;

namespace EkaTrack.Client.Services;

public class TvmazeService
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public TvmazeService(HttpClient http)
    {
        _http = http;
    }

    public async Task<TvmazeShowData?> GetTvmazeSeasonsAsync(int tmdbId)
    {
        var response = await _http.GetAsync($"/media/tv/{tmdbId}/tvmaze");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<TvmazeShowData>(JsonOptions);
    }

    public async Task<bool> ToggleEpisodeWatchedAsync(long tvmazeEpisodeId, int seasonNumber, int episodeNumber, string showTitle)
    {
        var response = await _http.PostAsJsonAsync($"/tracking/episode/{tvmazeEpisodeId}/toggle", new
        {
            season_number = seasonNumber,
            episode_number = episodeNumber,
            show_title = showTitle
        }, JsonOptions);
        return response.IsSuccessStatusCode;
    }

    public async Task ToggleEpisodeBatchAsync(
        List<(long TvmazeEpisodeId, int SeasonNumber, int EpisodeNumber, string ShowTitle)> episodes,
        bool watched)
    {
        var body = new
        {
            episodes = episodes.Select(e => new
            {
                tvmaze_episode_id = e.TvmazeEpisodeId,
                season_number = e.SeasonNumber,
                episode_number = e.EpisodeNumber,
                show_title = e.ShowTitle
            }).ToList(),
            watched
        };
        var response = await _http.PostAsJsonAsync("/tracking/batch", body, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task ToggleAllTvEpisodesAsync(int tmdbId, string showTitle, bool watched)
    {
        var tvmazeData = await GetTvmazeSeasonsAsync(tmdbId);
        if (tvmazeData?.Seasons is null || tvmazeData.TvmazeId is null) return;
        var allEps = tvmazeData.Seasons
            .Where(s => s.Episodes is { Count: > 0 })
            .SelectMany(s => s.Episodes)
            .ToList();
        if (allEps.Count == 0) return;
        var batch = allEps.Select(ep => (
            ep.TvmazeEpisodeId,
            ep.SeasonNumber,
            ep.EpisodeNumber ?? 0,
            showTitle
        )).ToList();
        await ToggleEpisodeBatchAsync(batch, watched);
    }
}
