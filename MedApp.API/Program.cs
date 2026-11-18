using MedApp.API.Common.Extensions;
using MedApp.API.Jobs;
using MedApp.Application.Common.Extensions;
using MedApp.Infrastructure.Common.Extensions;
using MedApp.Infrastructure.Common.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddApi(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScheduledJobHost();

var app = builder.Build();

// A deploy's one-off command (the Helm chart's migrations Job): make the first admin if there are no accounts, then
// exit, without starting the web server.
if (args is [FirstAdminSetup.Command])
    return await FirstAdminSetup.RunCommandAsync(app.Services, CancellationToken.None);

// Runs one scheduled job now (taking its lease like any run), then exits: for running a job by hand.
if (args is [RunJobCommand.Command, var jobName])
    return await RunJobCommand.RunAsync(app.Services, jobName, CancellationToken.None);

app.MapDefaultEndpoints();
app.UseApi();

app.Run();
return 0;
