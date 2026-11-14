using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MedApp.Client;
using MedApp.Client.Auth;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<CredentialHandler>();
builder.Services.AddScoped(sp =>
    new HttpClient(sp.GetRequiredService<CredentialHandler>())
    {
        // The API's https launch-profile port, which the Aspire AppHost keeps fixed.
        BaseAddress = new Uri("https://localhost:7038/")
    });

builder.Services.AddScoped<AuthState>();

await builder.Build().RunAsync();