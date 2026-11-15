using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MediatR;

namespace MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.SetPatientProcedureStageDefinitionArchived;

/// <summary>Archives a stage definition (it can't be added to templates any more) or restores it.</summary>
public sealed record SetPatientProcedureStageDefinitionArchivedCommand(Guid StageDefinitionId, bool IsArchived)
    : IRequest<PatientProcedureStageDefinitionResponse>;
