using AutoFixture;
using FluentAssertions;
using MedApp.Application.Calendar.ClosedDays.Commands.DeleteClosedDay;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.ClosedDays.Commands.DeleteClosedDay;

[TestFixture]
public sealed class DeleteClosedDayHandlerTests
{
    private Mock<IClosedDayRepository> _repository = null!;
    private DeleteClosedDayHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        var fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IClosedDayRepository>();
        fixture.Inject(_repository.Object);
        fixture.Inject<TimeProvider>(new FixedTimeProvider(Now));
        _handler = fixture.Create<DeleteClosedDayHandler>();
    }

    private ClosedDay Given(DateOnly date)
    {
        var day = CalendarTestConstants.CreateClosedDay(date);
        _repository.Setup(r => r.GetByIdAsync(day.Id, It.IsAny<CancellationToken>())).ReturnsAsync(day);
        return day;
    }

    [Test]
    public async Task Handle_FutureDay_DeletesIt()
    {
        var day = Given(ClosedWednesday);
        using var cts = new CancellationTokenSource();

        await _handler.Handle(new DeleteClosedDayCommand(day.Id), cts.Token);

        _repository.Verify(r => r.GetByIdAsync(day.Id, cts.Token), Times.Once);
        _repository.Verify(r => r.DeleteAsync(day, cts.Token), Times.Once);
    }

    [Test]
    public async Task Handle_Today_DeletesIt()
    {
        var day = Given(Today);

        await _handler.Handle(new DeleteClosedDayCommand(day.Id), CancellationToken.None);

        _repository.Verify(r => r.DeleteAsync(day, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_PastDay_ThrowsConflictAndKeepsIt()
    {
        var day = Given(Today.AddDays(-1));

        var act = () => _handler.Handle(new DeleteClosedDayCommand(day.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(ClosedDayRules.PastCantBeRemoved);
        _repository.Verify(r => r.DeleteAsync(It.IsAny<ClosedDay>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_Unknown_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new DeleteClosedDayCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
