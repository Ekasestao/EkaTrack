using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EkaWatch.Client.Services;
using Moq;
using Moq.Protected;

namespace EkaWatch.Tests.Services;

public class ListsServiceTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly HttpClient _http;
    private readonly ListsService _service;

    public ListsServiceTests()
    {
        _http = new HttpClient(_handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
        _service = new ListsService(_http);
    }

    private void SetupResponse(HttpStatusCode status, object? body)
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = body is null ? null : JsonContent.Create(body, options: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
            });
    }

    private HttpRequestMessage? GetLastRequest() =>
        _handlerMock.Invocations
            .Where(i => i.Method.Name == "SendAsync")
            .Select(i => i.Arguments[0] as HttpRequestMessage)
            .LastOrDefault();


    // ─── GetListsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetListsAsync_ReturnsLists()
    {
        var expected = new[]
        {
            new { id = 1, name = "Watchlist", description = "", list_type = "watchlist", item_count = 0, created_at = "2025-01-01T00:00:00" },
            new { id = 2, name = "Visto", description = "", list_type = "watched", item_count = 0, created_at = "2025-01-01T00:00:00" },
        };
        SetupResponse(HttpStatusCode.OK, expected);

        var result = await _service.GetListsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Watchlist", result[0].Name);
        Assert.Equal("watchlist", result[0].ListType);
    }

    [Fact]
    public async Task GetListsAsync_SendsRequestToCorrectUrl()
    {
        SetupResponse(HttpStatusCode.OK, Array.Empty<object>());
        await _service.GetListsAsync();

        var req = GetLastRequest();
        Assert.NotNull(req);
        Assert.Equal(HttpMethod.Get, req!.Method);
        Assert.Equal("/lists", req.RequestUri?.AbsolutePath);
    }


    // ─── CreateListAsync ───────────────────────────────────────────────

    [Fact]
    public async Task CreateListAsync_SendsPostWithName()
    {
        SetupResponse(HttpStatusCode.OK, new { id = 3, name = "Favoritas", description = "", list_type = "custom", item_count = 0, created_at = "2025-01-01T00:00:00" });

        var result = await _service.CreateListAsync("Favoritas", null);

        Assert.Equal("Favoritas", result.Name);
        Assert.Equal("custom", result.ListType);

        var req = GetLastRequest();
        Assert.Equal(HttpMethod.Post, req!.Method);
        Assert.Equal("/lists", req.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task CreateListAsync_SendsDescription()
    {
        SetupResponse(HttpStatusCode.OK, new { id = 3, name = "Test", description = "My list", list_type = "custom", item_count = 0, created_at = "2025-01-01T00:00:00" });

        await _service.CreateListAsync("Test", "My list");

        var body = await GetLastRequest()!.Content!.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("My list", body.GetProperty("description").GetString());
    }


    // ─── UpdateListAsync ───────────────────────────────────────────────

    [Fact]
    public async Task UpdateListAsync_SendsPatchWithName()
    {
        SetupResponse(HttpStatusCode.OK, new { });
        await _service.UpdateListAsync(1, "New name", "New desc");

        var req = GetLastRequest();
        Assert.Equal(HttpMethod.Patch, req!.Method);
        Assert.Equal("/lists/1", req.RequestUri?.AbsolutePath);
    }


    // ─── DeleteListAsync ───────────────────────────────────────────────

    [Fact]
    public async Task DeleteListAsync_SendsDelete()
    {
        SetupResponse(HttpStatusCode.OK, new { });
        await _service.DeleteListAsync(1);

        var req = GetLastRequest();
        Assert.Equal(HttpMethod.Delete, req!.Method);
        Assert.Equal("/lists/1", req.RequestUri?.AbsolutePath);
    }


    // ─── GetListItemsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetListItemsAsync_ReturnsItems()
    {
        var expected = new[]
        {
            new { id = 1, tmdb_id = 550, media_type = "movie", title = "Fight Club", poster_path = "/poster.jpg", added_at = "2025-01-01T00:00:00" }
        };
        SetupResponse(HttpStatusCode.OK, expected);

        var items = await _service.GetListItemsAsync(1);

        Assert.Single(items);
        Assert.Equal("Fight Club", items[0].Title);
        Assert.Equal("movie", items[0].MediaType);
    }

    [Fact]
    public async Task GetListItemsAsync_SendsCorrectUrl()
    {
        SetupResponse(HttpStatusCode.OK, Array.Empty<object>());
        await _service.GetListItemsAsync(1);

        var req = GetLastRequest();
        Assert.Equal("/lists/1/items", req!.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetListItemsAsync_WithMediaTypeFilter_SendsQueryParam()
    {
        SetupResponse(HttpStatusCode.OK, Array.Empty<object>());
        await _service.GetListItemsAsync(1, "movie");

        var req = GetLastRequest();
        Assert.Contains("?media_type=movie", req!.RequestUri?.Query);
    }


    // ─── AddItemToListAsync ────────────────────────────────────────────

    [Fact]
    public async Task AddItemToListAsync_SendsPostWithItemData()
    {
        SetupResponse(HttpStatusCode.OK, new { id = 10, tmdb_id = 550, media_type = "movie", title = "Fight Club", poster_path = "/p.jpg", added_at = "2025-01-01T00:00:00" });

        var result = await _service.AddItemToListAsync(1, 550, "movie", "Fight Club", "/p.jpg");

        Assert.Equal("Fight Club", result.Title);
        Assert.Equal(550, result.TmdbId);

        var req = GetLastRequest();
        Assert.Equal(HttpMethod.Post, req!.Method);
        Assert.Contains("/lists/1/items", req!.RequestUri?.AbsolutePath);
    }


    // ─── RemoveItemFromListAsync ──────────────────────────────────────

    [Fact]
    public async Task RemoveItemFromListAsync_SendsDelete()
    {
        SetupResponse(HttpStatusCode.OK, new { });
        await _service.RemoveItemFromListAsync(1, 42);

        var req = GetLastRequest();
        Assert.Equal(HttpMethod.Delete, req!.Method);
        Assert.Equal("/lists/1/items/42", req!.RequestUri?.AbsolutePath);
    }
}
