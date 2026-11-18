using MedApp.Application.Common.Identity;
using MedApp.Application.Letters.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Letters;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Practices;
using Moq;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic letter templates: an invented centre, and only merge fields for anything about a patient.</summary>
public static class LettersTestConstants
{
    public const string TemplateName = "Synthetic clinic letter";
    public const string OtherTemplateName = "Synthetic discharge summary";
    public const string Letterhead = "Synthetic Specialist Centre\n1 Imaginary Lane\nFictionton ZZ1 1ZZ";
    public const string BodySkeleton = "Dear Doctor,\n\n## Diagnosis\n\n## Plan\n\nRe: {{patient.name}}, born {{patient.dateOfBirth}}";
    public const string Closing = "Yours sincerely,\n\n{{author.name}}\n{{author.jobTitle}}";

    public static readonly DateTime CreatedAtUtc = new(2026, 11, 2, 9, 30, 0, DateTimeKind.Utc);
    public static readonly DateTime NowUtc = new(2026, 11, 18, 10, 15, 0, DateTimeKind.Utc);

    public static LetterTemplate CreateTemplate(
        string name = TemplateName,
        Guid? teamId = null,
        LetterKind kind = LetterKind.ClinicLetter,
        bool isArchived = false,
        int version = 3) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Kind = kind,
        TeamId = teamId,
        Letterhead = Letterhead,
        BodySkeleton = BodySkeleton,
        Closing = Closing,
        IsArchived = isArchived,
        Version = version,
        CreatedAt = CreatedAtUtc,
        LastUpdated = CreatedAtUtc
    };

    /// <summary>A renderer whose font shows everything, except the characters given.</summary>
    public static Mock<ILetterRenderer> RendererMissing(params string[] characters)
    {
        var renderer = new Mock<ILetterRenderer>();
        renderer.Setup(r => r.FindUnsupportedCharacters(It.IsAny<string?>()))
            .Returns((string? text) => characters.Where(c => text?.Contains(c, StringComparison.Ordinal) == true).ToList());
        return renderer;
    }

    // Letters (PR 16): synthetic staff, each with only the letter permissions their work needs.

    /// <summary>A nurse who writes and sends letters, but can't approve them.</summary>
    public static readonly PathwayActor Author = new(
        Guid.Parse("0000001e-0000-0000-0000-000000000001"), "a.writer",
        new HashSet<Permission> { Permission.UseApp, Permission.ViewPatients, Permission.WriteLetters, Permission.SendLetters });

    /// <summary>A clinician who approves letters.</summary>
    public static readonly PathwayActor Approver = new(
        Guid.Parse("0000001e-0000-0000-0000-000000000002"), "b.approver",
        new HashSet<Permission> { Permission.UseApp, Permission.ViewPatients, Permission.WriteLetters, Permission.ApproveLetters, Permission.SendLetters });

    /// <summary>A clinician who writes and approves their own letters.</summary>
    public static readonly PathwayActor ClinicianAuthor = new(
        Guid.Parse("0000001e-0000-0000-0000-000000000003"), "c.clinician",
        new HashSet<Permission> { Permission.UseApp, Permission.ViewPatients, Permission.WriteLetters, Permission.ApproveLetters, Permission.SendLetters });

    /// <summary>The booking team: sends letters only.</summary>
    public static readonly PathwayActor Sender = new(
        Guid.Parse("0000001e-0000-0000-0000-000000000004"), "d.booking",
        new HashSet<Permission> { Permission.UseApp, Permission.ViewPatients, Permission.SendLetters });

    /// <summary>An admin: corrects records, but writes, approves and sends nothing.</summary>
    public static readonly PathwayActor AdminActor = new(
        Guid.Parse("0000001e-0000-0000-0000-000000000005"), "e.admin", Permissions.Admin);

    public static Dictionary<Guid, string> Usernames => new()
    {
        [Author.UserId] = Author.Username,
        [Approver.UserId] = Approver.Username,
        [ClinicianAuthor.UserId] = ClinicianAuthor.Username,
        [Sender.UserId] = Sender.Username,
        [AdminActor.UserId] = AdminActor.Username,
    };

    /// <summary>Full names and job titles: invented.</summary>
    public static Dictionary<Guid, StaffDetails> StaffDetails => new()
    {
        [Author.UserId] = new("Alex Writer", "Specialist Nurse"),
        [Approver.UserId] = new("Dr Bo Approver", "Consultant Dermatologist"),
        [ClinicianAuthor.UserId] = new("Dr Cam Clinician", "Consultant"),
        [Sender.UserId] = new(null, null),
    };

    public const string LetterBody = "Dear Doctor,\n\nWe saw {{patient.name}} in clinic.";

    /// <summary>
    /// A letter on <paramref name="pathway"/>: to <paramref name="practice"/> and the patient (filled from the record),
    /// by <paramref name="author"/>, in <paramref name="status"/> (an approved one with its PDF, not yet sent).
    /// </summary>
    public static Letter CreateLetter(
        PatientPathway pathway,
        Practice practice,
        LetterStatus status = LetterStatus.Draft,
        PathwayActor? author = null,
        Guid? approverId = null,
        LetterKind kind = LetterKind.ClinicLetter)
    {
        var id = Guid.NewGuid();
        var patient = pathway.Patient;
        var letter = new Letter
        {
            Id = id,
            PatientId = pathway.PatientId,
            PathwayId = pathway.Id,
            TemplateId = Guid.NewGuid(),
            TemplateName = TemplateName,
            Kind = kind,
            Letterhead = Letterhead,
            Body = LetterBody,
            Closing = Closing,
            Status = status,
            AuthorUserId = (author ?? Author).UserId,
            ApproverUserId = approverId ?? (status is LetterStatus.AwaitingApproval or LetterStatus.Approved ? Approver.UserId : null),
            SubmittedAtUtc = status == LetterStatus.Draft ? null : CreatedAtUtc.AddHours(1),
            ApprovedAtUtc = status == LetterStatus.Approved ? NowUtc.AddDays(-1) : null,
            PdfAttachmentId = status == LetterStatus.Approved ? Guid.NewGuid() : null,
            Version = 2,
            CreatedAt = CreatedAtUtc,
            LastUpdated = CreatedAtUtc,
            Recipients =
            [
                new LetterRecipient
                {
                    Id = Guid.NewGuid(), LetterId = id, Position = 0, Kind = LetterRecipientKind.GpPractice, PracticeId = practice.Id,
                    Name = practice.Name, AddressLines = practice.AddressLines, Postcode = practice.Postcode, Email = practice.SecureEmail,
                },
                new LetterRecipient
                {
                    Id = Guid.NewGuid(), LetterId = id, Position = 1, Kind = LetterRecipientKind.Patient,
                    Name = $"{patient.FirstName} {patient.LastName}", AddressLines = patient.AddressLines, Postcode = patient.Postcode,
                },
            ],
        };
        return letter;
    }
}
