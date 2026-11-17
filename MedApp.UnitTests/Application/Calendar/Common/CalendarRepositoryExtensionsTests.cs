using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class CalendarRepositoryExtensionsTests
{
    private Mock<ICalendarSetupRepository> _repository = null!;
    private CancellationTokenSource _cts = null!;
    private CancellationToken Token => _cts.Token;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<ICalendarSetupRepository>();
        _cts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    [Test]
    public async Task GetActivityTypeOrThrowAsync_Exists_ReturnsIt()
    {
        _repository.Setup(r => r.GetActivityTypeAsync(Lunch.Id, Token)).ReturnsAsync(Lunch);

        (await _repository.Object.GetActivityTypeOrThrowAsync(Lunch.Id, Token)).Should().BeSameAs(Lunch);
    }

    [Test]
    public async Task GetActivityTypeOrThrowAsync_Missing_ThrowsNotFound()
    {
        var id = Guid.NewGuid();

        var act = () => _repository.Object.GetActivityTypeOrThrowAsync(id, Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Activity type '{id}' was not found.");
    }

    [Test]
    public async Task GetActivityTypeForBlockAsync_Current_ReturnsIt()
    {
        _repository.Setup(r => r.GetActivityTypeAsync(Lunch.Id, Token)).ReturnsAsync(Lunch);

        (await _repository.Object.GetActivityTypeForBlockAsync(Lunch.Id, null, Token)).Should().BeSameAs(Lunch);
    }

    [Test]
    public async Task GetActivityTypeForBlockAsync_ArchivedForANewBlock_ThrowsConflict()
    {
        var archived = CreateArchivedActivityType();
        _repository.Setup(r => r.GetActivityTypeAsync(archived.Id, Token)).ReturnsAsync(archived);

        var act = () => _repository.Object.GetActivityTypeForBlockAsync(archived.Id, Lunch.Id, Token);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"The '{archived.Name}' activity type is archived, so it can't be used for new blocks.");
    }

    [Test]
    public async Task GetActivityTypeForBlockAsync_ArchivedButTheBlocksOwn_ReturnsIt()
    {
        var archived = CreateArchivedActivityType();
        _repository.Setup(r => r.GetActivityTypeAsync(archived.Id, Token)).ReturnsAsync(archived);

        (await _repository.Object.GetActivityTypeForBlockAsync(archived.Id, archived.Id, Token)).Should().BeSameAs(archived);
    }

    [Test]
    public async Task GetActivityTypeForBlockAsync_Missing_ThrowsNotFound()
    {
        var act = () => _repository.Object.GetActivityTypeForBlockAsync(Guid.NewGuid(), null, Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task GetDayTemplateOrThrowAsync_Exists_ReturnsIt()
    {
        var template = CreateDayTemplate();
        _repository.Setup(r => r.GetDayTemplateAsync(DayTemplateId, Today, Token)).ReturnsAsync(template);

        (await _repository.Object.GetDayTemplateOrThrowAsync(DayTemplateId, Today, Token)).Should().BeSameAs(template);
    }

    [Test]
    public async Task GetDayTemplateOrThrowAsync_Missing_ThrowsNotFound()
    {
        var act = () => _repository.Object.GetDayTemplateOrThrowAsync(DayTemplateId, Today, Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Day template '{DayTemplateId}' was not found.");
    }

    [Test]
    public async Task GetOwnDayTemplateOrThrowAsync_Theirs_ReturnsIt()
    {
        var template = CreateDayTemplate(ownerUserId: StaffUserId);
        _repository.Setup(r => r.GetDayTemplateAsync(DayTemplateId, Today, Token)).ReturnsAsync(template);

        (await _repository.Object.GetOwnDayTemplateOrThrowAsync(DayTemplateId, StaffUserId, Today, Token)).Should().BeSameAs(template);
        _repository.Verify(r => r.GetDayTemplateAsync(DayTemplateId, Today, Token), Times.Once);
    }

    [Test]
    public async Task GetOwnDayTemplateOrThrowAsync_SomeoneElses_ThrowsNotFound()
    {
        _repository.Setup(r => r.GetDayTemplateAsync(DayTemplateId, Today, Token)).ReturnsAsync(CreateDayTemplate(ownerUserId: OtherStaffUserId));

        var act = () => _repository.Object.GetOwnDayTemplateOrThrowAsync(DayTemplateId, StaffUserId, Today, Token);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Day template '{DayTemplateId}' isn't one of this person's templates.");
    }

    [Test]
    public async Task GetOwnDayTemplateOrThrowAsync_Missing_ThrowsNotFound()
    {
        var act = () => _repository.Object.GetOwnDayTemplateOrThrowAsync(DayTemplateId, StaffUserId, Today, Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Day template '{DayTemplateId}' was not found.");
    }

    [Test]
    public async Task GetDayTemplateForUpdateOrThrowAsync_Exists_ReturnsIt()
    {
        var template = CreateDayTemplate();
        _repository.Setup(r => r.GetDayTemplateForUpdateAsync(DayTemplateId, Today, Token)).ReturnsAsync(template);

        (await _repository.Object.GetDayTemplateForUpdateOrThrowAsync(DayTemplateId, Today, Token)).Should().BeSameAs(template);
    }

    [Test]
    public async Task GetDayTemplateForUpdateOrThrowAsync_Missing_ThrowsNotFound()
    {
        var act = () => _repository.Object.GetDayTemplateForUpdateOrThrowAsync(DayTemplateId, Today, Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Day template '{DayTemplateId}' was not found.");
    }

    [Test]
    public async Task GetWeekTemplateOrThrowAsync_Exists_ReturnsIt()
    {
        var template = CreateWeekTemplate();
        _repository.Setup(r => r.GetWeekTemplateAsync(WeekTemplateId, Today, Token)).ReturnsAsync(template);

        (await _repository.Object.GetWeekTemplateOrThrowAsync(WeekTemplateId, Today, Token)).Should().BeSameAs(template);
    }

    [Test]
    public async Task GetWeekTemplateOrThrowAsync_Missing_ThrowsNotFound()
    {
        var act = () => _repository.Object.GetWeekTemplateOrThrowAsync(WeekTemplateId, Today, Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Week template '{WeekTemplateId}' was not found.");
    }

    [Test]
    public async Task GetOwnWeekTemplateOrThrowAsync_Theirs_ReturnsIt()
    {
        var template = CreateWeekTemplate(ownerUserId: StaffUserId);
        _repository.Setup(r => r.GetWeekTemplateAsync(WeekTemplateId, Today, Token)).ReturnsAsync(template);

        (await _repository.Object.GetOwnWeekTemplateOrThrowAsync(WeekTemplateId, StaffUserId, Today, Token)).Should().BeSameAs(template);
        _repository.Verify(r => r.GetWeekTemplateAsync(WeekTemplateId, Today, Token), Times.Once);
    }

    [Test]
    public async Task GetOwnWeekTemplateOrThrowAsync_SomeoneElses_ThrowsNotFound()
    {
        _repository.Setup(r => r.GetWeekTemplateAsync(WeekTemplateId, Today, Token)).ReturnsAsync(CreateWeekTemplate(ownerUserId: OtherStaffUserId));

        var act = () => _repository.Object.GetOwnWeekTemplateOrThrowAsync(WeekTemplateId, StaffUserId, Today, Token);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"Week template '{WeekTemplateId}' isn't one of this person's templates.");
    }

    [Test]
    public async Task GetOwnWeekTemplateOrThrowAsync_Missing_ThrowsNotFound()
    {
        var act = () => _repository.Object.GetOwnWeekTemplateOrThrowAsync(WeekTemplateId, StaffUserId, Today, Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Week template '{WeekTemplateId}' was not found.");
    }

    [Test]
    public async Task GetWeekTemplateForUpdateOrThrowAsync_Exists_ReturnsIt()
    {
        var template = CreateWeekTemplate();
        _repository.Setup(r => r.GetWeekTemplateForUpdateAsync(WeekTemplateId, Today, Token)).ReturnsAsync(template);

        (await _repository.Object.GetWeekTemplateForUpdateOrThrowAsync(WeekTemplateId, Today, Token)).Should().BeSameAs(template);
    }

    [Test]
    public async Task GetWeekTemplateForUpdateOrThrowAsync_Missing_ThrowsNotFound()
    {
        var act = () => _repository.Object.GetWeekTemplateForUpdateOrThrowAsync(WeekTemplateId, Today, Token);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Week template '{WeekTemplateId}' was not found.");
    }
}
