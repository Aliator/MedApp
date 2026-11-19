using System.Reflection;
using FluentAssertions;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MediatR;

namespace MedApp.UnitTests.Application.Common.Teams;

/// <summary>
/// MC5's areas (decision 203 of <c>docs/multi-clinic-plan.md</c>): every query of referrals, letters, results, consent,
/// MDT items, appointments and their messages carries the viewer's team walls, as a <see cref="TeamScope"/> or an actor that holds one,
/// so a forgotten wall doesn't compile. Setup lists (letter templates, merge fields) aren't team data.
/// </summary>
[TestFixture]
public sealed class WalledAreaQueriesConventionTests
{
    private static readonly string[] Areas =
    [
        "MedApp.Application.Referrals.Queries",
        "MedApp.Application.Letters.Queries.GetLetter",
        "MedApp.Application.Letters.Queries.SearchLetters",
        "MedApp.Application.Results.Queries",
        "MedApp.Application.Consent.Queries",
        "MedApp.Application.Mdt.Queries",
        "MedApp.Application.Appointments.Queries",
        "MedApp.Application.PatientMessages.Queries.GetAppointmentMessages",
        "MedApp.Application.Consults.Queries",
    ];

    private static readonly Type[] Carriers = [typeof(TeamScope), typeof(PathwayActor), typeof(CalendarActor)];

    [Test]
    public void EveryQueryOfMc5sAreas_CarriesTheViewersTeamWalls()
    {
        var queries = typeof(PathwayMapper).Assembly.GetTypes()
            .Where(t => t.Namespace is { } ns && Areas.Any(a => ns == a || ns.StartsWith(a + ".", StringComparison.Ordinal)))
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
            .ToList();

        queries.Should().HaveCountGreaterThanOrEqualTo(10, "the scan must find the walled areas' queries");
        foreach (var query in queries)
            query.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Should().Contain(p => Carriers.Contains(p.PropertyType), $"{query.Name} reads team data, so it carries the viewer's scope");
    }
}
