using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Specimens.Responses;
using MedApp.Domain.Specimens;
using static MedApp.Domain.Specimens.SpecimenStatus;

namespace MedApp.Application.Specimens.Common;

/// <summary>Specimens, their types and labs as the API returns them.</summary>
public static class SpecimenMapper
{
    /// <summary>
    /// A specimen's page (and its label and request sheet): the specimen is loaded with its patient, pathway (and
    /// stages), type and lab; <paramref name="usernames"/> has the usernames of everyone it names (anyone missing is a
    /// "Deleted user"); <paramref name="others"/> are the other specimens its stage took; <paramref name="label"/> is its
    /// number drawn as a QR code. The <c>Can...</c> flags follow <paramref name="viewer"/>'s permissions.
    /// </summary>
    public static SpecimenResponse ToResponse(
        Specimen specimen,
        IReadOnlyDictionary<Guid, string> usernames,
        IReadOnlyList<Specimen> others,
        QrCodeImage label,
        PathwayActor viewer,
        DateTime nowUtc,
        bool canOpenLabCase = false)
    {
        var canRecord = viewer.Has(Permission.RecordSpecimens);
        var canProcess = viewer.Has(Permission.ProcessSpecimens);
        bool Can(SpecimenStatus to) => SpecimenWorkflow.WhyCantMoveTo(specimen, to) is null;

        return new SpecimenResponse
        {
            Id = specimen.Id,
            Number = specimen.Number,
            PatientId = specimen.PatientId,
            Patient = PathwayMapper.ToPatient(specimen.Patient),
            PatientFlags = specimen.Patient is null ? null : PatientMapper.ToFlags(specimen.Patient),
            Source = specimen.Source.ToString(),
            RequestingTeamId = specimen.RequestingTeamId,
            RequestingTeamName = specimen.RequestingTeam?.Name,
            HospitalId = specimen.HospitalId,
            HospitalName = HospitalName(specimen),
            HospitalRequestId = specimen.HospitalRequestId,
            LabTestId = specimen.LabTestId,
            LabTestName = specimen.LabTest?.Name,
            LabCaseId = specimen.LabCaseId,
            PathwayId = specimen.PathwayId,
            PathwayTitle = specimen.Pathway?.Title,
            StageId = specimen.StageId,
            StageName = specimen.Pathway?.Stages.FirstOrDefault(s => s.Id == specimen.StageId)?.Name,
            SpecimenTypeId = specimen.SpecimenTypeId,
            SpecimenTypeName = specimen.SpecimenType?.Name ?? string.Empty,
            ExpectedTurnaroundDays = specimen.SpecimenType?.ExpectedTurnaroundDays ?? 0,
            Site = specimen.Site,
            RequestNotes = specimen.RequestNotes,
            RequestingUserId = specimen.RequestingUserId,
            RequestingUsername = Username(usernames, specimen.RequestingUserId),
            Status = specimen.Status.ToString(),
            IsOpen = SpecimenWorkflow.IsOpen(specimen.Status),
            TakenAtUtc = specimen.TakenAtUtc,
            TakenByUsername = Username(usernames, specimen.TakenByUserId),
            SentAtUtc = specimen.SentAtUtc,
            SentByUsername = Username(usernames, specimen.SentByUserId),
            CourierReference = specimen.CourierReference,
            ReceivedAtUtc = specimen.ReceivedAtUtc,
            ReceivedByUsername = Username(usernames, specimen.ReceivedByUserId),
            LabReference = specimen.LabReference,
            ReportedAtUtc = specimen.ReportedAtUtc,
            ReportedByUsername = Username(usernames, specimen.ReportedByUserId),
            ReportAttachmentId = specimen.ReportAttachmentId,
            RejectionReason = specimen.RejectionReason?.ToString(),
            ClosedDetail = specimen.ClosedDetail,
            ClosedAtUtc = specimen.ClosedAtUtc,
            ClosedByUsername = Username(usernames, specimen.ClosedByUserId),
            ExpectedByUtc = SpecimenWorkflow.ExpectedBy(specimen, nowUtc),
            IsOverdueAtLab = SpecimenWorkflow.IsOverdueAtLab(specimen, nowUtc),
            Clock = SpecimenWorkflow.CaseClock(specimen, nowUtc),
            Version = specimen.Version,
            CanEdit = canRecord && SpecimenWorkflow.WhyCantEdit(specimen) is null,
            CanMarkSent = canRecord && Can(Sent),
            CanMarkReceived = canProcess && Can(Received),
            CanReject = canProcess && Can(RejectedByLab),
            CanReport = canProcess && Can(Reported),
            CanMarkLost = SpecimenWorkflow.MayMarkLost(viewer) && Can(Lost),
            CanMarkEnteredInError = SpecimenWorkflow.MayMarkEnteredInError(specimen, viewer, nowUtc) && Can(EnteredInError),
            ReadOnlyReason = ReadOnlyReason(specimen),
            LabCaseNumber = specimen.LabCase?.Number,
            CanOpenLabCase = canOpenLabCase,
            Label = new SpecimenLabelResponse { QrPath = label.Path, Size = label.Size },
            OtherSpecimensFromStage = others
                .Where(o => o.Id != specimen.Id)
                .OrderBy(o => o.TakenAtUtc).ThenBy(o => o.Id)
                .Select(o => ToSummary(o, nowUtc))
                .ToList(),
        };
    }

    /// <summary>The hospital that sent it (MC18), by name; null for a clinic's.</summary>
    public static string? HospitalName(Specimen specimen) =>
        specimen.HospitalId is null ? null : specimen.Hospital?.Name ?? UnknownHospital;

    /// <summary>What a hospital is called when it isn't read with the record.</summary>
    public const string UnknownHospital = "The hospital";

    /// <summary>A specimen in a list, with its patient, type and lab: dates, names and statuses only.</summary>
    public static SpecimenSummaryResponse ToSummary(Specimen specimen, DateTime nowUtc) => new()
    {
        Id = specimen.Id,
        Number = specimen.Number,
        PatientId = specimen.PatientId,
        PatientName = specimen.Patient is null ? string.Empty : PatientMapper.FullName(specimen.Patient),
        PatientNhsNumber = specimen.Patient?.NhsNumber,
        PatientDateOfBirth = specimen.Patient?.DateOfBirth ?? default,
        Source = specimen.Source.ToString(),
        RequestingTeamId = specimen.RequestingTeamId,
        RequestingTeamName = specimen.RequestingTeam?.Name,
        HospitalId = specimen.HospitalId,
        HospitalName = HospitalName(specimen),
        HospitalRequestId = specimen.HospitalRequestId,
        LabTestId = specimen.LabTestId,
        LabTestName = specimen.LabTest?.Name,
        LabCaseId = specimen.LabCaseId,
        PathwayId = specimen.PathwayId,
        StageId = specimen.StageId,
        SpecimenTypeName = specimen.SpecimenType?.Name ?? string.Empty,
        Status = specimen.Status.ToString(),
        IsOpen = SpecimenWorkflow.IsOpen(specimen.Status),
        TakenAtUtc = specimen.TakenAtUtc,
        SentAtUtc = specimen.SentAtUtc,
        ReceivedAtUtc = specimen.ReceivedAtUtc,
        ReportedAtUtc = specimen.ReportedAtUtc,
        ClosedAtUtc = specimen.ClosedAtUtc,
        ExpectedByUtc = SpecimenWorkflow.ExpectedBy(specimen, nowUtc),
        IsOverdueAtLab = SpecimenWorkflow.IsOverdueAtLab(specimen, nowUtc),
        Clock = SpecimenWorkflow.CaseClock(specimen, nowUtc),
    };

    public static SpecimenTypeResponse ToResponse(SpecimenType type, bool inUse) => new()
    {
        Id = type.Id,
        Name = type.Name,
        ExpectedTurnaroundDays = type.ExpectedTurnaroundDays,
        SnomedCode = type.SnomedCode,
        IsArchived = type.IsArchived,
        CanBeDeleted = !inUse,
    };


    /// <summary>Everyone a specimen names, for one username lookup.</summary>
    public static IEnumerable<Guid> PeopleIn(Specimen specimen) =>
        new[]
        {
            specimen.RequestingUserId, specimen.TakenByUserId, specimen.SentByUserId, specimen.ReceivedByUserId,
            specimen.ReportedByUserId, specimen.ClosedByUserId
        }.OfType<Guid>().Distinct();

    /// <summary>Why the specimen can't change now: the record's restriction or anonymisation, or its being closed.</summary>
    public static string? ReadOnlyReason(Specimen specimen) =>
        PatientRules.IsAnonymised(specimen.Patient) ? PatientRules.AnonymisedMessage
        : !SpecimenWorkflow.IsOpen(specimen.Status) ? SpecimenWorkflow.ClosedMessage(specimen.Status)
        : PatientRules.IsRestricted(specimen.Patient)
            ? "This patient's record is restricted: the sample can still go on to the lab and be reported, but its details can't be changed."
            : null;

    private static string Username(IReadOnlyDictionary<Guid, string> usernames, Guid userId) =>
        usernames.TryGetValue(userId, out var name) ? name : PathwayMapper.DeletedUser;

    private static string? Username(IReadOnlyDictionary<Guid, string> usernames, Guid? userId) =>
        userId is { } id ? Username(usernames, id) : null;
}
