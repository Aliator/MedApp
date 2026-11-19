using System.Text.Json;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Lab;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;

namespace MedApp.Application.Lab.Common;

/// <summary>
/// Applies one request's change to a lab case and writes its audit entries (<see cref="PatientPathwayAuditAction"/> 50
/// to 55), to save in the same <c>SaveChangesAsync</c>, so the history can't miss one; the only way to change a case
/// (MC15 of <c>docs/multi-clinic-plan.md</c>). It moves the case's <see cref="LabCase.Version"/> on once per change.
/// Handlers check first that the change is allowed (<see cref="LabCaseRules"/>); this only applies it.
/// <para>
/// Details hold the case's ID and number, the test, steps and outcomes by ID and name (admin-written) and the result's
/// ID; <b>never free text</b>: the results, why a repeat was asked for and every detail typed live only on the case.
/// A case isn't a change to its pathway, so this doesn't go through <see cref="PathwayChange"/>.
/// </para>
/// </summary>
public sealed class LabCaseChange(LabCase labCase, PatientPathway? pathway, PathwayActor actor, DateTime nowUtc)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly List<PatientPathwayAuditEntry> _auditEntries = [];
    private readonly List<LabCaseStep> _newSteps = [];
    private readonly List<LabCaseSurgeryDetails> _newSurgeryDetails = [];
    private bool _moved;

    public LabCase Case => labCase;

    public IReadOnlyList<PatientPathwayAuditEntry> AuditEntries => _auditEntries;

    /// <summary>Steps it recorded, to add explicitly (their keys are set, so EF would take them for existing rows).</summary>
    public IReadOnlyList<LabCaseStep> NewSteps => _newSteps;

    /// <summary>Surgery details it added (MC20), to add explicitly as steps are.</summary>
    public IReadOnlyList<LabCaseSurgeryDetails> NewSurgeryDetails => _newSurgeryDetails;

    /// <summary>
    /// A new case (its version is 1), numbered from the one sequence (H-73): the lab's, for these samples or (a lab
    /// stage's case waiting for its sample, MC16) none yet. With samples, its Received step is their receipt.
    /// </summary>
    public static LabCaseChange Open(
        string number,
        Guid? labTestId,
        LabTest? test,
        Guid patientId,
        Guid labTeamId,
        SpecimenSource source,
        Guid? requestingTeamId,
        Guid? hospitalId,
        Guid? hospitalRequestId,
        PatientPathway? pathway,
        Guid? stageId,
        IReadOnlyList<Specimen> samples,
        PathwayActor actor,
        DateTime nowUtc)
    {
        var labCase = new LabCase
        {
            Id = Guid.NewGuid(),
            Number = number,
            LabTestId = labTestId,
            LabTest = test,
            PatientId = patientId,
            TeamId = labTeamId,
            Source = source,
            RequestingTeamId = requestingTeamId,
            HospitalId = hospitalId,
            HospitalRequestId = hospitalRequestId,
            PathwayId = pathway?.Id,
            StageId = stageId,
            Status = LabCaseStatus.Open,
            OpenedAtUtc = nowUtc,
            OpenedByUserId = actor.UserId,
            Version = 0,
        };
        // Its navigations as read, for the response (never saved through it: they're tracked already).
        if ((pathway?.Patient ?? samples.FirstOrDefault()?.Patient) is { } patient)
            labCase.Patient = patient;
        if (samples.FirstOrDefault()?.RequestingTeam is { } clinic)
            labCase.RequestingTeam = clinic;
        else if (pathway?.Team is { } team && team.Id == requestingTeamId)
            labCase.RequestingTeam = team;
        if (pathway is not null)
            labCase.Pathway = pathway;

        var change = new LabCaseChange(labCase, pathway, actor, nowUtc);
        change.Record(PatientPathwayAuditAction.LabCaseOpened, new
        {
            labTestId,
            test = test?.Name,
            source = source.ToString()
        });
        if (samples.Count > 0)
            change.Join(samples);
        return change;
    }

    /// <summary>
    /// Samples join the case (MC16: a lab stage's case waiting for them, or the stage's other samples): each takes its
    /// test if it had none, and the first received is its Received step (once).
    /// </summary>
    public void Join(IReadOnlyList<Specimen> samples)
    {
        if (samples.Count == 0)
            return;

        foreach (var sample in samples)
        {
            sample.LabCaseId = labCase.Id;
            sample.LabCase = labCase;
            sample.LabTestId ??= labCase.LabTestId;
            if (!labCase.Specimens.Contains(sample))
                labCase.Specimens.Add(sample);
        }

        if (!LabCaseRules.IsReceived(labCase))
        {
            var first = samples[0];
            labCase.ReceivedAtUtc = first.ReceivedAtUtc ?? nowUtc;
            AddStep(new LabCaseStep
            {
                Id = Guid.NewGuid(),
                LabCaseId = labCase.Id,
                Sequence = NextSequence(),
                Kind = LabCaseStepKind.Received,
                AtUtc = labCase.ReceivedAtUtc.Value,
                ByUserId = first.ReceivedByUserId ?? actor.UserId,
            });
        }

        Moved();
    }

    /// <summary>The lab chose the case's test (a lab stage's case that waited without one).</summary>
    public void ChooseTest(LabTest test)
    {
        labCase.LabTestId = test.Id;
        labCase.LabTest = test;
        Moved();
    }

    /// <summary>Records a step (Accessioned, or one of the test's), with where it went for a second opinion.</summary>
    public LabCaseStep RecordStep(LabTestStep? testStep, string? detail, DateTime atUtc)
    {
        var step = new LabCaseStep
        {
            Id = Guid.NewGuid(),
            LabCaseId = labCase.Id,
            Sequence = NextSequence(),
            Kind = testStep is null ? LabCaseStepKind.Accessioned : LabCaseStepKind.TestStep,
            TestStepId = testStep?.Id,
            Detail = detail,
            AtUtc = atUtc,
            ByUserId = actor.UserId,
        };
        AddStep(step);
        Record(PatientPathwayAuditAction.LabStepRecorded, new
        {
            stepId = step.Id,
            step = LabCaseRules.StepName(labCase, step),
            choice = (string?)null,
            outcome = (string?)null
        });
        return step;
    }

    /// <summary>Records a choice's outcome, now, as the lab member chose it; an insufficient sample closes the case for a repeat.</summary>
    public LabCaseStep RecordOutcome(LabChoice choice, LabOutcome outcome, string? detail)
    {
        var step = new LabCaseStep
        {
            Id = Guid.NewGuid(),
            LabCaseId = labCase.Id,
            Sequence = NextSequence(),
            Kind = LabCaseStepKind.Outcome,
            Choice = choice,
            Outcome = outcome,
            Detail = detail,
            AtUtc = nowUtc,
            ByUserId = actor.UserId,
        };
        AddStep(step);
        Record(PatientPathwayAuditAction.LabStepRecorded, new
        {
            stepId = step.Id,
            step = LabCaseRules.StepName(labCase, step),
            choice = choice.ToString(),
            outcome = outcome.ToString()
        });

        if (outcome == LabOutcome.Insufficient)
        {
            labCase.Status = LabCaseStatus.RepeatRequested;
            labCase.RepeatDetail = detail;
            labCase.RepeatRequestedAtUtc = nowUtc;
            labCase.RepeatRequestedByUserId = actor.UserId;
            Record(PatientPathwayAuditAction.RepeatSampleRequested, new { });
        }

        return step;
    }

    /// <summary>The results entered and the report authorised: the case is reported, with the result it made (a clinic case's).</summary>
    public void Authorise(string results, Guid? resultId)
    {
        labCase.Status = LabCaseStatus.Reported;
        labCase.ReportText = results;
        labCase.ReportedAtUtc = nowUtc;
        labCase.ReportedByUserId = actor.UserId;
        labCase.ResultId = resultId;
        Record(PatientPathwayAuditAction.LabReportAuthorised, new { resultId });
    }

    /// <summary>
    /// A hospital case's authorised report sent back to its hospital, in the same change (MC20): addressed to the
    /// request's surgeon (never chosen, H-72), by the hospital's delivery method, recorded as sent; the case then awaits
    /// the hospital's surgery details. Audited (<c>LabReportSent</c>: the hospital, the letter's kind and the method).
    /// </summary>
    public void SendReportToHospital(MedApp.Domain.Hospitals.Hospital? hospital, MedApp.Domain.Hospitals.HospitalRequest? request)
    {
        var method = hospital?.ReportDeliveryMethod ?? MedApp.Domain.Letters.LetterDispatchMethod.Post;
        labCase.ReportSentAtUtc = nowUtc;
        labCase.ReportSentByUserId = actor.UserId;
        labCase.ReportAddressedTo = request?.SurgeonName ?? LabCaseRules.UnknownSurgeon;
        labCase.ReportDeliveryMethod = method;
        labCase.Status = LabCaseStatus.AwaitingSurgeryDetails;
        Record(PatientPathwayAuditAction.LabReportSent, new
        {
            hospitalId = hospital?.Id ?? labCase.HospitalId,
            hospital = hospital?.Name,
            letterKind = LabCaseRules.LabReportLetterKind,
            recipientKind = nameof(MedApp.Domain.Letters.LetterRecipientKind.Hospital),
            method = method.ToString()
        });
    }

    /// <summary>
    /// The hospital's surgery details, as the lab typed them (MC20). Added to a case awaiting them, they close it
    /// (<c>LabCaseClosed</c>); before its report, they're kept and the case stays open. Audited
    /// (<c>SurgeryDetailsAdded</c>: their ID and whether no surgery is expected; never the text).
    /// </summary>
    public LabCaseSurgeryDetails AddSurgeryDetails(
        DateOnly? surgeryOn, string? procedure, string? outcome, bool noSurgeryExpected, string? detail)
    {
        var details = new LabCaseSurgeryDetails
        {
            Id = Guid.NewGuid(),
            LabCaseId = labCase.Id,
            SurgeryOn = surgeryOn,
            Procedure = procedure,
            Outcome = outcome,
            NoSurgeryExpected = noSurgeryExpected,
            Detail = detail,
            AddedAtUtc = nowUtc,
            AddedByUserId = actor.UserId,
        };
        labCase.SurgeryDetails.Add(details);
        _newSurgeryDetails.Add(details);
        Record(PatientPathwayAuditAction.SurgeryDetailsAdded, new { surgeryDetailsId = details.Id, noSurgeryExpected });

        if (labCase.Status == LabCaseStatus.AwaitingSurgeryDetails)
        {
            labCase.Status = LabCaseStatus.Closed;
            labCase.ClosedAtUtc = nowUtc;
            labCase.ClosedByUserId = actor.UserId;
            Record(PatientPathwayAuditAction.LabCaseClosed, new { });
        }

        return details;
    }

    /// <summary>The latest step marked entered in error: kept, struck through, with what was wrong (never in the audit).</summary>
    public void MarkStepEnteredInError(LabCaseStep step, string detail)
    {
        step.EnteredInErrorAtUtc = nowUtc;
        step.EnteredInErrorByUserId = actor.UserId;
        step.ErrorDetail = detail;
        Record(PatientPathwayAuditAction.LabStepEnteredInError, new
        {
            stepId = step.Id,
            step = LabCaseRules.StepName(labCase, step)
        });
    }

    /// <summary>The case marked entered in error: its samples leave it, so the lab can open the right case for them.</summary>
    public void MarkEnteredInError(string detail, IEnumerable<Specimen> samples)
    {
        labCase.Status = LabCaseStatus.EnteredInError;
        labCase.ErrorDetail = detail;
        labCase.EnteredInErrorAtUtc = nowUtc;
        labCase.EnteredInErrorByUserId = actor.UserId;
        foreach (var sample in samples.ToList())
        {
            sample.LabCaseId = null;
            sample.LabCase = null;
            labCase.Specimens.Remove(sample);
        }

        Record(PatientPathwayAuditAction.LabCaseEnteredInError, new { });
    }

    /// <summary>Moves its version on, once per change, for a change with no audit entry of its own (a sample joining).</summary>
    public void Moved()
    {
        if (_moved)
            return;
        labCase.Version++;
        _moved = true;
    }

    private void AddStep(LabCaseStep step)
    {
        labCase.Steps.Add(step);
        _newSteps.Add(step);
    }

    private int NextSequence() => labCase.Steps.Count == 0 ? 0 : labCase.Steps.Max(s => s.Sequence) + 1;

    private void Record(PatientPathwayAuditAction action, object values)
    {
        Moved();

        var details = new Dictionary<string, object?>
        {
            ["labCaseId"] = labCase.Id,
            ["number"] = labCase.Number,
        };
        foreach (var property in JsonSerializer.SerializeToElement(values, Json).EnumerateObject())
            details[property.Name] = property.Value;

        var stage = pathway?.Stages.FirstOrDefault(s => s.Id == labCase.StageId);
        _auditEntries.Add(new PatientPathwayAuditEntry
        {
            Id = Guid.NewGuid(),
            PatientPathwayId = pathway?.Id ?? PatientPathwayAuditEntry.NoPathway,
            PathwayTitle = pathway?.Title ?? string.Empty,
            PatientId = labCase.PatientId,
            // The clinic's, so its staff see their case's history (the lab sees it through its grant); else the lab's.
            TeamId = labCase.RequestingTeamId ?? labCase.TeamId,
            StageId = labCase.StageId,
            StageName = stage?.Name,
            Action = action,
            UserId = actor.UserId,
            Username = actor.Username,
            // One change's entries are a tick apart, so they sort in order.
            AtUtc = nowUtc.AddTicks(_auditEntries.Count),
            Details = JsonSerializer.Serialize(details, Json),
        });
    }
}
