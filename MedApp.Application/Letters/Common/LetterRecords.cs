using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using MedApp.Application.Referrals.Repositories;
using MedApp.Application.Sites.Repositories;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Contacts;
using MedApp.Domain.Letters;
using MedApp.Domain.Patients;
using MedApp.Domain.Practices;
using MedApp.Domain.Referrals;
using Site = MedApp.Domain.Sites.Site;
using Microsoft.Extensions.Options;

namespace MedApp.Application.Letters.Common;

/// <summary>A recipient as an author chose it, before it's checked and filled from the record.</summary>
public sealed record LetterRecipientInput(
    LetterRecipientKind Kind,
    Guid? PracticeId,
    string? Name,
    string? AddressLines,
    string? Postcode,
    string? Email);

/// <summary>
/// What letters' handlers share: loading a letter with its patient and its pathway or referral, filling its merge
/// fields from the record (identity and administrative data only), working out its recipients' names and addresses,
/// and its page's response.
/// </summary>
public sealed class LetterRecords(
    ILetterRepository letters,
    IPatientRepository patients,
    IPatientPathwayRepository pathways,
    IReferralRepository referrals,
    IPracticeRepository practices,
    IIdentityReadService identity,
    IStaffDetailsService staff,
    ISiteRepository sites,
    ILetterTransport transport,
    IOptions<LettersOptions> options)
{
    public ILetterRepository Letters => letters;

    public bool SecureEmailAvailable => transport.IsAvailable;

    public IReadOnlyList<string> AllowedDomains => options.Value.AllowedSecureEmailDomains;

    public static NotFoundException NotFound(Guid id) => new($"Letter '{id}' was not found.");

    public const string NoUnlistedPracticeMessage =
        "This patient has no GP practice outside the directory recorded. Choose the practice from the directory.";

    /// <summary>A pathway to write a letter about, with its patient, read-only (letters never change a pathway).</summary>
    public Task<Domain.Pathways.PatientPathways.PatientPathway?> GetPathwayAsync(Guid id, CancellationToken ct) =>
        pathways.GetByIdAsync(id, ct);

    /// <summary>A referral to write a letter about, with its patient and specialty, read-only.</summary>
    public Task<Referral?> GetReferralAsync(Guid id, CancellationToken ct) => referrals.GetByIdAsync(id, ct);

    /// <summary>A letter with its patient and its pathway or referral; tracked for changes when <paramref name="forUpdate"/>. 404 if there's none.</summary>
    public async Task<LetterRecord> LoadAsync(Guid id, bool forUpdate, CancellationToken ct)
    {
        var letter = (forUpdate ? await letters.GetForUpdateAsync(id, ct) : await letters.GetByIdAsync(id, ct))
                     ?? throw NotFound(id);
        return await LoadForAsync(letter, ct);
    }

    /// <summary>The letter's patient, and its pathway or referral (the pathway's referral when it has none of its own).</summary>
    public async Task<LetterRecord> LoadForAsync(Letter letter, CancellationToken ct)
    {
        var pathway = letter.PathwayId is { } pathwayId ? await pathways.GetByIdAsync(pathwayId, ct) : null;
        var referral = letter.ReferralId is { } referralId
            ? await referrals.GetByIdAsync(referralId, ct)
            : pathway is not null ? await referrals.GetForPathwayAsync(pathway.Id, ct) : null;
        var patient = pathway?.Patient
                      ?? referral?.Patient
                      ?? await patients.GetByIdAsync(letter.PatientId, ct)
                      ?? throw new NotFoundException($"Patient '{letter.PatientId}' was not found.");
        // A referral read for a pathway comes without its patient: it's the same one.
        return new LetterRecord(letter, patient, pathway, referral);
    }

    /// <summary>
    /// The merge fields' values for this letter now (decision 99): the patient's identity and address, their registered
    /// practice (from the directory, or held on the patient when it isn't listed), the referral's date received, the last attended appointment on the pathway, the author's and (when
    /// known) approver's names and job titles, today and the main site. <b>Never a clinical value.</b>
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string?>> MergeValuesAsync(
        LetterRecord record, Guid? approverUserId, DateOnly today, CancellationToken ct)
    {
        var patient = record.Patient;
        var practice = patient.PracticeId is { } practiceId ? await practices.GetAsync(practiceId, ct) : null;
        var lastAttended = record.Letter.PathwayId is { } pathwayId ? await letters.GetLastAttendedAsync(pathwayId, ct) : null;
        var author = await staff.GetAsync(record.Letter.AuthorUserId, ct);
        var approver = approverUserId is { } approverId ? await staff.GetAsync(approverId, ct) : null;
        var site = await sites.GetSiteAsync(Site.MainSiteId, ct);

        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [LetterMergeFields.PatientName] = PatientMapper.FullName(patient),
            [LetterMergeFields.PatientDateOfBirth] = LetterMergeFields.FormatDate(patient.DateOfBirth),
            [LetterMergeFields.PatientNhsNumber] = patient.NhsNumber is { } nhs ? NhsNumbers.Format(nhs) : null,
            [LetterMergeFields.PatientAddress] = Address(patient.AddressLines, patient.Postcode),
            // The directory's practice, or the one held on the patient when it isn't listed (never both).
            [LetterMergeFields.PracticeName] = practice?.Name ?? patient.UnlistedPracticeName,
            [LetterMergeFields.PracticeAddress] = practice is not null
                ? Address(practice.AddressLines, practice.Postcode)
                : Address(patient.UnlistedPracticeAddressLines, patient.UnlistedPracticePostcode),
            [LetterMergeFields.ReferralReceivedDate] = record.Referral is { } referral ? LetterMergeFields.FormatDate(referral.ReceivedOn) : null,
            [LetterMergeFields.LastAttendedAppointmentDate] = lastAttended is { } at ? LetterMergeFields.FormatDate(UkTime.DateOf(at)) : null,
            [LetterMergeFields.AuthorName] = author?.FullName,
            [LetterMergeFields.AuthorJobTitle] = author?.JobTitle,
            [LetterMergeFields.ApproverName] = approver?.FullName,
            [LetterMergeFields.ApproverJobTitle] = approver?.JobTitle,
            [LetterMergeFields.Today] = LetterMergeFields.FormatDate(today),
            [LetterMergeFields.SiteName] = site?.Name,
        };
    }

    /// <summary>
    /// The merge fields the letter's text uses that have no value now, leaving out the approver's (unknown until it's
    /// approved, and checked then).
    /// </summary>
    public async Task<IReadOnlyList<string>> MissingMergeFieldsAsync(LetterRecord record, DateOnly today, CancellationToken ct)
    {
        var letter = record.Letter;
        var used = LetterMergeFields.Used(letter.Letterhead, letter.Body, letter.Closing)
            .Where(n => n is not LetterMergeFields.ApproverName and not LetterMergeFields.ApproverJobTitle)
            .ToList();
        if (used.Count == 0)
            return [];

        var values = await MergeValuesAsync(record, null, today, ct);
        return used.Where(n => string.IsNullOrWhiteSpace(values.GetValueOrDefault(n))).ToList();
    }

    /// <summary>
    /// The recipients an author chose, checked against the record and filled from it: a GP practice from the
    /// directory (or the patient's own practice that isn't listed), the patient at their address, the referrer from the referral, or another typed. 400 for one that
    /// can't be (a referrer without a referral; a practice that isn't in the directory is a 404).
    /// </summary>
    public async Task<List<LetterRecipient>> RecipientsAsync(
        LetterRecord record, IReadOnlyList<LetterRecipientInput> chosen, CancellationToken ct)
    {
        var practiceIds = chosen.Select(c => c.PracticeId).OfType<Guid>()
            .Concat(record.Referral?.PracticeId is { } referralPractice ? [referralPractice] : [])
            .Distinct()
            .ToList();
        var known = (await practices.GetByIdsAsync(practiceIds, ct)).ToDictionary(p => p.Id);

        var recipients = new List<LetterRecipient>();
        foreach (var (input, position) in chosen.Select((c, i) => (c, i)))
        {
            var recipient = new LetterRecipient
            {
                Id = Guid.NewGuid(),
                LetterId = record.Letter.Id,
                Position = position,
                Kind = input.Kind,
                DispatchStatus = LetterDispatchStatus.NotSent,
            };

            switch (input.Kind)
            {
                case LetterRecipientKind.GpPractice when input.PracticeId is null:
                    // The patient's own practice that isn't in the directory, filled from the patient.
                    if (!UnlistedPractices.Has(record.Patient))
                        throw Invalid("Recipients", NoUnlistedPracticeMessage);
                    break;

                case LetterRecipientKind.GpPractice:
                    var practice = known.GetValueOrDefault(input.PracticeId!.Value)
                                   ?? throw new NotFoundException($"Practice '{input.PracticeId}' was not found.");
                    recipient.PracticeId = practice.Id;
                    break;

                case LetterRecipientKind.Referrer:
                    if (record.Referral is null)
                        throw Invalid("Recipients", "This letter isn't about a referral, so it has no referrer to send to. Choose another kind of recipient.");
                    recipient.PracticeId = record.Referral.PracticeId;
                    if (record.Referral.PracticeId is null)
                    {
                        recipient.AddressLines = PostalAddress.Join(PostalAddress.Split(input.AddressLines?.Replace("\r\n", "\n")));
                        recipient.Postcode = UkPostcodes.Normalise(input.Postcode);
                    }
                    break;

                case LetterRecipientKind.Other:
                    recipient.Name = input.Name!.Trim();
                    recipient.AddressLines = PostalAddress.Join(PostalAddress.Split(input.AddressLines?.Replace("\r\n", "\n")));
                    recipient.Postcode = UkPostcodes.Normalise(input.Postcode);
                    recipient.Email = string.IsNullOrWhiteSpace(input.Email) ? null : Authentication.Emails.Common.EmailAddressRules.Normalize(input.Email);
                    break;
            }

            Refresh(recipient, record, known);
            recipients.Add(recipient);
        }

        return recipients;
    }

    /// <summary>
    /// Copies the recipients' names and addresses from the record as it is now (decision 101: done again when the
    /// letter is approved, so what was sent doesn't change when someone moves). Returns why any can't be sent to, or
    /// null: a practice gone from the directory, or a recipient without an address (but the patient, who can be
    /// handed it).
    /// </summary>
    public async Task<string?> RefreshRecipientsAsync(LetterRecord record, CancellationToken ct)
    {
        var letter = record.Letter;
        var ids = letter.Recipients.Select(r => r.PracticeId).OfType<Guid>().Distinct().ToList();
        var known = (await practices.GetByIdsAsync(ids, ct)).ToDictionary(p => p.Id);

        var problems = new List<string>();
        foreach (var recipient in letter.Recipients.OrderBy(r => r.Position))
        {
            if (recipient.PracticeId is { } id && !known.ContainsKey(id))
            {
                problems.Add($"{Describe(recipient)}: the practice is no longer in the directory");
                continue;
            }

            if (recipient is { Kind: LetterRecipientKind.GpPractice, PracticeId: null } && !UnlistedPractices.Has(record.Patient))
            {
                problems.Add($"{Describe(recipient)}: the patient's practice outside the directory is no longer recorded");
                continue;
            }

            Refresh(recipient, record, known);
            if (recipient.Kind != LetterRecipientKind.Patient && string.IsNullOrWhiteSpace(recipient.AddressLines)
                                                              && string.IsNullOrWhiteSpace(recipient.Email))
                problems.Add($"{Describe(recipient)} has no address");
        }

        return problems.Count == 0
            ? null
            : $"The letter can't be approved: {string.Join("; ", problems)}. Change its recipients, or add the address to the record.";
    }

    /// <summary>The page's response for <paramref name="viewer"/>.</summary>
    public async Task<LetterResponse> ToResponseAsync(LetterRecord record, PathwayActor viewer, DateOnly today, CancellationToken ct)
    {
        var usernames = await identity.GetUsernamesByIdAsync(LetterMapper.PeopleIn(record.Letter), ct);
        var missing = record.Letter.Status is LetterStatus.Draft or LetterStatus.AwaitingApproval
            ? await MissingMergeFieldsAsync(record, today, ct)
            : [];
        var correction = record.Letter.Status == LetterStatus.Withdrawn ? await letters.GetCorrectionIdAsync(record.Letter.Id, ct) : null;
        return LetterMapper.ToResponse(
            record, usernames, viewer, new LetterPageFacts(SecureEmailAvailable, AllowedDomains, missing, correction));
    }

    /// <summary>Fills a recipient's name, address and email from the record: the patient's, a practice's or the referrer's.</summary>
    private static void Refresh(LetterRecipient recipient, LetterRecord record, IReadOnlyDictionary<Guid, Practice> known)
    {
        switch (recipient.Kind)
        {
            case LetterRecipientKind.Patient:
                recipient.Name = PatientMapper.FullName(record.Patient);
                recipient.AddressLines = record.Patient.AddressLines;
                recipient.Postcode = record.Patient.Postcode;
                recipient.Email = null;
                break;

            case LetterRecipientKind.GpPractice when recipient.PracticeId is { } id && known.TryGetValue(id, out var practice):
                FromPractice(recipient, practice);
                break;

            case LetterRecipientKind.GpPractice when recipient.PracticeId is null && UnlistedPractices.Has(record.Patient):
                // Held on the patient, not in the directory: no secure email, so it's posted.
                recipient.Name = record.Patient.UnlistedPracticeName!;
                recipient.AddressLines = record.Patient.UnlistedPracticeAddressLines;
                recipient.Postcode = record.Patient.UnlistedPracticePostcode;
                recipient.Email = null;
                break;

            case LetterRecipientKind.Referrer when record.Referral is { } referral:
                // The directory's practice when the referral names one (its address and secure email too), or the
                // practice as typed from the referral letter; with the referring clinician, if known.
                var practiceName = referral.ReferrerPractice;
                if (recipient.PracticeId is { } referrerPractice && known.TryGetValue(referrerPractice, out var directory))
                {
                    FromPractice(recipient, directory);
                    practiceName = directory.Name;
                }
                recipient.Name = Truncate(
                    string.IsNullOrWhiteSpace(referral.ReferrerClinician) ? practiceName : $"{referral.ReferrerClinician.Trim()}, {practiceName}",
                    LetterRecipient.MaxNameLength);
                break;
        }
    }

    private static void FromPractice(LetterRecipient recipient, Practice practice)
    {
        recipient.Name = practice.Name;
        recipient.AddressLines = practice.AddressLines;
        recipient.Postcode = practice.Postcode;
        recipient.Email = practice.SecureEmail;
    }

    /// <summary>"The patient", "the GP practice (Example Medical Practice)", for messages.</summary>
    public static string Describe(LetterRecipient recipient) => recipient.Kind switch
    {
        LetterRecipientKind.Patient => "The patient",
        LetterRecipientKind.GpPractice => $"The GP practice ({recipient.Name})",
        LetterRecipientKind.Referrer => $"The referrer ({recipient.Name})",
        _ => recipient.Name,
    };

    /// <summary>A name and its address lines with the postcode, one per line; null when there's no address.</summary>
    private static string? Address(string? addressLines, string? postcode)
    {
        var lines = PostalAddress.Split(addressLines).Concat(string.IsNullOrWhiteSpace(postcode) ? [] : [postcode]).ToList();
        return lines.Count == 0 ? null : string.Join("\n", lines);
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    private static FluentValidation.ValidationException Invalid(string property, string message) =>
        new([new FluentValidation.Results.ValidationFailure(property, message)]);
}
