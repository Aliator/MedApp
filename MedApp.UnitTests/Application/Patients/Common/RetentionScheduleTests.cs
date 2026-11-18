using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Pathways.PatientPathways;
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

        due.Should().Be(new RetentionDue(RetentionBasis.Adult, Conclusion, new DateOnly(2028, 3, 10)));
    }

    [Test]
    public void Due_EighteenThatDay_IsAnAdult()
    {
        RetentionSchedule.Due(new DateOnly(2002, 3, 10), Conclusion, Defaults).Class.Should().Be(RetentionBasis.Adult);
    }

    [Test]
    public void Due_Seventeen_IsUntilTheirTwentySixthBirthday()
    {
        var due = RetentionSchedule.Due(new DateOnly(2002, 3, 11), Conclusion, Defaults);

        due.Should().Be(new RetentionDue(RetentionBasis.Child, Conclusion, new DateOnly(2028, 3, 11)));
    }

    [Test]
    public void Due_Sixteen_IsUntilTheirTwentyFifthBirthday()
    {
        var due = RetentionSchedule.Due(new DateOnly(2003, 6, 1), Conclusion, Defaults);

        due.Should().Be(new RetentionDue(RetentionBasis.Child, Conclusion, new DateOnly(2028, 6, 1)));
    }

    [Test]
    public void Due_Baby_IsUntilTheirTwentyFifthBirthday()
    {
        RetentionSchedule.Due(new DateOnly(2019, 12, 1), Conclusion, Defaults).DueOn.Should().Be(new DateOnly(2044, 12, 1));
    }

    [Test]
    public void Due_LeapDayBirthday_FallsOnTheFirstOfMarchInOtherYears()
    {
        // In England a 29 February birthday falls on 1 March in other years, so the 25th is then.
        RetentionSchedule.Due(new DateOnly(2008, 2, 29), Conclusion, Defaults).DueOn.Should().Be(new DateOnly(2033, 3, 1));
    }

    [Test]
    public void Due_LeapDayBirthday_InALeapYear_StaysOnTheTwentyNinth()
    {
        // Born 29 Feb 2004, 17 at the conclusion: the 26th birthday is in 2030, an ordinary year; the 28th, in 2032, a leap one.
        RetentionSchedule.AddYears(new DateOnly(2004, 2, 29), 28).Should().Be(new DateOnly(2032, 2, 29));
    }

    [Test]
    public void AddYears_LeapDay_ToAnOrdinaryYear_IsTheFirstOfMarch_AndOtherDatesAreAsDateOnly()
    {
        RetentionSchedule.AddYears(new DateOnly(2004, 2, 29), 1).Should().Be(new DateOnly(2005, 3, 1));
        RetentionSchedule.AddYears(new DateOnly(2005, 2, 28), 1).Should().Be(new DateOnly(2006, 2, 28));
        RetentionSchedule.AddYears(new DateOnly(2010, 3, 1), 8).Should().Be(new DateOnly(2018, 3, 1));
    }

    [TestCase(2021, 2, 28, 16)]
    [TestCase(2021, 3, 1, 17)]
    [TestCase(2024, 2, 28, 19)]
    [TestCase(2024, 2, 29, 20)]
    public void AgeOn_LeapDayBirthday_TurnsOnTheFirstOfMarchInOtherYears(int year, int month, int day, int age)
    {
        RetentionSchedule.AgeOn(new DateOnly(2004, 2, 29), new DateOnly(year, month, day)).Should().Be(age);
    }

    [Test]
    public void Due_LeapDayBirthday_SeventeenOnlyFromTheFirstOfMarch()
    {
        // Born 29 Feb 2004: on 28 Feb 2021 still 16, so until the 25th birthday; from 1 March 17, so the 26th.
        RetentionSchedule.Due(new DateOnly(2004, 2, 29), new DateOnly(2021, 2, 28), Defaults).DueOn.Should().Be(new DateOnly(2029, 3, 1));
        RetentionSchedule.Due(new DateOnly(2004, 2, 29), new DateOnly(2021, 3, 1), Defaults).DueOn.Should().Be(new DateOnly(2030, 3, 1));
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
    public void Due_Candidate_UsesTheLaterOfTheRecordsAndItsPathwaysLastChange_AsAUkDate()
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
        var due = new RetentionDue(RetentionBasis.Child, new DateOnly(2010, 1, 1), new DateOnly(2018, 1, 1));

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

    [Test]
    public void Due_Candidate_CountsTheLatestAuditEntry_WhichOutlivesADeletedPathway()
    {
        // Registered at 16; an archived pathway, deleted since, ran until 17 and a half (only its audit is left).
        var dob = new DateOnly(2000, 1, 1);
        var recordChange = new DateTime(2016, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var deletedPathwayLastAudit = new DateTime(2017, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var withoutAudit = PrivacyTestConstants.CreateCandidate(dob, recordChange);
        var withAudit = withoutAudit with { LastAuditUtc = deletedPathwayLastAudit };

        RetentionSchedule.Due(withoutAudit, Defaults).DueOn.Should().Be(new DateOnly(2025, 1, 1), "16 at the record's last change: the 25th birthday");
        var due = RetentionSchedule.Due(withAudit, Defaults);
        due.ConclusionOfTreatment.Should().Be(new DateOnly(2017, 7, 1));
        due.DueOn.Should().Be(new DateOnly(2026, 1, 1), "17 at the deleted pathway's last change: the 26th birthday");
    }

    [Test]
    public void Due_Candidate_AnOlderAuditEntry_ChangesNothing()
    {
        var candidate = PrivacyTestConstants.CreateCandidate(new DateOnly(1970, 1, 1), new DateTime(2015, 1, 1, 12, 0, 0, DateTimeKind.Utc))
            with { LastAuditUtc = new DateTime(2010, 1, 1, 12, 0, 0, DateTimeKind.Utc) };

        RetentionSchedule.Due(candidate, Defaults).ConclusionOfTreatment.Should().Be(new DateOnly(2015, 1, 1));
    }

    [Test]
    public void Due_OncologyPathway_IsThirtyYearsAfterTheConclusionOfTreatment()
    {
        var due = RetentionSchedule.Due(new DateOnly(1970, 5, 1), Conclusion, Defaults, [RetentionClass.Oncology]);

        due.Should().Be(new RetentionDue(RetentionBasis.Oncology, Conclusion, new DateOnly(2050, 3, 10)));
    }

    [Test]
    public void Due_SeveralClasses_TakesTheLongest()
    {
        var due = RetentionSchedule.Due(
            new DateOnly(1970, 5, 1), Conclusion, Defaults,
            [RetentionClass.MentalHealth, RetentionClass.Standard, RetentionClass.Maternity]);

        due.Should().Be(new RetentionDue(RetentionBasis.Maternity, Conclusion, new DateOnly(2045, 3, 10)));
    }

    [Test]
    public void Due_StandardOnly_KeepsTheAdultRule()
    {
        RetentionSchedule.Due(new DateOnly(1970, 5, 1), Conclusion, Defaults, [RetentionClass.Standard])
            .Should().Be(new RetentionDue(RetentionBasis.Adult, Conclusion, new DateOnly(2028, 3, 10)));
    }

    [Test]
    public void Due_ChildRuleLongerThanTheClass_KeepsTheChildRule()
    {
        var options = new RetentionOptions { Classes = { MentalHealth = 5 } };

        var due = RetentionSchedule.Due(new DateOnly(2019, 6, 1), Conclusion, options, [RetentionClass.MentalHealth]);

        due.Should().Be(new RetentionDue(RetentionBasis.Child, Conclusion, new DateOnly(2044, 6, 1)));
    }

    [Test]
    public void Due_CandidateWithClasses_UsesThem()
    {
        var candidate = new RetentionCandidate(
            Guid.NewGuid(), "Test", "Patient", new DateOnly(1970, 5, 1),
            new DateTime(2020, 3, 10, 12, 0, 0, DateTimeKind.Utc), null, null, null,
            RetentionClasses: [RetentionClass.MentalHealth]);

        RetentionSchedule.Due(candidate, Defaults).Should()
            .Be(new RetentionDue(RetentionBasis.MentalHealth, Conclusion, new DateOnly(2040, 3, 10)));
    }

    [TestCase(30, 25, 20, true)]
    [TestCase(0, 25, 20, false)]
    [TestCase(30, 0, 20, false)]
    [TestCase(30, 25, -1, false)]
    public void IsValid_EveryClassAtLeastAYear(int oncology, int maternity, int mentalHealth, bool valid)
    {
        new RetentionOptions { Classes = { Oncology = oncology, Maternity = maternity, MentalHealth = mentalHealth } }
            .IsValid.Should().Be(valid);
    }
}
