using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Domain.Calendar;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class CalendarSetupMapperTests
{
    [Test]
    public void Weekdays_AreMondayToFridayInOrder()
    {
        CalendarSetupMapper.Weekdays.Should().Equal(
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday);
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public void ToResponse_ActivityType_CopiesItAndCanBeDeletedOnlyWhenUnused(bool inUse, bool canBeDeleted)
    {
        var archived = CreateArchivedActivityType();

        var response = CalendarSetupMapper.ToResponse(archived, inUse);

        response.Id.Should().Be(archived.Id);
        response.Name.Should().Be(archived.Name);
        response.Colour.Should().Be(archived.Colour);
        response.IsArchived.Should().BeTrue();
        response.CanBeDeleted.Should().Be(canBeDeleted);
    }

    [Test]
    public async Task ToResponseAsync_ActivityType_LooksUpWhetherItIsInUse()
    {
        var repository = new Mock<ICalendarSetupRepository>();
        repository
            .Setup(r => r.GetActivityTypesInUseAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { Lunch.Id });
        using var cts = new CancellationTokenSource();

        var response = await CalendarSetupMapper.ToResponseAsync(Lunch, repository.Object, cts.Token);

        response.CanBeDeleted.Should().BeFalse();
        repository.Verify(r => r.GetActivityTypesInUseAsync(It.Is<IEnumerable<Guid>>(ids => ids.Single() == Lunch.Id), cts.Token), Times.Once);
    }

    [Test]
    public void ToResponse_DayTemplate_GivesTodaysBlocksByStartTimeWithTheirUsage()
    {
        var training = CreateTemplateBlock(14, 15, Training, note: TemplateNote);
        var lunch = CreateTemplateBlock(12, 13);
        var closed = CreateTemplateBlock(9, 10, validTo: Today.AddDays(-1));
        var future = CreateTemplateBlock(10, 11, validFrom: Today.AddDays(1));
        var template = BuildDayTemplate([training, closed, lunch, future], isArchived: true);

        var response = CalendarSetupMapper.ToResponse(template, new DayTemplateUsage(2, true), Today, canEdit: true);

        response.Id.Should().Be(DayTemplateId);
        response.OwnerUserId.Should().Be(StaffUserId);
        response.Name.Should().Be(DayTemplateName);
        response.IsArchived.Should().BeTrue();
        response.Blocks.Select(b => b.Id).Should().Equal(lunch.Id, training.Id);
        response.WeekTemplateCount.Should().Be(2);
        response.CanBeDeleted.Should().BeFalse();
    }

    [Test]
    public void ToResponse_DayTemplateBlocksAtTheSameTime_AreOrderedById()
    {
        var first = CreateTemplateBlock(12, 13);
        var second = CreateTemplateBlock(12, 13, Training);
        var ordered = new[] { first, second }.OrderBy(b => b.Id).Select(b => b.Id).ToList();

        var response = CalendarSetupMapper.ToResponse(BuildDayTemplate([second, first]), DayTemplateUsage.None, Today, canEdit: true);

        response.Blocks.Select(b => b.Id).Should().Equal(ordered);
    }

    [TestCase(0, false, true)]
    [TestCase(0, true, false)]
    [TestCase(3, true, false)]
    public void ToResponse_DayTemplate_CanBeDeletedOnlyWhenNeverUsed(int weekTemplates, bool hasBeenUsed, bool canBeDeleted)
    {
        var response = CalendarSetupMapper.ToResponse(BuildDayTemplate(), new DayTemplateUsage(weekTemplates, hasBeenUsed), Today, canEdit: true);

        response.WeekTemplateCount.Should().Be(weekTemplates);
        response.CanBeDeleted.Should().Be(canBeDeleted);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ToResponse_DayTemplate_CopiesItsOwnerAndWhetherItCanBeEdited(bool canEdit)
    {
        var template = BuildDayTemplate(ownerUserId: OtherStaffUserId);

        var response = CalendarSetupMapper.ToResponse(template, DayTemplateUsage.None, Today, canEdit);

        response.OwnerUserId.Should().Be(OtherStaffUserId);
        response.CanEdit.Should().Be(canEdit);
    }

    [Test]
    public void ToResponse_DayTemplateBlock_CopiesItWithItsActivityType()
    {
        var block = CreateTemplateBlock(12, 13, Lunch, startMinute: 15, endMinute: 45, note: TemplateNote);

        var response = CalendarSetupMapper.ToResponse(block);

        response.Id.Should().Be(block.Id);
        response.ActivityTypeId.Should().Be(Lunch.Id);
        response.ActivityName.Should().Be(Lunch.Name);
        response.Colour.Should().Be(Lunch.Colour);
        response.StartTime.Should().Be(new TimeOnly(12, 15));
        response.EndTime.Should().Be(new TimeOnly(13, 45));
        response.Note.Should().Be(TemplateNote);
    }

    [Test]
    public void ToResponse_DayTemplateBlockWithoutItsActivityTypeLoaded_GivesEmptyNameAndColour()
    {
        var block = CreateTemplateBlock(12, 13);
        block.ActivityType = null!;

        var response = CalendarSetupMapper.ToResponse(block);

        response.ActivityTypeId.Should().Be(Lunch.Id);
        response.ActivityName.Should().BeEmpty();
        response.Colour.Should().BeEmpty();
    }

    [Test]
    public async Task ToResponseAsync_DayTemplate_LooksUpItsUsageForToday()
    {
        var repository = new Mock<ICalendarSetupRepository>();
        repository
            .Setup(r => r.GetDayTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DayTemplateUsage> { [DayTemplateId] = new(1, true) });
        using var cts = new CancellationTokenSource();

        var response = await CalendarSetupMapper.ToResponseAsync(BuildDayTemplate(), repository.Object, Today, canEdit: true, cts.Token);

        response.WeekTemplateCount.Should().Be(1);
        response.CanBeDeleted.Should().BeFalse();
        repository.Verify(r => r.GetDayTemplateUsageAsync(It.Is<IEnumerable<Guid>>(ids => ids.Single() == DayTemplateId), Today, cts.Token), Times.Once);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ToResponseAsync_DayTemplate_PassesOnWhetherItCanBeEdited(bool canEdit)
    {
        var repository = new Mock<ICalendarSetupRepository>();
        repository
            .Setup(r => r.GetDayTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DayTemplateUsage>());

        var response = await CalendarSetupMapper.ToResponseAsync(BuildDayTemplate(), repository.Object, Today, canEdit, CancellationToken.None);

        response.OwnerUserId.Should().Be(StaffUserId);
        response.CanEdit.Should().Be(canEdit);
    }

    [Test]
    public async Task ToResponseAsync_DayTemplateNeverUsed_HasNoUsage()
    {
        var repository = new Mock<ICalendarSetupRepository>();
        repository
            .Setup(r => r.GetDayTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DayTemplateUsage>());

        var response = await CalendarSetupMapper.ToResponseAsync(BuildDayTemplate(), repository.Object, Today, canEdit: true, CancellationToken.None);

        response.WeekTemplateCount.Should().Be(0);
        response.CanBeDeleted.Should().BeTrue();
    }

    [Test]
    public void ToResponse_WeekTemplate_GivesEachWeekdaysDayTemplateValidToday()
    {
        var clinicDay = BuildDayTemplate();
        var theatreDay = BuildDayTemplate(id: OtherDayTemplateId, name: OtherDayTemplateName);
        var template = BuildWeekTemplate(
        [
            // Monday changed today: the row from today wins over the one that ended yesterday.
            CreateLoadedWeekDay(DayOfWeek.Monday, clinicDay, validTo: Today.AddDays(-1)),
            CreateLoadedWeekDay(DayOfWeek.Monday, theatreDay, validFrom: Today),
            CreateLoadedWeekDay(DayOfWeek.Wednesday, clinicDay),
            // Friday only from tomorrow, so nothing today.
            CreateLoadedWeekDay(DayOfWeek.Friday, theatreDay, validFrom: Today.AddDays(1))
        ], isArchived: true);

        var response = CalendarSetupMapper.ToResponse(template, new WeekTemplateUsage(4, true), Today, canEdit: true);

        response.Id.Should().Be(WeekTemplateId);
        response.OwnerUserId.Should().Be(StaffUserId);
        response.Name.Should().Be(WeekTemplateName);
        response.IsArchived.Should().BeTrue();
        response.Days.Select(d => d.DayOfWeek).Should().Equal("Monday", "Tuesday", "Wednesday", "Thursday", "Friday");
        response.Days.Select(d => d.DayTemplateId).Should().Equal(OtherDayTemplateId, null, DayTemplateId, null, null);
        response.Days.Select(d => d.DayTemplateName).Should().Equal(OtherDayTemplateName, null, DayTemplateName, null, null);
        response.StaffCount.Should().Be(4);
        response.CanBeDeleted.Should().BeFalse();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ToResponse_WeekTemplate_CopiesItsOwnerAndWhetherItCanBeEdited(bool canEdit)
    {
        var template = BuildWeekTemplate(ownerUserId: OtherStaffUserId);

        var response = CalendarSetupMapper.ToResponse(template, WeekTemplateUsage.None, Today, canEdit);

        response.OwnerUserId.Should().Be(OtherStaffUserId);
        response.CanEdit.Should().Be(canEdit);
    }

    [Test]
    public void ToResponse_WeekTemplateTwoRowsValidToday_GivesTheLaterOne()
    {
        var clinicDay = BuildDayTemplate();
        var theatreDay = BuildDayTemplate(id: OtherDayTemplateId, name: OtherDayTemplateName);
        var template = BuildWeekTemplate(
        [
            CreateLoadedWeekDay(DayOfWeek.Tuesday, theatreDay, validFrom: Today),
            CreateLoadedWeekDay(DayOfWeek.Tuesday, clinicDay, validFrom: LongAgo)
        ]);

        var response = CalendarSetupMapper.ToResponse(template, WeekTemplateUsage.None, Today, canEdit: true);

        response.Days[1].DayTemplateId.Should().Be(OtherDayTemplateId);
    }

    [Test]
    public void ToResponse_WeekDayWithoutItsDayTemplateLoaded_GivesNoName()
    {
        var day = CreateWeekDay(DayOfWeek.Monday);
        day.DayTemplate = null!;

        var response = CalendarSetupMapper.ToResponse(BuildWeekTemplate([day]), WeekTemplateUsage.None, Today, canEdit: true);

        response.Days[0].DayTemplateId.Should().Be(DayTemplateId);
        response.Days[0].DayTemplateName.Should().BeNull();
        response.CanBeDeleted.Should().BeTrue();
        response.StaffCount.Should().Be(0);
    }

    [Test]
    public async Task ToResponseAsync_WeekTemplate_LooksUpItsUsageForToday()
    {
        var repository = new Mock<ICalendarSetupRepository>();
        repository
            .Setup(r => r.GetWeekTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WeekTemplateUsage> { [WeekTemplateId] = new(2, true) });
        using var cts = new CancellationTokenSource();

        var response = await CalendarSetupMapper.ToResponseAsync(BuildWeekTemplate(), repository.Object, Today, canEdit: true, cts.Token);

        response.StaffCount.Should().Be(2);
        response.CanBeDeleted.Should().BeFalse();
        repository.Verify(r => r.GetWeekTemplateUsageAsync(It.Is<IEnumerable<Guid>>(ids => ids.Single() == WeekTemplateId), Today, cts.Token), Times.Once);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ToResponseAsync_WeekTemplate_PassesOnWhetherItCanBeEdited(bool canEdit)
    {
        var repository = new Mock<ICalendarSetupRepository>();
        repository
            .Setup(r => r.GetWeekTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WeekTemplateUsage>());

        var response = await CalendarSetupMapper.ToResponseAsync(BuildWeekTemplate(), repository.Object, Today, canEdit, CancellationToken.None);

        response.OwnerUserId.Should().Be(StaffUserId);
        response.CanEdit.Should().Be(canEdit);
    }

    [Test]
    public async Task ToResponseAsync_WeekTemplateNeverUsed_HasNoUsage()
    {
        var repository = new Mock<ICalendarSetupRepository>();
        repository
            .Setup(r => r.GetWeekTemplateUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WeekTemplateUsage>());

        var response = await CalendarSetupMapper.ToResponseAsync(BuildWeekTemplate(), repository.Object, Today, canEdit: true, CancellationToken.None);

        response.StaffCount.Should().Be(0);
        response.CanBeDeleted.Should().BeTrue();
    }

    [Test]
    public void Usage_None_IsUnused()
    {
        DayTemplateUsage.None.Should().Be(new DayTemplateUsage(0, false));
        WeekTemplateUsage.None.Should().Be(new WeekTemplateUsage(0, false));
    }
}
