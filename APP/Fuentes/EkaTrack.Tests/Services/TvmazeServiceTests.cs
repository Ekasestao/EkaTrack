using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EkaTrack.Client.Models;
using EkaTrack.Client.Services;
using Moq;
using Moq.Protected;

namespace EkaTrack.Tests.Services;

public class TvmazeServiceTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly HttpClient _http;
    private readonly TvmazeService _service;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public TvmazeServiceTests()
    {
        _http = new HttpClient(_handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
        _service = new TvmazeService(_http);
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
    public async Task GetTvmazeSeasonsAsync_SendsRequestToCorrectUrl()
    {
        SetupResponse(HttpStatusCode.OK, new { tvmaze_id = 14459, seasons = new object[] { } });

        await _service.GetTvmazeSeasonsAsync(65942);

        var req = GetLastRequest();
        Assert.NotNull(req);
        Assert.Equal(HttpMethod.Get, req.Method);
        Assert.Equal("/media/tv/65942/tvmaze", req.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task GetTvmazeSeasonsAsync_ReturnsSeasons()
    {
        var data = new
        {
            tvmaze_id = 14459,
            seasons = new[]
            {
                new
                {
                    tvmaze_season_id = 38865,
                    season_number = 1,
                    name = "Season 1",
                    episode_count = 25,
                    poster_url = "https://static.tvmaze.com/poster.jpg",
                    episodes = new[]
                    {
                        new
                        {
                            tvmaze_episode_id = 1001,
                            season_number = 1,
                            episode_number = 1,
                            name = "Episode 1",
                            still_url = (string?)null,
                            air_date = "2016-04-04",
                            runtime = 24,
                            summary = "<p>First episode</p>",
                            watched = false
                        }
                    }
                }
            }
        };

        SetupResponse(HttpStatusCode.OK, data);

        var result = await _service.GetTvmazeSeasonsAsync(65942);

        Assert.NotNull(result);
        Assert.Equal(14459, result.TvmazeId);
        Assert.Single(result.Seasons);
        Assert.Equal(1, result.Seasons[0].SeasonNumber);
        Assert.Equal("Season 1", result.Seasons[0].Name);
        Assert.Equal(25, result.Seasons[0].EpisodeCount);
        Assert.Equal("https://static.tvmaze.com/poster.jpg", result.Seasons[0].PosterUrl);
        Assert.Single(result.Seasons[0].Episodes);
        Assert.Equal(1001, result.Seasons[0].Episodes[0].TvmazeEpisodeId);
        Assert.Equal(1, result.Seasons[0].Episodes[0].EpisodeNumber);
        Assert.Equal("Episode 1", result.Seasons[0].Episodes[0].Name);
        Assert.False(result.Seasons[0].Episodes[0].Watched);
    }

    [Fact]
    public async Task GetTvmazeSeasonsAsync_ReturnsNullWhenNotFound()
    {
        SetupResponse(HttpStatusCode.NotFound, null);

        var result = await _service.GetTvmazeSeasonsAsync(99999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetTvmazeSeasonsAsync_HandlesNullTvmazeId()
    {
        var data = new
        {
            tvmaze_id = (int?)null,
            seasons = new object[] { }
        };

        SetupResponse(HttpStatusCode.OK, data);

        var result = await _service.GetTvmazeSeasonsAsync(99999);

        Assert.NotNull(result);
        Assert.Null(result.TvmazeId);
        Assert.Empty(result.Seasons);
    }

    [Fact]
    public async Task ToggleEpisodeWatchedAsync_SendsRequestToCorrectUrl()
    {
        SetupResponse(HttpStatusCode.OK, new { status = 200, watched = true });

        await _service.ToggleEpisodeWatchedAsync(1001, 1, 1, "Re:Zero");

        var req = GetLastRequest();
        Assert.NotNull(req);
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("/tracking/episode/1001/toggle", req.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task ToggleEpisodeWatchedAsync_ReturnsTrueOnSuccess()
    {
        SetupResponse(HttpStatusCode.OK, new { status = 200, watched = true });

        var result = await _service.ToggleEpisodeWatchedAsync(1001, 1, 1, "Re:Zero");

        Assert.True(result);
    }

    [Fact]
    public async Task ToggleEpisodeWatchedAsync_ReturnsFalseOnFailure()
    {
        SetupResponse(HttpStatusCode.Unauthorized, null);

        var result = await _service.ToggleEpisodeWatchedAsync(1001, 1, 1, "Re:Zero");

        Assert.False(result);
    }

    [Fact]
    public async Task ToggleAllTvEpisodesAsync_SendsBatchRequest()
    {
        var seasonsData = new
        {
            tvmaze_id = 14459,
            seasons = new[]
            {
                new
                {
                    tvmaze_season_id = 38865,
                    season_number = 1,
                    name = "Season 1",
                    episode_count = 1,
                    poster_url = (string?)null,
                    episodes = new[]
                    {
                        new
                        {
                            tvmaze_episode_id = 1001,
                            season_number = 1,
                            episode_number = 1,
                            name = "Ep1",
                            still_url = (string?)null,
                            air_date = "2024-01-01",
                            runtime = 24,
                            summary = "",
                            watched = false
                        }
                    }
                }
            }
        };

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.AbsolutePath == "/media/tv/12345/tvmaze"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(seasonsData, options: JsonOptions)
            });

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.AbsolutePath == "/tracking/batch"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        await _service.ToggleAllTvEpisodesAsync(12345, "TestShow", true);

        _handlerMock.Protected().Verify("SendAsync", Times.Exactly(2),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ToggleAllTvEpisodesAsync_FiltersFutureEpisodesWhenWatched()
    {
        var seasonsData = new
        {
            tvmaze_id = 14459,
            seasons = new[]
            {
                new
                {
                    tvmaze_season_id = 38865,
                    season_number = 1,
                    name = "Season 1",
                    episode_count = 2,
                    poster_url = (string?)null,
                    episodes = new[]
                    {
                        new
                        {
                            tvmaze_episode_id = 1001,
                            season_number = 1,
                            episode_number = 1,
                            name = "Ep1",
                            still_url = (string?)null,
                            air_date = "2024-01-01",
                            runtime = 24,
                            summary = "",
                            watched = false
                        },
                        new
                        {
                            tvmaze_episode_id = 2001,
                            season_number = 1,
                            episode_number = 2,
                            name = "Ep2",
                            still_url = (string?)null,
                            air_date = "2099-12-31",
                            runtime = 24,
                            summary = "",
                            watched = false
                        }
                    }
                }
            }
        };

        HttpRequestMessage? batchRequest = null;

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.AbsolutePath == "/media/tv/12345/tvmaze"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(seasonsData, options: JsonOptions)
            });

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.AbsolutePath == "/tracking/batch"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => batchRequest = req);

        await _service.ToggleAllTvEpisodesAsync(12345, "TestShow", true);

        Assert.NotNull(batchRequest);
        var body = await batchRequest.Content!.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        var episodes = json.RootElement.GetProperty("episodes").EnumerateArray().ToList();
        Assert.Single(episodes);
        Assert.Equal(1001, episodes[0].GetProperty("tvmaze_episode_id").GetInt64());
    }

    [Fact]
    public async Task ToggleAllTvEpisodesAsync_IncludesFutureEpisodesWhenUnwatch()
    {
        var seasonsData = new
        {
            tvmaze_id = 14459,
            seasons = new[]
            {
                new
                {
                    tvmaze_season_id = 38865,
                    season_number = 1,
                    name = "Season 1",
                    episode_count = 2,
                    poster_url = (string?)null,
                    episodes = new[]
                    {
                        new
                        {
                            tvmaze_episode_id = 1001,
                            season_number = 1,
                            episode_number = 1,
                            name = "Ep1",
                            still_url = (string?)null,
                            air_date = "2024-01-01",
                            runtime = 24,
                            summary = "",
                            watched = false
                        },
                        new
                        {
                            tvmaze_episode_id = 2001,
                            season_number = 1,
                            episode_number = 2,
                            name = "Ep2",
                            still_url = (string?)null,
                            air_date = "2099-12-31",
                            runtime = 24,
                            summary = "",
                            watched = false
                        }
                    }
                }
            }
        };

        HttpRequestMessage? batchRequest = null;

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.AbsolutePath == "/media/tv/12345/tvmaze"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(seasonsData, options: JsonOptions)
            });

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.AbsolutePath == "/tracking/batch"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => batchRequest = req);

        await _service.ToggleAllTvEpisodesAsync(12345, "TestShow", false);

        Assert.NotNull(batchRequest);
        var body = await batchRequest.Content!.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        var episodes = json.RootElement.GetProperty("episodes").EnumerateArray().ToList();
        Assert.Equal(2, episodes.Count);
    }

    [Fact]
    public async Task ToggleAllTvEpisodesAsync_DoesNothingWhenTvmazeNull()
    {
        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.AbsolutePath == "/media/tv/99999/tvmaze"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound));

        await _service.ToggleAllTvEpisodesAsync(99999, "Unknown", true);

        _handlerMock.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
}
