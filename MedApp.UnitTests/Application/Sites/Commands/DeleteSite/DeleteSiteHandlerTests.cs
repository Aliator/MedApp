using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Sites.Commands.DeleteSite;
using MedApp.Application.Sites.Common;
using Moq;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Application.Sites.Commands.DeleteSite;

[TestFixture]
public sealed class DeleteSiteHandlerTests : SiteHandlerTestBase
{
    private DeleteSiteHandler Handler => Fixture.Create<DeleteSiteHandler>();

    [Test]
    public async Task Handle_UnusedSite_DeletesIt()
    {
        GivenSites(NewSite(), NewSite(OtherSiteId, OtherSiteName));

        await Handler.Handle(new DeleteSiteCommand(SiteId), Token);

        Repository.Verify(r => r.GetSitesInUseAsync(It.IsAny<IEnumerable<Guid>>(), Token), Times.Once);
        Repository.Verify(r => r.DeleteSiteAsync(SiteId, Token), Times.Once);
    }

    [Test]
    public async Task Handle_AnArchivedSiteWhenOnlyOneIsOpen_DeletesIt()
    {
        GivenSites(NewSite(isArchived: true), NewSite(OtherSiteId, OtherSiteName));

        await Handler.Handle(new DeleteSiteCommand(SiteId), Token);

        Repository.Verify(r => r.DeleteSiteAsync(SiteId, Token), Times.Once);
    }

    [Test]
    public async Task Handle_UsedSite_ThrowsConflict()
    {
        GivenSites(NewSite(), NewSite(OtherSiteId, OtherSiteName));
        SitesInUse.Add(SiteId);

        var act = () => Handler.Handle(new DeleteSiteCommand(SiteId), Token);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"{SiteName} has rooms, closed days or staff working hours, so it can't be deleted. Archive it instead.");
        Repository.Verify(r => r.DeleteSiteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_TheLastOpenSite_ThrowsConflict()
    {
        GivenSites(NewSite());

        var act = () => Handler.Handle(new DeleteSiteCommand(SiteId), Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(SiteRules.LastOpenSite);
        Repository.Verify(r => r.DeleteSiteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_UnknownSite_ThrowsNotFound()
    {
        var act = () => Handler.Handle(new DeleteSiteCommand(SiteId), Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
