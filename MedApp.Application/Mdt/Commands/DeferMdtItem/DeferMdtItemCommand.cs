using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Mdt.Responses;
using MediatR;

namespace MedApp.Application.Mdt.Commands.DeferMdtItem;

/// <summary>
/// Moves an open item to a later MDT (the team deferred it: it becomes Deferred), or lists it again after its meeting was
/// cancelled (it stays as it was). From the version the caller saw; refused while the record is restricted or anonymised.
/// </summary>
public sealed record DeferMdtItemCommand(Guid ItemId, Guid MeetingId, int ExpectedVersion, PathwayActor Actor)
    : IRequest<MdtItemResponse>;
