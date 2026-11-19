using MedApp.Application.Patients.Common;
﻿using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Patients.Commands.CreatePatient;
using MedApp.Application.Patients.Commands.DeletePatient;
using MedApp.Application.Patients.Commands.UpdatePatient;
using MedApp.Application.Patients.Queries.GetAllPatients;
using MedApp.Application.Patients.Queries.GetPatientById;
using MedApp.Application.Patients.Queries.SearchPatients;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

[TestFixture]
public sealed class PatientsControllerTests
{
    private IFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = AutoFixtureFactory.Create();

    private static PatientsController Controller(RecordingMediator mediator) =>
        new PatientsController(mediator).WithRequest(SignedIn("User"));

    [Test]
    public async Task CreatePatient_Created_ReturnsItWithItsAddress()
    {
        var request = _fixture.Create<CreatePatientRequest>();
        var patient = _fixture.Create<PatientResponse>();
        var mediator = new RecordingMediator(_ => patient);

        var result = await Controller(mediator).CreatePatient(request);

        mediator.Request.Should().BeEquivalentTo(new CreatePatientCommand(
            request.FirstName, request.LastName, request.DateOfBirth, request.Email, request.NhsNumber,
            request.AddressLines, request.Postcode, request.MobilePhone, request.PracticeId, request.ReminderChannel,
            UnlistedPracticeInput.From(request.UnlistedPractice)));
        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(PatientsController.GetPatientById));
        created.RouteValues!["id"].Should().Be(patient.Id);
        created.Value.Should().Be(patient);
    }

    [Test]
    public async Task GetPatientById_Found_ReturnsThem_AndNotFoundWhenNot()
    {
        var patient = _fixture.Create<PatientResponse>();
        var mediator = new RecordingMediator(_ => patient);

        var found = await Controller(mediator).GetPatientById(RecordId);
        var missing = await Controller(new RecordingMediator()).GetPatientById(RecordId);

        mediator.Request.Should().Be(new GetPatientByIdQuery(
            RecordId, MedApp.Application.Common.Teams.TeamScope.For(UserId, [], MedApp.UnitTests.Common.Constants.TestPermissions.Staff)));
        found.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(patient);
        missing.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task SearchPatients_SendsTheSearchAndReturnsThePage()
    {
        var request = _fixture.Create<SearchPatientsRequest>();
        var page = new PagedResponse<PatientResponse>([], 1, 10, 0, 0);
        var mediator = new RecordingMediator(_ => page);
        using var cts = new CancellationTokenSource();

        var result = await Controller(mediator).SearchPatients(request, cts.Token);

        mediator.Request.Should().Be(new SearchPatientsQuery(
            request.Page, request.PageSize, request.Sort, request.Ascending, request.FirstName, request.LastName,
            request.Email, request.DateOfBirthFrom, request.DateOfBirthTo, request.NhsNumber));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(page);
    }

    [Test]
    public async Task GetAllPatients_ReturnsThem()
    {
        IReadOnlyList<PatientResponse> patients = [_fixture.Create<PatientResponse>()];
        var mediator = new RecordingMediator(_ => patients);

        var result = await Controller(mediator).GetAllPatients();

        mediator.Request.Should().BeOfType<GetAllPatientsQuery>();
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(patients);
    }

    [Test]
    public async Task UpdatePatient_Found_ReturnsThem_AndNotFoundWhenNot()
    {
        var request = _fixture.Create<UpdatePatientRequest>();
        var patient = _fixture.Create<PatientResponse>();
        var mediator = new RecordingMediator(_ => patient);

        var updated = await Controller(mediator).UpdatePatient(RecordId, request);
        var missing = await Controller(new RecordingMediator()).UpdatePatient(RecordId, request);

        mediator.Request.Should().BeEquivalentTo(new UpdatePatientCommand(
            RecordId, request.FirstName, request.LastName, request.DateOfBirth, request.Email, request.NhsNumber,
            request.AddressLines, request.Postcode, request.MobilePhone, request.PracticeId, request.ReminderChannel,
            UnlistedPracticeInput.From(request.UnlistedPractice)));
        updated.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(patient);
        missing.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task DeletePatient_Deleted_ReturnsNoContent_AndNotFoundWhenNot()
    {
        var mediator = new RecordingMediator(_ => true);

        var deleted = await Controller(mediator).DeletePatient(RecordId);
        var missing = await Controller(new RecordingMediator(_ => false)).DeletePatient(RecordId);

        mediator.Request.Should().Be(new DeletePatientCommand(RecordId));
        deleted.Should().BeOfType<NoContentResult>();
        missing.Should().BeOfType<NotFoundResult>();
    }
}
