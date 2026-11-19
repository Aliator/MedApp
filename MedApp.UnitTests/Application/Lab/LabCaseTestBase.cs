using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Teams;
using MedApp.Application.Lab.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Lab;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Specimens;
using MedApp.UnitTests.Application.Specimens;
using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.TheLab;

/// <summary>
/// Shared set-up for the lab case tests: a clinic pathway whose second stage is the lab's, a sample taken at its first
/// stage and received, and a case at the lab stage, with the people who work it.
/// </summary>
public abstract class LabCaseTestBase : SpecimenTestBase
{
    protected static readonly HashSet<Permission> LabPermissions =
        [Permission.UseApp, Permission.ViewPatients, Permission.ProcessSpecimens];

    /// <summary>A lab member who works cases.</summary>
    protected static readonly PathwayActor LabMember = new(
        OtherUserId, OtherUsername, LabPermissions, new TeamScope(OtherUserId, [MedApp.Domain.Teams.Team.LabId], false));

    /// <summary>A pathologist: works cases and authorises reports.</summary>
    protected static readonly PathwayActor Pathologist = new(
        Admin.UserId, Admin.Username, new HashSet<Permission>([.. LabPermissions, Permission.AuthoriseLabReports]),
        new TeamScope(Admin.UserId, [MedApp.Domain.Teams.Team.LabId], false));

    /// <summary>A clinic's member of staff (the pathway's team).</summary>
    protected static readonly PathwayActor ClinicStaff = new(
        Staff.UserId, Staff.Username, new HashSet<Permission> { Permission.UseApp, Permission.ViewPatients, Permission.WorkPathways, Permission.ProcessSpecimens },
        new TeamScope(Staff.UserId, [TeamId], false));

    protected LabCaseAccess CaseAccess => new(LabCases.Object, Boundary.Object, Teams.Object, Identity.Object);

    /// <summary>A pathway whose second stage is the lab's, its first finished, the lab stage reached and handed over.</summary>
    protected static PatientPathway LabPathway()
    {
        var pathway = CreatePathway(PatientPathwayStatus.InProgress, finishedStages: 1);
        pathway.TeamId = TeamId;
        var labStage = pathway.StageAt(2);
        labStage.OwnerTeamId = MedApp.Domain.Teams.Team.LabId;
        labStage.OwnerTeam = new MedApp.Domain.Teams.Team { Id = MedApp.Domain.Teams.Team.LabId, Name = "Lab", Kind = MedApp.Domain.Teams.TeamKind.Lab };
        labStage.ReachedAtUtc = Now.AddDays(-2);
        labStage.HandedOverFromTeamId = TeamId;
        return pathway;
    }

    /// <summary>
    /// A clinic case at the pathway's lab stage, for the sample taken at the first stage (received), with the steps
    /// given (Received is always first).
    /// </summary>
    protected LabCase GivenCase(PatientPathway pathway, Specimen? sample = null, params LabCaseStep[] steps)
    {
        sample ??= CreateReceived(pathway);
        var labCase = new LabCase
        {
            Id = Guid.NewGuid(),
            Number = "MA26-000500",
            LabTestId = Histology.Id,
            LabTest = Histology,
            PatientId = pathway.PatientId,
            Patient = pathway.Patient,
            TeamId = MedApp.Domain.Teams.Team.LabId,
            Source = SpecimenSource.Clinic,
            RequestingTeamId = pathway.TeamId,
            PathwayId = pathway.Id,
            Pathway = pathway,
            StageId = pathway.StageAt(2).Id,
            Status = LabCaseStatus.Open,
            OpenedAtUtc = Now.AddHours(-2),
            OpenedByUserId = LabMember.UserId,
            ReceivedAtUtc = Now.AddDays(-1),
            Version = 2,
        };
        labCase.Steps.Add(Step(labCase, 0, LabCaseStepKind.Received, by: LabMember.UserId));
        foreach (var step in steps)
            labCase.Steps.Add(step);
        sample.LabCaseId = labCase.Id;
        sample.LabCase = labCase;
        labCase.Specimens.Add(sample);
        pathway.StageAt(2).LabCases.Add(labCase);

        LabCases.Setup(l => l.GetForUpdateAsync(labCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(labCase);
        LabCases.Setup(l => l.GetByIdAsync(labCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(labCase);
        Pathways.Setup(p => p.GetForUpdateAsync(pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(pathway);
        return labCase;
    }

    protected static Specimen CreateReceived(PatientPathway pathway)
    {
        var sample = SpecimensTestConstants.CreateSpecimen(pathway, SpecimenStatus.Received);
        sample.LabTestId = SpecimensTestConstants.Histology().Id;
        return sample;
    }

    protected static LabCaseStep Step(
        LabCase labCase, int sequence, LabCaseStepKind kind, Guid? testStepId = null, LabChoice? choice = null,
        LabOutcome? outcome = null, Guid? by = null) => new()
    {
        Id = Guid.NewGuid(),
        LabCaseId = labCase.Id,
        Sequence = sequence,
        Kind = kind,
        TestStepId = testStepId,
        Choice = choice,
        Outcome = outcome,
        AtUtc = Now.AddHours(-20 + sequence),
        ByUserId = by ?? LabMember.UserId,
    };

    /// <summary>Accessioned, "Sample adequate?" Adequate, and the test's two required steps.</summary>
    protected LabCaseStep[] ThroughRequiredSteps(LabCase? labCase = null)
    {
        var c = labCase ?? new LabCase();
        var required = Histology.Steps.Where(s => !s.IsOptional).OrderBy(s => s.Position).ToList();
        return
        [
            Step(c, 1, LabCaseStepKind.Accessioned),
            Step(c, 2, LabCaseStepKind.Outcome, choice: LabChoice.SampleAdequate, outcome: LabOutcome.Adequate),
            Step(c, 3, LabCaseStepKind.TestStep, testStepId: required[0].Id),
            Step(c, 4, LabCaseStepKind.TestStep, testStepId: required[1].Id),
        ];
    }
}
