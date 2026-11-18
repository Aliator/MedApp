using MedApp.Application.Pathways.Common;
using MedApp.Domain.Attachments;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Attachments.Common;

/// <summary>What a file is attached to. Stored by name only in responses and audit details.</summary>
public enum AttachmentTargetKind
{
    Patient,
    Pathway,
    Stage
}

/// <summary>
/// What a file is (or will be) attached to: a patient, one of their pathways, or a stage of one, with the
/// pathway and stage loaded, to check the workflow's rules and record their names, and the patient, to check their
/// record can be changed (<see cref="Patients.Common.PatientRules"/>).
/// </summary>
public sealed record AttachmentTarget(Guid PatientId, PatientPathway? Pathway, PatientPathwayStage? Stage)
{
    /// <summary>The patient, when it isn't the pathway's (a patient's own file); see <see cref="Owner"/>.</summary>
    public Patient? Patient { get; init; }

    /// <summary>The patient whose record the file is part of, if loaded.</summary>
    public Patient? Owner => Patient ?? Pathway?.Patient;

    public AttachmentTargetKind Kind =>
        Stage is not null ? AttachmentTargetKind.Stage
        : Pathway is not null ? AttachmentTargetKind.Pathway
        : AttachmentTargetKind.Patient;

    /// <summary>What an attachment is attached to, from its IDs alone.</summary>
    public static AttachmentTargetKind KindOf(Attachment attachment) =>
        attachment.StageId is not null ? AttachmentTargetKind.Stage
        : attachment.PathwayId is not null ? AttachmentTargetKind.Pathway
        : AttachmentTargetKind.Patient;

    /// <summary>
    /// Why files can't be added to it, or removed from it by staff, now; null if they can. A patient's files can
    /// always change. A pathway's follow its details: not once it's cancelled or archived. A stage's follow its
    /// notes: only the stage being worked on, or one an admin has unlocked, of an open pathway.
    /// </summary>
    public string? WhyFilesCantChange() => Kind switch
    {
        AttachmentTargetKind.Stage => PathwayWorkflow.WhyStageCantBeEdited(Pathway!, Stage!),
        AttachmentTargetKind.Pathway when !PathwayWorkflow.CanEditDetails(Pathway!) =>
            $"Files can't be added to or removed from a pathway while it's {PathwayWorkflow.Describe(Pathway!.Status)}.",
        _ => null
    };
}
