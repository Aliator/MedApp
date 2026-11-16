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

    public async Task<bool> TryReserveAttemptAsync(Guid challengeId, DateTime utcNow, CancellationToken ct) =>
        await context.SignInChallenges
            .Where(challenge => challenge.Id == challengeId
                                && challenge.FailedAttempts < SignInChallenge.MaxFailedAttempts
                                && challenge.ExpiresAtUtc > utcNow)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.FailedAttempts, c => c.FailedAttempts + 1), ct) > 0;

    public async Task<bool> ConsumeAsync(Guid challengeId, CancellationToken ct) =>
        await context.SignInChallenges
            .Where(challenge => challenge.Id == challengeId)
            .ExecuteDeleteAsync(ct) > 0;
}
