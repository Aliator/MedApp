using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.DevMcp.DemoData;
using static MedApp.UnitTests.Common.Constants.DemoDataTestConstants;

namespace MedApp.UnitTests.Tools.DemoData;

[TestFixture]
public sealed class ScheduleAllocatorTests
{
    private ScheduleAllocator _allocator = null!;
    private Random _random = null!;

    [SetUp]
    public void SetUp()
    {
        _allocator = new ScheduleAllocator();
        _random = new Random(Seed);
    }

    private static DateTime At(DateOnly date, int hour, int minute = 0) => UkTime.ToUtc(date, new TimeOnly(hour, minute));

    [Test]
    public void Find_EmptyCalendar_ReturnsATimeInsideTheWorkingDayOnTheCalendarsRows()
    {
        var slot = _allocator.Find(StaffId, At(Monday, 7), 45, _random);

        slot.Should().NotBeNull();
        WorkingHours.WhyOutside(slot!.StartUtc, slot.EndUtc, DemoClosedDays.Set).Should().BeNull();
        (UkTime.TimeOf(slot.StartUtc).Minute % ScheduleAllocator.StepMinutes).Should().Be(0);
        (slot.EndUtc - slot.StartUtc).Should().Be(TimeSpan.FromMinutes(45));
        UkTime.DateOf(slot.StartUtc).Should().Be(Monday);
    }

    [Test]
    public void Find_DayFullyBlocked_ReturnsTheNextWorkingDay()
    {
        _allocator.AddBlock(StaffId, At(Monday, 8), At(Monday, 18));

        var slot = _allocator.Find(StaffId, At(Monday, 8), 30, _random);

        UkTime.DateOf(slot!.StartUtc).Should().Be(Monday.AddDays(1));
    }

    [Test]
    public void Find_FromSaturday_SkipsTheWeekend()
    {
        var slot = _allocator.Find(StaffId, At(Saturday, 9), 30, _random);

        UkTime.DateOf(slot!.StartUtc).Should().Be(Saturday.AddDays(2));
    }

    [Test]
    public void Find_NeverOverlapsABlockOrAReservation()
    {
        _allocator.AddBlock(StaffId, At(Monday, 8), At(Monday, 12));
        _allocator.AddBlock(StaffId, At(Monday, 13), At(Monday, 18));

        for (var i = 0; i < 4; i++)
        {
            var slot = _allocator.Find(StaffId, At(Monday, 8), 15, _random)!;
            _allocator.Reserve(StaffId, slot.StartUtc, slot.EndUtc);
            UkTime.DateOf(slot.StartUtc).Should().Be(Monday, "the hour from 12:00 holds four 15-minute appointments");
        }

        var fifth = _allocator.Find(StaffId, At(Monday, 8), 15, _random)!;
        UkTime.DateOf(fifth.StartUtc).Should().Be(Monday.AddDays(1));
        _allocator.Reservations().Should().HaveCount(4);
    }

    [Test]
    public void Find_NotBeforeTheEarliestTime()
    {
        var slot = _allocator.Find(StaffId, At(Monday, 16, 50), 60, _random);

        slot!.StartUtc.Should().BeOnOrAfter(At(Monday, 16, 50));
        UkTime.DateOf(slot.StartUtc).Should().Be(Monday, "17:00 to 18:00 is still free");
    }

    [Test]
    public void Find_PreferredDayTemplate_IsTriedFirst()
    {
        _allocator.SetDayTemplate(StaffId, Monday.AddDays(3), TheatreDayId);

        var slot = _allocator.Find(StaffId, At(Monday, 8), 90, _random, id => id == TheatreDayId);

        UkTime.DateOf(slot!.StartUtc).Should().Be(Monday.AddDays(3));
    }

    [Test]
    public void Find_NoPreferredDayInReach_FallsBackToAnyDay()
    {
        var slot = _allocator.Find(StaffId, At(Monday, 8), 90, _random, id => id == TheatreDayId);

        UkTime.DateOf(slot!.StartUtc).Should().Be(Monday);
    }

    [Test]
    public void Find_DayAtItsCap_IsPassedOver()
    {
        _allocator.SetCap(StaffId, Monday, 1);
        var first = _allocator.Find(StaffId, At(Monday, 8), 30, _random)!;
        _allocator.Reserve(StaffId, first.StartUtc, first.EndUtc);

        var second = _allocator.Find(StaffId, At(Monday, 8), 30, _random);

        _allocator.AppointmentsOn(StaffId, Monday).Should().Be(1);
        UkTime.DateOf(second!.StartUtc).Should().Be(Monday.AddDays(1));
    }

    [Test]
    public void Reserve_TimeAlreadyTaken_Throws()
    {
        _allocator.Reserve(StaffId, At(Monday, 10), At(Monday, 11));

        var overlap = () => _allocator.Reserve(StaffId, At(Monday, 10, 30), At(Monday, 11, 30));

        overlap.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Reserve_AnotherPersonsCalendar_IsUnaffected()
    {
        _allocator.Reserve(StaffId, At(Monday, 10), At(Monday, 11));

        _allocator.IsFree(OtherStaffId, At(Monday, 10), At(Monday, 11)).Should().BeTrue();
    }

    [Test]
    public void IsFree_OutsideWorkingHoursOrOffTheBoundary_IsFalse()
    {
        _allocator.IsFree(StaffId, At(Monday, 7, 30), At(Monday, 8, 30)).Should().BeFalse();
        _allocator.IsFree(StaffId, At(Monday, 17, 30), At(Monday, 18, 30)).Should().BeFalse();
        _allocator.IsFree(StaffId, At(Monday, 9, 2), At(Monday, 9, 32)).Should().BeFalse();
        _allocator.IsFree(StaffId, At(Saturday, 10), At(Saturday, 11)).Should().BeFalse();
    }

    [Test]
    public void Release_FreesTheTimeAgain()
    {
        _allocator.Reserve(StaffId, At(Monday, 10), At(Monday, 11));

        _allocator.Release(StaffId, At(Monday, 10), At(Monday, 11));

        _allocator.IsFree(StaffId, At(Monday, 10), At(Monday, 11)).Should().BeTrue();
        _allocator.AppointmentsOn(StaffId, Monday).Should().Be(0);
    }

    [Test]
    public void Rollback_TakesBackReservationsSinceTheCheckpoint()
    {
        _allocator.Reserve(StaffId, At(Monday, 9), At(Monday, 10));
        var checkpoint = _allocator.Checkpoint();
        _allocator.Reserve(StaffId, At(Monday, 11), At(Monday, 12));
        _allocator.Reserve(OtherStaffId, At(Monday, 11), At(Monday, 12));

        _allocator.Rollback(checkpoint);

        _allocator.IsFree(StaffId, At(Monday, 9), At(Monday, 10)).Should().BeFalse();
        _allocator.IsFree(StaffId, At(Monday, 11), At(Monday, 12)).Should().BeTrue();
        _allocator.IsFree(OtherStaffId, At(Monday, 11), At(Monday, 12)).Should().BeTrue();
        _allocator.Reservations().Should().HaveCount(1);
    }

    [Test]
    public void Find_SameSeed_GivesTheSameTimes()
    {
        var times = new[] { 1, 2 }.Select(_ =>
        {
            var allocator = new ScheduleAllocator();
            var random = new Random(Seed);
            return Enumerable.Range(0, 10).Select(_ =>
            {
                var slot = allocator.Find(StaffId, At(Monday, 8), 30, random)!;
                allocator.Reserve(StaffId, slot.StartUtc, slot.EndUtc);
                return slot.StartUtc;
            }).ToList();
        }).ToList();

        times[0].Should().Equal(times[1]);
    }
}
