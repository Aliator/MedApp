using AutoFixture;
using FluentAssertions;
using MedApp.Application.Common.Paging;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Application.Patients.Repositories;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.Patients;
using MedApp.UnitTests.Common.Constants;
using MedApp.UnitTests.Common.Fixtures;
using Moq;

namespace MedApp.UnitTests.Application.Patients.Queries.SearchPatients;

[TestFixture]
public sealed class SearchPatientsHandlerTests
{
    private IFixture _fixture = null!;
    private Mock<IPatientRepository> _repository = null!;
    private SearchPatientsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();

        _repository = new Mock<IPatientRepository>();
        _fixture.Inject<IPatientRepository>(_repository.Object);

        _handler = _fixture.Create<SearchPatientsHandler>();
    }

    [Test]
    public async Task Handle_PageOfPatients_MapsThemWithTotals()
    {
        var patients = new[] { PatientsTestConstants.CreateValidPatient(), PatientsTestConstants.CreateValidPatient() };
        var query = PatientsTestConstants.CreateSearchQuery() with { Page = 2 };
        _repository
            .Setup(r => r.SearchAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Patient>(patients, 12));

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Items.Should().BeEquivalentTo(patients.Select(p =>
            new PatientResponse(p.Id, p.FirstName, p.LastName, p.DateOfBirth, p.Email)));
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(PatientsTestConstants.DefaultPageSize);
        result.TotalCount.Should().Be(12);
        result.TotalPages.Should().Be(2);
    }

    [Test]
    public async Task Handle_NoMatches_ReturnsAnEmptyPageAndNoPages()
    {
        _repository
            .Setup(r => r.SearchAsync(It.IsAny<SearchPatientsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Patient>([], 0));

        var result = await _handler.Handle(PatientsTestConstants.CreateSearchQuery(), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    [Test]
    public async Task Handle_PassesQueryAndCancellationTokenToRepository()
    {
        var query = PatientsTestConstants.CreateSearchQuery();
        using var cts = new CancellationTokenSource();
        _repository
            .Setup(r => r.SearchAsync(It.IsAny<SearchPatientsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Patient>([], 0));

        await _handler.Handle(query, cts.Token);

        _repository.Verify(r => r.SearchAsync(query, cts.Token), Times.Once);
    }
}
