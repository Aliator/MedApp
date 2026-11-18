using FluentAssertions;
using FluentValidation;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

/// <summary>Closed days' shared rules: <see cref="ClosedDaySet"/>, the working day with them, and their mapper and rules.</summary>
[TestFixture]
public sealed class ClosedDaysTests
{
    private static readonly ClosedDaySet Closed = new([CreateClosedDay()]);

    [Test]
    public void ClosedDaySet_NamesItsDates()
    {
        Closed.IsClosed(ClosedWednesday).Should().BeTrue();
        Closed.NameOn(ClosedWednesday).Should().Be(ClosedDayName);
        Closed.IsClosed(NextMonday).Should().BeFalse();
        Closed.NameOn(NextMonday).Should().BeNull();
        Closed.Dates.Should().Equal(ClosedWednesday);
    }

    [Test]
    public void ClosedDaySet_TheSameDateTwice_KeepsTheFirstName()
    {
        new ClosedDaySet([(ClosedWednesday, "First"), (ClosedWednesday, "Second")]).NameOn(ClosedWednesday).Should().Be("First");
    }

    [Test]
    public void ClosedDaySet_ASitesDay_ClosesOnlyThatSite_NeverACalendar()
    {
        var set = new ClosedDaySet([InMemoryClosedDays.On(NextMonday, "Boiler repair", SitesTestConstants.SiteId), CreateClosedDay()]);

        set.IsClosed(NextMonday).Should().BeFalse("a site's closed day isn't closed everywhere");
        set.NameOn(NextMonday).Should().BeNull();
        set.Dates.Should().Equal(ClosedWednesday);
        set.IsClosedAt(NextMonday, SitesTestConstants.SiteId).Should().BeTrue();
        set.NameAt(NextMonday, SitesTestConstants.SiteId).Should().Be("Boiler repair");
        set.IsClosedAt(NextMonday, SitesTestConstants.OtherSiteId).Should().BeFalse();
        set.NameAt(NextMonday, SitesTestConstants.OtherSiteId).Should().BeNull();
        WorkingHours.IsWorkingDay(NextMonday, set).Should().BeTrue();
    }

    [Test]
    public void ClosedDaySet_ADayClosedEverywhere_ClosesEverySite_WithItsName()
    {
        var set = new ClosedDaySet([CreateClosedDay(), InMemoryClosedDays.On(ClosedWednesday, "Boiler repair", SitesTestConstants.SiteId)]);

        set.NameAt(ClosedWednesday, SitesTestConstants.SiteId).Should().Be(ClosedDayName);
        set.IsClosedAt(ClosedWednesday, SitesTestConstants.OtherSiteId).Should().BeTrue();
    }

    [Test]
    public void ClosedDaySet_None_HasNothing()
    {
        ClosedDaySet.None.Dates.Should().BeEmpty();
        ClosedDaySet.None.IsClosed(ClosedWednesday).Should().BeFalse();
    }

    [Test]
    public void IsWorkingDay_WithClosedDays_LeavesOutWeekendsAndClosedDays()
    {
        WorkingHours.IsWorkingDay(NextMonday, Closed).Should().BeTrue();
        WorkingHours.IsWorkingDay(ClosedWednesday, Closed).Should().BeFalse();
        WorkingHours.IsWorkingDay(NextSaturday, Closed).Should().BeFalse();
        WorkingHours.IsWorkingDay(ClosedWednesday).Should().BeTrue("without the closed days, it's only Monday to Friday");
    }

    [Test]
    public void ClosedDay_SaysWhenAndWhy()
    {
        WorkingHours.ClosedDay(new DateOnly(2026, 12, 25), "Christmas Day")
            .Should().Be("The clinic is closed on Friday 25 December (Christmas Day): nothing can be booked, blocked or arranged then.");
    }

    [Test]
    public void WhyNotWorking_OnTheDefault_SaysWeekendOrClosedDayOrNothing()
    {
        string? Why(DateOnly date) => WorkingTimeSet.None.For(StaffUserId, date, Closed).WhyNotWorking();

        Why(NextSaturday).Should().Be(WorkingHours.NotWorking(DayOfWeek.Saturday));
        Why(ClosedWednesday).Should().Be(WorkingHours.ClosedDay(ClosedWednesday, ClosedDayName));
        Why(NextMonday).Should().BeNull();
    }

    [Test]
    public void WhyOutside_OnAClosedDay_SaysTheClinicIsClosed()
    {
        WorkingHours.WhyOutside(At(ClosedWednesday, 10), At(ClosedWednesday, 11), Closed)
            .Should().Be(WorkingHours.ClosedDay(ClosedWednesday, ClosedDayName));
        WorkingHours.WhyOutside(At(ClosedWednesday, 10), At(ClosedWednesday, 11), ClosedDaySet.None).Should().BeNull("no closed days were given");
    }

    [Test]
    public void WhyOutside_OnAClosedDayButOutsideTheTimes_SaysTheTimesFirst()
    {
        // The same order as before closed days: the end before the start, the day, then the working day's times.
        WorkingHours.WhyOutside(At(ClosedWednesday, 11), At(ClosedWednesday, 10), Closed).Should().Be(WorkingHours.EndNotAfterStart);
    }

    [Test]
    public async Task WorkingDays_LooksUpTheDaysClosedDays()
    {
        var repository = new Mock<IClosedDayRepository>();
        repository.Setup(r => r.GetAsync(ClosedWednesday, ClosedWednesday, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateClosedDay()]);
        var workingDays = new WorkingDays(repository.Object, new InMemoryWorkingHours());
        using var cts = new CancellationTokenSource();

        (await workingDays.WhyOutsideAsync(At(ClosedWednesday, 10), At(ClosedWednesday, 11), cts.Token))
            .Should().Be(WorkingHours.ClosedDay(ClosedWednesday, ClosedDayName));
        (await workingDays.WhyNotWorkingDayAsync(StaffUserId, ClosedWednesday, cts.Token))
            .Should().Be(WorkingHours.ClosedDay(ClosedWednesday, ClosedDayName));

        repository.Verify(r => r.GetAsync(ClosedWednesday, ClosedWednesday, cts.Token), Times.Exactly(2));
    }

    [Test]
    public async Task WorkingDays_ABadTime_NeedsNoLookup()
    {
        var repository = new Mock<IClosedDayRepository>(MockBehavior.Strict);
        var hours = new Mock<IWorkingHoursRepository>(MockBehavior.Strict);
        var workingDays = new WorkingDays(repository.Object, hours.Object);

        (await workingDays.WhyOutsideAsync(At(NextMonday, 5), At(NextMonday, 9), CancellationToken.None))
            .Should().Be(WorkingHours.OutsideBounds);
        (await workingDays.WhyOutsideAsync(StaffUserId, At(NextMonday, 10, 2), At(NextMonday, 11), CancellationToken.None))
            .Should().Be(WorkingHours.NotOnBoundary);
    }

    [Test]
    public async Task WorkingDays_ForAPerson_UsesTheirHours()
    {
        var workingDays = InMemoryClosedDays.WorkingDays(CreateClosedDay());

        (await workingDays.WhyOutsideAsync(StaffUserId, At(NextMonday, 7), At(NextMonday, 9), CancellationToken.None))
            .Should().Be(WorkingHours.OutsideWorkingDay);
        (await workingDays.WhyNotWorkingDayAsync(StaffUserId, NextSaturday, CancellationToken.None))
            .Should().Be(WorkingHours.NotWorking(DayOfWeek.Saturday));
        (await workingDays.WhyOutsideAsync(At(NextSaturday, 10), At(NextSaturday, 11), CancellationToken.None))
            .Should().BeNull("without a person, only what's nobody's counts: someone may work Saturdays");
    }

    [Test]
    public async Task WorkingDays_AnOpenWorkingDay_IsFine()
    {
        var workingDays = InMemoryClosedDays.WorkingDays(CreateClosedDay());

        (await workingDays.WhyOutsideAsync(At(NextMonday, 10), At(NextMonday, 11), CancellationToken.None)).Should().BeNull();
        (await workingDays.WhyNotWorkingDayAsync(StaffUserId, NextMonday, CancellationToken.None)).Should().BeNull();
    }

    private sealed record Timed(DateTime StartUtc, DateTime EndUtc, DateOnly Date);

    private sealed class TimedValidator : AbstractValidator<Timed>
    {
        public TimedValidator(WorkingDays workingDays)
        {
            RuleFor(x => x.StartUtc).InsideWorkingDay(x => x.EndUtc, _ => StaffUserId, workingDays);
            RuleFor(x => x.Date).WorkingDay(_ => StaffUserId, workingDays);
        }
    }

    [Test]
    public void WorkingDayRules_RefuseAClosedDayLikeAWeekend()
    {
        var validator = new TimedValidator(InMemoryClosedDays.WorkingDays(CreateClosedDay()));

        validator.ValidateNow(new Timed(At(NextMonday, 10), At(NextMonday, 11), NextMonday)).IsValid.Should().BeTrue();
        validator.ValidateNow(new Timed(At(ClosedWednesday, 10), At(ClosedWednesday, 11), ClosedWednesday))
            .Errors.Select(e => e.ErrorMessage).Should().Equal(
                WorkingHours.ClosedDay(ClosedWednesday, ClosedDayName), WorkingHours.ClosedDay(ClosedWednesday, ClosedDayName));
        validator.ValidateNow(new Timed(At(NextSaturday, 10), At(NextSaturday, 11), NextSaturday))
            .Errors.Select(e => e.ErrorMessage).Should().Equal(WorkingHours.NotWorking(DayOfWeek.Saturday), WorkingHours.NotWorking(DayOfWeek.Saturday));
    }

    [Test]
    public void ClosedDayRules_EnsureNotPast_RefusesOnlyDaysBeforeToday()
    {
        var act = () => ClosedDayRules.EnsureNotPast(Today.AddDays(-1), Today, "Nope.");

        act.Should().Throw<ConflictException>().WithMessage("Nope.");
        ClosedDayRules.EnsureNotPast(Today, Today, "Nope.");
        ClosedDayRules.EnsureNotPast(Today.AddDays(1), Today, "Nope.");
    }

    [Test]
    public void ClosedDayMapper_ToResponse_MarksPastDays()
    {
        var day = CreateClosedDay();

        var response = ClosedDayMapper.ToResponse(day, Today);

        response.Id.Should().Be(day.Id);
        response.Date.Should().Be(ClosedWednesday);
        response.Name.Should().Be(ClosedDayName);
        response.IsPast.Should().BeFalse();
        ClosedDayMapper.ToResponse(day, ClosedWednesday).IsPast.Should().BeFalse("today isn't past");
        ClosedDayMapper.ToResponse(day, ClosedWednesday.AddDays(1)).IsPast.Should().BeTrue();
    }

    [Test]
    public void ClosedDayMapper_ToResponse_NamesTheSiteOnlyForASitesDay()
    {
        var everywhere = ClosedDayMapper.ToResponse(CreateClosedDay(), Today, "Ignored");
        everywhere.SiteId.Should().BeNull();
        everywhere.SiteName.Should().BeNull();

        var atSite = ClosedDayMapper.ToResponse(InMemoryClosedDays.On(ClosedWednesday, "Boiler repair", SitesTestConstants.SiteId), Today, SitesTestConstants.SiteName);
        atSite.SiteId.Should().Be(SitesTestConstants.SiteId);
        atSite.SiteName.Should().Be(SitesTestConstants.SiteName);
    }

    [Test]
    public void ClosedDayRules_EnsureNotClosed_EverywhereStopsAll_ASiteStopsOnlyItself()
    {
        var everywhere = CreateClosedDay();
        var atSite = InMemoryClosedDays.On(ClosedWednesday, "Boiler repair", SitesTestConstants.SiteId);

        ((Action)(() => ClosedDayRules.EnsureNotClosed([everywhere], null, null))).Should().Throw<ConflictException>();
        ((Action)(() => ClosedDayRules.EnsureNotClosed([everywhere], SitesTestConstants.OtherSiteId, "Other"))).Should().Throw<ConflictException>();
        ((Action)(() => ClosedDayRules.EnsureNotClosed([atSite], SitesTestConstants.SiteId, SitesTestConstants.SiteName)))
            .Should().Throw<ConflictException>().WithMessage($"*already a closed day at {SitesTestConstants.SiteName}: Boiler repair.");
        ClosedDayRules.EnsureNotClosed([atSite], null, null);
        ClosedDayRules.EnsureNotClosed([atSite], SitesTestConstants.OtherSiteId, "Other");
        ClosedDayRules.EnsureNotClosed([], SitesTestConstants.SiteId, SitesTestConstants.SiteName);
    }

    [Test]
    public void ClosedDayMapper_ToSkipped_CarriesTheReason()
    {
        var skipped = ClosedDayMapper.ToSkipped(new BankHoliday(ClosedWednesday, "Christmas Day"), "Past");

        skipped.Date.Should().Be(ClosedWednesday);
        skipped.Name.Should().Be("Christmas Day");
        skipped.Reason.Should().Be("Past");
    }
}
