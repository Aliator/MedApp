using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Patients.Responses;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Patients.Common;

[TestFixture]
public sealed class PatientMapperTests
{
    [Test]
    public void ToResponse_Patient_ReturnsTheirDetails()
    {
        var patient = PatientsTestConstants.CreateValidPatient();

        var response = PatientMapper.ToResponse(patient);

        response.Should().Be(new PatientResponse(patient.Id, patient.FirstName, patient.LastName, patient.DateOfBirth, patient.Email));
        response.IsRestricted.Should().BeFalse();
        response.IsDisputed.Should().BeFalse();
        response.IsAnonymised.Should().BeFalse();
    }

    [Test]
    public void ToResponse_FlaggedPatient_ReturnsTheFlagsAndReasons()
    {
        var patient = PatientsTestConstants.CreateValidPatient();
        patient.RestrictedAtUtc = PrivacyTestConstants.FlaggedAtUtc;
        patient.RestrictionReason = "Restricted reason";
        patient.DisputedAtUtc = PrivacyTestConstants.FlaggedAtUtc.AddDays(1);
        patient.DisputeReason = "Disputed reason";
        patient.AnonymisedAtUtc = PrivacyTestConstants.FlaggedAtUtc.AddDays(2);

        var response = PatientMapper.ToResponse(patient);

        response.IsRestricted.Should().BeTrue();
        response.RestrictionReason.Should().Be("Restricted reason");
        response.RestrictedAtUtc.Should().Be(patient.RestrictedAtUtc);
        response.IsDisputed.Should().BeTrue();
        response.DisputeReason.Should().Be("Disputed reason");
        response.DisputedAtUtc.Should().Be(patient.DisputedAtUtc);
        response.IsAnonymised.Should().BeTrue();
        response.AnonymisedAtUtc.Should().Be(patient.AnonymisedAtUtc);
    }

    [Test]
    public void FullName_IsFirstThenLast()
    {
        PatientMapper.FullName(PatientsTestConstants.CreateValidPatient())
            .Should().Be($"{PatientsTestConstants.ValidFirstName} {PatientsTestConstants.ValidLastName}");
    }

    [Test]
    public void ToFlags_CopiesEveryFlagAndReason()
    {
        var patient = PrivacyTestConstants.CreateRestrictedPatient();
        patient.DisputedAtUtc = PrivacyTestConstants.FlaggedAtUtc;
        patient.DisputeReason = "Disputed";

        PatientMapper.ToFlags(patient).Should().BeEquivalentTo(new
        {
            IsRestricted = true,
            RestrictionReason = PrivacyTestConstants.Reason,
            RestrictedAtUtc = (DateTime?)PrivacyTestConstants.FlaggedAtUtc,
            IsDisputed = true,
            DisputeReason = "Disputed",
            DisputedAtUtc = (DateTime?)PrivacyTestConstants.FlaggedAtUtc,
            IsAnonymised = false,
            AnonymisedAtUtc = (DateTime?)null
        });
        PatientMapper.ToFlags(PrivacyTestConstants.CreateAnonymisedPatient()).IsAnonymised.Should().BeTrue();
    }

    [Test]
    public void ToLabel_IsTheIdAndName()
    {
        var id = Guid.NewGuid();

        PatientMapper.ToLabel(id, "Freya Ahmed").Should().Be(new MedApp.Contracts.Patients.Responses.PatientLabelResponse(id, "Freya Ahmed"));
    }
}
