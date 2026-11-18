using MedApp.Application.Patients.Common;
using MedApp.Application.Procedures.Common;
using MedApp.Contracts.Procedures.NoteDeletionRequests.Responses;
using MedApp.Domain.Patients;
using MedApp.Domain.Procedures.PatientProcedures;

namespace MedApp.Application.Procedures.NoteDeletionRequests.Common;

public static class NoteDeletionRequestMapper
{
    /// <summary>
    /// Every user a request's response names besides those it recorded by username: the note's author, and the admin
    /// who deleted it.
    /// </summary>
    public static IEnumerable<Guid> UserIds(ProcedureNoteDeletionRequest request, PatientProcedure? procedure) =>
        NoteOf(request, procedure) is { } note
            ? new[] { note.AuthorUserId, note.DeletedByUserId }.OfType<Guid>()
            : [];

    /// <summary>
    /// A request as an admin decides it: the note (null once its procedure is deleted), the patient as the patient
    /// banner shows them, the procedure's title, who asked, when and why, and the outcome. It can be decided while it's
    /// pending, its note is there, and the patient's record can change.
    /// </summary>
    public static NoteDeletionRequestResponse ToResponse(
        ProcedureNoteDeletionRequest request,
        PatientProcedure? procedure,
        Patient? patient,
        IReadOnlyDictionary<Guid, string> usernames)
    {
        var note = NoteOf(request, procedure);
        var whyCantDecide = request.IsPending ? PatientRules.WhyCantChange(patient) : null;

        return new NoteDeletionRequestResponse
        {
            Id = request.Id,
            Status = request.Status.ToString(),
            Reason = request.Reason.ToString(),
            Detail = request.Detail,
            RequestedByUsername = request.RequestedByUsername,
            RequestedAtUtc = request.RequestedAtUtc,
            DecidedByUsername = request.DecidedByUsername,
            DecidedAtUtc = request.DecidedAtUtc,
            ProcedureId = request.PatientProcedureId,
            ProcedureTitle = procedure?.Title,
            Patient = ProcedureMapper.ToPatient(patient),
            Note = note is null
                ? null
                : new NoteDeletionRequestNoteResponse
                {
                    Id = note.Id,
                    Text = note.Text,
                    AuthorUsername = ProcedureNoteMapper.AuthorName(note.AuthorUserId, usernames),
                    CreatedAtUtc = note.CreatedAtUtc,
                    LastEditedAtUtc = note.LastEditedAtUtc,
                    Version = note.Version,
                    DeletedAtUtc = note.DeletedAtUtc,
                    DeletedByUsername = note.DeletedByUserId is { } deletedBy
                        ? usernames.GetValueOrDefault(deletedBy, ProcedureMapper.DeletedUser)
                        : null,
                    DeletionReason = note.DeletionReason?.ToString(),
                    DeletionDetail = note.DeletionDetail
                },
            CanDecide = request.IsPending && note is not null && !note.IsDeleted && whyCantDecide is null,
            WhyCantDecide = whyCantDecide
        };
    }

    private static PatientProcedureNote? NoteOf(ProcedureNoteDeletionRequest request, PatientProcedure? procedure) =>
        procedure?.Notes.FirstOrDefault(n => n.Id == request.NoteId);
}
