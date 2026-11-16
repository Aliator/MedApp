using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Domain.Calendar;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarSetupTestConstants;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Calendar.Common;

[TestFixture]
public sealed class TemplateChecksTests
{
    private Mock<ICalendarSetupRepository> _repository = null!;
    private CancellationTokenSource _cts = null!;
    private List<CalendarActivityType> _activityTypes = null!;
    private List<DayTemplate> _dayTemplates = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<ICalendarSetupRepository>();
        _cts = new CancellationTokenSource();
        _activityTypes = [Lunch, Training];
        _dayTemplates = [BuildDayTemplate(), BuildDayTemplate(id: OtherDayTemplateId, name: OtherDayTemplateName)];
        _repository
            .Setup(r => r.GetActivityTypesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyList<CalendarActivityType>)_activityTypes.Where(t => ids.Contains(t.Id)).ToList());
        _repository
            .Setup(r => r.GetDayTemplatesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyList<DayTemplate>)_dayTemplates.Where(t => ids.Contains(t.Id)).ToList());
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    private Task CheckActivityTypes(IReadOnlySet<Guid>? already, params DayTemplateBlockValue[] blocks) =>
        TemplateChecks.CheckActivityTypesAsync(_repository.Object, blocks, already ?? new HashSet<Guid>(), _cts.Token);

    private Task CheckDayTemplates(IReadOnlySet<Guid>? already, params Guid?[] weekdays) =>
        TemplateChecks.CheckDayTemplatesAsync(
            _repository.Object,
            WeekTemplateDayValue.From(weekdays[0], weekdays.ElementAtOrDefault(1), weekdays.ElementAtOrDefault(2), weekdays.ElementAtOrDefault(3), weekdays.ElementAtOrDefault(4)),
            already ?? new HashSet<Guid>(),
            _cts.Token);

    [Test]
    public async Task CheckActivityTypesAsync_AllExistAndCurrent_PassesLookingEachUpOnce()
    {
        await CheckActivityTypes(null, BlockValue(9, 10), BlockValue(12, 13), BlockValue(14, 15, Training.Id));

        _repository.Verify(r => r.GetActivityTypesByIdAsync(
            It.Is<IEnumerable<Guid>>(ids => ids.Count() == 2 && ids.Contains(Lunch.Id) && ids.Contains(Training.Id)),
            _cts.Token), Times.Once);
    }

    [Test]
    public async Task CheckActivityTypesAsync_NoBlocks_Passes()
    {
        await CheckActivityTypes(null);
    }

    [Test]
    public async Task CheckActivityTypesAsync_SomeMissing_ThrowsNotFoundNamingThem()
    {
        var missing = Guid.NewGuid();

        var act = () => CheckActivityTypes(null, BlockValue(12, 13), BlockValue(14, 15, missing));

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Activity type(s) not found: {missing}.");
    }

    [Test]
    public async Task CheckActivityTypesAsync_ArchivedNewOnes_ThrowsConflictNamingThem()
    {
        var archived = CreateArchivedActivityType();
        _activityTypes.Add(archived);

        var act = () => CheckActivityTypes(null, BlockValue(12, 13), BlockValue(14, 15, archived.Id));

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"Archived activity types can't be added to a day template: {archived.Name}.");
    }

    [Test]
    public async Task CheckActivityTypesAsync_ArchivedOneAlreadyInTheTemplate_Passes()
    {
        var archived = CreateArchivedActivityType();
        _activityTypes.Add(archived);

        await CheckActivityTypes(new HashSet<Guid> { archived.Id }, BlockValue(14, 15, archived.Id));
    }

    [Test]
    public async Task CheckDayTemplatesAsync_AllExistAndCurrent_Passes()
    {
        await CheckDayTemplates(null, DayTemplateId, OtherDayTemplateId, null, DayTemplateId, null);

        _repository.Verify(r => r.GetDayTemplatesByIdAsync(It.Is<IEnumerable<Guid>>(ids => ids.Count() == 2), _cts.Token), Times.Once);
    }

    [Test]
    public async Task CheckDayTemplatesAsync_SomeMissing_ThrowsNotFoundNamingThem()
    {
        var missing = Guid.NewGuid();

        var act = () => CheckDayTemplates(null, DayTemplateId, missing);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Day template(s) not found: {missing}.");
    }

    [Test]
    public async Task CheckDayTemplatesAsync_ArchivedNewOnes_ThrowsConflictNamingThem()
    {
        _dayTemplates[1].IsArchived = true;

        var act = () => CheckDayTemplates(null, DayTemplateId, OtherDayTemplateId);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage($"Archived day templates can't be added to a week template: {OtherDayTemplateName}.");
    }

    [Test]
    public async Task CheckDayTemplatesAsync_ArchivedOneAlreadyInTheWeek_Passes()
    {
        _dayTemplates[1].IsArchived = true;

        await CheckDayTemplates(new HashSet<Guid> { OtherDayTemplateId }, OtherDayTemplateId);
    }

    [Test]
    public void ActivityTypesIn_GivesTheActivityTypesOfTheTemplatesBlocks()
    {
        var template = BuildDayTemplate([CreateTemplateBlock(9, 10), CreateTemplateBlock(12, 13), CreateTemplateBlock(14, 15, Training)]);

        TemplateChecks.ActivityTypesIn(template).Should().BeEquivalentTo([Lunch.Id, Training.Id]);
    }

    [Test]
    public void DayTemplatesIn_GivesTheDayTemplatesOfTheWeeksDays()
    {
        var template = BuildWeekTemplate(
        [
            CreateWeekDay(DayOfWeek.Monday),
            CreateWeekDay(DayOfWeek.Tuesday, OtherDayTemplateId),
            CreateWeekDay(DayOfWeek.Wednesday)
        ]);

        TemplateChecks.DayTemplatesIn(template).Should().BeEquivalentTo([DayTemplateId, OtherDayTemplateId]);
    }
}
