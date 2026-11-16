using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data;

public static class ConcurrencyExtensions
{
    /// <summary>
    /// Saves, turning a concurrency conflict (a row version changed since it was read) into a 409 with the patient's
    /// message (<see cref="PatientRules.ChangedMeanwhileMessage"/>). Nothing is saved when it conflicts.
    /// </summary>
    public static Task SaveChangesOrConflictAsync(this MedAppDbContext context, CancellationToken ct) =>
        context.SaveChangesOrConflictAsync(PatientRules.ChangedMeanwhileMessage, ct);

    /// <summary>
    /// Saves, turning a concurrency conflict (a row version, a patient's or an appointment's, changed since it was
    /// read) into a 409 with <paramref name="conflictMessage"/>, which says what changed in words the user can act on.
    /// Nothing is saved when it conflicts.
    /// </summary>
    public static async Task SaveChangesOrConflictAsync(this MedAppDbContext context, string conflictMessage, CancellationToken ct)
    {
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(conflictMessage);
        }
    }

    /// <summary>Whether a save failed because it would have broken the unique index <paramref name="indexName"/>.</summary>
    public static bool IsViolationOf(this DbUpdateException exception, string indexName) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 } sql
        && sql.Message.Contains(indexName, StringComparison.Ordinal);
}
