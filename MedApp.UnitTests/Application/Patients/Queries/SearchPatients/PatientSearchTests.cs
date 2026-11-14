using FluentAssertions;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Patients.Queries.SearchPatients;

[TestFixture]
public sealed class PatientSearchTests
{
    private static readonly Patient Ahmed =
        PatientsTestConstants.CreatePatient("Freya", "Ahmed", new DateOnly(1976, 12, 1), "freya.ahmed@example.com");
    private static readonly Patient Ali =
        PatientsTestConstants.CreatePatient("Priya", "Ali", new DateOnly(1966, 12, 29), "priya.ali@example.com");
    private static readonly Patient BennettArjun =
        PatientsTestConstants.CreatePatient("Arjun", "Bennett", new DateOnly(1957, 6, 24), "arjun.bennett@example.com");
    private static readonly Patient BennettGeorge =
        PatientsTestConstants.CreatePatient("George", "Bennett", new DateOnly(2010, 6, 23), "george.bennett@example.com");

    private static IQueryable<Patient> Patients() =>
        new[] { BennettGeorge, Ali, Ahmed, BennettArjun }.AsQueryable();

    [Test]
    public void Filter_NoFilters_ReturnsEveryPatient()
    {
        Patients().Filter(PatientsTestConstants.CreateSearchQuery()).Should().HaveCount(4);
    }

    [Test]
    public void Filter_TextFilters_MatchPartOfTheValueIgnoringCaseAndSpaces()
    {
        var query = PatientsTestConstants.CreateSearchQuery() with { LastName = " BENN ", Email = "GEORGE" };

        Patients().Filter(query).Should().ContainSingle().Which.Should().Be(BennettGeorge);
    }

    [Test]
    public void Filter_FirstName_MatchesFirstNames()
    {
        var query = PatientsTestConstants.CreateSearchQuery() with { FirstName = "riya" };

        Patients().Filter(query).Should().ContainSingle().Which.Should().Be(Ali);
    }

    [Test]
    public void Filter_BlankText_IsIgnored()
    {
        var query = PatientsTestConstants.CreateSearchQuery() with { FirstName = "   " };

        Patients().Filter(query).Should().HaveCount(4);
    }

    [Test]
    public void Filter_DateOfBirthRange_IncludesBothEnds()
    {
        var query = PatientsTestConstants.CreateSearchQuery() with
        {
            DateOfBirthFrom = new DateOnly(1966, 12, 29),
            DateOfBirthTo = new DateOnly(1976, 12, 1)
        };

        Patients().Filter(query).Should().BeEquivalentTo([Ali, Ahmed]);
    }

    [Test]
    public void Sort_Default_IsByLastThenFirstName()
    {
        Patients().Sort(null, true).Should().ContainInOrder(Ahmed, Ali, BennettArjun, BennettGeorge);
    }

    [Test]
    public void Sort_NameDescending_ReversesLastThenFirstName()
    {
        Patients().Sort("name", false).Should().ContainInOrder(BennettGeorge, BennettArjun, Ali, Ahmed);
    }

    [Test]
    public void Sort_DateOfBirth_IgnoresCaseOfTheColumnName()
    {
        Patients().Sort("DATEOFBIRTH", true).Should().ContainInOrder(BennettArjun, Ali, Ahmed, BennettGeorge);
        Patients().Sort("dateOfBirth", false).Should().ContainInOrder(BennettGeorge, Ahmed, Ali, BennettArjun);
    }

    [Test]
    public void Sort_Email_SortsByEmail()
    {
        Patients().Sort("email", true).Should().ContainInOrder(BennettArjun, Ahmed, BennettGeorge, Ali);
    }

    [Test]
    public void Sort_UnknownColumn_FallsBackToName()
    {
        Patients().Sort("unknown", true).Should().ContainInOrder(Ahmed, Ali, BennettArjun, BennettGeorge);
    }

    [TestCase("name", true)]
    [TestCase("DateOfBirth", true)]
    [TestCase("EMAIL", true)]
    [TestCase("id", false)]
    [TestCase("", false)]
    public void IsSortColumn_Name_IgnoresCase(string sort, bool expected)
    {
        PatientSearch.IsSortColumn(sort).Should().Be(expected);
    }
}
