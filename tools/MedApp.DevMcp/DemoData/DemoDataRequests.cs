using MediatR;
using MedApp.Application.DataRequests.Commands;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.DataRequests;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// One open data request (subject access) for the Data Requests page, through the app's commands: received nine days
/// ago from a patient's daughter (an invented name) with the patient's written consent, logged by the clinical lead,
/// and her identity checked from documents two days later. Still open, with its one-month deadline ahead.
/// </summary>
public static class DemoDataRequests
{
    public static async Task<string?> CreateAsync(SeedServices services, DemoWorld world, IReadOnlyList<PatientSeed> patients, CancellationToken ct)
    {
        var now = world.Timeline.NowUtc;
        var lead = world.Lead;
        var loggedAt = DemoTimeline.WorkingMoment(now.AddDays(-9), 10, 40);
        var checkedAt = DemoTimeline.WorkingMoment(now.AddDays(-7), 11, 15);
        if (checkedAt >= now)
            return null;

        // An adult with a pathway, whose record isn't restricted or anonymised.
        var patient = patients.FirstOrDefault(p => p.Role == PatientRole.Active && p.Id != Guid.Empty && !p.Restricted
                                                   && p.AnonymisedUtc is null && p.CreatedUtc < loggedAt.AddDays(-30) && p.AgeAt(now) >= 60
                                                   && world.Pathways.Any(x => x.PatientId == p.Id));
        if (patient is null)
            return null;

        Task<T> Send<T>(DateTime at, IRequest<T> request)
        {
            services.At(at, lead.ActivityActor);
            return services.Run(sp => Guards.Validated(() => sp.GetRequiredService<IMediator>().Send(request, ct)));
        }

        var request = await Send(loggedAt, new CreateDataRequestCommand(patient.Id, DueDates.ToUkDate(loggedAt).AddDays(-1), false,
            "Robin Ashdown-Vale", "Daughter, with the patient's written consent", lead.ActorAt(loggedAt)));
        await Send(checkedAt, new RecordIdentityCheckCommand(request.Id, DueDates.ToUkDate(checkedAt), nameof(IdentityCheckMethod.Documents),
            "Passport and the patient's signed consent form seen at the front desk.", request.Version, lead.ActorAt(checkedAt)));
        return patient.FullName;
    }
}
