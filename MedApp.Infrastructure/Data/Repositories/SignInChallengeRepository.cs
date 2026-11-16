using MedApp.Application.Authentication.TwoFactor.Repositories;
using MedApp.Domain.Authentication;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class SignInChallengeRepository(MedAppDbContext context) : ISignInChallengeRepository
{
    public async Task AddAsync(SignInChallenge challenge, DateTime utcNow, CancellationToken ct)
    {
        await context.SignInChallenges
            .Where(existing => existing.UserId == challenge.UserId || existing.ExpiresAtUtc <= utcNow)
            .ExecuteDeleteAsync(ct);

        context.SignInChallenges.Add(challenge);
        await context.SaveChangesAsync(ct);
    }

    public Task<SignInChallenge?> FindByTokenHashAsync(string tokenHash, CancellationToken ct) =>
        context.SignInChallenges
            .AsNoTracking()
            .FirstOrDefaultAsync(challenge => challenge.TokenHash == tokenHash, ct);

    public Task RecordFailureAsync(Guid challengeId, CancellationToken ct) =>
        context.SignInChallenges
            .Where(challenge => challenge.Id == challengeId)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.FailedAttempts, c => c.FailedAttempts + 1), ct);

    public async Task<bool> ConsumeAsync(Guid challengeId, CancellationToken ct) =>
        await context.SignInChallenges
            .Where(challenge => challenge.Id == challengeId)
            .ExecuteDeleteAsync(ct) > 0;
}
