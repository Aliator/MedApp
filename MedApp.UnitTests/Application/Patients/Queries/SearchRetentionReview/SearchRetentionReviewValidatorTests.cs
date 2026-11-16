using FluentAssertions;
using MedApp.Application.Patients.Queries.SearchRetentionReview;

namespace MedApp.UnitTests.Application.Patients.Queries.SearchRetentionReview;

[TestFixture]
public sealed class SearchRetentionReviewValidatorTests
{
    private readonly SearchRetentionReviewValidator _validator = new();

    [Test]
    public void Validate_FirstPage_IsValid()
    {
        _validator.Validate(new SearchRetentionReviewQuery(1, 10, true)).IsValid.Should().BeTrue();
    }

    [TestCase(0, 10, "Page")]
    [TestCase(1, 0, "PageSize")]
    [TestCase(1, 101, "PageSize")]
    public void Validate_BadPaging_IsInvalid(int page, int pageSize, string property)
    {
        _validator.Validate(new SearchRetentionReviewQuery(page, pageSize, false))
            .Errors.Should().ContainSingle(e => e.PropertyName == property);
    }
}
