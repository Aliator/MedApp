using AutoFixture;
using MedApp.Application.Sites.Repositories;
using MedApp.Domain.Sites;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.SitesTestConstants;

namespace MedApp.UnitTests.Application.Sites;

/// <summary>
/// Handler tests for sites and rooms: a mocked repository answering from the sites and rooms given
/// (<see cref="GivenSites"/>, <see cref="GivenRooms"/>), a fixed clock, and a token to check is passed on.
/// </summary>
public abstract class SiteHandlerTestBase
{
    protected IFixture Fixture = null!;
    protected Mock<ISiteRepository> Repository = null!;
    protected List<Site> Sites = null!;
    protected List<Room> Rooms = null!;
    protected HashSet<Guid> SitesInUse = null!;
    protected HashSet<Guid> RoomsInUse = null!;
    private CancellationTokenSource _cts = null!;

    protected CancellationToken Token => _cts.Token;

    [SetUp]
    public void SetUpRepository()
    {
        _cts = new CancellationTokenSource();
        Sites = [];
        Rooms = [];
        SitesInUse = [];
        RoomsInUse = [];
        Repository = new Mock<ISiteRepository>();

        Repository.Setup(r => r.GetSitesAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((bool includeArchived, CancellationToken _) =>
                (IReadOnlyList<Site>)Sites.Where(s => includeArchived || !s.IsArchived).OrderBy(s => s.Name).ToList());
        Repository.Setup(r => r.GetSiteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => Sites.FirstOrDefault(s => s.Id == id));
        Repository.Setup(r => r.GetRoomsAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? siteId, bool includeArchived, CancellationToken _) =>
                (IReadOnlyList<Room>)Rooms
                    .Where(r => (siteId == null || r.SiteId == siteId) && (includeArchived || !r.IsArchived))
                    .OrderBy(r => r.Name)
                    .ToList());
        Repository.Setup(r => r.GetRoomAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => Rooms.FirstOrDefault(r => r.Id == id));
        Repository.Setup(r => r.SiteNameExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, Guid? exceptId, CancellationToken _) =>
                Sites.Any(s => string.Equals(s.Name, name.Trim(), StringComparison.OrdinalIgnoreCase) && s.Id != exceptId));
        Repository.Setup(r => r.RoomNameExistsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid siteId, string name, Guid? exceptId, CancellationToken _) =>
                Rooms.Any(r => r.SiteId == siteId && string.Equals(r.Name, name.Trim(), StringComparison.OrdinalIgnoreCase) && r.Id != exceptId));
        Repository.Setup(r => r.CountOpenSitesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Sites.Count(s => !s.IsArchived));
        Repository.Setup(r => r.GetSitesInUseAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => (IReadOnlySet<Guid>)ids.Where(SitesInUse.Contains).ToHashSet());
        Repository.Setup(r => r.GetRoomsInUseAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => (IReadOnlySet<Guid>)ids.Where(RoomsInUse.Contains).ToHashSet());
        Repository.Setup(r => r.AddSiteAsync(It.IsAny<Site>(), It.IsAny<CancellationToken>()))
            .Callback((Site site, CancellationToken _) => Sites.Add(site))
            .Returns(Task.CompletedTask);
        Repository.Setup(r => r.AddRoomAsync(It.IsAny<Room>(), It.IsAny<CancellationToken>()))
            .Callback((Room room, CancellationToken _) => Rooms.Add(room))
            .Returns(Task.CompletedTask);

        Fixture = AutoFixtureFactory.Create();
        Fixture.Inject(Repository.Object);
        Fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
    }

    [TearDown]
    public void TearDownToken() => _cts.Dispose();

    protected void GivenSites(params Site[] sites) => Sites.AddRange(sites);

    protected void GivenRooms(params Room[] rooms) => Rooms.AddRange(rooms);
}
