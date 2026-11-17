using MedApp.Application.Common.Exceptions;
using MedApp.Application.Notepads.Common;
using MedApp.Application.Notepads.Repositories;
using MedApp.Domain.Notepads;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>Notepads, one per account (deleted with it, by the foreign key's cascade).</summary>
public sealed class NotepadRepository(MedAppDbContext context) : INotepadRepository
{
    public async Task<Notepad?> GetForUpdateAsync(Guid userId, CancellationToken ct) =>
        await context.Notepads.FirstOrDefaultAsync(n => n.UserId == userId, ct);

    public async Task AddAsync(Notepad notepad, CancellationToken ct)
    {
        context.Notepads.Add(notepad);
        try
        {
            await context.SaveChangesAsync(ct);
        }
        // Another tab saved the first version first: its row has this key.
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            context.Entry(notepad).State = EntityState.Detached;
            throw new ConflictException(NotepadRules.ChangedMeanwhileMessage);
        }
    }

    public Task SaveAsync(CancellationToken ct) =>
        context.SaveChangesOrConflictAsync(NotepadRules.ChangedMeanwhileMessage, ct);

    public async Task DeleteAsync(Notepad notepad, CancellationToken ct)
    {
        context.Notepads.Remove(notepad);
        await context.SaveChangesOrConflictAsync(NotepadRules.ChangedMeanwhileMessage, ct);
    }
}
