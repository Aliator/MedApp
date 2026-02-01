using FluentValidation;
using FluentValidation.AspNetCore;
using MedApp.Application;
using MedApp.Application.Common.Extensions;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Application.Patients.Commands.UpdatePatient;
using MedApp.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();