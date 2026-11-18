using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Mdt.Responses;
using MediatR;

namespace MedApp.Application.Mdt.Commands.WithdrawMdtItem;

/// <summary>
/// Takes an item off its MDT's list without discussion, from the version the caller saw. Allowed while the record is
/// restricted (it stops work), not once it's anonymised.
/// </summary>
public sealed record WithdrawMdtItemCommand(Guid ItemId, int ExpectedVersion, PathwayActor Actor) : IRequest<MdtItemResponse>;
