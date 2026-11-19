using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Consults.Responses;
using MediatR;

namespace MedApp.Application.Consults.Commands.RequestConsult;

/// <summary>Sends a consult about a pathway to another team (a clinic or the lab), with the question.</summary>
public sealed record RequestConsultCommand(Guid PathwayId, Guid AskedTeamId, string Question, PathwayActor Actor)
    : IRequest<ConsultResponse>;
