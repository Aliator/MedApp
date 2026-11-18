using MedApp.Application.Patients.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Pathways.NoteDeletionRequests.Responses;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;

namespace MedApp.Application.Pathways.NoteDeletionRequests.Common;

public static class NoteDeletionRequestMapper
{
    /// <summary>
    /// Every user a request's response names besides those it recorded by username: the note's author, and the admin
    /// who deleted it.
    /// </summary>
    public static IEnumerable<Guid> UserIds(PathwayNoteDeletionRequest request, PatientPathway? pathway) =>
        NoteOf(request, pathway) is { } note
            ? new[] { note.AuthorUserId, note.DeletedByUserId }.OfType<Guid>()
            : [];

    /// <summary>
    /// A request as an admin decides it: the note (null once its pathway is deleted), the patient as the patient
    /// banner shows them, the pathway's title, who asked, when and why, and the outcome. It can be decided while it's
    /// pending, its note is there, and the patient's record can change.
    /// </summary>
    public static NoteDeletionRequestResponse ToResponse(
        PathwayNoteDeletionRequest request,
        PatientPathway? pathway,
        Patient? patient,
        IReadOnlyDictionary<Guid, string> usernames)
    {
        var note = NoteOf(request, pathway);
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
            PathwayId = request.PatientPathwayId,
            PathwayTitle = pathway?.Title,
            Patient = PathwayMapper.ToPatient(patient),
            Note = note is null
                ? null
                : new NoteDeletionRequestNoteResponse
                {
                    Id = note.Id,
                    Text = note.Text,
                    AuthorUsername = PathwayNoteMapper.AuthorName(note.AuthorUserId, usernames),
                    CreatedAtUtc = note.CreatedAtUtc,
                    LastEditedAtUtc = note.LastEditedAtUtc,
                    Version = note.Version,
                    DeletedAtUtc = note.DeletedAtUtc,
                    DeletedByUsername = note.DeletedByUserId is { } deletedBy
                        ? usernames.GetValueOrDefault(deletedBy, PathwayMapper.DeletedUser)
                        : null,
                    DeletionReason = note.DeletionReason?.ToString(),
                    DeletionDetail = note.DeletionDetail
                },
            CanDecide = request.IsPending && note is not null && !note.IsDeleted && whyCantDecide is null,
            WhyCantDecide = whyCantDecide
        };
    }

    private static PatientPathwayNote? NoteOf(PathwayNoteDeletionRequest request, PatientPathway? pathway) =>
        pathway?.Notes.FirstOrDefault(n => n.Id == request.NoteId);
}
