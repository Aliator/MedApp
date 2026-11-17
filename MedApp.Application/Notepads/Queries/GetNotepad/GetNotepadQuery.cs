using MedApp.Contracts.Notepads.Responses;
using MediatR;

namespace MedApp.Application.Notepads.Queries.GetNotepad;

/// <summary>A user's own notepad. Not recorded in the activity log: it's theirs, and not linked to a patient.</summary>
public sealed record GetNotepadQuery(Guid UserId) : IRequest<NotepadResponse>;
