using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Attachments;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Attachments.Common;

/// <summary>Finds what a file is (or will be) attached to, or a 404.</summary>
public static class AttachmentTargets
{
    /// <summary>
    /// The patient, pathway, stage or referral a request names (exactly one, which its validator checks), with the
    /// pathway and stage, or the referral and its patient, loaded.
    /// </summary>
    public static async Task<AttachmentTarget> ResolveAsync(
        Guid? patientId,
        Guid? pathwayId,
        Guid? stageId,
        Guid? referralId,
        IPatientRepository patients,
        IPatientPathwayRepository pathways,
        IAttachmentRepository attachments,
        CancellationToken ct)
    {
        if (referralId is { } referralIdValue)
        {
            var referral = await attachments.GetReferralAsync(referralIdValue, ct)
                           ?? throw new NotFoundException($"Referral '{referralIdValue}' was not found.");
            return new AttachmentTarget(referral.PatientId, null, null) { Referral = referral };
        }

        if (stageId is { } stage)
        {
            var stagePathwayId = await attachments.GetStagePathwayIdAsync(stage, ct)
                                   ?? throw new NotFoundException($"Stage '{stage}' was not found.");
            var pathway = await GetPathwayAsync(stagePathwayId, pathways, ct);
            return new AttachmentTarget(pathway.PatientId, pathway, pathway.GetStageOrThrow(stage));
        }

        if (pathwayId is { } pathwayIdValue)
        {
            var pathway = await GetPathwayAsync(pathwayIdValue, pathways, ct);
            return new AttachmentTarget(pathway.PatientId, pathway, null);
        }

        var patient = patientId ?? throw new ArgumentException("A patient, pathway or stage is needed.");
        var found = await patients.GetByIdAsync(patient, ct) ?? throw new NotFoundException($"Patient '{patient}' was not found.");
        return new AttachmentTarget(patient, null, null) { Patient = found };
    }

    /// <summary>
    /// What an existing attachment is attached to. Its pathway can't be deleted while it has files (a 409), but if
    /// it somehow has been, the target is the patient, so the file can still be dealt with.
    /// </summary>
    public static async Task<AttachmentTarget> OfAsync(
        Attachment attachment,
        IPatientRepository patients,
        IPatientPathwayRepository pathways,
        IAttachmentRepository attachments,
        CancellationToken ct)
    {
        // Referrals are never deleted, so a referral's file has its referral (and, through it, its patient).
        if (attachment.ReferralId is { } referralId
            && await attachments.GetReferralAsync(referralId, ct) is { } referral)
            return new AttachmentTarget(attachment.PatientId, null, null) { Referral = referral };

        var pathway = attachment.PathwayId is { } pathwayId ? await pathways.GetByIdAsync(pathwayId, ct) : null;
        if (pathway is null)
            return new AttachmentTarget(attachment.PatientId, null, null)
            {
                Patient = await patients.GetByIdAsync(attachment.PatientId, ct)
            };

        var stage = attachment.StageId is { } stageId ? pathway.Stages.FirstOrDefault(s => s.Id == stageId) : null;
        return new AttachmentTarget(attachment.PatientId, pathway, stage);
    }

    private static async Task<PatientPathway> GetPathwayAsync(
        Guid id, IPatientPathwayRepository pathways, CancellationToken ct) =>
        await pathways.GetByIdAsync(id, ct) ?? throw new NotFoundException($"Pathway '{id}' was not found.");
}
