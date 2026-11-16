using FluentAssertions;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Patients.Queries.SearchPatients;

[TestFixture]
public sealed class PatientSearchFiltersUsedTests
{
    [Test]
    public void FiltersUsed_NoneOrBlank_IsEmpty()
    {
        var query = PatientsTestConstants.CreateSearchQuery() with { FirstName = "  ", LastName = "", Email = null };

        PatientSearch.FiltersUsed(query).Should().BeEmpty();
    }

    [Test]
    public void FiltersUsed_Every_NamesThemInOrder()
    {
        var query = PatientsTestConstants.CreateSearchQuery() with
        {
            FirstName = "a",
            LastName = "b",
            Email = "c",
            DateOfBirthFrom = new DateOnly(1990, 1, 1),
            DateOfBirthTo = new DateOnly(2000, 1, 1)
        };

        PatientSearch.FiltersUsed(query).Should().Equal("firstName", "lastName", "email", "dateOfBirthFrom", "dateOfBirthTo");
    }
}
