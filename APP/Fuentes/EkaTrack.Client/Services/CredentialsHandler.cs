using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace EkaTrack.Client.Services
{
    public class CredentialsHandler : DelegatingHandler
    {
        public CredentialsHandler(HttpMessageHandler innerHandler)
            : base(innerHandler) { }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
