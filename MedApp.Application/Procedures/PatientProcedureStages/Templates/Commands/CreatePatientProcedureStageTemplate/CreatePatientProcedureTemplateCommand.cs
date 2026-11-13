using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Templates.Commands.CreatePatientProcedureStageTemplate;

public sealed record CreatePatientProcedureTemplateCommand(
    string Name,
    IReadOnlyList<Guid> StageDefinitionIdsInOrder
) : IRequest<PatientProcedureStageTemplateResponse>;