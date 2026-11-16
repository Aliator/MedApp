using FluentAssertions;
using MedApp.Application.Patients.Commands.KeepPatientRecord;
using MedApp.Application.Patients.Common;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.Extensions.Options;

namespace MedApp.UnitTests.Application.Patients.Commands.KeepPatientRecord;

[TestFixture]
public sealed class KeepPatientRecordValidatorTests
{
    // ActivityTestConstants.Now is 16 November 2026, 09:30 UTC (GMT), so today in the UK is 16 November 2026.
    private static readonly DateOnly Today = new(2026, 11, 16);

    private static KeepPatientRecordValidator Validator(int maxYears = 2) =>
        new(new FixedTimeProvider(ActivityTestConstants.Now), Options.Create(new RetentionOptions { MaxKeepYears = maxYears }));

    [Test]
    public void Validate_TomorrowToTheLimit_IsValid()
    {
        Validator().Validate(new KeepPatientRecordCommand(Guid.NewGuid(), Today.AddDays(1), "Why")).IsValid.Should().BeTrue();
        Validator().Validate(new KeepPatientRecordCommand(Guid.NewGuid(), Today.AddYears(2), "Why")).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_TodayOrBefore_IsInvalid()
    {
        Validator().Validate(new KeepPatientRecordCommand(Guid.NewGuid(), Today, "Why"))
            .Errors.Should().ContainSingle(e => e.ErrorMessage == "The review date must be after today.");
    }

    [Test]
    public void Validate_PastTheLimit_IsInvalid()
    {
        Validator().Validate(new KeepPatientRecordCommand(Guid.NewGuid(), Today.AddYears(2).AddDays(1), "Why"))
            .Errors.Should().ContainSingle(e => e.ErrorMessage == "The review date must be no more than 2 years ahead.");
        Validator(1).Validate(new KeepPatientRecordCommand(Guid.NewGuid(), Today.AddYears(1).AddDays(1), "Why"))
            .Errors.Should().ContainSingle(e => e.ErrorMessage == "The review date must be no more than 1 year ahead.");
    }

    [Test]
    public void Validate_Reason_IsRequiredAndLimited()
    {
        Validator().Validate(new KeepPatientRecordCommand(Guid.NewGuid(), Today.AddDays(1), ""))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Reason");
        Validator().Validate(new KeepPatientRecordCommand(Guid.NewGuid(), Today.AddDays(1), new string('a', KeepPatientRecordValidator.MaxReasonLength + 1)))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Reason");
    }
}
