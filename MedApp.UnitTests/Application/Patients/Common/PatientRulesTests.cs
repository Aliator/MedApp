using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Patients.Common;

[TestFixture]
public sealed class PatientRulesTests
{
    [Test]
    public void OrdinaryPatient_CanChange()
    {
        var patient = PatientsTestConstants.CreateValidPatient();

        PatientRules.IsRestricted(patient).Should().BeFalse();
        PatientRules.IsAnonymised(patient).Should().BeFalse();
        PatientRules.CanChange(patient).Should().BeTrue();
        PatientRules.WhyCantChange(patient).Should().BeNull();
        FluentActions.Invoking(() => PatientRules.EnsureCanChange(patient)).Should().NotThrow();
    }

    [Test]
    public void NoPatient_CanChange()
    {
        PatientRules.IsRestricted(null).Should().BeFalse();
        PatientRules.IsAnonymised(null).Should().BeFalse();
        PatientRules.CanChange(null).Should().BeTrue();
        FluentActions.Invoking(() => PatientRules.EnsureCanChange(null)).Should().NotThrow();
    }

    [Test]
    public void RestrictedPatient_CantChange()
    {
        var patient = PrivacyTestConstants.CreateRestrictedPatient();

        PatientRules.IsRestricted(patient).Should().BeTrue();
        PatientRules.CanChange(patient).Should().BeFalse();
        PatientRules.WhyCantChange(patient).Should().Be(PatientRules.RestrictedMessage);
        FluentActions.Invoking(() => PatientRules.EnsureCanChange(patient))
            .Should().Throw<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
    }

    [Test]
    public void AnonymisedPatient_CantChange_EvenIfAlsoRestricted()
    {
        var patient = PrivacyTestConstants.CreateAnonymisedPatient();
        patient.RestrictedAtUtc = PrivacyTestConstants.FlaggedAtUtc;

        PatientRules.IsAnonymised(patient).Should().BeTrue();
        PatientRules.WhyCantChange(patient).Should().Be(PatientRules.AnonymisedMessage);
        FluentActions.Invoking(() => PatientRules.EnsureCanChange(patient))
            .Should().Throw<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
    }
}
