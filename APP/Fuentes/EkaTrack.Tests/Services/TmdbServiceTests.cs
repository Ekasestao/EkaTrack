using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EkaTrack.Client.Models;
using EkaTrack.Client.Services;
using Moq;
using Moq.Protected;

namespace EkaTrack.Tests.Services;

public class TmdbServiceTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly HttpClient _http;
    private readonly TmdbService _service;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public TmdbServiceTests()
    {
        _http = new HttpClient(_handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
        _service = new TmdbService(_http);
    }

    private void SetupResponse(HttpStatusCode status, object? body)
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = body is null ? null : JsonContent.Create(body, options: JsonOptions)
            });
    }

    private HttpRequestMessage? GetLastRequest() =>
        _handlerMock.Invocations
            .Where(i => i.Method.Name == "SendAsync")
            .Select(i => i.Arguments[0] as HttpRequestMessage)
            .LastOrDefault();

    [Fact]
    public async Task GetTrendingAsync_ReturnsMediaItems()
    {
        var results = new[]
        {
            new
            {
                id = 1,
                title = (string?)"Movie One",
                name = (string?)null,
                media_type = "movie",
                poster_path = "/poster1.jpg",
                vote_average = 8.5,
                overview = "A great movie"
            },
            new
            {
                id = 2,
                title = (string?)null,
                name = (string?)"TV Show One",
                media_type = "tv",
                poster_path = "/poster2.jpg",
                vote_average = 7.2,
                overview = "A great show"
            }
        };

        SetupResponse(HttpStatusCode.OK, new { results });

        var items = await _service.GetTrendingAsync();

        Assert.Equal(2, items.Count);

        Assert.Equal(1, items[0].Id);
        Assert.Equal("Movie One", items[0].Title);
        Assert.Equal("movie", items[0].MediaType);
        Assert.Equal("/poster1.jpg", items[0].PosterPath);
        Assert.Equal(8.5, items[0].VoteAverage);

        Assert.Equal(2, items[1].Id);
        Assert.Equal("TV Show One", items[1].Title);
        Assert.Equal("tv", items[1].MediaType);
        Assert.Equal("/poster2.jpg", items[1].PosterPath);
        Assert.Equal(7.2, items[1].VoteAverage);
    }

    [Fact]
    public async Task GetTrendingAsync_SendsRequestToCorrectUrl()
    {
        SetupResponse(HttpStatusCode.OK, new { results = Array.Empty<object>() });

        await _service.GetTrendingAsync();

        var req = GetLastRequest();
        Assert.NotNull(req);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Equal("/trending", req.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetTrendingAsync_UsesTitleWhenNameIsNull()
    {
        var results = new[]
        {
            new
            {
                id = 1,
                title = "Only Title",
                name = (string?)null,
                media_type = "movie",
                poster_path = "/p.jpg",
                vote_average = 5.0,
                overview = "desc"
            }
        };

        SetupResponse(HttpStatusCode.OK, new { results });

        var items = await _service.GetTrendingAsync();

        Assert.Single(items);
        Assert.Equal("Only Title", items[0].Title);
    }

    [Fact]
    public async Task GetTrendingAsync_UsesNameWhenTitleIsNull()
    {
        var results = new[]
        {
            new
            {
                id = 1,
                title = (string?)null,
                name = "Only Name",
                media_type = "tv",
                poster_path = "/p.jpg",
                vote_average = 5.0,
                overview = "desc"
            }
        };

        SetupResponse(HttpStatusCode.OK, new { results });

        var items = await _service.GetTrendingAsync();

        Assert.Single(items);
        Assert.Equal("Only Name", items[0].Title);
    }

    [Fact]
    public async Task GetMediaDetailAsync_SendsRequestToCorrectUrl()
    {
        SetupResponse(HttpStatusCode.OK, new { });

        await _service.GetMediaDetailAsync("movie", 550);

        var req = GetLastRequest();
        Assert.NotNull(req);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Equal("/media/movie/550", req.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetMediaDetailAsync_ReturnsMovieDetail()
    {
        var data = new
        {
            id = 550,
            title = "Fight Club",
            overview = "A ticking time bomb.",
            poster_path = "/poster.jpg",
            backdrop_path = "/backdrop.jpg",
            vote_average = 8.4,
            vote_count = 28000,
            release_date = "1999-10-15",
            runtime = 139,
            tagline = "Mischief. Mayhem. Soap.",
            status = "Released",
            genres = new[]
            {
                new { id = 18, name = "Drama" }
            }
        };

        SetupResponse(HttpStatusCode.OK, data);

        var detail = await _service.GetMediaDetailAsync("movie", 550);

        Assert.NotNull(detail);
        Assert.Equal(550, detail.Id);
        Assert.Equal("Fight Club", detail.Title);
        Assert.Equal("A ticking time bomb.", detail.Overview);
        Assert.Equal("/poster.jpg", detail.PosterPath);
        Assert.Equal("/backdrop.jpg", detail.BackdropPath);
        Assert.Equal(8.4, detail.VoteAverage);
        Assert.Equal(28000, detail.VoteCount);
        Assert.Equal("1999-10-15", detail.ReleaseDate);
        Assert.Equal("1999-10-15", detail.ReleaseDateDisplay);
        Assert.Equal(139, detail.Runtime);
        Assert.Equal("Mischief. Mayhem. Soap.", detail.Tagline);
        Assert.Equal("Released", detail.Status);

        Assert.Single(detail.Genres);
        Assert.Equal(18, detail.Genres[0].Id);
        Assert.Equal("Drama", detail.Genres[0].Name);
    }

    [Fact]
    public async Task GetMediaDetailAsync_ReturnsTvDetail()
    {
        var data = new
        {
            id = 1399,
            name = "Game of Thrones",
            overview = "Seven noble families fight.",
            poster_path = "/got.jpg",
            backdrop_path = "/got-bg.jpg",
            vote_average = 8.9,
            vote_count = 22000,
            first_air_date = "2011-04-17",
            episode_run_time = new[] { 60 },
            tagline = "Winter is coming",
            status = "Ended",
            genres = new[]
            {
                new { id = 10765, name = "Sci-Fi & Fantasy" }
            }
        };

        SetupResponse(HttpStatusCode.OK, data);

        var detail = await _service.GetMediaDetailAsync("tv", 1399);

        Assert.NotNull(detail);
        Assert.Equal(1399, detail.Id);
        Assert.Equal("Game of Thrones", detail.Title);
        Assert.Equal("2011-04-17", detail.FirstAirDate);
        Assert.Equal("2011-04-17", detail.ReleaseDateDisplay);
        Assert.Equal("Winter is coming", detail.Tagline);
    }

    [Fact]
    public async Task GetMediaDetailAsync_ReturnsCredits()
    {
        var data = new
        {
            id = 550,
            title = "Fight Club",
            credits = new
            {
                cast = new[]
                {
                    new { id = 819, name = "Edward Norton", character = "The Narrator", profile_path = "/ed.jpg", order = 0 },
                    new { id = 287, name = "Brad Pitt", character = "Tyler Durden", profile_path = "/brad.jpg", order = 1 }
                },
                crew = new[]
                {
                    new { id = 376, name = "David Fincher", job = "Director", department = "Directing", profile_path = "/fincher.jpg" }
                }
            }
        };

        SetupResponse(HttpStatusCode.OK, data);

        var detail = await _service.GetMediaDetailAsync("movie", 550);

        Assert.NotNull(detail);
        Assert.NotNull(detail.Credits);
        Assert.Equal(2, detail.Credits.Cast.Count);
        Assert.Equal("Edward Norton", detail.Credits.Cast[0].Name);
        Assert.Equal("The Narrator", detail.Credits.Cast[0].Character);
        Assert.Equal("/ed.jpg", detail.Credits.Cast[0].ProfilePath);
        Assert.Equal(0, detail.Credits.Cast[0].Order);

        Assert.Single(detail.Credits.Crew);
        Assert.Equal("David Fincher", detail.Credits.Crew[0].Name);
        Assert.Equal("Director", detail.Credits.Crew[0].Job);
    }

    [Fact]
    public async Task SearchAsync_SendsRequestToCorrectUrl()
    {
        SetupResponse(HttpStatusCode.OK, new { results = Array.Empty<object>() });

        await _service.SearchAsync("test");

        var req = GetLastRequest();
        Assert.NotNull(req);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Equal("/search", req.RequestUri?.AbsolutePath);
        Assert.Equal("?q=test&page=1", req.RequestUri?.Query);
    }

    [Fact]
    public async Task SearchAsync_ReturnsResults()
    {
        var results = new[]
        {
            new
            {
                id = 1,
                title = "Test Movie",
                name = (string?)null,
                media_type = "movie",
                poster_path = "/p.jpg",
                vote_average = 7.5,
                overview = "A test movie"
            }
        };

        SetupResponse(HttpStatusCode.OK, new { results });

        var items = await _service.SearchAsync("test");

        Assert.Single(items);
        Assert.Equal("Test Movie", items[0].Title);
        Assert.Equal("movie", items[0].MediaType);
    }

    [Fact]
    public async Task SearchAsync_SendsPageParam()
    {
        SetupResponse(HttpStatusCode.OK, new { results = Array.Empty<object>() });

        await _service.SearchAsync("test", 3);

        var req = GetLastRequest();
        Assert.NotNull(req);
        Assert.Equal("?q=test&page=3", req.RequestUri?.Query);
    }

    [Fact]
    public async Task GetNowPlayingAsync_SendsRequestToCorrectUrl()
    {
        SetupResponse(HttpStatusCode.OK, new { results = Array.Empty<object>(), page = 1, total_pages = 1 });

        await _service.GetNowPlayingAsync();

        var req = GetLastRequest();
        Assert.NotNull(req);
        Assert.Equal("/nuevo/estrenos", req.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetNowPlayingAsync_ReturnsResults()
    {
        var data = new
        {
            results = new[]
            {
                new { id = 1, title = "New Movie", media_type = "movie", poster_path = "/p.jpg", vote_average = 7.0, overview = "desc", release_date = "2024-01-15" }
            },
            page = 1,
            total_pages = 5
        };

        SetupResponse(HttpStatusCode.OK, data);

        var result = await _service.GetNowPlayingAsync();

        Assert.Single(result.Results);
        Assert.Equal("New Movie", result.Results[0].Title);
        Assert.Equal("2024-01-15", result.Results[0].ReleaseDate);
        Assert.Equal(5, result.TotalPages);
    }

    [Fact]
    public async Task GetUpcomingAsync_ReturnsResults()
    {
        var data = new
        {
            results = new[]
            {
                new { id = 2, title = "Upcoming Movie", media_type = "movie", poster_path = "/p2.jpg", vote_average = 8.0, overview = "desc", release_date = "2024-06-01" }
            },
            page = 1,
            total_pages = 10
        };

        SetupResponse(HttpStatusCode.OK, data);

        var result = await _service.GetUpcomingAsync();

        Assert.Single(result.Results);
        Assert.Equal("Upcoming Movie", result.Results[0].Title);
        Assert.Equal("2024-06-01", result.Results[0].ReleaseDate);
        Assert.Equal(10, result.TotalPages);
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmptyWhenNoQuery()
    {
        var items = await _service.SearchAsync("");

        Assert.Empty(items);
    }

    [Fact]
    public async Task GetTrendingPageAsync_SendsPageParam()
    {
        SetupResponse(HttpStatusCode.OK, new { results = Array.Empty<object>(), page = 2, total_pages = 10 });

        await _service.GetTrendingPageAsync(2);

        var req = GetLastRequest();
        Assert.NotNull(req);
        Assert.Equal("?media_type=all&time_window=week&page=2", req.RequestUri?.Query);
    }

    [Fact]
    public async Task GetTrendingPageAsync_ReturnsPaginationInfo()
    {
        var data = new
        {
            results = new[]
            {
                new { id = 1, title = "Movie", media_type = "movie", poster_path = "/p.jpg", vote_average = 7.0, overview = "desc" }
            },
            page = 1,
            total_pages = 500
        };

        SetupResponse(HttpStatusCode.OK, data);

        var result = await _service.GetTrendingPageAsync(1);

        Assert.NotNull(result);
        Assert.Equal(1, result.Page);
        Assert.Equal(500, result.TotalPages);
        Assert.Single(result.Results);
        Assert.Equal("Movie", result.Results[0].Title);
    }

}
