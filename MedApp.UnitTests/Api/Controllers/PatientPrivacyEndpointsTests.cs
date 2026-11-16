using AutoFixture;
using FluentAssertions;
using MedApp.API.Controllers;
using MedApp.Application.Patients.Commands.AnonymisePatient;
using MedApp.Application.Patients.Commands.KeepPatientRecord;
using MedApp.Application.Patients.Commands.SetPatientFlag;
using MedApp.Application.Patients.Queries.ExportPatient;
using MedApp.Application.Patients.Queries.SearchRetentionReview;
using MedApp.Contracts.Common;
using MedApp.Contracts.Patients.Requests;
using MedApp.Contracts.Patients.Responses;
using MedApp.UnitTests.Common.Fixtures;
using Microsoft.AspNetCore.Mvc;
using static MedApp.UnitTests.Common.Constants.ApiTestConstants;

namespace MedApp.UnitTests.Api.Controllers;

/// <summary>The patient endpoints for the privacy features (admins only): export, flags, anonymising and retention.</summary>
[TestFixture]
public sealed class PatientPrivacyEndpointsTests
{
    private IFixture _fixture = null!;

    [SetUp]
    public void SetUp() => _fixture = AutoFixtureFactory.Create();

    private static PatientsController Controller(RecordingMediator mediator) =>
        new PatientsController(mediator).WithRequest(SignedIn("Admin"));

    [Test]
    public async Task ExportPatient_SendsTheQuery_AndIsNeverCached()
    {
        var export = new PatientExportResponse();
        var mediator = new RecordingMediator(_ => export);
        var controller = Controller(mediator);
        using var cts = new CancellationTokenSource();

        var result = await controller.ExportPatient(RecordId, cts.Token);

        mediator.Request.Should().Be(new ExportPatientQuery(RecordId));
        mediator.Tokens.Single().Should().Be(cts.Token);
        controller.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(export);
    }

    [TestCase(true, "Why")]
    [TestCase(false, null)]
    public async Task SetRestriction_SendsTheRestrictedFlag(bool set, string? reason)
    {
        var patient = _fixture.Create<PatientResponse>();
        var mediator = new RecordingMediator(_ => patient);
        using var cts = new CancellationTokenSource();

        var result = await Controller(mediator).SetRestriction(RecordId, new SetPatientFlagRequest { Set = set, Reason = reason }, cts.Token);

        mediator.Request.Should().Be(new SetPatientFlagCommand(RecordId, PatientFlag.Restricted, set, reason));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(patient);
    }

    [Test]
    public async Task SetDispute_SendsTheDisputedFlag()
    {
        var patient = _fixture.Create<PatientResponse>();
        var mediator = new RecordingMediator(_ => patient);

        var result = await Controller(mediator).SetDispute(RecordId, new SetPatientFlagRequest { Set = true, Reason = "Why" }, CancellationToken.None);

        mediator.Request.Should().Be(new SetPatientFlagCommand(RecordId, PatientFlag.Disputed, true, "Why"));
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(patient);
    }

    [Test]
    public async Task AnonymisePatient_SendsTheReasonAndConfirmation()
    {
        var patient = _fixture.Create<PatientResponse>();
        var mediator = new RecordingMediator(_ => patient);
        using var cts = new CancellationTokenSource();

        var result = await Controller(mediator).AnonymisePatient(
            RecordId, new AnonymisePatientRequest { Reason = "Why", Confirmation = "Test Patient" }, cts.Token);

        mediator.Request.Should().Be(new AnonymisePatientCommand(RecordId, "Why", "Test Patient"));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(patient);
    }

    [Test]
    public async Task AnonymisePatient_MissingFields_SendsThemEmpty()
    {
        var mediator = new RecordingMediator();

        await Controller(mediator).AnonymisePatient(RecordId, new AnonymisePatientRequest { Reason = null!, Confirmation = null! }, CancellationToken.None);

        mediator.Request.Should().Be(new AnonymisePatientCommand(RecordId, string.Empty, string.Empty));
    }

    [Test]
    public async Task SearchRetentionReview_SendsThePaging_AndReturnsThePage()
    {
        var page = new PagedResponse<RetentionReviewItemResponse>([], 2, 5, 0, 0);
        var mediator = new RecordingMediator(_ => page);
        using var cts = new CancellationTokenSource();

        var result = await Controller(mediator).SearchRetentionReview(
            new SearchRetentionReviewRequest { Page = 2, PageSize = 5, IncludeKept = true }, cts.Token);

        mediator.Request.Should().Be(new SearchRetentionReviewQuery(2, 5, true));
        mediator.Tokens.Single().Should().Be(cts.Token);
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(page);
    }

    [Test]
    public async Task KeepPatientRecord_SendsTheDecision_AndReturnsNoContent()
    {
        var mediator = new RecordingMediator();
        var until = new DateOnly(2027, 6, 1);
        using var cts = new CancellationTokenSource();

        var result = await Controller(mediator).KeepPatientRecord(
            RecordId, new KeepPatientRecordRequest { KeepUntil = until, Reason = "Legal hold" }, cts.Token);
        await Controller(mediator).KeepPatientRecord(
            RecordId, new KeepPatientRecordRequest { KeepUntil = until, Reason = null! }, cts.Token);

        mediator.Sent.Should().Equal(
            new KeepPatientRecordCommand(RecordId, until, "Legal hold"),
            new KeepPatientRecordCommand(RecordId, until, string.Empty));
        mediator.Tokens.Should().AllSatisfy(t => t.Should().Be(cts.Token));
        result.Should().BeOfType<NoContentResult>();
    }
}
