using MedApp.Application.Common.Exceptions;
using MedApp.Application.Notepads.Common;
using MedApp.Application.Notepads.Repositories;
using MedApp.Contracts.Notepads.Responses;
using MedApp.Domain.Notepads;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Notepads.Commands.SaveNotepad;

/// <summary>
/// Saves a notepad. A 409 when it was saved elsewhere since the page read it (another tab), or when it has gone
/// <see cref="NotepadOptions.KeepDays"/> without a change: then it's cleared first, and the page, which still shows the
/// old text, is told so rather than saving that text again.
/// </summary>
public sealed class SaveNotepadHandler(
    INotepadRepository repository,
    IOptions<NotepadOptions> options,
    TimeProvider timeProvider)
    : IRequestHandler<SaveNotepadCommand, NotepadResponse>
{
    public async Task<NotepadResponse> Handle(SaveNotepadCommand request, CancellationToken cancellationToken)
    {
        var keepDays = options.Value.KeepDays;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var notepad = await repository.GetForUpdateAsync(request.UserId, cancellationToken);

        if (notepad is not null && NotepadRules.IsExpired(notepad, now, keepDays))
        {
            await repository.DeleteAsync(notepad, cancellationToken);
            throw new ConflictException(NotepadRules.ClearedMessage(keepDays));
        }

        if ((notepad?.Version ?? 0) != request.ExpectedVersion)
            throw new ConflictException(NotepadRules.ChangedMeanwhileMessage);

        if (notepad is null)
        {
            notepad = new Notepad { UserId = request.UserId, Text = request.Text, LastUpdatedUtc = now, Version = 1 };
            await repository.AddAsync(notepad, cancellationToken);
        }
        else
        {
            notepad.Text = request.Text;
            notepad.LastUpdatedUtc = now;
            notepad.Version++;
            await repository.SaveAsync(cancellationToken);
        }

        return NotepadMapper.ToResponse(notepad, keepDays);
    }
}
