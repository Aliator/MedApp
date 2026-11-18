using FluentAssertions;
using MedApp.Application.Calendar.ClosedDays.Queries.GetClosedDays;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ClosedDays.Queries.GetClosedDays;

[TestFixture]
public sealed class GetClosedDaysValidatorTests
{
    private readonly GetClosedDaysValidator _validator = new();

    [Test]
    public void Validate_NoRange_Passes()
    {
        _validator.Validate(new GetClosedDaysQuery(null, null)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_OneDay_Passes()
    {
        _validator.Validate(new GetClosedDaysQuery(Today, Today)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EitherEndOpen_Passes()
    {
        _validator.Validate(new GetClosedDaysQuery(Today, null)).IsValid.Should().BeTrue();
        _validator.Validate(new GetClosedDaysQuery(null, Today)).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_ToBeforeFrom_Fails()
    {
        _validator.Validate(new GetClosedDaysQuery(Today, Today.AddDays(-1)))
            .Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(GetClosedDaysValidator.ToBeforeFrom);
    }
}
