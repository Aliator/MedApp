using MedApp.Application.Pathways.Common;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Referrals.Common;

/// <summary>
/// What staff type from a referral letter, tidied: text trimmed, empty optional text as none, the ODS code in upper case
/// without spaces, and the priority read by name. Registering and editing take the same details. The practice from the
/// directory (<paramref name="PracticeId"/>) is optional, beside the practice as typed.
/// </summary>
public sealed record ReferralDetails(
    Guid TeamId,
    DateOnly ReceivedOn,
    string ReferrerPractice,
    string? ReferrerClinician,
    string? ReferrerOdsCode,
    PatientPathwayPriority RequestedPriority,
    string Reason,
    Guid? PracticeId = null)
{
    /// <summary>The details as typed, tidied. The validator has checked the priority is a priority's name.</summary>
    public static ReferralDetails From(
        Guid teamId,
        DateOnly receivedOn,
        string referrerPractice,
        string? referrerClinician,
        string? referrerOdsCode,
        string requestedPriority,
        string reason,
        Guid? practiceId = null) => new(
        teamId,
        receivedOn,
        referrerPractice.Trim(),
        string.IsNullOrWhiteSpace(referrerClinician) ? null : referrerClinician.Trim(),
        OdsCodes.Normalise(referrerOdsCode),
        PathwayEnums.Parse<PatientPathwayPriority>(requestedPriority),
        reason.Trim(),
        practiceId);
}
