using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EkaTrack.Client.Services;
using Moq;
using Microsoft.JSInterop;
using Moq.Protected;

namespace EkaTrack.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly HttpClient _http;
    private readonly Mock<IJSRuntime> _jsMock = new();
    private readonly AuthService _service;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public AuthServiceTests()
    {
        _http = new HttpClient(_handlerMock.Object) { BaseAddress = new Uri("http://localhost/") };
        _service = new AuthService(_http, _jsMock.Object);
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

    [Fact]
    public async Task RegisterAsync_ReturnsFalse_WhenApiReturns403()
    {
        SetupResponse(HttpStatusCode.Forbidden, new { status = 403, message = "Registro cerrado" });

        var result = await _service.RegisterAsync("newuser", "new@test.com", "pass123", "pass123");

        Assert.False(result);
    }
}
