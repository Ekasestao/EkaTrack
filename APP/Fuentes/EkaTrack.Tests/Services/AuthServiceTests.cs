using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EkaTrack.Client.Services;
using Moq;
using Microsoft.JSInterop;
using Moq.Protected;

namespace EkaTrack.Tests.Services;

class StubJsRuntime : IJSRuntime
{
    public string? StoredToken { get; set; }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (typeof(TValue) == typeof(string) && identifier == "localStorage.getItem" && args is ["token"])
            return ValueTask.FromResult((TValue)(object)(StoredToken ?? string.Empty));

        return ValueTask.FromResult(default(TValue)!);
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        return InvokeAsync<TValue>(identifier, args);
    }
}

public class AuthServiceTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly HttpClient _http;
    private readonly StubJsRuntime _jsStub = new();
    private readonly AuthService _service;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public AuthServiceTests()
    {
        _http = new HttpClient(_handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
        _service = new AuthService(_http, _jsStub);
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
    public async Task RegisterAsync_ReturnsFalse_WhenApiReturns403()
    {
        SetupResponse(HttpStatusCode.Forbidden, new { status = 403, message = "Registro cerrado" });

        var result = await _service.RegisterAsync("newuser", "new@test.com", "pass123", "pass123");

        Assert.False(result);
    }

    // ─── InitAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task InitAsync_SendsRequestToMeEndpoint()
    {
        SetupResponse(HttpStatusCode.OK, new { status = 200, user = new { id = 5, username = "testuser", email = "t@t.com" }, token = "abc.def.ghi" });

        await _service.InitAsync();

        var req = GetLastRequest();
        Assert.NotNull(req);
        Assert.Equal(HttpMethod.Get, req!.Method);
        Assert.Equal("/me", req.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task InitAsync_SetsUserFromMeEndpoint()
    {
        SetupResponse(HttpStatusCode.OK, new { status = 200, user = new { id = 5, username = "testuser", email = "t@t.com" }, token = "abc.def.ghi" });

        await _service.InitAsync();

        Assert.True(_service.IsLoggedIn);
        Assert.Equal(5, _service.UserId);
        Assert.Equal("testuser", _service.Username);
    }

    [Fact]
    public async Task InitAsync_DoesNotSetUser_WhenSessionExpired()
    {
        SetupResponse(HttpStatusCode.Unauthorized, new { status = 401, message = "No autenticado" });

        await _service.InitAsync();

        Assert.False(_service.IsLoggedIn);
        Assert.Null(_service.UserId);
        Assert.Null(_service.Username);
    }

    [Fact]
    public async Task InitAsync_DoesNotSetAuthHeader_WhenUnauthenticated()
    {
        SetupResponse(HttpStatusCode.Unauthorized, new { status = 401, message = "No autenticado" });

        await _service.InitAsync();

        Assert.Null(_http.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task InitAsync_SetsAuthHeader_FromStoredToken()
    {
        _jsStub.StoredToken = "eyJ.eyJ9.sig";

        SetupResponse(HttpStatusCode.OK, new { status = 200, user = new { id = 5, username = "testuser", email = "t@t.com" }, token = "new.abc.def" });

        await _service.InitAsync();

        Assert.NotNull(_http.DefaultRequestHeaders.Authorization);
        Assert.Equal("Bearer", _http.DefaultRequestHeaders.Authorization!.Scheme);
        Assert.Equal("new.abc.def", _http.DefaultRequestHeaders.Authorization!.Parameter);
    }

    // ─── LoginAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_SetsAuthHeader_OnSuccess()
    {
        SetupResponse(HttpStatusCode.OK, new { status = 200, user = new { id = 5, username = "testuser", email = "t@t.com" }, token = "abc.def.ghi" });

        var (success, _) = await _service.LoginAsync("testuser", "pass");

        Assert.True(success);
        Assert.NotNull(_http.DefaultRequestHeaders.Authorization);
        Assert.Equal("Bearer", _http.DefaultRequestHeaders.Authorization!.Scheme);
        Assert.Equal("abc.def.ghi", _http.DefaultRequestHeaders.Authorization!.Parameter);
    }

    [Fact]
    public async Task LoginAsync_DoesNotSetAuthHeader_OnFailure()
    {
        SetupResponse(HttpStatusCode.Unauthorized, new { status = 401, message = "Credenciales incorrectas" });

        var (success, _) = await _service.LoginAsync("testuser", "wrongpass");

        Assert.False(success);
        Assert.Null(_http.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public async Task LogoutAsync_ClearsAuthHeader()
    {
        _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "abc.def.ghi");
        SetupResponse(HttpStatusCode.OK, new { status = 200 });

        await _service.LogoutAsync();

        Assert.Null(_http.DefaultRequestHeaders.Authorization);
    }
}
