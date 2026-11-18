using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Domain.Authentication;
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
                           && code.ExpiresAtUtc > utcNow)
            .OrderByDescending(code => code.SentAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(EmailCode code, DateTime utcNow, CancellationToken ct)
    {
        await context.EmailCodes
            .Where(existing => existing.UserId == code.UserId && existing.Purpose == code.Purpose
                               || existing.ExpiresAtUtc <= utcNow)
            .ExecuteDeleteAsync(ct);

        context.EmailCodes.Add(code);
        await context.SaveChangesAsync(ct);
        context.Entry(code).State = EntityState.Detached;
    }

    public async Task<bool> TryResendAsync(Guid codeId, string codeHash, DateTime utcNow, CancellationToken ct)
    {
        var sentBefore = utcNow - EmailCode.ResendInterval;
        var expires = utcNow + EmailCode.Lifetime;
        return await context.EmailCodes
            .Where(code => code.Id == codeId
                           && code.ExpiresAtUtc > utcNow
                           && code.Resends < EmailCode.MaxResends
                           && code.SentAtUtc <= sentBefore)
            .ExecuteUpdateAsync(set => set
                .SetProperty(code => code.CodeHash, codeHash)
                .SetProperty(code => code.SentAtUtc, utcNow)
                .SetProperty(code => code.ExpiresAtUtc, expires)
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
