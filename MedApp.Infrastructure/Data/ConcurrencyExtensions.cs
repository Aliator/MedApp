using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data;

public static class ConcurrencyExtensions
{
    /// <summary>
    /// Saves, turning a concurrency conflict (a patient's row version changed since it was read) into a 409 with a
    /// message the user can act on. Nothing is saved when it conflicts.
    /// </summary>
    public static async Task SaveChangesOrConflictAsync(this MedAppDbContext context, CancellationToken ct)
    {
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(PatientRules.ChangedMeanwhileMessage);
        }
    }
}
