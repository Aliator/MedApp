using FluentAssertions;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Patients.Queries.SearchPatients;

[TestFixture]
public sealed class SearchPatientsValidatorTests
{
    private SearchPatientsValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new SearchPatientsValidator();
    }

    [Test]
    public void Validate_DefaultQuery_Passes()
    {
        _validator.Validate(PatientsTestConstants.CreateSearchQuery()).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_EveryFilterSet_Passes()
    {
        var query = PatientsTestConstants.CreateSearchQuery() with
        {
            Page = 3,
            PageSize = 100,
            Sort = "dateOfBirth",
            Ascending = false,
            FirstName = PatientsTestConstants.ValidFirstName,
            LastName = PatientsTestConstants.ValidLastName,
            Email = PatientsTestConstants.ValidEmail,
            DateOfBirthFrom = new DateOnly(1980, 1, 1),
            DateOfBirthTo = new DateOnly(1980, 1, 1)
        };

        _validator.Validate(query).IsValid.Should().BeTrue();
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Validate_PageBelowOne_Fails(int page)
    {
        var result = _validator.Validate(PatientsTestConstants.CreateSearchQuery() with { Page = page });

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Page");
    }

    [TestCase(0)]
    [TestCase(101)]
    public void Validate_PageSizeOutOfRange_Fails(int pageSize)
    {
        var result = _validator.Validate(PatientsTestConstants.CreateSearchQuery() with { PageSize = pageSize });

        result.Errors.Should().ContainSingle(e => e.PropertyName == "PageSize");
    }

    [Test]
    public void Validate_UnknownSort_Fails()
    {
        var result = _validator.Validate(PatientsTestConstants.CreateSearchQuery() with { Sort = "createdAt" });

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Sort");
    }

    [Test]
    public void Validate_FilterTooLong_Fails()
    {
        var result = _validator.Validate(PatientsTestConstants.CreateSearchQuery() with
        {
            FirstName = new string('a', PatientsTestConstants.MaxNameLength + 1)
        });

        result.Errors.Should().ContainSingle(e => e.PropertyName == "FirstName");
    }

    [Test]
    public void Validate_DateOfBirthRangeBackwards_Fails()
    {
        var result = _validator.Validate(PatientsTestConstants.CreateSearchQuery() with
        {
            DateOfBirthFrom = new DateOnly(1990, 1, 1),
            DateOfBirthTo = new DateOnly(1980, 1, 1)
        });

        result.Errors.Should().ContainSingle(e => e.PropertyName == "DateOfBirthTo");
    }

    [Test]
    public void Validate_OnlyOneEndOfTheDateOfBirthRange_Passes()
    {
        var query = PatientsTestConstants.CreateSearchQuery() with { DateOfBirthTo = new DateOnly(1980, 1, 1) };

        _validator.Validate(query).IsValid.Should().BeTrue();
    }
}
