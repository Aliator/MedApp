using FluentAssertions;
using MedApp.Application.Authentication.Users.Queries.SearchUsers;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Auth.Users.Queries.SearchUsers;

[TestFixture]
public sealed class SearchUsersValidatorTests
{
    private SearchUsersValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _validator = new SearchUsersValidator();
    }

    [Test]
    public void Validate_DefaultQuery_Passes()
    {
        _validator.Validate(AuthTestConstants.CreateSearchUsersQuery()).IsValid.Should().BeTrue();
    }

    [TestCase("username")]
    [TestCase("Username")]
    public void Validate_UsernameSortWithFilters_Passes(string sort)
    {
        var query = AuthTestConstants.CreateSearchUsersQuery() with
        {
            Sort = sort,
            Username = AuthTestConstants.Usernames[0],
            Role = AuthTestConstants.Roles[0]
        };

        _validator.Validate(query).IsValid.Should().BeTrue();
    }

    [Test]
    public void Validate_UnknownSort_Fails()
    {
        var result = _validator.Validate(AuthTestConstants.CreateSearchUsersQuery() with { Sort = "role" });

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Sort");
    }

    [Test]
    public void Validate_PageBelowOne_Fails()
    {
        var result = _validator.Validate(AuthTestConstants.CreateSearchUsersQuery() with { Page = 0 });

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Page");
    }

    [TestCase(0)]
    [TestCase(101)]
    public void Validate_PageSizeOutOfRange_Fails(int pageSize)
    {
        var result = _validator.Validate(AuthTestConstants.CreateSearchUsersQuery() with { PageSize = pageSize });

        result.Errors.Should().ContainSingle(e => e.PropertyName == "PageSize");
    }

    [Test]
    public void Validate_FilterTooLong_Fails()
    {
        var result = _validator.Validate(AuthTestConstants.CreateSearchUsersQuery() with { Username = new string('a', 257) });

        result.Errors.Should().ContainSingle(e => e.PropertyName == "Username");
    }
}
