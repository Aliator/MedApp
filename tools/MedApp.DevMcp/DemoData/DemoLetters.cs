using MediatR;
using MedApp.Application.Letters.Commands.ApproveLetter;
using MedApp.Application.Letters.Commands.CreateLetter;
using MedApp.Application.Letters.Commands.ReturnLetter;
using MedApp.Application.Letters.Commands.SendLetter;
using MedApp.Application.Letters.Commands.SubmitLetter;
using MedApp.Application.Letters.Commands.UpdateLetter;
using MedApp.Application.Letters.Commands.WithdrawLetter;
using MedApp.Application.Letters.Commands.WriteLetterCorrection;
using MedApp.Application.Letters.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Letters.Responses;
using MedApp.Domain.Letters;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The centre's letters, through the app's own letter commands at the moments the story planned them
/// (<see cref="LetterPlan"/>): each discharge summary a discharge letter stage needed, written by the lead from the
/// template (its body the referral's planned outcome, in the clinician's words), approved by them or, for a nurse,
/// submitted to the clinical lead (one returned with a comment first), its PDF made, and recorded as posted to the GP
/// practice and the patient by the booking coordinator. One is withdrawn after it was sent and a correction sent. A
/// few more show the other states now: a draft, one awaiting approval, one approved and still to send, and a request
/// for information drafted on a referral awaiting triage.
/// </summary>
public static class DemoLetters
{
    public static async Task<(object Counts, IReadOnlyDictionary<string, Guid> Records)> CreateAsync(SeedServices services, DemoWorld world, CancellationToken ct)
    {
        var now = world.Timeline.NowUtc;
        var random = world.Random;

        Task<LetterResponse> Send(DateTime at, StaffMember by, IRequest<LetterResponse> request)
        {
            services.At(at, by.ActivityActor);
            return services.Run(sp => Guards.Validated(() => sp.GetRequiredService<IMediator>().Send(request, ct)));
        }

        Guid Template(string specialty, LetterKind kind) => world.Setup.LetterTemplates[(specialty, kind)];

        async Task<LetterResponse> Write(StaffMember author, DateTime at, Guid templateId, Guid? pathwayId, Guid? referralId, string? body)
        {
            var letter = await Send(at, author, new CreateLetterCommand(templateId, pathwayId, referralId, author.ActorAt(at)));
            return body is null ? letter : await Edit(letter, author, at.AddMinutes(random.Next(3, 15)), body);
        }

        Task<LetterResponse> Edit(LetterResponse letter, StaffMember author, DateTime at, string body) =>
            Send(at, author, new UpdateLetterCommand(letter.Id, letter.Letterhead, body, letter.Closing, letter.LargePrint,
                letter.Recipients.Select(r => new LetterRecipientCommandItem(r.Kind, r.PracticeId, r.Name, r.AddressLines, r.Postcode, r.Email)).ToList(),
                letter.Version, author.ActorAt(at)));

        async Task<LetterResponse> Post(LetterResponse letter, StaffMember sender, DateTime at)
        {
            foreach (var recipient in letter.Recipients.Where(r => r.DispatchStatus != nameof(LetterDispatchStatus.Sent)).ToList())
                letter = await Send(at, sender, new RecordLetterSentCommand(letter.Id, recipient.Id, nameof(LetterDispatchMethod.Post),
                    DueDates.ToUkDate(at), letter.Version, sender.ActorAt(at)));
            return letter;
        }

        var sent = 0;
        var byStatus = new Dictionary<string, int>();
        LetterResponse? correctable = null;
        StaffMember? correctableAuthor = null;
        foreach (var plan in world.LetterPlans.OrderBy(p => p.WrittenUtc))
        {
            var author = plan.Author;
            var letter = await Write(author, plan.WrittenUtc, Template(plan.Specialty, plan.Kind), plan.PathwayId, null, plan.Body);

            if (plan.Approver is { } approver && plan.SubmittedUtc is { } submittedAt)
            {
                letter = await Send(submittedAt, author, new SubmitLetterCommand(letter.Id, approver.Id, letter.Version, author.ActorAt(submittedAt)));
                if (plan.ReturnedUtc is { } returnedAt)
                {
                    letter = await Send(returnedAt, approver, new ReturnLetterCommand(letter.Id,
                        "Please add what the GP should do if the readings rise again.", letter.Version, approver.ActorAt(returnedAt)));
                    var fixedAt = returnedAt.AddMinutes(random.Next(20, 90));
                    if (fixedAt < now)
                    {
                        letter = await Edit(letter, author, fixedAt, plan.Body.Replace(
                            "This is a synthetic letter",
                            "If the readings rise again, please re-refer.\n\nThis is a synthetic letter", StringComparison.Ordinal));
                        var againAt = fixedAt.AddMinutes(random.Next(2, 10));
                        if (againAt < now)
                            letter = await Send(againAt, author, new SubmitLetterCommand(letter.Id, approver.Id, letter.Version, author.ActorAt(againAt)));
                    }
                }
            }

            // Approved by its approver once submitted, or by its author (a clinician) as their own draft.
            if (plan.ApprovedUtc is { } approvedAt && (plan.Approver is null || letter.Status == nameof(LetterStatus.AwaitingApproval)))
            {
                var by = plan.Approver ?? author;
                letter = await Send(approvedAt, by, new ApproveLetterCommand(letter.Id, letter.Version, by.ActorAt(approvedAt)));
                if (plan.SentUtc is { } sentAt)
                {
                    letter = await Post(letter, plan.Sender, sentAt);
                    sent++;
                    if (correctable is null && sentAt < now.AddDays(-6) && sentAt > now.AddDays(-30))
                        (correctable, correctableAuthor) = (letter, author);
                }
            }

            byStatus[letter.Status] = byStatus.GetValueOrDefault(letter.Status) + 1;
        }

        // Withdrawn after it was sent (a wrong date in it), and its correction written, approved and posted.
        Guid? withdrawn = null;
        if (correctable is not null && correctableAuthor is not null)
        {
            var at = DemoTimeline.InWorkingHours(correctable.LastUpdated.AddDays(2 + random.NextDouble() * 2), random);
            if (at < now.AddDays(-1))
            {
                var author = correctableAuthor;
                await Send(at, author, new WithdrawLetterCommand(correctable.Id,
                    "It gave the wrong date for the last clinic visit; a corrected letter follows.", correctable.Version, author.ActorAt(at)));
                var correction = await Send(at.AddMinutes(5), author, new WriteLetterCorrectionCommand(correctable.Id, author.ActorAt(at.AddMinutes(5))));
                correction = await Edit(correction, author, at.AddMinutes(12), correctable.Body + "\n\nThis letter corrects a date given in our earlier letter.");
                correction = await Send(at.AddMinutes(15), author, new ApproveLetterCommand(correction.Id, correction.Version, author.ActorAt(at.AddMinutes(15))));
                var postAt = DemoTimeline.InWorkingHours(at.AddHours(3), random);
                await Post(correction, world.Coordinator, postAt < now ? postAt : at.AddMinutes(40));
                withdrawn = correctable.PathwayId;
            }
        }

        var records = await ExtrasAsync(world, Write, Send);
        if (withdrawn is { } pathwayId)
            records["withdrawnAndCorrected"] = pathwayId;
        return (new
        {
            dischargeSummaries = world.LetterPlans.Count,
            dischargeSummariesByStatus = byStatus,
            posted = sent,
            withdrawnAndCorrected = withdrawn is not null,
            others = records.Keys.Where(k => k != "withdrawnAndCorrected").Order().ToList(),
        }, records);
    }

    /// <summary>
    /// Letters in the other states, on open pathways now: a draft clinic letter from the dermatologist, one from the
    /// diabetes nurse awaiting the lead's approval, an approved one still to send, and a request for information on a
    /// dermatology referral awaiting triage.
    /// </summary>
    private static async Task<Dictionary<string, Guid>> ExtrasAsync(
        DemoWorld world,
        Func<StaffMember, DateTime, Guid, Guid?, Guid?, string?, Task<LetterResponse>> write,
        Func<DateTime, StaffMember, IRequest<LetterResponse>, Task<LetterResponse>> send)
    {
        var now = world.Timeline.NowUtc;
        var at = DemoTimeline.InWorkingHours(now.AddDays(-1).AddHours(-2), world.Random);
        if (at >= now)
            at = now.AddHours(-3);

        PatientPathway? Open(string lead, PathwayKind kind) => world.Pathways
            .Where(p => p.TemplateId == world.TemplateIds[kind] && PathwayWorkflow.IsOpen(p.Status) && p.Status != PatientPathwayStatus.Blocked
                        && p.Assignments.Any(a => a.UserId == world.Member(lead).Id))
            .OrderByDescending(p => p.CreatedAt).FirstOrDefault();

        string Clinic(string text) => "Re: {{patient.name}}, born {{patient.dateOfBirth}}\n{{patient.address}}\n\nDear colleague at {{practice.name}},\n\n" +
                                      text + "\n\nThis is a synthetic letter for a demonstration.";

        var made = new Dictionary<string, Guid>();
        if (Open(DemoStaff.DermatologistUsername, PathwayKind.SkinLesion) is { } skin)
        {
            var by = world.Member(DemoStaff.DermatologistUsername);
            var draft = await write(by, at, world.Setup.LetterTemplates[(DemoSpecialties.Dermatology, LetterKind.ClinicLetter)], skin.Id, null,
                Clinic("I saw your patient in clinic today about the lesion you referred. [To finish after the histology.]"));
            made["draft"] = skin.Id;
        }

        Guid? diabetesId = null;
        if (Open(DemoStaff.DiabetesNurseUsername, PathwayKind.Diabetes) is { } diabetes)
        {
            diabetesId = diabetes.Id;
            var by = world.Member(DemoStaff.DiabetesNurseUsername);
            var letter = await write(by, at.AddMinutes(30), world.Setup.LetterTemplates[(DemoSpecialties.Diabetes, LetterKind.ClinicLetter)], diabetes.Id, null,
                Clinic("I reviewed your patient in the diabetes clinic. We agreed new targets and a medicines review with our pharmacist."));
            letter = await send(at.AddMinutes(40), by, new SubmitLetterCommand(letter.Id, world.Lead.Id, letter.Version, by.ActorAt(at.AddMinutes(40))));
            made["awaitingApproval"] = diabetes.Id;
        }

        // Returned by the lead with a comment, for the nurse to change and submit again.
        if (world.Pathways.Where(p => p.TemplateId == world.TemplateIds[PathwayKind.Diabetes] && PathwayWorkflow.IsOpen(p.Status)
                                      && p.Status != PatientPathwayStatus.Blocked && p.Id != diabetesId
                                      && p.Assignments.Any(a => a.UserId == world.Member(DemoStaff.DiabetesNurseUsername).Id))
                .OrderByDescending(p => p.CreatedAt).Skip(1).FirstOrDefault() is { } another)
        {
            var by = world.Member(DemoStaff.DiabetesNurseUsername);
            var letter = await write(by, at.AddMinutes(45), world.Setup.LetterTemplates[(DemoSpecialties.Diabetes, LetterKind.ClinicLetter)], another.Id, null,
                Clinic("I reviewed your patient in the diabetes clinic. Their readings are better since the last change."));
            letter = await send(at.AddMinutes(50), by, new SubmitLetterCommand(letter.Id, world.Lead.Id, letter.Version, by.ActorAt(at.AddMinutes(50))));
            letter = await send(at.AddMinutes(120), world.Lead, new ReturnLetterCommand(letter.Id,
                "Please say which medicine changed, so the GP can update the repeat.", letter.Version, world.Lead.ActorAt(at.AddMinutes(120))));
            made["returned"] = another.Id;
        }

        if (Open(DemoStaff.SkinGpUsername, PathwayKind.InflammatorySkin) is { } inflammatory)
        {
            var by = world.Member(DemoStaff.SkinGpUsername);
            var letter = await write(by, at.AddMinutes(60), world.Setup.LetterTemplates[(DemoSpecialties.Dermatology, LetterKind.ClinicLetter)], inflammatory.Id, null,
                Clinic("I saw your patient in the skin clinic. We agreed a new treatment plan, which the patient has in writing."));
            letter = await send(at.AddMinutes(70), by, new ApproveLetterCommand(letter.Id, letter.Version, by.ActorAt(at.AddMinutes(70))));
            made["approvedToSend"] = inflammatory.Id;
        }

        var referral = world.Referrals
            .Where(r => r.Status == ReferralStatus.AwaitingTriage && r.SpecialtyId == world.Specialty(DemoSpecialties.Dermatology).Id && r.RegisteredAtUtc < at)
            .OrderBy(r => r.RegisteredAtUtc).FirstOrDefault();
        if (referral is not null)
        {
            var by = world.Member(DemoStaff.DermatologistUsername);
            var letter = await write(by, at.AddMinutes(90), world.Setup.LetterTemplates[(DemoSpecialties.Dermatology, LetterKind.InformationRequest)], null, referral.Id, null);
            made["informationRequestDraft"] = referral.Id;
        }

        return made;
    }
}
