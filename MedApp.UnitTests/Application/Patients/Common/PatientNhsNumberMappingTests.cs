using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Pathways.Common;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.NhsTestConstants;

namespace MedApp.UnitTests.Application.Patients.Common;

/// <summary>Where the NHS number goes: the patient's response, the subject access export, anonymisation, the banner.</summary>
[TestFixture]
public sealed class PatientNhsNumberMappingTests
{
    [Test]
    public void PatientMapper_ToResponse_HasTheNumber_OrNull()
    {
        var patient = PatientsTestConstants.CreateValidPatient();

        PatientMapper.ToResponse(patient).NhsNumber.Should().BeNull();

        patient.NhsNumber = NhsNumber;
        PatientMapper.ToResponse(patient).NhsNumber.Should().Be(NhsNumber);
    }

    [Test]
    public void PatientExportMapper_ToExported_HasTheNumber()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.NhsNumber = NhsNumber;

        PatientExportMapper.ToExported(patient).NhsNumber.Should().Be(NhsNumber);
    }

    [Test]
    public void PatientAnonymiser_Apply_RemovesTheNumber()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.NhsNumber = NhsNumber;

        PatientAnonymiser.Apply(
            new PatientRecord { Patient = patient, Pathways = [], PathwayHistory = [], Activity = [], Appointments = [], AppointmentHistory = [], PatientMessages = [], Notifications = [], NoteVersions = [], NoteDeletionRequests = [], Attachments = [], Referrals = [], ReferralHistory = [], Letters = [], Specimens = [], ConsentRecords = [] },
            ActivityTestConstants.Now);

        patient.NhsNumber.Should().BeNull();
    }

    [Test]
    public void PathwayMapper_ToPatient_IsWhoTheBannerShows()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.NhsNumber = NhsNumber;
        patient.RestrictedAtUtc = ActivityTestConstants.Now;
        patient.AnonymisedAtUtc = ActivityTestConstants.Now;

        var banner = PathwayMapper.ToPatient(patient)!;

        banner.Should().BeEquivalentTo(new
        {
            patient.Id,
            patient.FirstName,
            patient.LastName,
            patient.DateOfBirth,
            NhsNumber,
            IsRestricted = true,
            IsDisputed = false,
            IsAnonymised = true
        });
        PathwayMapper.ToPatient(null).Should().BeNull();
    }

    [Test]
    public void PathwayMapper_ToResponse_CarriesThePatient()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.NhsNumber = NhsNumber;
        var pathway = PathwaysTestConstants.CreatePathway(patient: patient);

        var response = PathwayMapper.ToResponse(pathway, new Dictionary<Guid, string>(), ActivityTestConstants.Now);

        response.Patient!.NhsNumber.Should().Be(NhsNumber);
        response.Patient.Id.Should().Be(pathway.Patient.Id);
    }
}
