using MedApp.API.Common.Extensions;
using MedApp.Application.Common.Extensions;
using MedApp.Infrastructure.Common.Extensions;
using MedApp.Infrastructure.Common.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddApi(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// A deploy's one-off command (the Helm chart's migrations Job): make the first admin if there are no accounts, then
// exit, without starting the web server.
if (args is [FirstAdminSetup.Command])
    return await FirstAdminSetup.RunCommandAsync(app.Services, CancellationToken.None);

app.MapDefaultEndpoints();
app.UseApi();

app.Run();
return 0;
