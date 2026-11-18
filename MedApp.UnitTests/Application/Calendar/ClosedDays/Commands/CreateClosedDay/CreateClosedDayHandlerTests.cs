using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.ClosedDays.Commands.CreateClosedDay;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ClosedDays.Commands.CreateClosedDay;

[TestFixture]
public sealed class CreateClosedDayHandlerTests
{
    private Mock<IClosedDayRepository> _repository = null!;
    private CreateClosedDayHandler _handler = null!;
    private List<ClosedDay> _added = null!;

    [SetUp]
    public void SetUp()
    {
        var fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IClosedDayRepository>();
        _added = [];
        _repository.Setup(r => r.AddAsync(It.IsAny<IReadOnlyCollection<ClosedDay>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<ClosedDay> days, CancellationToken _) => _added.AddRange(days))
            .Returns(Task.CompletedTask);
        fixture.Inject(_repository.Object);
        fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
        _handler = fixture.Create<CreateClosedDayHandler>();
    }

    [Test]
    public async Task Handle_FutureDate_AddsItWithTheTrimmedNameAndWhoAddedIt()
    {
        using var cts = new CancellationTokenSource();

        var response = await _handler.Handle(new CreateClosedDayCommand(ClosedWednesday, $"  {ClosedDayName} ", AdminActor), cts.Token);

        var day = _added.Should().ContainSingle().Subject;
        day.Date.Should().Be(ClosedWednesday);
        day.Name.Should().Be(ClosedDayName);
        day.CreatedAtUtc.Should().Be(Now);
        day.CreatedByUserId.Should().Be(AdminUserId);
        _repository.Verify(r => r.GetOnAsync(ClosedWednesday, cts.Token), Times.Once);
        _repository.Verify(r => r.AddAsync(It.IsAny<IReadOnlyCollection<ClosedDay>>(), cts.Token), Times.Once);

        response.Id.Should().Be(day.Id);
        response.Date.Should().Be(ClosedWednesday);
        response.Name.Should().Be(ClosedDayName);
        response.IsPast.Should().BeFalse();
    }

    [Test]
    public async Task Handle_Today_AddsIt()
    {
        await _handler.Handle(new CreateClosedDayCommand(Today, ClosedDayName, AdminActor), CancellationToken.None);

        _added.Should().ContainSingle(d => d.Date == Today);
    }

    [Test]
    public async Task Handle_PastDate_ThrowsConflictAndAddsNothing()
    {
        var act = () => _handler.Handle(new CreateClosedDayCommand(Today.AddDays(-1), ClosedDayName, AdminActor), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ClosedDayRules.PastCantBeAdded);
        _added.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_DateAlreadyClosed_ThrowsConflictNamingIt()
    {
        _repository.Setup(r => r.GetOnAsync(ClosedWednesday, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CalendarTestConstants.CreateClosedDay(name: "Christmas Day"));

        var act = () => _handler.Handle(new CreateClosedDayCommand(ClosedWednesday, ClosedDayName, AdminActor), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Wednesday 25 November is already a closed day: Christmas Day.");
        _added.Should().BeEmpty();
    }
}
