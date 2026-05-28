using System.Net.Http.Json;
using System.Text.Json;
using EkaTrack.Client.Models;

namespace EkaTrack.Client.Services;

public class TmdbService
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public TmdbService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<TmdbMediaItem>> GetTrendingAsync()
    {
        var response = await _http.GetAsync("/trending");
        response.EnsureSuccessStatusCode();
        var wrapper = await response.Content.ReadFromJsonAsync<TmdbTrendingResponse>(JsonOptions);
        return wrapper?.Results ?? [];
    }

    public async Task<TmdbMediaDetail?> GetMediaDetailAsync(string mediaType, int tmdbId)
    {
        var response = await _http.GetAsync($"/media/{mediaType}/{tmdbId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TmdbMediaDetail>(JsonOptions);
    }

    public async Task<List<TmdbMediaItem>> SearchAsync(string query, int page = 1)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var response = await _http.GetAsync($"/search?q={Uri.EscapeDataString(query)}&page={page}");
        response.EnsureSuccessStatusCode();
        var wrapper = await response.Content.ReadFromJsonAsync<TmdbSearchResponse>(JsonOptions);
        return wrapper?.Results ?? [];
    }

    public async Task<TmdbPaginatedResponse> GetTrendingPageAsync(int page = 1, string mediaType = "all", string timeWindow = "week")
    {
        var response = await _http.GetAsync($"/trending?media_type={mediaType}&time_window={timeWindow}&page={page}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TmdbPaginatedResponse>(JsonOptions))!;
    }

    public async Task<TmdbPaginatedResponse> GetNowPlayingAsync(int page = 1)
    {
        var response = await _http.GetAsync($"/nuevo/estrenos?page={page}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TmdbPaginatedResponse>(JsonOptions))!;
    }

    public async Task<TmdbPaginatedResponse> GetUpcomingAsync(int page = 1)
    {
        var response = await _http.GetAsync($"/nuevo/proximamente?page={page}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TmdbPaginatedResponse>(JsonOptions))!;
    }

    public async Task<bool> VoteAsync(string mediaType, int tmdbId, int? value)
    {
        var response = await _http.PostAsJsonAsync("/vote", new
        {
            media_type = mediaType,
            tmdb_id = tmdbId,
            value
        }, JsonOptions);
        return response.IsSuccessStatusCode;
    }

    private class TmdbTrendingResponse
    {
        public List<TmdbMediaItem> Results { get; set; } = [];
    }

    private class TmdbSearchResponse
    {
        public List<TmdbMediaItem> Results { get; set; } = [];
    }
}
