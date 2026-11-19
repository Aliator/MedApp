using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Consults.Responses;
using MediatR;

namespace MedApp.Application.Consults.Commands.CloseConsult;

/// <summary>Closes a consult after its reply, ending the asked team's reading of the pathway.</summary>
public sealed record CloseConsultCommand(Guid ConsultId, int ExpectedVersion, PathwayActor Actor) : IRequest<ConsultResponse>;
