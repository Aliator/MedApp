using FluentAssertions;
using MedApp.Application.Specialties.Commands.CreateSpecialty;
using MedApp.Application.Specialties.Commands.UpdateSpecialty;

namespace MedApp.UnitTests.Application.Specialties.Commands;

[TestFixture]
public sealed class SpecialtyValidatorsTests
{
    private static readonly string LongestName = new('a', 100);

    [TestCase("Synthetic dermatology", true)]
    [TestCase("", false)]
    [TestCase("   ", false)]
    public void Create_Name(string name, bool valid) =>
        new CreateSpecialtyValidator().Validate(new CreateSpecialtyCommand(name)).IsValid.Should().Be(valid);

    [Test]
    public void Create_NameLength_AtMostOneHundredOnceTrimmed()
    {
        var validator = new CreateSpecialtyValidator();
        validator.Validate(new CreateSpecialtyCommand($"  {LongestName}  ")).IsValid.Should().BeTrue();
        validator.Validate(new CreateSpecialtyCommand(LongestName + "a")).Errors
            .Should().ContainSingle(e => e.ErrorMessage == "Name must not exceed 100 characters.");
    }

    [TestCase(1, true)]
    [TestCase(365, true)]
    [TestCase(0, false)]
    [TestCase(366, false)]
    public void Targets_FromOneTo365Days(int days, bool valid)
    {
        new CreateSpecialtyValidator().Validate(new CreateSpecialtyCommand("Synthetic urology", days, days, days, days))
            .IsValid.Should().Be(valid);
        var update = new UpdateSpecialtyValidator().Validate(
            new UpdateSpecialtyCommand(Guid.NewGuid(), "Synthetic urology", 1, null, days, null, null));
        update.IsValid.Should().Be(valid);
        if (!valid)
            update.Errors.Single().ErrorMessage.Should().Be(MedApp.Application.Specialties.Common.SpecialtyValidation.TargetDaysMessage);
    }

    [Test]
    public void Update_Valid() =>
        new UpdateSpecialtyValidator().Validate(new UpdateSpecialtyCommand(Guid.NewGuid(), "Synthetic urology", 1))
            .IsValid.Should().BeTrue();

    [Test]
    public void Update_NoVersion_IsInvalid() =>
        new UpdateSpecialtyValidator().Validate(new UpdateSpecialtyCommand(Guid.NewGuid(), "Synthetic urology", 0))
            .Errors.Should().ContainSingle(e => e.PropertyName == nameof(UpdateSpecialtyCommand.ExpectedVersion));

    [Test]
    public void Update_NoIdOrBlankName_IsInvalid()
    {
        new UpdateSpecialtyValidator().Validate(new UpdateSpecialtyCommand(Guid.Empty, "Synthetic urology", 1)).IsValid.Should().BeFalse();
        new UpdateSpecialtyValidator().Validate(new UpdateSpecialtyCommand(Guid.NewGuid(), " ", 1)).IsValid.Should().BeFalse();
    }
}
