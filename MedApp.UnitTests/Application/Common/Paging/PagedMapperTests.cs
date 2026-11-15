using FluentAssertions;
using MedApp.Application.Common.Paging;
using MedApp.Contracts.Common;

namespace MedApp.UnitTests.Application.Common.Paging;

[TestFixture]
public sealed class PagedMapperTests
{
    [Test]
    public void ToResponse_PageOfResponses_KeepsThemAndWorksOutThePages()
    {
        var result = PagedMapper.ToResponse(new PagedResult<string>(["a", "b"], 25), page: 2, pageSize: 10);

        result.Should().BeEquivalentTo(new PagedResponse<string>(["a", "b"], 2, 10, 25, 3));
    }

    [Test]
    public void ToResponse_WithAMap_MapsEachItemInOrder()
    {
        var result = PagedMapper.ToResponse(new PagedResult<int>([3, 1], 2), page: 1, pageSize: 10, n => $"#{n}");

        result.Items.Should().Equal("#3", "#1");
        result.TotalCount.Should().Be(2);
        result.TotalPages.Should().Be(1);
    }

    [Test]
    public void ToResponse_NothingFound_HasNoPages()
    {
        var result = PagedMapper.ToResponse(new PagedResult<string>([], 0), page: 1, pageSize: 10);

        result.Items.Should().BeEmpty();
        result.TotalPages.Should().Be(0);
    }
}
