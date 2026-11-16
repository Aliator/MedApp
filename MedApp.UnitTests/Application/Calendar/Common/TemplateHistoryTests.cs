using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Domain.Calendar;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class TemplateHistoryTests
{
    private static readonly Guid TemplateA = Guid.Parse("0000000c-0000-0000-0000-0000000000a1");
    private static readonly Guid TemplateB = Guid.Parse("0000000c-0000-0000-0000-0000000000b1");

    /// <summary>A person's default week rows replaced by <paramref name="desired"/> today.</summary>
    private static TemplateHistoryChange<StaffWeekTemplate> Replace(IEnumerable<StaffWeekTemplate> rows, params Guid[] desired) =>
        TemplateHistory.Replace(
            rows,
            desired,
            Today,
            r => r.WeekTemplateId,
            (weekTemplateId, validFrom) => CreateStaffWeek(weekTemplateId, validFrom: validFrom));

    [Test]
    public void Replace_SameValues_KeepsEveryRow()
    {
        var row = CreateStaffWeek(TemplateA, validFrom: LongAgo);

        var change = Replace([row], TemplateA);

        change.HasChanges.Should().BeFalse();
        change.Delete.Should().BeEmpty();
        change.Close.Should().BeEmpty();
        change.Add.Should().BeEmpty();
    }

    [Test]
    public void Replace_RowFromBeforeTodayNoLongerWanted_ClosesItYesterday()
    {
        var row = CreateStaffWeek(TemplateA, validFrom: LongAgo);

        var change = Replace([row], TemplateB);

        change.Close.Should().ContainSingle().Which.Should().BeSameAs(row);
        change.ClosedValidTo.Should().Be(Today.AddDays(-1));
        change.Delete.Should().BeEmpty();
        change.Add.Should().ContainSingle().Which.Should().Match<StaffWeekTemplate>(
            r => r.WeekTemplateId == TemplateB && r.ValidFrom == Today && r.ValidTo == null);
    }

    [Test]
    public void Replace_RowAddedTodayNoLongerWanted_DeletesIt()
    {
        var row = CreateStaffWeek(TemplateA, validFrom: Today);

        var change = Replace([row], TemplateB);

        change.Delete.Should().ContainSingle().Which.Should().BeSameAs(row);
        change.Close.Should().BeEmpty();
        change.Add.Should().ContainSingle().Which.WeekTemplateId.Should().Be(TemplateB);
    }

    [Test]
    public void Replace_NothingWanted_EndsEveryCurrentRow()
    {
        var old = CreateStaffWeek(TemplateA, validFrom: LongAgo);

        var change = Replace([old]);

        change.Close.Should().ContainSingle().Which.Should().BeSameAs(old);
        change.Add.Should().BeEmpty();
        change.HasChanges.Should().BeTrue();
    }

    [Test]
    public void Replace_RowsThatEndedBeforeToday_AreLeftAlone()
    {
        var ended = CreateStaffWeek(TemplateA, validFrom: LongAgo, validTo: Today.AddDays(-10));

        var change = Replace([ended], TemplateA);

        change.Delete.Should().BeEmpty();
        change.Close.Should().BeEmpty();
        change.Add.Should().ContainSingle().Which.ValidFrom.Should().Be(Today);
        ended.ValidTo.Should().Be(Today.AddDays(-10));
    }

    [Test]
    public void Replace_DoesNotChangeTheRowsItIsGiven()
    {
        var row = CreateStaffWeek(TemplateA, validFrom: LongAgo);

        Replace([row], TemplateB);

        row.ValidTo.Should().BeNull();
    }

    [Test]
    public void Replace_EqualValues_EachMatchOneRow()
    {
        var blocks = new[] { CreateTemplateBlock(12, 13, validFrom: LongAgo) };
        var lunch = DayTemplateBlockValue.Of(blocks[0]);

        var change = TemplateHistory.Replace(
            blocks,
            [lunch, lunch],
            Today,
            DayTemplateBlockValue.Of,
            (value, validFrom) => value.ToBlock(DayTemplateId, validFrom));

        change.Close.Should().BeEmpty();
        change.Add.Should().ContainSingle().Which.Should().Match<DayTemplateBlock>(
            b => b.StartTime == new TimeOnly(12, 0) && b.ValidFrom == Today);
    }

    [Test]
    public void Replace_DayTemplateEdit_KeepsUnchangedBlocksAndReplacesChangedOnes()
    {
        var lunch = CreateTemplateBlock(12, 13, validFrom: LongAgo);
        var training = CreateTemplateBlock(9, 10, Training, validFrom: LongAgo);
        var movedTraining = DayTemplateBlockValue.Of(training) with { StartTime = new TimeOnly(15, 0), EndTime = new TimeOnly(16, 0) };

        var change = TemplateHistory.Replace(
            [lunch, training],
            [DayTemplateBlockValue.Of(lunch), movedTraining],
            Today,
            DayTemplateBlockValue.Of,
            (value, validFrom) => value.ToBlock(DayTemplateId, validFrom));

        change.Close.Should().ContainSingle().Which.Should().BeSameAs(training);
        change.Delete.Should().BeEmpty();
        change.Add.Should().ContainSingle().Which.Should().Match<DayTemplateBlock>(
            b => b.ActivityTypeId == Training.Id && b.StartTime == new TimeOnly(15, 0) && b.ValidFrom == Today);
    }

    [Test]
    public void Replace_CustomComparer_DecidesWhatIsTheSame()
    {
        var row = CreateTemplateBlock(12, 13, note: "Lunch");
        var sameButShouting = DayTemplateBlockValue.Of(row) with { Note = "LUNCH" };

        var change = TemplateHistory.Replace(
            [row],
            [sameButShouting],
            Today,
            DayTemplateBlockValue.Of,
            (value, validFrom) => value.ToBlock(DayTemplateId, validFrom),
            new NoteIgnoringCase());

        change.HasChanges.Should().BeFalse();
    }

    private sealed class NoteIgnoringCase : IEqualityComparer<DayTemplateBlockValue>
    {
        public bool Equals(DayTemplateBlockValue? x, DayTemplateBlockValue? y) =>
            x is not null && y is not null
            && x with { Note = x.NoteText.ToLowerInvariant() } == y with { Note = y.NoteText.ToLowerInvariant() };

        public int GetHashCode(DayTemplateBlockValue obj) => 0;
    }
}
