using MedApp.API.Common.Extensions;
using MedApp.Application.Common.Extensions;
using MedApp.Infrastructure.Common.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApi(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseApi();

app.Run();