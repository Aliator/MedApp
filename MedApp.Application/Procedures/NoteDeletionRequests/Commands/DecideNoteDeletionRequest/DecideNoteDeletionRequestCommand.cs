using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.NoteDeletionRequests.Responses;
using MediatR;

namespace MedApp.Application.Procedures.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;

/// <summary>
/// An admin's decision on a request to delete a note. <paramref name="DeleteNote"/> deletes it (soft) from the version
/// the admin read (<paramref name="ExpectedVersion"/>, required then: a 409 if it was edited since), with
/// <paramref name="Reason"/> and <paramref name="Detail"/>, or the requester's when the reason is left out; otherwise it's
/// kept. Either way the request is closed and the requester told. A request already decided is a 409, and so is one whose
/// patient's record is restricted or anonymised.
/// </summary>
public sealed record DecideNoteDeletionRequestCommand(
    Guid RequestId, bool DeleteNote, int? ExpectedVersion, string? Reason, string? Detail, ProcedureActor Actor)
    : IRequest<NoteDeletionRequestResponse>;
