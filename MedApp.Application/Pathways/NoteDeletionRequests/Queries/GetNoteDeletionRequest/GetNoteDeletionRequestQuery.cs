using MedApp.Contracts.Pathways.NoteDeletionRequests.Responses;
using MediatR;

namespace MedApp.Application.Pathways.NoteDeletionRequests.Queries.GetNoteDeletionRequest;

/// <summary>A request to delete a note, as an admin decides it (with the note and the patient). Recorded as a view of the record.</summary>
public sealed record GetNoteDeletionRequestQuery(Guid Id) : IRequest<NoteDeletionRequestResponse>;
