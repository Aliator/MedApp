using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.DeletePatientProcedureStageTemplate;

public sealed record DeletePatientProcedureTemplateCommand(Guid TemplateId) : IRequest<bool>;