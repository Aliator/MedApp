using MedApp.Domain.Appointments;
using MedApp.Domain.Monitoring;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.Domain.Results;
using MedApp.Domain.Specimens;

namespace MedApp.Application.Monitoring.ServiceAlerts;

/// <summary>
/// What the service alerts read: the database's tables in the repository (so the LINQ runs as SQL), or lists in tests.
/// Read-only.
/// </summary>
public sealed record ServiceAlertSources(
    IQueryable<Referral> Referrals,
    IQueryable<ReferralClockPause> Pauses,
    IQueryable<Result> Results,
    IQueryable<Appointment> Appointments,
    IQueryable<Specimen> Specimens,
    IQueryable<PatientPathwayStage> Stages,
    IQueryable<MonitoringAlertSent> Sent,
    IQueryable<MedApp.Domain.Lab.LabCase>? LabCases = null);
