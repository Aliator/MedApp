using FluentAssertions;
using MedApp.Application.Activity.Queries.SearchActivity;

namespace MedApp.UnitTests.Application.Activity.Queries.SearchActivity;

[TestFixture]
public sealed class SearchActivityValidatorTests
{
    private readonly SearchActivityValidator _validator = new();

    private static SearchActivityQuery Query() => new(1, 10, false, null, null, null, null, null);

    [Test]
    public void Validate_DefaultQuery_IsValid()
    {
        _validator.Validate(Query() with { Actions = ["PatientViewed"], Search = "a" }).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_BadPaging_IsInvalid()
    {
        _validator.Validate(Query() with { Page = 0 }).Errors.Should().ContainSingle(e => e.PropertyName == "Page");
        _validator.Validate(Query() with { PageSize = 0 }).Errors.Should().ContainSingle(e => e.PropertyName == "PageSize");
        _validator.Validate(Query() with { PageSize = 101 }).Errors.Should().ContainSingle(e => e.PropertyName == "PageSize");
    }

    [Test]
    public void Validate_UnknownAction_IsInvalid()
    {
        _validator.Validate(Query() with { Actions = ["Nonsense"] }).IsValid.Should().BeFalse();
    }

    [Test]
    public void Validate_LongSearch_IsInvalid()
    {
        _validator.Validate(Query() with { Search = new string('a', 201) }).Errors.Should().ContainSingle(e => e.PropertyName == "Search");
    }

    [Test]
    public void Validate_DateRangeBackwards_IsInvalid_ButEqualIsFine()
    {
        var day = new DateOnly(2026, 6, 15);
        _validator.Validate(Query() with { From = day, To = day.AddDays(-1) }).Errors.Should().ContainSingle(e => e.PropertyName == "To");
        _validator.Validate(Query() with { From = day, To = day }).IsValid.Should().BeTrue();
    }
}
