using FluentValidation;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Lab.Common;
using MedApp.Application.Lab.Repositories;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Application.Results.Common;
using MedApp.Application.Specimens.Commands.ReportSpecimen;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Contracts.Lab.Responses;
using MedApp.Domain.Lab;
using MedApp.Domain.Results;
using MedApp.Domain.Specimens;
using MediatR;

namespace MedApp.Application.Lab.Cases.Commands;

/// <summary>Opening a lab case by hand for a received sample with none: the lab chooses the test (never chosen for it).</summary>
public sealed record OpenLabCaseCommand(Guid SpecimenId, Guid LabTestId, int ExpectedVersion, PathwayActor Actor)
    : IRequest<LabCaseResponse>;

/// <summary>The results entered and the report authorised, in one action (AuthoriseLabReports).</summary>
public sealed record AuthoriseLabReportCommand(Guid LabCaseId, string? Results, int ExpectedVersion, PathwayActor Actor)
    : IRequest<LabCaseResponse>;

/// <summary>The case marked entered in error, freeing its samples.</summary>
public sealed record MarkLabCaseEnteredInErrorCommand(Guid LabCaseId, string? Detail, int ExpectedVersion, PathwayActor Actor)
    : IRequest<LabCaseResponse>;

public sealed class OpenLabCaseValidator : AbstractValidator<OpenLabCaseCommand>
{
    public OpenLabCaseValidator()
    {
        RuleFor(x => x.SpecimenId).NotEmpty().WithMessage("Choose the sample.");
        RuleFor(x => x.LabTestId).NotEmpty().WithMessage(OpenLabCaseHandler.TestMessage);
    }
}

public sealed class AuthoriseLabReportValidator : AbstractValidator<AuthoriseLabReportCommand>
{
    public AuthoriseLabReportValidator()
    {
        RuleFor(x => x.Results).Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage(LabCaseRules.ResultsMissingMessage)
            .Must(r => r is null || r.Trim().Length <= LabCase.MaxReportTextLength).WithMessage(LabCaseRules.ResultsTooLongMessage);
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage(LabCaseValidation.VersionMessage);
    }
}

public sealed class MarkLabCaseEnteredInErrorValidator : AbstractValidator<MarkLabCaseEnteredInErrorCommand>
{
    public MarkLabCaseEnteredInErrorValidator()
    {
        RuleFor(x => x.Detail).Must(d => !string.IsNullOrWhiteSpace(d)).WithMessage(LabCaseRules.WhatWasWrongMessage)
            .Must(d => d is null || d.Trim().Length <= LabCase.MaxDetailLength)
            .WithMessage($"The detail must be at most {LabCase.MaxDetailLength} characters.");
        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage(LabCaseValidation.VersionMessage);
    }
}

/// <summary>
/// Opens a lab case by hand (the lab team): 404 for an unknown sample; 409 for an anonymised record, a sample changed
/// since it was shown, one already in a case, or one not received; 400 for a test that isn't in the catalogue or is
/// archived. A lab stage's case waiting for its sample takes it, with the test chosen (MC16), and picks the stage up;
/// else a new case opens, numbered from the one sequence, with the stage's other received samples that have none.
/// </summary>
public sealed class OpenLabCaseHandler(
    LabCaseAccess cases,
    ILabCaseRepository repository,
    ISpecimenRepository specimens,
    IPatientPathwayRepository pathways,
    ITeamBoundary boundary,
    LabReceipt receipt,
    TimeProvider timeProvider)
    : IRequestHandler<OpenLabCaseCommand, LabCaseResponse>
{
    public const string TestMessage = "Choose the test from the lab’s catalogue.";
    public const string AnonymisedMessage = "This patient's record has been anonymised, so a case can't be opened.";
    public const string SampleChangedMessage = "Someone else changed this sample while you had it open. Reload it, then try again.";
    public const string NotReceivedMessage = "Only a sample the lab has received can open a lab case.";

    public static string AlreadyInCase(string? number) => $"This sample is already in lab case {number ?? "another"}.";

    public static string StageHasOpenCase(string number) => $"This sample's lab stage already has an open case ({number}).";

    public async Task<LabCaseResponse> Handle(OpenLabCaseCommand request, CancellationToken cancellationToken)
    {
        var labTeamId = await cases.LabTeamIdAsync(cancellationToken);
        LabCaseAccess.EnsureWorksCases(request.Actor, labTeamId);

        var sample = await specimens.GetForUpdateOrThrowAsync(request.SpecimenId, cancellationToken);
        await boundary.EnsureSpecimenVisibleAsync(
            sample, request.Actor.Teams, labTeamId, SpecimenRepositoryExtensions.NotFound(sample.Id).Message, cancellationToken);
        if (PatientRules.IsAnonymised(sample.Patient))
            throw new ConflictException(AnonymisedMessage);
        if (sample.Version != request.ExpectedVersion)
            throw new ConflictException(SampleChangedMessage);
        if (sample.LabCaseId is not null)
            throw new ConflictException(AlreadyInCase(sample.LabCase?.Number));
        if (sample.Status != SpecimenStatus.Received)
            throw new ConflictException(NotReceivedMessage);

        var test = await repository.GetTestAsync(request.LabTestId, cancellationToken);
        if (test is null || test.IsArchived)
            throw LabCaseValidation.Invalid(nameof(request.LabTestId), TestMessage);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var pathway = sample.PathwayId is { } pathwayId ? await pathways.GetForLabWorkOrThrowAsync(pathwayId, cancellationToken) : null;
        var fromSameStage = sample.StageId is { } stageId
            ? await specimens.GetReceivedWithoutCaseAsync(stageId, sample.Id, cancellationToken)
            : [];
        List<Specimen> joining = [sample, .. fromSameStage];

        // A lab stage's case waiting for its sample takes it, with the test chosen (MC16); else a new case opens.
        var labStage = sample.Source == SpecimenSource.Clinic && pathway is not null
            ? LabStages.LabStageAfter(pathway, sample.StageId, labTeamId)
            : null;
        var waiting = labStage is null
            ? null
            : (await repository.GetOpenForStagesAsync([labStage.Id], cancellationToken)).FirstOrDefault();

        LabCaseChange change;
        PathwayChange? pickedUp = null;
        if (waiting is not null)
        {
            if (LabCaseRules.IsReceived(waiting))
                throw new ConflictException(StageHasOpenCase(waiting.Number));

            change = new LabCaseChange(waiting, pathway, request.Actor, now);
            change.ChooseTest(test);
            pickedUp = LabReceipt.JoinStageCase(change, joining, pathway, request.Actor, now);
            repository.Track(change, isNew: false);
        }
        else
        {
            change = LabCaseChange.Open(
                await receipt.NextNumberAsync(now, cancellationToken), test.Id, test, sample.PatientId, labTeamId!.Value,
                sample.Source, sample.RequestingTeamId, sample.HospitalId, sample.HospitalRequestId, pathway,
                labStage?.Id ?? sample.StageId, joining, request.Actor, now);
            repository.Track(change, isNew: true);
            labStage?.LabCases.Add(change.Case);
        }

        // A hospital sample's request points at its new case (its first was marked entered in error, MC18).
        if (sample.HospitalRequest is { } hospitalRequest)
        {
            hospitalRequest.LabCaseId = change.Case.Id;
            hospitalRequest.LabCaseNumber = change.Case.Number;
            change.Case.HospitalRequest = hospitalRequest;
            change.Case.Hospital ??= sample.Hospital;
        }

        if (pickedUp is { HasChanges: true })
            await pathways.SaveAsync(pickedUp, cancellationToken);
        else
            await repository.SaveAsync(cancellationToken);

        return await cases.ResponseAsync(change.Case, request.Actor, labTeamId, now, cancellationToken);
    }
}

/// <summary>
/// Enters the results and authorises the report (a holder of AuthoriseLabReports in the lab team): 409 unless every step
/// is done. The case's received samples are reported; a clinic case's report becomes a result for the requesting
/// clinician to sign off (told, <c>ResultReady</c>); a hospital case's goes back to its hospital (MC20, recorded as
/// sent: <c>LabReportSent</c>), so the case awaits surgery details; and a pathway's lab stage gets the results (MC16:
/// <c>PutLabResults</c>, which starts and picks it up but never finishes it: the lab finishes it by hand). One save.
/// </summary>
public sealed class AuthoriseLabReportHandler(
    LabCaseAccess cases,
    ILabCaseRepository repository,
    IPatientPathwayRepository pathways,
    IIdentityReadService identity,
    INotificationSender notifications,
    TimeProvider timeProvider)
    : IRequestHandler<AuthoriseLabReportCommand, LabCaseResponse>
{
    public const string NotAllowedMessage = "Your roles don't allow authorising lab reports.";

    public static string StepsNotFinished(LabNext? next) => $"The case's steps aren't finished: {LabCaseRules.NotNext(next)}";

    public async Task<LabCaseResponse> Handle(AuthoriseLabReportCommand request, CancellationToken cancellationToken)
    {
        var (labCase, labTeamId) = await cases.ForWorkAsync(request.LabCaseId, request.ExpectedVersion, request.Actor, cancellationToken);
        if (!request.Actor.Has(Permission.AuthoriseLabReports))
            throw new ForbiddenException(NotAllowedMessage);
        var next = LabCaseRules.Next(labCase);
        if (next?.Kind != LabNextKind.Report)
            throw new ConflictException(StepsNotFinished(next));

        var results = request.Results!.Trim();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var pathway = labCase.PathwayId is { } pathwayId ? await pathways.GetForLabWorkOrThrowAsync(pathwayId, cancellationToken) : null;

        var samples = labCase.Specimens.OrderBy(s => s.TakenAtUtc).ThenBy(s => s.Number).ToList();
        foreach (var sample in samples.Where(s => s.Status == SpecimenStatus.Received))
        {
            var reported = new SpecimenChange(sample, pathway, request.Actor, now.AddTicks(1));
            reported.ReportInCase(labCase.Id);
            repository.Track(null, reported.AuditEntries);
        }

        // A clinic case's report becomes a result for the requesting clinician to sign off (the hospital's goes back to
        // the hospital with MC20).
        Result? result = null;
        var first = samples.FirstOrDefault();
        if (labCase.Source == SpecimenSource.Clinic && first is not null)
        {
            result = new Result
            {
                Id = Guid.NewGuid(),
                PatientId = labCase.PatientId,
                Patient = labCase.Patient,
                TeamId = first.TeamId,
                PathwayId = labCase.PathwayId,
                Pathway = pathway,
                SpecimenId = first.Id,
                Specimen = first,
                Source = ResultSource.Specimen,
                Title = ReportSpecimenHandler.ResultTitle(first),
                ReportAttachmentId = Guid.Empty,
                LabCaseId = labCase.Id,
                ReportText = results,
                ReportedAtUtc = now,
                ReportedByUserId = request.Actor.UserId,
                ResponsibleUserId = first.RequestingUserId,
            };
            var responsible = await identity.GetUsernamesByIdAsync([first.RequestingUserId], cancellationToken);
            var resultChange = new ResultChange(result, pathway, request.Actor, now.AddTicks(2));
            resultChange.Record(responsible.GetValueOrDefault(first.RequestingUserId, PathwayMapper.DeletedUser));
            repository.Track(result, resultChange.AuditEntries);
        }

        var change = new LabCaseChange(labCase, pathway, request.Actor, now.AddTicks(3));
        change.Authorise(results, result?.Id);
        // A hospital case's report goes back to its hospital in the same change (MC20), so it awaits surgery details.
        if (labCase.Source == SpecimenSource.Hospital)
            change.SendReportToHospital(labCase.HospitalRequest?.Hospital ?? labCase.Hospital, labCase.HospitalRequest);
        repository.Track(change, isNew: false);

        // A pathway's lab stage gets the results, in the same save (MC16); the lab then finishes it on the pathway.
        PathwayChange? pathwayChange = null;
        if (pathway?.Stages.FirstOrDefault(s => s.Id == labCase.StageId) is { } stage && LabStages.IsLabStage(stage, labTeamId))
        {
            pathwayChange = new PathwayChange(pathway, request.Actor, now.AddTicks(4));
            pathwayChange.PutLabResults(stage, results, now);
        }

        if (pathwayChange is not null)
            await pathways.SaveAsync(pathwayChange, cancellationToken);
        else
            await repository.SaveAsync(cancellationToken);

        if (result is not null && ResultNotifications.Ready(result, request.Actor, now) is { } ready)
            await notifications.SendAsync(ready, cancellationToken);

        return await cases.ResponseAsync(labCase, request.Actor, labTeamId, now, cancellationToken);
    }
}

/// <summary>
/// Marks the case entered in error (whoever opened it, working cases, within 24 hours, or a holder of CorrectRecords):
/// 409 for an anonymised record, a stale version or a case that isn't open; 403 for anyone else. Its samples leave it,
/// so the lab can open the right case for them. Audited (<c>LabCaseEnteredInError</c>, never the detail).
/// </summary>
public sealed class MarkLabCaseEnteredInErrorHandler(LabCaseAccess cases, ILabCaseRepository repository, TimeProvider timeProvider)
    : IRequestHandler<MarkLabCaseEnteredInErrorCommand, LabCaseResponse>
{
    public async Task<LabCaseResponse> Handle(MarkLabCaseEnteredInErrorCommand request, CancellationToken cancellationToken)
    {
        var labTeamId = await cases.LabTeamIdAsync(cancellationToken);
        var (labCase, _) = await cases.FindAsync(request.LabCaseId, request.Actor, labTeamId, forUpdate: true, cancellationToken);
        LabCaseRules.EnsureCanWork(labCase, request.ExpectedVersion);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (!LabCaseRules.MayMarkEnteredInError(labCase, request.Actor, labTeamId, now))
            throw new ForbiddenException(LabCaseRules.CaseEnteredInErrorNotAllowedMessage);

        var change = new LabCaseChange(labCase, labCase.Pathway, request.Actor, now);
        change.MarkEnteredInError(request.Detail!.Trim(), labCase.Specimens);
        repository.Track(change, isNew: false);
        await repository.SaveAsync(cancellationToken);

        return await cases.ResponseAsync(labCase, request.Actor, labTeamId, now, cancellationToken);
    }
}
