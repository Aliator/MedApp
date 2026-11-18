using FluentAssertions;
using MedApp.Application.DataRequests.Common;
using MedApp.Application.DataRequests.Queries;
using MedApp.Application.DataRequests.Repositories;
using MedApp.Domain.DataRequests;
using MedApp.Domain.Patients;
using static MedApp.UnitTests.Common.Constants.DataRequestTestConstants;

namespace MedApp.UnitTests.Application.DataRequests;

/// <summary>The data requests list's search and sort, the LINQ run in memory as EF runs it in SQL.</summary>
[TestFixture]
public sealed class DataRequestSearchTests
{
    private static DataRequest Request(string first, string last, int receivedDaysAgo, int dueInDays, DataRequestStatus status)
    {
        var patient = new Patient { Id = Guid.NewGuid(), FirstName = first, LastName = last };
        var template = Open(patient);
        return new DataRequest
        {
            Id = template.Id,
            PatientId = patient.Id,
            Patient = patient,
            ReceivedOn = Today.AddDays(-receivedDaysAgo),
            RequestedByPatient = true,
            DueOn = Today.AddDays(dueInDays),
            Status = status,
            CreatedAtUtc = template.CreatedAtUtc,
            CreatedByUserId = template.CreatedByUserId,
            CreatedByUsername = template.CreatedByUsername,
            LastUpdatedUtc = template.LastUpdatedUtc,
            Version = 1
        };
    }

    private static readonly DataRequest Ada = Request("Ada", "Zephyr", 3, 20, DataRequestStatus.Open);
    private static readonly DataRequest Bea = Request("Bea", "Young", 10, 5, DataRequestStatus.Sent);
    private static readonly DataRequest Cal = Request("Cal", "Xu", 1, 30, DataRequestStatus.Open);

    private static List<DataRequest> Run(DataRequestFilter filter) =>
        DataRequestSearch.Order(DataRequestSearch.Apply(new[] { Ada, Bea, Cal }.AsQueryable(), filter), filter).ToList();

    [TestCase("bea young")]
    [TestCase("YOUNG")]
    public void Search_FindsThePatientsName_IgnoringCase(string search)
    {
        Run(new DataRequestFilter(null, null, false, search)).Should().Equal(Bea);
    }

    [Test]
    public void Search_NothingMatching_FindsNothing_AndBlankFindsAll()
    {
        Run(new DataRequestFilter(null, null, false, "nobody")).Should().BeEmpty();
        Run(new DataRequestFilter(null, null, false, "  ")).Should().HaveCount(3);
    }

    [TestCase("patient", true, "Cal", "Bea", "Ada")]
    [TestCase("Patient", false, "Ada", "Bea", "Cal")]
    [TestCase("received", true, "Bea", "Ada", "Cal")]
    [TestCase("due", false, "Cal", "Ada", "Bea")]
    [TestCase("status", false, "Bea", "Ada", "Cal")]
    public void Sort_ByAColumn_InEitherDirection(string sort, bool ascending, params string[] names)
    {
        var order = Run(new DataRequestFilter(null, null, false, null, sort, ascending)).Select(r => r.Patient.FirstName).ToList();

        // Status ties (Ada and Cal are both open) fall back to the ID, so only the first is fixed there.
        if (sort == "status")
            order.First().Should().Be(names[0]);
        else
            order.Should().Equal(names);
    }

    [Test]
    public void Sort_None_IsOpenOnesByDueDateThenTheRestNewestReceived()
    {
        Run(new DataRequestFilter(null, null, false)).Should().Equal(Ada, Cal, Bea);
        Run(new DataRequestFilter(null, null, false, null, "unknown")).Should().Equal(Ada, Cal, Bea);
        DataRequestSearch.Order(new[] { Bea, Cal, Ada }.AsQueryable()).Should().Equal(Ada, Cal, Bea);
    }

    [Test]
    public void Validator_ASearchOver200Characters_IsRefused()
    {
        var validator = new SearchDataRequestsValidator();
        validator.Validate(new SearchDataRequestsQuery(1, 10, null, null, false, Admin, new string('x', 201))).IsValid.Should().BeFalse();
        validator.Validate(new SearchDataRequestsQuery(1, 10, null, null, false, Admin, "Freya", "due", false)).IsValid.Should().BeTrue();
    }
}
