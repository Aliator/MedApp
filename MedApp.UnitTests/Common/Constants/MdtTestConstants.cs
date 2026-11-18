using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Calendar;
using MedApp.Domain.Mdt;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specialties;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic MDTs and items for the MDT list's tests.</summary>
public static class MdtTestConstants
{
    public const string Question = "Synthetic question for the team.";
    public const string Outcome = "Synthetic outcome as the team wrote it.";

    public static readonly Specialty Specialty = new() { Id = Guid.Parse("33333333-0000-0000-0000-0000000000d1"), Name = "Dermatology" };

    /// <summary>A clinician who works on pathways and records MDT outcomes.</summary>
    public static readonly PathwayActor Clinician = new(
        PathwaysTestConstants.Staff.UserId,
        PathwaysTestConstants.Staff.Username,
        new HashSet<Permission> { Permission.ViewPatients, Permission.WorkPathways, Permission.RecordMdtOutcomes });

    /// <summary>Someone who works on pathways but doesn't record outcomes.</summary>
    public static readonly PathwayActor Nurse = new(
        Guid.Parse("0000000a-0000-0000-0000-0000000000a2"),
        "test.nurse",
        new HashSet<Permission> { Permission.ViewPatients, Permission.WorkPathways });

    /// <summary>An MDT of <see cref="Specialty"/> starting <paramref name="startUtc"/>, for an hour, with the other member of staff invited.</summary>
    public static Meeting CreateMdt(DateTime startUtc)
    {
        var id = Guid.NewGuid();
        return new Meeting
        {
            Id = id,
            Title = "Skin MDT",
            StartUtc = startUtc,
            EndUtc = startUtc.AddHours(1),
            IsMdt = true,
            SpecialtyId = Specialty.Id,
            Attendees =
            [
                new MeetingAttendee { MeetingId = id, StaffUserId = PathwaysTestConstants.OtherUserId },
                new MeetingAttendee { MeetingId = id, StaffUserId = PathwaysTestConstants.Staff.UserId },
            ]
        };
    }

    /// <summary>An item for a pathway (assigned to <see cref="PathwaysTestConstants.Staff"/>) on <paramref name="meeting"/>.</summary>
    public static MdtItem CreateItem(Meeting? meeting, MdtItemStatus status = MdtItemStatus.Listed, PatientPathway? pathway = null)
    {
        pathway ??= PathwaysTestConstants.CreatePathway(PatientPathwayStatus.InProgress);
        return new MdtItem
        {
            Id = Guid.NewGuid(),
            PatientId = pathway.PatientId,
            Patient = pathway.Patient,
            PathwayId = pathway.Id,
            Pathway = pathway,
            SpecialtyId = Specialty.Id,
            Specialty = Specialty,
            MeetingId = meeting?.Id,
            Meeting = meeting,
            Question = Question,
            Status = status,
            ListedByUserId = Nurse.UserId,
            ListedByUsername = Nurse.Username,
            ListedAtUtc = PathwaysTestConstants.Now.AddDays(-3),
            Version = 1
        };
    }
}
