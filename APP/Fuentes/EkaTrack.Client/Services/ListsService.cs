using System.Net.Http.Json;
using System.Text.Json;
using EkaTrack.Client.Models;

namespace EkaTrack.Client.Services;

public class ListsService
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public ListsService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ListInfo>> GetListsAsync()
    {
        var response = await _http.GetAsync("/lists");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<ListInfo>>(JsonOptions);
        return result ?? [];
    }

    public async Task<ListInfo> CreateListAsync(string name, string? description)
    {
        var response = await _http.PostAsJsonAsync("/lists", new { name, description }, JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ListInfo>(JsonOptions))!;
    }

    public async Task UpdateListAsync(int listId, string name, string? description)
    {
        var response = await _http.PatchAsJsonAsync($"/lists/{listId}", new { name, description }, JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteListAsync(int listId)
    {
        var response = await _http.DeleteAsync($"/lists/{listId}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<ListItemModel>> GetListItemsAsync(int listId, string? mediaType = null)
    {
        var url = $"/lists/{listId}/items";
        if (!string.IsNullOrEmpty(mediaType))
            url += $"?media_type={mediaType}";

        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<ListItemModel>>(JsonOptions);
        return result ?? [];
    }

    public async Task<ListItemModel> AddItemToListAsync(int listId, int tmdbId, string mediaType, string title, string? posterPath, double? voteAverage = null)
    {
        var response = await _http.PostAsJsonAsync($"/lists/{listId}/items", new
        {
            tmdb_id = tmdbId,
            media_type = mediaType,
            title,
            poster_path = posterPath,
            vote_average = voteAverage
        }, JsonOptions);
        if (response.IsSuccessStatusCode)
            return (await response.Content.ReadFromJsonAsync<ListItemModel>(JsonOptions))!;
        if ((int)response.StatusCode == 409)
        {
            var items = await GetListItemsAsync(listId);
            return items.First(i => i.TmdbId == tmdbId && i.MediaType == mediaType);
        }
        response.EnsureSuccessStatusCode();
        throw new InvalidOperationException("Unreachable");
    }

    public async Task RemoveItemFromListAsync(int listId, int itemId)
    {
        var response = await _http.DeleteAsync($"/lists/{listId}/items/{itemId}");
        response.EnsureSuccessStatusCode();
    }
}
