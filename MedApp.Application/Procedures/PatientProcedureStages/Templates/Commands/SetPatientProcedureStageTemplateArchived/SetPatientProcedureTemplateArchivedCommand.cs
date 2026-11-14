using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.SetPatientProcedureStageTemplateArchived;

/// <summary>Archives a template (it can't be used for new procedures) or restores it.</summary>
public sealed record SetPatientProcedureTemplateArchivedCommand(Guid TemplateId, bool IsArchived)
    : IRequest<PatientProcedureStageTemplateResponse>;
