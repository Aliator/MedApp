using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.NoteDeletionRequests.Responses;
using MediatR;

namespace MedApp.Application.Procedures.NoteDeletionRequests.Commands.DecideNoteDeletionRequest;

/// <summary>
/// An admin's decision on a request to delete a note: <paramref name="DeleteNote"/> deletes it (soft, with
/// <paramref name="Reason"/>, or the requester's reason when null), otherwise it's kept. Either way the request is
/// closed and the requester told. A request already decided is a 409, and so is one whose patient's record is restricted
/// or anonymised.
/// </summary>
public sealed record DecideNoteDeletionRequestCommand(Guid RequestId, bool DeleteNote, string? Reason, ProcedureActor Actor)
    : IRequest<NoteDeletionRequestResponse>;
