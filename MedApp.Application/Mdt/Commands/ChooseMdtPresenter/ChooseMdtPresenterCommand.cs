using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Mdt.Responses;
using MediatR;

namespace MedApp.Application.Mdt.Commands.ChooseMdtPresenter;

/// <summary>
/// Chooses who presents an open item at its MDT (MC21), from the meeting's attendees, or nobody (null). Only the item's
/// own team chooses, from the version the caller saw; refused while the record is restricted or anonymised.
/// </summary>
public sealed record ChooseMdtPresenterCommand(Guid ItemId, Guid? PresenterUserId, int ExpectedVersion, PathwayActor Actor)
    : IRequest<MdtItemResponse>;
