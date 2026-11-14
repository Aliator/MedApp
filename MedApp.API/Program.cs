using MedApp.API.Common.Extensions;
using MedApp.Application.Common.Extensions;
using MedApp.Infrastructure.Common.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseApi();

app.Run();