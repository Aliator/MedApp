using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Sites.Commands.SetSiteHours;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Application.Sites.Commands.SetSiteHours;

[TestFixture]
public sealed class SetSiteHoursValidatorTests
{
    private readonly SetSiteHoursValidator _validator = new();

    private IEnumerable<string> Errors(params SiteHoursDay[] days) =>
        _validator.Validate(new SetSiteHoursCommand(SiteId, days)).Errors.Select(e => e.ErrorMessage);

    [Test]
    public void Validate_ATuesdayEveningAndASaturdayMorning_Passes()
    {
        Errors(new SiteHoursDay("Tuesday", new TimeOnly(8, 0), new TimeOnly(20, 0)), new SiteHoursDay("Saturday", new TimeOnly(9, 0), new TimeOnly(13, 0)))
            .Should().BeEmpty();
    }

    [Test]
    public void Validate_BadDaysAndTimes_FailSayingWhy()
    {
        Errors(new SiteHoursDay("Someday", new TimeOnly(8, 0), new TimeOnly(9, 0))).Should().Equal(HoursValidation.DayOfWeekMessage);
        Errors(new SiteHoursDay("Monday", new TimeOnly(8, 0), new TimeOnly(23, 30))).Should().Equal(WorkingHours.OutsideBounds);
        Errors(new SiteHoursDay("Monday", new TimeOnly(8, 0), new TimeOnly(9, 0)), new SiteHoursDay("Monday", new TimeOnly(10, 0), new TimeOnly(11, 0)))
            .Should().Equal(HoursValidation.DayRepeated);
    }

    [Test]
    public void Validate_NoSiteOrNoList_Fails()
    {
        _validator.Validate(new SetSiteHoursCommand(Guid.Empty, [])).IsValid.Should().BeFalse();
        _validator.Validate(new SetSiteHoursCommand(SiteId, null)).IsValid.Should().BeFalse();
    }
}
