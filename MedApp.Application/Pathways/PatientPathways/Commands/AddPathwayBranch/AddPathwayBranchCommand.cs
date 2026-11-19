using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.AddPathwayBranch;

/// <summary>
/// Adds one of the template's addable branches to a pathway part-way (MC10), after <paramref name="AfterStageId"/>,
/// with the clinician's <paramref name="Reason"/> (never in the audit log). Only the pathway's own team; never suggested.
/// </summary>
public sealed record AddPathwayBranchCommand(
    Guid PathwayId,
    Guid TemplateBranchId,
    Guid AfterStageId,
    string? Reason,
    PathwayActor Actor)
    : IRequest<PatientPathwayResponse>;
