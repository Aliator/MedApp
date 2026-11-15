using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedures.Commands.CreatePatientProcedure;

/// <summary>Starts a procedure for a patient from a template, copying the template's stages. A blank title uses the template's name.</summary>
public sealed record CreatePatientProcedureCommand(
    Guid PatientId,
    Guid TemplateId,
    string? Title,
    string Notes,
    DateOnly DueDate,
    string Priority,
    ProcedureActor Actor)
    : IRequest<PatientProcedureResponse>;
