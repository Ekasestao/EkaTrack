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

        private void SetAuthHeader()
        {
            if (UserId.HasValue)
            {
                _http.DefaultRequestHeaders.Remove("X-User-Id");
                _http.DefaultRequestHeaders.Add("X-User-Id", UserId.Value.ToString());
            }
        }

        private void RemoveAuthHeader()
        {
            _http.DefaultRequestHeaders.Remove("X-User-Id");
        }

        public async Task InitAsync()
        {
            var id = await _js.InvokeAsync<string>("localStorage.getItem", "user_id");
            var username = await _js.InvokeAsync<string>("localStorage.getItem", "username");

            if (!string.IsNullOrEmpty(id))
            {
                UserId = int.Parse(id);
                Username = username;
                SetAuthHeader();
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
                SetAuthHeader();
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
                SetAuthHeader();
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
            RemoveAuthHeader();
            await _js.InvokeVoidAsync("localStorage.removeItem", "user_id");
            await _js.InvokeVoidAsync("localStorage.removeItem", "username");
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