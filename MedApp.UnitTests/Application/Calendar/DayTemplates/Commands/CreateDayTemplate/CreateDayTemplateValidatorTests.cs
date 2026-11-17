using FluentAssertions;
using MedApp.Application.Calendar.DayTemplates.Commands.CreateDayTemplate;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.DayTemplates.Commands.CreateDayTemplate;

/// <summary>The create command has the shared rules (see <c>DayTemplateValidationTests</c>) and needs an owner.</summary>
[TestFixture]
public sealed class CreateDayTemplateValidatorTests
{
    private readonly CreateDayTemplateValidator _validator = new();

    [Test]
    public void Validate_OwnerNameAndBlocks_Passes()
    {
        _validator.Validate(new CreateDayTemplateCommand(StaffUserId, DayTemplateName, [BlockValue(12, 13)], StaffActor))
            .IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_BlankNameAndBadBlock_FailsForBoth()
    {
        var result = _validator.Validate(new CreateDayTemplateCommand(StaffUserId, " ", [BlockValue(19, 20)], StaffActor));

        result.Errors.Should().HaveCount(2);
    }

    [Test]
    public void Validate_NoOwner_FailsSayingMemberOfStaffIsRequired()
    {
        var result = _validator.Validate(new CreateDayTemplateCommand(Guid.Empty, DayTemplateName, [BlockValue(12, 13)], StaffActor));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "OwnerUserId")
            .Which.ErrorMessage.Should().Be("Member of staff is required.");
    }
}
