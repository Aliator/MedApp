using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.DataRequests;

namespace MedApp.Application.DataRequests.Common;

/// <summary>
/// Takes what the clinician review withheld out of a patient's export, in place, so the pack's report, its JSON and its
/// files all leave out the same things (decision 161):
/// <list type="bullet">
/// <item>A withheld note goes with every version of it and every request to delete it.</item>
/// <item>A withheld file goes from the files and their list; so does a withheld letter's PDF, a withheld result's
/// report, and a withheld referral's own files (its letter).</item>
/// <item>A withheld referral goes with its history.</item>
/// <item>Any history or access log entry whose details name a withheld item goes too, so nothing about it (a file's
/// name, a title) is left in the record's history.</item>
/// <item>The data requests' own reviews (what was withheld, and why) are left out: saying what was withheld, or why,
/// could itself reveal it. The report says, in general terms, that information was withheld.</item>
/// </list>
/// </summary>
public static class DataRequestPackFilter
{
    /// <summary>What was left out, for the report and the ZIP.</summary>
    public sealed record Outcome(int WithheldCount, IReadOnlySet<Guid> WithheldFileIds);

    public static Outcome Apply(PatientExportResponse export, IReadOnlyCollection<DataRequestWithheldItem> withheld)
    {
        Guid[] Ids(DataRequestItemKind kind) => withheld.Where(w => w.Kind == kind).Select(w => w.ItemId).ToArray();

        var notes = Ids(DataRequestItemKind.Note).ToHashSet();
        var letters = Ids(DataRequestItemKind.Letter).ToHashSet();
        var results = Ids(DataRequestItemKind.Result).ToHashSet();
        var mdtItems = Ids(DataRequestItemKind.MdtItem).ToHashSet();
        var referrals = Ids(DataRequestItemKind.Referral).ToHashSet();

        var files = Ids(DataRequestItemKind.File).ToHashSet();
        files.UnionWith(export.Letters.Where(l => letters.Contains(l.Id) && l.PdfAttachmentId is not null).Select(l => l.PdfAttachmentId!.Value));
        files.UnionWith(export.Results.Where(r => results.Contains(r.Id)).Select(r => r.ReportAttachmentId));
        files.UnionWith(export.Attachments.Where(a => a.ReferralId is { } id && referrals.Contains(id)).Select(a => a.Id));

        foreach (var pathway in export.Pathways)
            pathway.Notes.RemoveAll(n => notes.Contains(n.Id));
        export.NoteHistory.RemoveAll(v => notes.Contains(v.NoteId));
        export.NoteDeletionRequests.RemoveAll(r => notes.Contains(r.NoteId));
        export.Attachments.RemoveAll(a => files.Contains(a.Id));
        export.Letters.RemoveAll(l => letters.Contains(l.Id));
        export.Results.RemoveAll(r => results.Contains(r.Id));
        export.MdtItems.RemoveAll(i => mdtItems.Contains(i.Id));
        export.Referrals.RemoveAll(r => referrals.Contains(r.Id));
        export.ReferralHistory.RemoveAll(e => referrals.Contains(e.ReferralId));

        // Anything in the history that names a withheld item (its ID, as details hold IDs).
        var named = notes.Concat(letters).Concat(results).Concat(mdtItems).Concat(referrals).Concat(files)
            .Select(id => id.ToString("D"))
            .ToList();
        bool Names(string details) => named.Any(id => details.Contains(id, StringComparison.OrdinalIgnoreCase));
        export.PathwayHistory.RemoveAll(e => Names(e.Details));
        export.AppointmentHistory.RemoveAll(e => Names(e.Details));
        export.ReferralHistory.RemoveAll(e => Names(e.Details));
        export.AccessLog.RemoveAll(e => Names(e.Details));

        foreach (var request in export.DataRequests)
            request.Withheld = [];

        return new Outcome(withheld.Count, files);
    }
}
