using MedApp.Application.Pathways.Audit.Queries.SearchPathwayAudit;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Queries.SearchPatientPathways;
using MedApp.Domain.Appointments;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.UnitTests.Common.Constants;

public static class PathwaysTestConstants
{
    /// <summary>"Now" in pathway tests: midday UTC on a summer day, when UK time is BST (UTC+1).</summary>
    public static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The UK date at <see cref="Now"/>.</summary>
    public static readonly DateOnly Today = new(2026, 6, 15);

    public static readonly PathwayActor Staff = new(Guid.Parse("0000000a-0000-0000-0000-000000000001"), "test.staff");
    public static readonly PathwayActor Admin = new(Guid.Parse("0000000a-0000-0000-0000-000000000002"), "test.admin");
    public static readonly Guid OtherUserId = Guid.Parse("0000000a-0000-0000-0000-000000000003");
    public const string OtherUsername = "other.staff";

    public const string ConsultationStage = "Consultation";
    public const string PathwayStage = "Procedure";
    public const string TemplateName = "Test template";

    /// <summary>The team test templates are in (see <see cref="TeamsTestConstants"/>).</summary>
    public static readonly Guid TeamId = Guid.Parse("0000005a-0000-0000-0000-0000000000aa");
    public const string PathwayTitle = "Test pathway";
    public const string Reason = "Waiting for results.";

    /// <summary>Why an admin finished a stage without its visit: synthetic.</summary>
    public const string WithoutVisitReason = "Seen at another site; synthetic reason.";
    public const int MaxNotesLength = 4000;

    public static readonly Guid ConsultationDefinitionId = Guid.Parse("0000000d-0000-0000-0000-000000000001");
    public static readonly Guid PathwayDefinitionId = Guid.Parse("0000000d-0000-0000-0000-000000000002");

    /// <summary>The accounts the mocked username lookup knows.</summary>
    public static Dictionary<Guid, string> Usernames => new()
    {
        [Staff.UserId] = Staff.Username,
        [Admin.UserId] = Admin.Username,
        [OtherUserId] = OtherUsername,
    };

    /// <summary>
    /// A pathway with three stages, Consultation, Pathway, Consultation (a repeated stage), due in a week.
    /// Unless Unassigned, <see cref="Staff"/> is assigned. The first <paramref name="finishedStages"/> are finished.
    /// </summary>
    public static PatientPathway CreatePathway(
        PatientPathwayStatus status = PatientPathwayStatus.Assigned,
        int finishedStages = 0,
        Patient? patient = null,
        DateTime? dueDateUtc = null,
        PatientPathwayPriority priority = PatientPathwayPriority.Medium,
        string title = PathwayTitle,
        DateTime? createdAt = null)
    {
        var id = Guid.NewGuid();
        patient ??= PatientsTestConstants.CreateValidPatient();

        var pathway = new PatientPathway
        {
            Id = id,
            Title = title,
            DueDateUtc = dueDateUtc ?? Now.AddDays(7),
            Priority = priority,
            Status = status,
            PatientId = patient.Id,
            Patient = patient,
            TemplateId = Guid.NewGuid(),
            TemplateName = TemplateName,
            CreatedAt = createdAt ?? Now.AddDays(-7),
            LastUpdated = createdAt ?? Now.AddDays(-7),
            Stages =
            [
                CreateStage(id, 1, ConsultationDefinitionId, ConsultationStage),
                CreateStage(id, 2, PathwayDefinitionId, PathwayStage),
                CreateStage(id, 3, ConsultationDefinitionId, ConsultationStage),
            ],
        };

        if (status != PatientPathwayStatus.Unassigned)
            pathway.Assignments.Add(new PatientPathwayAssignment
            {
                PatientPathwayId = id,
                UserId = Staff.UserId,
                AssignedByUserId = Admin.UserId,
                AssignedAtUtc = Now.AddDays(-6),
            });

        foreach (var stage in pathway.Stages.Take(finishedStages))
            Finish(stage);

        return pathway;
    }

    public static PatientPathwayStage CreateStage(Guid pathwayId, int order, Guid definitionId, string name) => new()
    {
        Id = Guid.NewGuid(),
        PatientPathwayId = pathwayId,
        StageOrder = order,
        StageDefinitionId = definitionId,
        Name = name,
        Description = $"{name} description",
        Instructions = $"{name} instructions",
        CreatedAt = Now.AddDays(-7),
        LastUpdated = Now.AddDays(-7),
    };

    public const string ResultsStage = "Results and team review";
    public const string BenignBranch = "Benign";
    public const string TreatmentBranch = "Treatment";
    public const string DischargeStage = "Discharge letter";
    public const string BenignStage = "Benign letter";
    public const string TreatmentStage = "Treatment";
    public const string FollowUpStage = "Follow-up";

    /// <summary>Why a stage was skipped or reinstated: synthetic.</summary>
    public const string SkipDetail = "Synthetic detail: seen elsewhere.";

    /// <summary>
    /// A pathway that branches, InProgress unless given: 1 "Results and team review" is a branch point; 2 "Benign letter"
    /// is the Benign branch; 3 "Treatment" and 4 "Follow-up" (optional) the Treatment branch; 5 "Discharge letter"
    /// always runs. The first <paramref name="finishedStages"/> are finished.
    /// </summary>
    public static PatientPathway CreateBranchingPathway(
        PatientPathwayStatus status = PatientPathwayStatus.InProgress, int finishedStages = 0)
    {
        var pathway = CreatePathway(status);
        var id = pathway.Id;
        pathway.Stages =
        [
            CreateStage(id, 1, ConsultationDefinitionId, ResultsStage),
            CreateStage(id, 2, PathwayDefinitionId, BenignStage),
            CreateStage(id, 3, PathwayDefinitionId, TreatmentStage),
            CreateStage(id, 4, ConsultationDefinitionId, FollowUpStage),
            CreateStage(id, 5, PathwayDefinitionId, DischargeStage),
        ];
        pathway.StageAt(1).ChoosesBranch = true;
        pathway.StageAt(2).Branch = BenignBranch;
        pathway.StageAt(3).Branch = TreatmentBranch;
        pathway.StageAt(4).Branch = TreatmentBranch;
        pathway.StageAt(4).IsOptional = true;

        foreach (var stage in pathway.Stages.Take(finishedStages))
            Finish(stage);

        return pathway;
    }

    /// <summary>Marks a stage skipped by <see cref="Staff"/>, for a reason.</summary>
    public static void Skip(PatientPathwayStage stage, StageSkipReason reason = StageSkipReason.NotNeeded, string? detail = null)
    {
        stage.SkippedAtUtc = Now.AddDays(-3);
        stage.SkippedByUserId = Staff.UserId;
        stage.SkipReason = reason;
        stage.SkipDetail = detail;
    }

    /// <summary>Marks a stage started and finished (locked) by <see cref="Staff"/>.</summary>
    public static void Finish(PatientPathwayStage stage)
    {
        stage.StartedAtUtc = Now.AddDays(-5);
        stage.StartedByUserId = Staff.UserId;
        stage.Notes = "Done.";
        stage.IsCompleted = true;
        stage.IsLocked = true;
        stage.CompletedAtUtc = Now.AddDays(-4);
        stage.CompletedByUserId = Staff.UserId;
    }

    /// <summary>Synthetic text for a pathway note.</summary>
    public const string NoteText = "Synthetic note: prefers a morning appointment.";

    /// <summary>Adds a note (version 1, unedited) to a pathway, by <paramref name="author"/> (default <see cref="Staff"/>).</summary>
    public static PatientPathwayNote AddNote(
        this PatientPathway pathway, Guid? author = null, string text = NoteText, DateTime? createdAt = null, int version = 1)
    {
        var note = new PatientPathwayNote
        {
            Id = Guid.NewGuid(),
            PatientPathwayId = pathway.Id,
            AuthorUserId = author ?? Staff.UserId,
            Text = text,
            CreatedAtUtc = createdAt ?? Now.AddDays(-2),
            LastEditedAtUtc = version > 1 ? Now.AddDays(-1) : null,
            Version = version,
        };
        pathway.Notes.Add(note);
        return note;
    }

    /// <summary>Synthetic detail beside the reason for deleting a note, or asking to.</summary>
    public const string DeletionDetail = "Synthetic detail: belongs to another pathway.";

    /// <summary>
    /// A pending request, by <paramref name="requester"/> (default <see cref="Staff"/>), to delete a note on the
    /// pathway, made a day before <see cref="Now"/>; the note points at it, as the handler leaves it.
    /// </summary>
    public static PathwayNoteDeletionRequest RequestDeletion(
        this PatientPathway pathway, PatientPathwayNote note, PathwayActor? requester = null)
    {
        requester ??= Staff;
        var request = new PathwayNoteDeletionRequest
        {
            Id = Guid.NewGuid(),
            NoteId = note.Id,
            PatientPathwayId = pathway.Id,
            PatientId = pathway.PatientId,
            RequestedByUserId = requester.UserId,
            RequestedByUsername = requester.Username,
            Reason = NoteDeletionReason.WrongPatient,
            Detail = DeletionDetail,
            RequestedAtUtc = Now.AddDays(-1)
        };
        note.PendingDeletionRequestId = request.Id;
        return request;
    }

    /// <summary>The ordered stage at a 1-based position.</summary>
    public static PatientPathwayStage StageAt(this PatientPathway pathway, int order) =>
        pathway.Stages.Single(s => s.StageOrder == order);

    /// <summary>A search for the first page with no filters (closed pathways hidden), as <see cref="Staff"/>.</summary>
    public static SearchPatientPathwaysQuery CreateSearchQuery() =>
        new(1, 10, null, true, null, null, null, null, null, null, false, false, null, null, false, null, Staff.UserId);

    /// <summary>An audit search for the first page with no filters, newest first.</summary>
    public static SearchPathwayAuditQuery CreateAuditSearchQuery() =>
        new(1, 10, false, null, null, null, null, null, null, null);

    public static PatientPathwayStageDefinition CreateDefinition(string name, bool isArchived = false, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = name,
        Description = $"{name} description",
        Instructions = $"{name} instructions",
        IsArchived = isArchived,
    };

    /// <summary>A template listing these definitions in order (repeats allowed), with each map's definition loaded.</summary>
    public static PathwayTemplate CreateTemplate(
        string name = TemplateName,
        bool isArchived = false,
        params PatientPathwayStageDefinition[] stages)
    {
        var template = new PathwayTemplate { Id = Guid.NewGuid(), Name = name, IsArchived = isArchived };
        template.Maps = stages
            .Select((d, i) => new PathwayTemplateMap
            {
                TemplateId = template.Id,
                Template = template,
                StageDefinitionId = d.Id,
                StageDefinition = d,
                StageOrder = i + 1,
            })
            .ToList();
        return template;
    }

    public static PatientPathwayAuditEntry CreateAuditEntry(
        PatientPathwayAuditAction action,
        DateTime atUtc,
        string username = "test.staff",
        string pathwayTitle = PathwayTitle,
        string? stageName = null) => new()
    {
        Id = Guid.NewGuid(),
        PatientPathwayId = Guid.NewGuid(),
        PathwayTitle = pathwayTitle,
        PatientId = Guid.NewGuid(),
        StageId = stageName is null ? null : Guid.NewGuid(),
        StageName = stageName,
        Action = action,
        UserId = Guid.NewGuid(),
        Username = username,
        AtUtc = atUtc,
        Details = "{}",
    };

    /// <summary>
    /// An appointment for one of a pathway's stages: Requested (no staff or time), or with <see cref="Staff"/> at a
    /// time around <see cref="Now"/> (a day before for Attended and Did not attend, a day after otherwise).
    /// </summary>
    public static Appointment CreateStageAppointment(
        PatientPathway pathway,
        PatientPathwayStage stage,
        AppointmentStatus status = AppointmentStatus.Requested)
    {
        var booked = status != AppointmentStatus.Requested;
        var start = status is AppointmentStatus.Attended or AppointmentStatus.DidNotAttend ? Now.AddDays(-1) : Now.AddDays(1);
        return new Appointment
        {
            Id = Guid.NewGuid(),
            PatientId = pathway.PatientId,
            PathwayId = pathway.Id,
            StageId = stage.Id,
            Status = status,
            StaffUserId = booked ? Staff.UserId : null,
            StartUtc = booked ? start : null,
            EndUtc = booked ? start.AddMinutes(30) : null,
            IsAutomatic = !booked,
            CreatedAtUtc = Now.AddDays(-2),
            CreatedByUserId = Staff.UserId,
            LastUpdated = Now.AddDays(-2),
        };
    }

    /// <summary>A definition whose stages involve a visit.</summary>
    public static PatientPathwayStageDefinition WithVisit(this PatientPathwayStageDefinition definition, VisitRequirement requirement)
    {
        definition.VisitRequirement = requirement;
        return definition;
    }
}
