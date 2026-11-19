using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Domain.Appointments;
using MedApp.Domain.Lab;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;
using MedApp.Domain.Referrals;
using MedApp.Domain.Specimens;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.TheLab;

/// <summary>Lab cases in the patient's record: exported with their free text, anonymised without it; hospital-only patients hidden.</summary>
[TestFixture]
public sealed class LabPrivacyTests : LabCaseTestBase
{
    private const string Canary = "Synthetic canary text";

    private (PatientRecord Record, LabCase Case) RecordWithACase()
    {
        var pathway = LabPathway();
        var labCase = GivenCase(pathway, null, ThroughRequiredSteps());
        labCase.Steps.Last().Detail = Canary;
        labCase.Status = LabCaseStatus.Reported;
        labCase.ReportText = Canary;
        labCase.ReportedAtUtc = Now;
        labCase.ReportedByUserId = Pathologist.UserId;
        var record = PrivacyTestConstants.CreateRecord(pathway.Patient, pathway) with { LabCases = [labCase] };
        return (record, labCase);
    }

    [Test]
    public void Export_ListsTheCaseWithItsStepsAndResults_StaffByUsername()
    {
        var (record, labCase) = RecordWithACase();

        PatientExportMapper.UserIds(record).Should().Contain([LabMember.UserId, Pathologist.UserId]);
        var exported = PatientExportMapper.ToResponse(record, Usernames, Now, "exporter").LabCases.Should().ContainSingle().Subject;

        exported.Should().BeEquivalentTo(new { labCase.Number, Status = "Reported", LabTestName = "Histology", ReportText = Canary, ReportedBy = Admin.Username });
        exported.Steps.Select(s => s.Name).Should().Equal("Received", "Accessioned", "Sample adequate?", "Grossing", "Processing");
        exported.Steps.Last().Detail.Should().Be(Canary);
    }

    [Test]
    public void Anonymiser_ReplacesTheCasesFreeText_KeepsItsNumberAndSteps()
    {
        var (record, labCase) = RecordWithACase();
        labCase.RepeatDetail = Canary;
        labCase.ErrorDetail = Canary;

        PatientAnonymiser.Apply(record, Now);

        labCase.ReportText.Should().Be(PatientAnonymiser.RemovedText);
        labCase.RepeatDetail.Should().Be(PatientAnonymiser.RemovedText);
        labCase.ErrorDetail.Should().Be(PatientAnonymiser.RemovedText);
        labCase.Steps.Last().Detail.Should().Be(PatientAnonymiser.RemovedText);
        labCase.Number.Should().Be("MA26-000500");
        labCase.Steps.Should().HaveCount(5);
    }

    [Test]
    public void Search_HidesAPatientKnownOnlyThroughAHospitalCase_UntilTheyHaveAPathway()
    {
        var hospitalOnly = PatientsTestConstants.CreateValidPatient();
        var withPathway = PatientsTestConstants.CreateValidPatient();
        var plain = PatientsTestConstants.CreateValidPatient();
        var cases = new[]
        {
            new LabCase { Id = Guid.NewGuid(), PatientId = hospitalOnly.Id, Source = SpecimenSource.Hospital },
            new LabCase { Id = Guid.NewGuid(), PatientId = withPathway.Id, Source = SpecimenSource.Hospital },
        }.AsQueryable();
        var pathways = new[] { CreatePathway(patient: withPathway) }.AsQueryable();
        var patients = new[] { hospitalOnly, withPathway, plain }.AsQueryable();

        patients.HospitalOnlyHidden(true, pathways, Array.Empty<Referral>().AsQueryable(), Array.Empty<Appointment>().AsQueryable(), cases)
            .Should().BeEquivalentTo([withPathway, plain]);
        patients.HospitalOnlyHidden(false, pathways, Array.Empty<Referral>().AsQueryable(), Array.Empty<Appointment>().AsQueryable(), cases)
            .Should().HaveCount(3);
    }
}
