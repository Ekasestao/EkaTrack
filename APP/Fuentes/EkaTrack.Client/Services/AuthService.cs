using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace EkaTrack.Client.Services
{
    public class AuthService
    {
        private readonly HttpClient _http;
        private readonly IJSRuntime _js;

        public int? UserId { get; private set; }
        public string? Username { get; private set; }
        public bool IsLoggedIn => UserId.HasValue;

        public AuthService(HttpClient http, IJSRuntime js)
        {
            _http = http;
            _js = js;
        }

    public async Task InitAsync()
    {
        try
        {
            var response = await _http.GetFromJsonAsync<InitResponse>("/me");
            if (response?.Status == 200 && response.User is not null)
            {
                UserId = response.User.Id;
                Username = response.User.Username;
            }
        }
        catch
        {
            // No autenticado o error de red
        }
    }

        public async Task<(bool Success, string? Error)> LoginAsync(string credential, string password)
        {
            var response = await _http.PostAsJsonAsync("/login", new
            {
                login_credential = credential,
                password
            });

            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();

            if (result?.Status == 200 && result.User != null)
            {
                UserId = result.User.Id;
                Username = result.User.Username;
                await _js.InvokeVoidAsync("localStorage.setItem", "user_id", result.User.Id.ToString());
                await _js.InvokeVoidAsync("localStorage.setItem", "username", result.User.Username);
                return (true, null);
            }

            return (false, result?.Message ?? "Error de conexión");
        }

        public async Task<bool> RegisterAsync(string username, string email, string password, string samePassword)
        {
            var response = await _http.PostAsJsonAsync("/register", new
            {
                username,
                email,
                password,
                same_password = samePassword
            });

            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();

            if (result?.Status == 200 && result.User != null)
            {
                UserId = result.User.Id;
                Username = result.User.Username;
                await _js.InvokeVoidAsync("localStorage.setItem", "user_id", result.User.Id.ToString());
                await _js.InvokeVoidAsync("localStorage.setItem", "username", result.User.Username);
                return true;
            }

            return false;
        }

        public async Task LogoutAsync()
        {
            await _http.PostAsync("/logout", null);
            UserId = null;
            Username = null;
            await _js.InvokeVoidAsync("localStorage.removeItem", "user_id");
            await _js.InvokeVoidAsync("localStorage.removeItem", "username");
        }

        private class InitResponse
        {
            public int Status { get; set; }
            public UserData? User { get; set; }
        }

        private class AuthResponse
        {
            public int Status { get; set; }
            public string? Message { get; set; }
            public UserData? User { get; set; }
        }

        private class UserData
        {
            public int Id { get; set; }
            public string Username { get; set; } = "";
            public string Email { get; set; } = "";
        }
    }
}