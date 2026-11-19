using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Consults.Responses;
using MediatR;

namespace MedApp.Application.Consults.Commands.WithdrawConsult;

/// <summary>Withdraws a consult before a reply, ending the asked team's reading of the pathway.</summary>
public sealed record WithdrawConsultCommand(Guid ConsultId, int ExpectedVersion, PathwayActor Actor) : IRequest<ConsultResponse>;
