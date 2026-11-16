using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Patients.Common;

[TestFixture]
public sealed class RetentionScheduleTests
{
    private static readonly RetentionOptions Defaults = new();
    private static readonly DateOnly Conclusion = new(2020, 3, 10);

    [Test]
    public void Due_Adult_IsEightYearsAfterTheConclusionOfTreatment()
    {
        var due = RetentionSchedule.Due(new DateOnly(1970, 5, 1), Conclusion, Defaults);

        due.Should().Be(new RetentionDue(RetentionClass.Adult, Conclusion, new DateOnly(2028, 3, 10)));
    }

    [Test]
    public void Due_EighteenThatDay_IsAnAdult()
    {
        RetentionSchedule.Due(new DateOnly(2002, 3, 10), Conclusion, Defaults).Class.Should().Be(RetentionClass.Adult);
    }

    [Test]
    public void Due_Seventeen_IsUntilTheirTwentySixthBirthday()
    {
        var due = RetentionSchedule.Due(new DateOnly(2002, 3, 11), Conclusion, Defaults);

        due.Should().Be(new RetentionDue(RetentionClass.Child, Conclusion, new DateOnly(2028, 3, 11)));
    }

    [Test]
    public void Due_Sixteen_IsUntilTheirTwentyFifthBirthday()
    {
        var due = RetentionSchedule.Due(new DateOnly(2003, 6, 1), Conclusion, Defaults);

        due.Should().Be(new RetentionDue(RetentionClass.Child, Conclusion, new DateOnly(2028, 6, 1)));
    }

    [Test]
    public void Due_Baby_IsUntilTheirTwentyFifthBirthday()
    {
        RetentionSchedule.Due(new DateOnly(2019, 12, 1), Conclusion, Defaults).DueOn.Should().Be(new DateOnly(2044, 12, 1));
    }

    [Test]
    public void Due_LeapDayBirthday_FallsOnTheTwentyEighth()
    {
        RetentionSchedule.Due(new DateOnly(2008, 2, 29), Conclusion, Defaults).DueOn.Should().Be(new DateOnly(2033, 2, 28));
    }

    [Test]
    public void Due_OtherPeriods_ComeFromTheOptions()
    {
        var options = new RetentionOptions { AdultYears = 10, AdultAge = 16, ChildUntilAge = 21, ChildAgedSeventeenUntilAge = 30 };

        RetentionSchedule.Due(new DateOnly(2000, 1, 1), Conclusion, options).DueOn.Should().Be(new DateOnly(2030, 3, 10));
        RetentionSchedule.Due(new DateOnly(2004, 6, 1), Conclusion, options).DueOn.Should().Be(new DateOnly(2034, 6, 1), "15 is one short of 16");
        RetentionSchedule.Due(new DateOnly(2010, 6, 1), Conclusion, options).DueOn.Should().Be(new DateOnly(2031, 6, 1));
    }

    [TestCase(2000, 3, 10, 20)]
    [TestCase(2000, 3, 11, 19)]
    [TestCase(2020, 3, 10, 0)]
    public void AgeOn_CountsWholeYears(int year, int month, int day, int expected)
    {
        RetentionSchedule.AgeOn(new DateOnly(year, month, day), Conclusion).Should().Be(expected);
    }

    [Test]
    public void ShortestYears_Defaults_IsEight()
    {
        RetentionSchedule.ShortestYears(Defaults).Should().Be(8);
    }

    [Test]
    public void ShortestYears_IsTheSmallestGap_AndNeverNegative()
    {
        RetentionSchedule.ShortestYears(new RetentionOptions { AdultYears = 3 }).Should().Be(3);
        RetentionSchedule.ShortestYears(new RetentionOptions { ChildUntilAge = 20 }).Should().Be(3);
        RetentionSchedule.ShortestYears(new RetentionOptions { ChildAgedSeventeenUntilAge = 20 }).Should().Be(2);
        RetentionSchedule.ShortestYears(new RetentionOptions { AdultYears = -1 }).Should().Be(0);
    }

    [Test]
    public void Due_Candidate_UsesTheLaterOfTheRecordsAndItsProceduresLastChange_AsAUkDate()
    {
        var dob = new DateOnly(1970, 1, 1);
        // 23:30 UTC on 9 June is 00:30 on 10 June in the UK (BST).
        var recordChange = new DateTime(2020, 6, 9, 23, 30, 0, DateTimeKind.Utc);

        RetentionSchedule.Due(PrivacyTestConstants.CreateCandidate(dob, recordChange), Defaults)
            .ConclusionOfTreatment.Should().Be(new DateOnly(2020, 6, 10));
        RetentionSchedule.Due(PrivacyTestConstants.CreateCandidate(dob, recordChange, recordChange.AddDays(5)), Defaults)
            .ConclusionOfTreatment.Should().Be(new DateOnly(2020, 6, 15));
        RetentionSchedule.Due(PrivacyTestConstants.CreateCandidate(dob, recordChange, recordChange.AddDays(-5)), Defaults)
            .ConclusionOfTreatment.Should().Be(new DateOnly(2020, 6, 10));
    }

    [Test]
    public void RetentionReviewMapper_ToResponse_CopiesTheCandidateAndWhenItsDue()
    {
        var candidate = PrivacyTestConstants.CreateCandidate(new DateOnly(1970, 1, 1), new DateTime(2010, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            keptUntil: new DateOnly(2027, 1, 1));
        var due = new RetentionDue(RetentionClass.Child, new DateOnly(2010, 1, 1), new DateOnly(2018, 1, 1));

        var response = RetentionReviewMapper.ToResponse(candidate, due);

        response.PatientId.Should().Be(candidate.PatientId);
        response.FirstName.Should().Be(candidate.FirstName);
        response.LastName.Should().Be(candidate.LastName);
        response.DateOfBirth.Should().Be(candidate.DateOfBirth);
        response.ConclusionOfTreatment.Should().Be(due.ConclusionOfTreatment);
        response.RetentionClass.Should().Be("Child");
        response.DueOn.Should().Be(due.DueOn);
        response.KeptUntil.Should().Be(candidate.KeptUntil);
        response.KeptReason.Should().Be(PrivacyTestConstants.Reason);
    }
}
