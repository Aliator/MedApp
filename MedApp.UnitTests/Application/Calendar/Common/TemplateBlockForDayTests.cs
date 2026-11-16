using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class TemplateBlockForDayTests
{
    [Test]
    public void Find_TemplateBlockOnTheDate_ReturnsItInUtc()
    {
        var lunch = CreateTemplateBlock(12, 13);
        var data = CreateData(NextMonday, NextFriday, templateBlocks: [lunch]);

        var found = TemplateBlockForDay.Find(data, lunch.Id, NextFriday);

        found.Source.Should().BeSameAs(lunch);
        found.Date.Should().Be(NextFriday);
        found.StartUtc.Should().Be(At(NextFriday, 12));
        found.EndUtc.Should().Be(At(NextFriday, 13));
    }

    [Test]
    public void Find_NotValidThatDay_ThrowsNotFound()
    {
        var lunch = CreateTemplateBlock(12, 13, validFrom: NextFriday);
        var data = CreateData(NextMonday, templateBlocks: [lunch]);

        var act = () => TemplateBlockForDay.Find(data, lunch.Id, NextMonday);

        act.Should().Throw<NotFoundException>().WithMessage("That template block isn't on this calendar on Monday 23 November.");
    }

    [Test]
    public void Find_AnotherDayTemplateThatDay_ThrowsNotFound()
    {
        var lunch = CreateTemplateBlock(12, 13);
        var data = CreateData(NextMonday, templateBlocks: [lunch], overrides: [CreateOverride(NextMonday, OtherDayTemplateId)]);

        var act = () => TemplateBlockForDay.Find(data, lunch.Id, NextMonday);

        act.Should().Throw<NotFoundException>();
    }

    [Test]
    public void Find_AlreadyChangedThatDay_ThrowsNotFound()
    {
        var lunch = CreateTemplateBlock(12, 13);
        var data = CreateData(NextMonday, templateBlocks: [lunch], exceptions: [CreateException(NextMonday, lunch)]);

        var act = () => TemplateBlockForDay.Find(data, lunch.Id, NextMonday);

        act.Should().Throw<NotFoundException>();
    }

    [Test]
    public void Find_Weekend_ThrowsNotFound()
    {
        var lunch = CreateTemplateBlock(12, 13);
        var data = CreateData(NextSaturday, templateBlocks: [lunch]);

        var act = () => TemplateBlockForDay.Find(data, lunch.Id, NextSaturday);

        act.Should().Throw<NotFoundException>().WithMessage("*Saturday 28 November*");
    }

    [Test]
    public void Exception_LeavesTheTemplateBlockOffThatDate()
    {
        var lunch = CreateTemplateBlock(12, 13);
        var projected = new ProjectedBlock(lunch, NextMonday, At(NextMonday, 12), At(NextMonday, 13));

        var exception = TemplateBlockForDay.Exception(StaffUserId, NextMonday, projected);

        exception.Id.Should().NotBeEmpty();
        exception.StaffUserId.Should().Be(StaffUserId);
        exception.Date.Should().Be(NextMonday);
        exception.DayTemplateBlockId.Should().Be(lunch.Id);
    }

    [Test]
    public void Exception_EachCall_HasItsOwnId()
    {
        var projected = new ProjectedBlock(CreateTemplateBlock(12, 13), NextMonday, At(NextMonday, 12), At(NextMonday, 13));

        TemplateBlockForDay.Exception(StaffUserId, NextMonday, projected).Id
            .Should().NotBe(TemplateBlockForDay.Exception(StaffUserId, NextMonday, projected).Id);
    }
}
