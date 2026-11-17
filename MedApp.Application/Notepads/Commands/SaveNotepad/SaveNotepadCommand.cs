using MedApp.Contracts.Notepads.Responses;
using MediatR;

namespace MedApp.Application.Notepads.Commands.SaveNotepad;

/// <summary>
/// Saves a user's whole notepad, from the version their page last saw (<paramref name="ExpectedVersion"/>, 0 for none).
/// Not recorded in the activity log: it's theirs, and not linked to a patient.
/// </summary>
public sealed record SaveNotepadCommand(Guid UserId, string Text, int ExpectedVersion) : IRequest<NotepadResponse>;
