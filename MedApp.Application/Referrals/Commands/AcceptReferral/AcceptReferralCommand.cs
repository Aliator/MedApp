using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MediatR;

namespace MedApp.Application.Referrals.Commands.AcceptReferral;

/// <summary>
/// A triager accepts a referral awaiting triage into a new pathway, from a template of the referral's specialty, with
/// the priority and target date they chose (nothing is prefilled), and optionally a title, someone to assign and a
/// first note. The pathway is created as any new pathway is, and saved with the referral's change.
/// </summary>
public sealed record AcceptReferralCommand(
    Guid ReferralId,
    Guid TemplateId,
    string Priority,
    DateOnly DueDate,
    string? Title,
    Guid? AssigneeUserId,
    string? Notes,
    int ExpectedVersion,
    PathwayActor Actor)
    : IRequest<PatientPathwayResponse>;
