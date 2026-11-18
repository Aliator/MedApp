using MedApp.Application.Common.Identity;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.DataRequests;
using MedApp.Domain.Patients;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic data requests and the people handling them.</summary>
public static class DataRequestTestConstants
{
    /// <summary>The UK date of <see cref="PathwaysTestConstants.Now"/>.</summary>
    public static readonly DateOnly Today = new(2026, 6, 15);

    public const string RequesterName = "Alex Synthetic";
    public const string Relationship = "Synthetic attorney";
    public const string WithheldReason = "Synthetic serious harm reason.";

    /// <summary>An admin: ManagePrivacy, no SignOffResults.</summary>
    public static readonly PathwayActor Admin = new(
        Guid.Parse("0d4f0000-0000-0000-0000-000000000001"), "admin", Permissions.Admin);

    /// <summary>A clinician: SignOffResults, no ManagePrivacy, so no data requests.</summary>
    public static readonly PathwayActor Clinician = new(
        Guid.Parse("0d4f0000-0000-0000-0000-000000000002"), "k.ashford",
        new HashSet<Permission> { Permission.UseApp, Permission.ViewPatients, Permission.SignOffResults });

    /// <summary>Neither: a booking clerk.</summary>
    public static readonly PathwayActor Clerk = new(
        Guid.Parse("0d4f0000-0000-0000-0000-000000000003"), "b.clerk",
        new HashSet<Permission> { Permission.UseApp, Permission.ViewPatients, Permission.BookAppointments });

    /// <summary>An open request, received today, on someone's behalf, not checked or reviewed.</summary>
    public static DataRequest Open(Patient? patient = null)
    {
        patient ??= PatientsTestConstants.CreateValidPatient();
        return new DataRequest
        {
            Id = Guid.NewGuid(),
            PatientId = patient.Id,
            Patient = patient,
            ReceivedOn = Today,
            RequestedByPatient = false,
            RequesterName = RequesterName,
            RequesterRelationship = Relationship,
            DueOn = Today.AddMonths(1),
            Status = DataRequestStatus.Open,
            CreatedAtUtc = PathwaysTestConstants.Now,
            CreatedByUserId = Admin.UserId,
            CreatedByUsername = Admin.Username,
            LastUpdatedUtc = PathwaysTestConstants.Now,
            Version = 1
        };
    }

    /// <summary>An open request with its identity checked and reviewed: its pack can be made.</summary>
    public static DataRequest Ready(Patient? patient = null)
    {
        var request = Open(patient);
        request.IdentityCheckedOn = Today;
        request.IdentityCheckMethod = IdentityCheckMethod.Documents;
        request.IdentityCheckedByUserId = Admin.UserId;
        request.IdentityCheckedByUsername = Admin.Username;
        request.ReviewedAtUtc = PathwaysTestConstants.Now;
        request.ReviewedByUserId = Clinician.UserId;
        request.ReviewedByUsername = Clinician.Username;
        return request;
    }
}
