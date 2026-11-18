using MedApp.Application.Notepads.Common;
using MedApp.Application.Notepads.Repositories;
using MedApp.Contracts.Notepads.Responses;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Notepads.Queries.GetNotepad;

/// <summary>A user's notepad. One that has gone <see cref="NotepadOptions.KeepDays"/> without a change is cleared (deleted) first.</summary>
public sealed class GetNotepadHandler(
    INotepadRepository repository,
    IOptions<NotepadOptions> options,
    TimeProvider timeProvider)
    : IRequestHandler<GetNotepadQuery, NotepadResponse>
{
    public async Task<NotepadResponse> Handle(GetNotepadQuery request, CancellationToken cancellationToken)
    {
        var keepDays = options.Value.KeepDays;
        var notepad = await repository.GetForUpdateAsync(request.UserId, cancellationToken);

        if (notepad is not null && NotepadRules.IsExpired(notepad, timeProvider.GetUtcNow().UtcDateTime, keepDays))
        {
            await repository.DeleteAsync(notepad, cancellationToken);
            notepad = null;
        }

        return NotepadMapper.ToResponse(notepad, keepDays);
    }
}
// CI proof: a scratch line, never merged.
