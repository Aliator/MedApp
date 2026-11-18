using FluentAssertions;
using MedApp.Application.Calendar.StaffCalendars.Queries.GetCalendarStaff;

namespace MedApp.UnitTests.Application.Calendar.StaffCalendars.Queries.GetCalendarStaff;

[TestFixture]
public sealed class GetCalendarStaffValidatorTests
{
    private readonly GetCalendarStaffValidator _validator = new();

    [TestCase(null)]
    [TestCase("WorkPathways")]
    [TestCase("OverrideStages")]
    public void Validate_NoneOrAPermissionsName_IsValid(string? permission) =>
        _validator.Validate(new GetCalendarStaffQuery(permission)).IsValid.Should().BeTrue();

    [TestCase("Nope")]
    [TestCase("workpathways")]
    [TestCase("1")]
    [TestCase("")]
    public void Validate_NotAPermissionsName_IsInvalidOnPermission(string permission)
    {
        var result = _validator.Validate(new GetCalendarStaffQuery(permission));

        result.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            PropertyName = "Permission",
            ErrorMessage = GetCalendarStaffValidator.UnknownPermission(permission),
        });
        GetCalendarStaffValidator.UnknownPermission("Nope").Should().Be("'Nope' isn't a permission.");
    }
}
