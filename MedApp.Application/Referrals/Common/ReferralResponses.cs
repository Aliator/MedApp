using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Repositories;
using MedApp.Contracts.Referrals.Responses;
using MedApp.Domain.Referrals;

namespace MedApp.Application.Referrals.Common;

/// <summary>A referral's page as the API returns it, with its history and letters read from the repository.</summary>
public static class ReferralResponses
{
    /// <summary>
    /// The referral (loaded with its patient and specialty), its history, how many letters it has, and whether the
    /// viewer can triage it (asking their membership of its specialty only when it's open and they hold the permission).
    /// </summary>
    public static async Task<ReferralResponse> ForAsync(
        Referral referral, IReferralRepository repository, PathwayActor viewer, CancellationToken ct)
    {
        var history = await repository.GetHistoryAsync(referral.Id, ct);
        var letters = await repository.CountLettersAsync(referral.Id, ct);
        var isMember = ReferralWorkflow.IsOpen(referral.Status)
                       && viewer.Has(Permission.TriageReferrals)
                       && await repository.IsSpecialtyMemberAsync(viewer.UserId, referral.SpecialtyId, ct);
        return ReferralMapper.ToResponse(referral, history, letters, viewer, isMember);
    }
}
