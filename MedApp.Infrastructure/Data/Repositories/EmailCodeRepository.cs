using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Domain.Authentication;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class EmailCodeRepository(MedAppDbContext context) : IEmailCodeRepository
{
    public Task<EmailCode?> FindLiveAsync(
        Guid userId,
        EmailCodePurpose purpose,
        Guid? challengeId,
        DateTime utcNow,
        CancellationToken ct) =>
        context.EmailCodes
            .AsNoTracking()
            .Where(code => code.UserId == userId
                           && code.Purpose == purpose
                           && code.ChallengeId == challengeId
                           && code.ExpiresAtUtc > utcNow
                           && code.FailedAttempts < EmailCode.MaxFailedAttempts)
            .OrderByDescending(code => code.SentAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> AddAsync(EmailCode code, DateTime utcNow, CancellationToken ct)
    {
        // Only codes that can't work any more: a live one (another request's, just made) stays, and the unique index
        // then refuses this one.
        await context.EmailCodes
            .Where(existing => existing.UserId == code.UserId
                               && existing.Purpose == code.Purpose
                               && existing.FailedAttempts >= EmailCode.MaxFailedAttempts
                               || existing.ExpiresAtUtc <= utcNow)
            .ExecuteDeleteAsync(ct);

        context.EmailCodes.Add(code);
        try
        {
            await context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return false;
        }
        finally
        {
            context.Entry(code).State = EntityState.Detached;
        }
    }

    public async Task<bool> TryResendAsync(Guid codeId, string codeHash, DateTime utcNow, DateTime expiresAtUtc, CancellationToken ct)
    {
        var sentBefore = utcNow - EmailCode.ResendInterval;
        return await context.EmailCodes
            .Where(code => code.Id == codeId
                           && code.ExpiresAtUtc > utcNow
                           && code.FailedAttempts < EmailCode.MaxFailedAttempts
                           && code.Resends < EmailCode.MaxResends
                           && code.SentAtUtc <= sentBefore)
            .ExecuteUpdateAsync(set => set
                .SetProperty(code => code.CodeHash, codeHash)
                .SetProperty(code => code.SentAtUtc, utcNow)
                .SetProperty(code => code.ExpiresAtUtc, expiresAtUtc)
                .SetProperty(code => code.Resends, code => code.Resends + 1), ct) > 0;
    }

    public async Task<bool> TryReserveAttemptAsync(Guid codeId, DateTime utcNow, CancellationToken ct) =>
        await context.EmailCodes
            .Where(code => code.Id == codeId
                           && code.FailedAttempts < EmailCode.MaxFailedAttempts
                           && code.ExpiresAtUtc > utcNow)
            .ExecuteUpdateAsync(set => set.SetProperty(code => code.FailedAttempts, code => code.FailedAttempts + 1), ct) > 0;

    public async Task<bool> ConsumeAsync(Guid codeId, string codeHash, CancellationToken ct) =>
        await context.EmailCodes
            .Where(code => code.Id == codeId && code.CodeHash == codeHash)
            .ExecuteDeleteAsync(ct) > 0;
}
