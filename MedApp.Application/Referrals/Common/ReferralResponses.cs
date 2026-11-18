using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Domain.Referrals;

namespace MedApp.Application.Referrals.Common;

/// <summary>A referral's page as the API returns it, with its history and letters read from the repository.</summary>
public static class ReferralResponses
{
    /// <summary>The referral (loaded with its patient and specialty), its history and how many letters it has.</summary>
    public static async Task<ReferralResponse> ForAsync(
        Referral referral, IReferralRepository repository, PathwayActor viewer, CancellationToken ct)
    {
        var history = await repository.GetHistoryAsync(referral.Id, ct);
        var letters = await repository.CountLettersAsync(referral.Id, ct);
        return ReferralMapper.ToResponse(referral, history, letters, viewer);
    }
}
