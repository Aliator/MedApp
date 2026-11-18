using System.Text;
using MedApp.Application.Letters.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.DataRequests;

namespace MedApp.Application.DataRequests.Common;

/// <summary>
/// The pack's report (decision 162): a plain-language letter, in Markdown for the letters' renderer, with what Article
/// 15 asks for (why the information is held, who it's shared with, how long it's kept, where it came from, the patient's
/// rights and how to complain to the ICO) beside the information itself, read from the export after the review's
/// withholding (<see cref="DataRequestPackFilter"/>). Every value from the record is escaped, so nothing in it can change
/// the formatting. The history and the full access log are in the pack's <c>record.json</c>; the report lists the most
/// recent accesses.
/// </summary>
public static class DataRequestReport
{
    /// <summary>The most access log entries the report lists (newest first); the rest are in <c>record.json</c>.</summary>
    public const int MaxAccessEntries = 300;

    public const string RecordFileName = "record.json";
    public const string FilesFolder = "files";

    /// <summary>What the report needs besides the export.</summary>
    public sealed record Inputs(
        PatientExportResponse Export,
        DataRequest Request,
        string CentreName,
        RetentionOptions Retention,
        int WithheldCount,
        IReadOnlyList<string> FilesIncluded,
        IReadOnlyList<string> FilesUnreadable,
        DateOnly Today);

    public static string Body(Inputs inputs)
    {
        var md = new StringBuilder();
        var export = inputs.Export;
        var patient = export.Patient;

        Para(md, $"You asked us for a copy of the information we hold about you. We received your request on {Words.Date(inputs.Request.ReceivedOn)}. " +
                 "This report explains why we hold your information, who we share it with, how long we keep it and where it came from, " +
                 "and then sets out the information itself. Your rights, and how to complain, are at the end.");
        Para(md, $"With this report you'll find the files we hold about you (in the \"{FilesFolder}\" folder) and the whole record in a " +
                 $"form a computer can read (\"{RecordFileName}\"), which you can take to another provider.");

        Heading(md, "Why we hold your information");
        Para(md, $"{inputs.CentreName} holds your information to provide and manage your care: your referrals, your care pathways, " +
                 "appointments, tests and their results, and letters about your care. The law allows this as part of providing health " +
                 "care (UK GDPR Article 6(1)(e), or 6(1)(b) for private care, and Article 9(2)(h)). We also record who has looked at " +
                 "or changed your record, to keep it safe.");
        Para(md, "We make no decisions about you by automated means: the people caring for you make every decision.");

        Heading(md, "Who we share it with");
        Bullets(md,
            "Your GP practice, and other clinicians involved in your care, in letters about you.",
            "The laboratories that test samples taken from you.",
            "Our technology suppliers, who host the system under contract and can't use your information for anything else.",
            "If you chose text or email reminders, the service that sends them for us (GOV.UK Notify).",
            "If you're a privately insured patient, your insurer, for the care they pay for.");
        Para(md, "We don't sell your information or use it for marketing.");

        Heading(md, "How long we keep it");
        var r = inputs.Retention;
        Para(md, $"We keep adults' health records for at least {r.AdultYears} years after their treatment ends, and children's until " +
                 $"their {Ordinal(r.ChildUntilAge)} birthday ({Ordinal(r.ChildAgedSeventeenUntilAge)} if they were 17 when treatment ended), " +
                 $"as the NHS Records Management Code of Practice sets out. Some care is kept longer: cancer care {r.Classes.Oncology} years, " +
                 $"maternity care {r.Classes.Maternity} years and mental health care {r.Classes.MentalHealth} years. After that, a " +
                 "person reviews the record before anything is destroyed.");

        Heading(md, "Where it came from");
        Para(md, "From you; from whoever referred you to us (usually your GP practice); from the staff caring for you; and from the " +
                 "laboratories that test your samples. Your name, date of birth and NHS number may be checked against the NHS's " +
                 "national list of patients.");

        Heading(md, "Information not included");
        Para(md, "Information about other people is left out, unless they agreed to it being shared or it's reasonable to include it. " +
                 "Staff are named by their usernames.");
        if (inputs.WithheldCount > 0)
            Para(md, "Some information isn't included, because a health professional decided that giving it to you would be likely " +
                     "to cause serious harm to your physical or mental health, or someone else's (Data Protection Act 2018, Schedule 3). " +
                     "You can ask us about this, or complain to the Information Commissioner's Office (below).");

        WriteDetails(md, patient);
        WriteReferrals(md, export);
        WritePathways(md, export);
        WriteAppointments(md, export);
        WriteMessages(md, export);
        WriteLetters(md, export);
        WriteSpecimensAndResults(md, export);
        WriteMdtAndConsent(md, export);
        WriteFiles(md, export, inputs);
        WriteDataRequests(md, export);
        WriteAccess(md, export);

        Heading(md, "Your rights");
        Bullets(md,
            "To have information that's wrong corrected. If we and you disagree about it, we record that you disagree.",
            "To ask us to restrict how we use your information while a concern is looked into.",
            "To object to how we use it, and in some cases to have it erased (health records usually have to be kept, as above).",
            $"To take your information to another provider: \"{RecordFileName}\" holds it in a form a computer can read.",
            $"To complain to us: write to the data protection officer at {inputs.CentreName}.",
            "To complain to the Information Commissioner's Office (ICO), the UK's regulator for information: ico.org.uk/make-a-complaint, or 0303 123 1113.");

        return md.ToString();
    }

    /// <summary>The PDF's file name inside the pack: never the patient's name.</summary>
    public const string PdfFileName = "Your data request report.pdf";

    /// <summary>The document the renderer makes the PDF from.</summary>
    public static LetterDocument Document(Inputs inputs)
    {
        var patient = inputs.Export.Patient;
        var name = $"{patient.FirstName} {patient.LastName}";
        var recipient = inputs.Request.RequestedByPatient || string.IsNullOrWhiteSpace(inputs.Request.RequesterName)
            ? new[] { name }.Concat(patient.AddressLines).Concat(string.IsNullOrWhiteSpace(patient.Postcode) ? [] : [patient.Postcode!]).ToList()
            : [inputs.Request.RequesterName!];

        return new LetterDocument(
            Title: "Your data request",
            Letterhead: [inputs.CentreName, Words.Date(inputs.Today)],
            RecipientAddress: recipient,
            Body: Body(inputs),
            Closing: ["Yours sincerely", string.Empty, "Data protection officer", inputs.CentreName],
            PatientIdentifiers: LetterIdentifiers.Line(name, Words.Date(patient.DateOfBirth), patient.NhsNumber),
            LargePrint: false,
            Marking: null);
    }

    private static void WriteDetails(StringBuilder md, ExportedPatientResponse p)
    {
        Heading(md, "Your details");
        Fields(md,
            ("Name", $"{p.FirstName} {p.LastName}"),
            ("Date of birth", Words.Date(p.DateOfBirth)),
            ("NHS number", p.NhsNumber),
            ("Address", string.Join(", ", p.AddressLines.Concat(string.IsNullOrWhiteSpace(p.Postcode) ? [] : [p.Postcode!]))),
            ("Mobile", p.MobilePhone),
            ("Email", p.Email),
            ("GP practice", p.PracticeName is null ? null : $"{p.PracticeName}{(p.PracticeOdsCode is null ? "" : $" ({p.PracticeOdsCode})")}"),
            ("Reminders", Words.Humanise(p.ReminderChannel)),
            ("Record added", Words.Time(p.CreatedAt)),
            ("Restricted", p.RestrictedAtUtc is null ? null : $"{Words.Time(p.RestrictedAtUtc)}: {p.RestrictionReason}"),
            ("Disputed", p.DisputedAtUtc is null ? null : $"{Words.Time(p.DisputedAtUtc)}: {p.DisputeReason}"));
    }

    private static void WriteReferrals(StringBuilder md, PatientExportResponse export)
    {
        if (export.Referrals.Count == 0)
            return;
        Heading(md, "Your referrals");
        foreach (var r in export.Referrals)
        {
            SubHeading(md, $"Referral to {r.TeamName}, received {Words.Date(r.ReceivedOn)}");
            Fields(md,
                ("From", string.Join(", ", new[] { r.ReferrerClinician, r.ReferrerPractice }.Where(v => !string.IsNullOrWhiteSpace(v)))),
                ("Priority asked for", Words.Humanise(r.RequestedPriority)),
                ("Reason", r.Reason),
                ("Status", Words.Humanise(r.Status)),
                ("Decided", r.DecidedAtUtc is null ? null : $"{Words.Time(r.DecidedAtUtc)} by {r.DecidedBy}"),
                ("Why", string.Join(" ", new[] { Words.Humanise(r.RejectionReason), r.DecisionDetail }.Where(v => !string.IsNullOrWhiteSpace(v)))),
                ("Sent on to", string.Join(" ", new[] { r.RedirectedTo, r.RedirectDetail }.Where(v => !string.IsNullOrWhiteSpace(v)))),
                ("Waiting time target", Words.Date(r.TargetOn)));
        }
    }

    private static void WritePathways(StringBuilder md, PatientExportResponse export)
    {
        if (export.Pathways.Count == 0)
            return;
        Heading(md, "Your care pathways");
        foreach (var p in export.Pathways)
        {
            SubHeading(md, p.Title);
            Fields(md,
                ("Status", Words.PathwayStatus(p.Status)),
                ("Priority", Words.Humanise(p.Priority)),
                ("Target date", Words.Date(p.DueDate)),
                ("Started", Words.Time(p.CreatedAt)),
                ("Staff", string.Join(", ", p.AssignedTo)));
            if (p.Stages.Count > 0)
            {
                Para(md, "**Stages**");
                foreach (var s in p.Stages.OrderBy(s => s.Order))
                {
                    var state = s.SkippedAtUtc is not null ? $"skipped {Words.Time(s.SkippedAtUtc)}"
                        : s.FinishedAtUtc is not null ? $"finished {Words.Time(s.FinishedAtUtc)} by {s.FinishedBy}"
                        : s.StartedAtUtc is not null ? $"started {Words.Time(s.StartedAtUtc)}"
                        : "not started";
                    Bullet(md, $"{s.Name}: {state}", s.Notes);
                }
                md.AppendLine();
            }

            if (p.Notes.Count > 0)
            {
                Para(md, "**Notes**");
                foreach (var n in p.Notes.OrderBy(n => n.CreatedAtUtc))
                    Bullet(md, $"{Words.Time(n.CreatedAtUtc)}, {n.Author}{(n.DeletedAtUtc is null ? "" : $" (deleted {Words.Time(n.DeletedAtUtc)})")}", n.Text);
                md.AppendLine();
            }
        }
    }

    private static void WriteAppointments(StringBuilder md, PatientExportResponse export)
    {
        if (export.Appointments.Count == 0)
            return;
        Heading(md, "Your appointments");
        foreach (var a in export.Appointments.OrderBy(a => a.StartUtc ?? a.CreatedAtUtc))
        {
            var when = a.StartUtc is null ? "Not yet booked" : Words.Time(a.StartUtc);
            var with = a.StaffUsername is null ? "" : $", with {a.StaffUsername}";
            var detail = string.Join("\n", new[] { a.Notes, a.CancelReason is null ? null : $"Cancelled: {a.CancelReason}" }
                .Where(v => !string.IsNullOrWhiteSpace(v)));
            Bullet(md, $"{when}{with}: {Words.Humanise(a.Status)}{(a.StageName is null ? "" : $" ({a.StageName})")}", detail);
        }
        md.AppendLine();
    }

    private static void WriteMessages(StringBuilder md, PatientExportResponse export)
    {
        if (export.Messages.Count == 0)
            return;
        Heading(md, "Texts and emails we sent you");
        foreach (var m in export.Messages)
            Bullet(md, $"{Words.Time(m.SentAtUtc ?? m.CreatedAtUtc)}, {Words.Humanise(m.Channel)} to {m.Destination}: {Words.Humanise(m.Status)}", m.Body);
        md.AppendLine();
    }

    private static void WriteLetters(StringBuilder md, PatientExportResponse export)
    {
        if (export.Letters.Count == 0)
            return;
        Heading(md, "Letters about you");
        foreach (var l in export.Letters)
        {
            SubHeading(md, $"{l.TemplateName}, {Words.Time(l.ApprovedAtUtc ?? l.CreatedAt)}");
            Fields(md,
                ("Status", Words.Humanise(l.Status)),
                ("Written by", l.Author),
                ("Approved by", l.Approver),
                ("Sent to", string.Join("; ", l.Recipients.Select(r =>
                    $"{r.Name} ({Words.Humanise(r.Kind)}){(r.SentOn is null ? "" : $", {Words.Humanise(r.Method)} {Words.Date(r.SentOn)}")}"))));
            Quote(md, l.Body);
        }
    }

    private static void WriteSpecimensAndResults(StringBuilder md, PatientExportResponse export)
    {
        if (export.Specimens.Count > 0)
        {
            Heading(md, "Samples taken for tests");
            foreach (var s in export.Specimens)
                Bullet(md, $"{s.Number}: {s.SpecimenTypeName}, taken {Words.Time(s.TakenAtUtc)}, sent to {s.LaboratoryName}: {Words.Humanise(s.Status)}",
                    string.Join("\n", new[] { s.Site, s.RequestNotes, s.RejectionReason, s.ClosedDetail }.Where(v => !string.IsNullOrWhiteSpace(v))));
            md.AppendLine();
        }

        if (export.Results.Count > 0)
        {
            Heading(md, "Results");
            foreach (var r in export.Results)
                Bullet(md, $"{r.Title}, reported {Words.Time(r.ReportedAtUtc)}: {Words.Humanise(r.Status)}" +
                           (r.SignedOffAtUtc is null ? "" : $", signed off by {r.SignedOffBy} ({Words.Humanise(r.Action)})"),
                    r.ActionDetail);
            md.AppendLine();
        }
    }

    private static void WriteMdtAndConsent(StringBuilder md, PatientExportResponse export)
    {
        if (export.MdtItems.Count > 0)
        {
            Heading(md, "Discussions at team meetings (MDTs)");
            foreach (var i in export.MdtItems)
                Bullet(md, $"{i.PathwayTitle}, {i.Team}{(i.MeetingStartUtc is null ? "" : $", {Words.Time(i.MeetingStartUtc)}")}: {Words.Humanise(i.Status)}",
                    string.Join("\n", new[] { $"Question: {i.Question}", i.Outcome is null ? null : $"Outcome: {i.Outcome}" }.Where(v => v is not null)));
            md.AppendLine();
        }

        if (export.ConsentRecords.Count > 0)
        {
            Heading(md, "Your consent");
            foreach (var c in export.ConsentRecords)
                Bullet(md, $"{c.FormName} (version {c.FormVersion}), signed {Words.Date(c.SignedOn)}: {Words.Humanise(c.Status)}" +
                           (c.WithdrawnOn is null ? "" : $", withdrawn {Words.Date(c.WithdrawnOn)}"),
                    c.StatusDetail);
            md.AppendLine();
        }
    }

    private static void WriteFiles(StringBuilder md, PatientExportResponse export, Inputs inputs)
    {
        if (export.Attachments.Count == 0)
            return;
        Heading(md, "Files");
        Para(md, $"The files still held are in the \"{FilesFolder}\" folder. Files deleted from your record are listed, but can no longer be opened.");
        foreach (var f in export.Attachments)
            Bullet(md, $"{f.FileName} ({f.TypeName}, {Words.Size(f.SizeBytes)}), added {Words.Time(f.UploadedAtUtc)} by {f.UploadedBy}" +
                       (f.DeletedAtUtc is null ? "" : $", deleted {Words.Time(f.DeletedAtUtc)}"), null);
        md.AppendLine();
        if (inputs.FilesUnreadable.Count > 0)
            Para(md, $"We couldn't read {inputs.FilesUnreadable.Count} of the files for this pack; ask us for {(inputs.FilesUnreadable.Count == 1 ? "it" : "them")}: " +
                     Plain(string.Join(", ", inputs.FilesUnreadable)) + ".");
    }

    private static void WriteDataRequests(StringBuilder md, PatientExportResponse export)
    {
        Heading(md, "Your requests for your information");
        foreach (var d in export.DataRequests)
            Bullet(md, $"Received {Words.Date(d.ReceivedOn)}, due {Words.Date(d.DueOn)}: {Words.Humanise(d.Status)}" +
                       (d.SentOn is null ? "" : $", sent {Words.Date(d.SentOn)}"),
                d.ExtensionReason is null ? null : $"Extended: {d.ExtensionReason}");
        md.AppendLine();
    }

    private static void WriteAccess(StringBuilder md, PatientExportResponse export)
    {
        Heading(md, "Who has looked at or changed your record");
        var entries = export.AccessLog.OrderByDescending(e => e.AtUtc).ToList();
        if (entries.Count > MaxAccessEntries)
            Para(md, $"The most recent {MaxAccessEntries} of {entries.Count}; every one is in \"{RecordFileName}\".");
        foreach (var e in entries.Take(MaxAccessEntries))
            Bullet(md, $"{Words.Time(e.AtUtc)}: {Words.Humanise(e.Action)} by {e.Username}", null);
        md.AppendLine();
    }

    // Markdown helpers: every value is escaped (LetterMergeFields.EscapeMarkdown), its lines kept as hard breaks.

    /// <summary>A value as Markdown that shows exactly it, its lines as hard breaks.</summary>
    public static string Plain(string? text) =>
        string.Join("\\\n", LetterMergeFields.SplitLines(text).Select(LetterMergeFields.EscapeMarkdown));

    private static void Heading(StringBuilder md, string text) => md.Append("## ").AppendLine(Plain(text)).AppendLine();

    private static void SubHeading(StringBuilder md, string text) => md.Append("### ").AppendLine(Plain(text)).AppendLine();

    private static void Para(StringBuilder md, string text) => md.AppendLine(text.StartsWith("**", StringComparison.Ordinal) ? text : Plain(text)).AppendLine();

    private static void Bullets(StringBuilder md, params string[] items)
    {
        foreach (var item in items)
            md.Append("- ").AppendLine(Plain(item));
        md.AppendLine();
    }

    /// <summary>One list item: its heading line, and any free text under it on its own lines.</summary>
    private static void Bullet(StringBuilder md, string head, string? text)
    {
        md.Append("- ").Append(Plain(head));
        var body = Plain(text);
        if (body.Length > 0)
            md.Append("\\\n").Append(body.Replace("\\\n", "\\\n  ", StringComparison.Ordinal));
        md.AppendLine();
    }

    /// <summary>Label and value lines, leaving out empty values.</summary>
    private static void Fields(StringBuilder md, params (string Label, string? Value)[] fields)
    {
        var lines = fields.Where(f => !string.IsNullOrWhiteSpace(f.Value))
            .Select(f => $"**{Plain(f.Label)}:** {Plain(f.Value)}")
            .ToList();
        if (lines.Count == 0)
            return;
        md.AppendLine(string.Join("\\\n", lines)).AppendLine();
    }

    /// <summary>A letter's own text, as a quote, shown as written (escaped, not formatted).</summary>
    private static void Quote(StringBuilder md, string? text)
    {
        var lines = LetterMergeFields.SplitLines(text);
        if (lines.Count == 0)
            return;
        md.AppendLine(string.Join("\n", lines.Select(l => "> " + LetterMergeFields.EscapeMarkdown(l) + "\\")).TrimEnd('\\')).AppendLine();
    }

    private static string Ordinal(int n) =>
        n % 100 is 11 or 12 or 13 ? $"{n}th"
        : (n % 10) switch { 1 => $"{n}st", 2 => $"{n}nd", 3 => $"{n}rd", _ => $"{n}th" };
}
