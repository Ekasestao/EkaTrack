using EkaTrack.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<EkaTrack.Client.Routes>("#app");
builder.RootComponents.Add<Microsoft.AspNetCore.Components.Web.HeadOutlet>("head::after");

builder.Services.AddRadzenComponents();

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://ekawatch.pythonanywhere.com/")
});

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ListsService>();
builder.Services.AddScoped<TmdbService>();
builder.Services.AddScoped<TvmazeService>();

await builder.Build().RunAsync();