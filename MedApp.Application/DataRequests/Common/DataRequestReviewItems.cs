using MedApp.Contracts.DataRequests;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.DataRequests;

namespace MedApp.Application.DataRequests.Common;

/// <summary>
/// The items of a patient's record a clinician may withhold from a data request's pack (decision 161), read from the
/// export so the review sees what the pack would hold: notes (those of deleted pathways too), files, letters, results,
/// MDT items and referrals, oldest first.
/// </summary>
public static class DataRequestReviewItems
{
    /// <summary>Why a note about another patient starts withheld: it's a third party's information (decision 161).</summary>
    public const string AboutAnotherPatientReason =
        "About another patient: written on this record by mistake, so it's another person's information.";

    public static IReadOnlyList<DataRequestReviewItemResponse> From(PatientExportResponse export, DataRequest request)
    {
        var items = new List<DataRequestReviewItemResponse>();

        var notesOnPathways = new HashSet<Guid>();
        foreach (var pathway in export.Pathways)
        foreach (var note in pathway.Notes)
        {
            notesOnPathways.Add(note.Id);
            items.Add(Item(DataRequestItemKind.Note, note.Id, note.CreatedAtUtc, $"Note on {pathway.Title}", note.Text,
                note.Author, note.DeletedAtUtc is not null,
                note.IsAboutAnotherPatient ? AboutAnotherPatientReason : null));
        }

        // Notes of pathways since deleted live on in their versions: the latest version of each.
        foreach (var versions in export.NoteHistory.Where(v => !notesOnPathways.Contains(v.NoteId)).GroupBy(v => v.NoteId))
        {
            var first = versions.OrderBy(v => v.Version).First();
            var latest = versions.OrderBy(v => v.Version).Last();
            items.Add(Item(DataRequestItemKind.Note, versions.Key, first.AtUtc, "Note on a deleted pathway", latest.Text,
                first.Username, isDeleted: true, startsWithheldReason: null));
        }

        foreach (var file in export.Attachments)
            items.Add(Item(DataRequestItemKind.File, file.Id, file.UploadedAtUtc, $"File: {file.FileName}", string.Empty,
                file.UploadedBy, file.DeletedAtUtc is not null, null));

        foreach (var letter in export.Letters)
            items.Add(Item(DataRequestItemKind.Letter, letter.Id, letter.CreatedAt,
                $"Letter: {letter.TemplateName} ({Words.Humanise(letter.Status)})", letter.Body, letter.Author, false, null));

        foreach (var result in export.Results)
            items.Add(Item(DataRequestItemKind.Result, result.Id, result.ReportedAtUtc, $"Result: {result.Title}",
                result.ActionDetail ?? string.Empty, result.ReportedBy, false, null));

        foreach (var mdt in export.MdtItems)
            items.Add(Item(DataRequestItemKind.MdtItem, mdt.Id, mdt.ListedAtUtc, $"MDT discussion: {mdt.PathwayTitle}",
                string.Join("\n\n", new[] { mdt.Question, mdt.Outcome }.Where(t => !string.IsNullOrWhiteSpace(t))),
                mdt.ListedBy, false, null));

        foreach (var referral in export.Referrals)
            items.Add(Item(DataRequestItemKind.Referral, referral.Id, referral.RegisteredAtUtc,
                $"Referral to {referral.SpecialtyName} from {referral.ReferrerPractice}", referral.Reason,
                referral.RegisteredBy, false, null));

        // After a review, what it withheld; before one, what starts withheld.
        var withheld = request.WithheldItems.ToDictionary(w => (w.Kind, w.ItemId), w => w.Reason);
        foreach (var item in items)
        {
            if (request.IsReviewed)
            {
                var kind = Enum.Parse<DataRequestItemKind>(item.Kind);
                item.IsWithheld = withheld.TryGetValue((kind, item.ItemId), out var reason);
                item.WithheldReason = item.IsWithheld ? reason : item.WithheldReason;
            }
        }

        return items.OrderBy(i => i.AtUtc).ThenBy(i => i.Kind, StringComparer.Ordinal).ThenBy(i => i.ItemId).ToList();
    }

    private static DataRequestReviewItemResponse Item(
        DataRequestItemKind kind, Guid id, DateTime atUtc, string title, string text, string? by, bool isDeleted,
        string? startsWithheldReason) => new()
    {
        Kind = kind.ToString(),
        ItemId = id,
        AtUtc = atUtc,
        Title = title,
        Text = text,
        By = string.IsNullOrEmpty(by) ? null : by,
        IsDeleted = isDeleted,
        IsWithheld = startsWithheldReason is not null,
        WithheldReason = startsWithheldReason
    };
}
