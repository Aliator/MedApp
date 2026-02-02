using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MedApp.Client;
using MedApp.Client.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ =>
    new HttpClient
    {
        BaseAddress = new Uri("https://localhost:44305/")
    });

builder.Services.AddScoped<AuthState>();

await builder.Build().RunAsync();