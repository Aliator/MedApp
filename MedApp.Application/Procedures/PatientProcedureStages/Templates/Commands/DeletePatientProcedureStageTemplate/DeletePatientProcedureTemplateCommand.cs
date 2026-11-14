using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.DeletePatientProcedureStageTemplate;

/// <summary>Deletes a template that no procedure was created from; one that has been used is archived instead.</summary>
public sealed record DeletePatientProcedureTemplateCommand(Guid TemplateId) : IRequest;
