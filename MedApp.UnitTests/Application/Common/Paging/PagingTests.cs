using FluentAssertions;
using PagingRules = MedApp.Application.Common.Paging.Paging;

namespace MedApp.UnitTests.Application.Common.Paging;

[TestFixture]
public sealed class PagingTests
{
    [TestCase(1, 10, 0)]
    [TestCase(2, 10, 10)]
    [TestCase(3, 25, 50)]
    public void Skip_Page_ReturnsRowsBeforeIt(int page, int pageSize, int expected)
    {
        PagingRules.Skip(page, pageSize).Should().Be(expected);
    }

    [TestCase(0, 10, 0)]
    [TestCase(1, 10, 1)]
    [TestCase(10, 10, 1)]
    [TestCase(11, 10, 2)]
    [TestCase(60, 10, 6)]
    public void TotalPages_TotalCount_RoundsUp(int totalCount, int pageSize, int expected)
    {
        PagingRules.TotalPages(totalCount, pageSize).Should().Be(expected);
    }
}
