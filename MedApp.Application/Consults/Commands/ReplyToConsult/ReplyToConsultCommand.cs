using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Consults.Responses;
using MediatR;

namespace MedApp.Application.Consults.Commands.ReplyToConsult;

/// <summary>The asked team's reply to a consult, as written.</summary>
public sealed record ReplyToConsultCommand(Guid ConsultId, string Reply, int ExpectedVersion, PathwayActor Actor)
    : IRequest<ConsultResponse>;
