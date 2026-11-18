using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Referrals.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Commands.RegisterReferral;

/// <summary>
/// Registers a referral for a patient already in MedApp, awaiting triage, recorded in its history. If the patient has an
/// open referral to the same team, it's a 400 unless <paramref name="ConfirmPossibleDuplicate"/>.
/// </summary>
public sealed record RegisterReferralCommand(
    Guid PatientId,
    Guid TeamId,
    DateOnly ReceivedOn,
    string ReferrerPractice,
    string? ReferrerClinician,
    string? ReferrerOdsCode,
    string RequestedPriority,
    string Reason,
    bool ConfirmPossibleDuplicate,
    PathwayActor Actor,
    Guid? PracticeId = null)
    : IRequest<ReferralResponse>;
