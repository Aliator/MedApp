using MedApp.Application.Referrals.Commands.AcceptReferral;
using MedApp.Application.Referrals.Common;
using System.Reflection;
using FluentAssertions;
using MedApp.Application.Appointments.Commands.CreateAppointment;
using MedApp.Application.Appointments.Commands.MarkAppointmentEnteredInError;
using MedApp.Application.Appointments.Commands.RecordPastAppointment;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Attachments.Commands.DeleteAttachment;
using MedApp.Application.Attachments.Commands.DownloadAttachment;
using MedApp.Application.Attachments.Commands.UploadAttachment;
using MedApp.Application.Patients.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.PatientPathways.Commands.CreatePatientPathway;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Consent.Commands.RecordConsent;
using MedApp.Application.Consent.Common;
using MedApp.Application.Specimens.Commands.RecordSpecimen;
using MedApp.Application.Specimens.Common;
using MediatR;

namespace MedApp.UnitTests.Application.Pathways.Common;

/// <summary>
/// A restricted or anonymised patient's pathways can't be changed because every change loads its pathway through
/// <see cref="PathwayRepositoryExtensions.GetForUpdateOrThrowAsync"/> (or, only to block or cancel it,
/// <see cref="PathwayRepositoryExtensions.GetForPauseOrThrowAsync"/>), which check the patient. These tests read the
/// compiled Application assembly's IL, so a new handler that loads a pathway for update some other way, and so
/// would skip the check, fails here. Appointments are guarded the same way
/// (<see cref="AppointmentRepositoryExtensions.GetForUpdateOrThrowAsync"/>, or, only to cancel one,
/// <see cref="AppointmentRepositoryExtensions.GetForCancelOrThrowAsync"/>).
/// </summary>
[TestFixture]
public sealed class PathwayRestrictionGuardConventionTests
{
    private static readonly Assembly Application = typeof(PathwayMapper).Assembly;

    private static readonly MethodInfo GetForUpdate =
        typeof(IPatientPathwayRepository).GetMethod(nameof(IPatientPathwayRepository.GetForUpdateAsync))!;

    /// <summary>Every overload of the guards: with the team walls (a team's change) or without (system work).</summary>
    private static readonly MethodInfo[] Guards = typeof(PathwayRepositoryExtensions).GetMethods()
        .Where(m => m.Name is nameof(PathwayRepositoryExtensions.GetForUpdateOrThrowAsync)
            or nameof(PathwayRepositoryExtensions.GetForPauseOrThrowAsync)
            or nameof(PathwayRepositoryExtensions.GetForCorrectionOrThrowAsync)
            or nameof(PathwayRepositoryExtensions.GetForStageWorkOrThrowAsync)
            or nameof(PathwayRepositoryExtensions.GetForNotesOrThrowAsync))
        .ToArray();

    private static readonly MethodInfo EnsureCanChange = typeof(PatientRules).GetMethod(nameof(PatientRules.EnsureCanChange))!;

    /// <summary>
    /// Commands that take the pathway repository only to read a pathway, and change nothing about the patient's
    /// record, so a restriction doesn't stop them: a download (audited, but nothing changes).
    /// </summary>
    /// <summary>
    /// Commands that read a pathway without changing it: downloading a file, and withdrawing a walk-in as entered in
    /// error (it reads the stage to ask for its visit again; the appointment goes through its own guard).
    /// </summary>
    private static readonly Type[] ReadOnlyCommands = [typeof(DownloadAttachmentHandler), typeof(MarkAppointmentEnteredInErrorHandler)];

    /// <summary>
    /// Commands that read the pathway (or patient) rather than loading it for update, and check the patient's record
    /// themselves with <see cref="PatientRules.EnsureCanChange"/>: creating a pathway or an appointment, attaching
    /// or deleting a file, recording an outside result, and listing a pathway for an MDT (it adds an MDT item, not a
    /// change to the pathway).
    /// </summary>
    private static readonly Type[] CheckTheirOwnPatient =
    [
        typeof(CreatePatientPathwayHandler), typeof(CreateAppointmentHandler), typeof(UploadAttachmentHandler),
        typeof(DeleteAttachmentHandler), typeof(RecordPastAppointmentHandler), typeof(MedApp.Application.Mdt.Commands.ListForMdt.ListForMdtHandler),
        typeof(MedApp.Application.Results.Commands.RecordExternalResult.RecordExternalResultHandler),
        typeof(MedApp.Application.Mdt.Commands.ListForMdt.ListForMdtHandler),
    ];

    /// <summary>
    /// Recording a specimen reads the pathway (it changes the specimen, not the pathway) and checks the patient's record
    /// through the specimen rules (<see cref="SpecimenWorkflow.WhyCantTake"/>), whose message says to ask an admin.
    /// </summary>
    private static readonly Type[] CheckThroughSpecimenRules = [typeof(RecordSpecimenHandler)];

    private static readonly MethodInfo WhyCantTake = typeof(SpecimenWorkflow).GetMethod(nameof(SpecimenWorkflow.WhyCantTake))!;

    /// <summary>
    /// Recording consent reads the pathway (it changes the consent record, not the pathway) and checks the patient's
    /// record through the consent rules (<see cref="ConsentWorkflow.WhyCantRecord"/>), whose message says to ask an admin.
    /// </summary>
    private static readonly Type[] CheckThroughConsentRules = [typeof(RecordConsentHandler)];

    private static readonly MethodInfo WhyCantRecord = typeof(ConsentWorkflow).GetMethod(nameof(ConsentWorkflow.WhyCantRecord))!;

    /// <summary>
    /// Accepting a referral creates a pathway for the referral's patient: it loads the referral through the triage
    /// guard, which checks the patient's record (<see cref="ReferralRepositoryExtensions.GetForTriageOrThrowAsync"/>).
    /// </summary>
    private static readonly MethodInfo TriageGuard =
        typeof(ReferralRepositoryExtensions).GetMethod(nameof(ReferralRepositoryExtensions.GetForTriageOrThrowAsync))!;

    private static readonly Type[] CreateFromAReferral = [typeof(AcceptReferralHandler)];

    private static readonly MethodInfo GetAppointmentForUpdate =
        typeof(IAppointmentRepository).GetMethod(nameof(IAppointmentRepository.GetForUpdateAsync))!;

    [Test]
    public void GetForUpdateAsync_IsCalledOnlyByTheGuards()
    {
        var callers = Application.GetTypes()
            .Where(type => CallsMadeBy(type, includeNested: false).Any(m => Same(m, GetForUpdate)))
            .Select(TopLevel)
            .Distinct()
            .ToList();

        callers.Should().Equal([typeof(PathwayRepositoryExtensions)],
            "a pathway loaded for update anywhere else would skip the restricted and anonymised check");
    }

    [Test]
    public void EveryPathwayCommandHandler_LoadsThroughAGuard()
    {
        var handlers = Application.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => RequestTypes(type).Any(request => request.Namespace?.Contains(".Commands.") == true))
            .Where(type => type.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IPatientPathwayRepository))))
            .ToList();

        handlers.Should().HaveCountGreaterThanOrEqualTo(10, "the scan must find the pathway commands");
        foreach (var handler in handlers)
        {
            var calls = CallsMadeBy(handler, includeNested: true).ToList();
            var isGuarded = calls.Any(m => Guards.Any(g => Same(m, g)))
                            || (CheckTheirOwnPatient.Contains(handler) && calls.Any(m => Same(m, EnsureCanChange)))
                            || (CheckThroughSpecimenRules.Contains(handler) && calls.Any(m => Same(m, WhyCantTake)))
                            || (CheckThroughConsentRules.Contains(handler) && calls.Any(m => Same(m, WhyCantRecord)))
                            || (CreateFromAReferral.Contains(handler) && calls.Any(m => Same(m, TriageGuard)))
                            || ReadOnlyCommands.Contains(handler);
            isGuarded.Should().BeTrue($"{handler.Name} changes a pathway, so it must load it through a guard that checks the patient");
        }
    }

    [Test]
    public void AppointmentGetForUpdateAsync_IsCalledOnlyByTheGuards()
    {
        var callers = Application.GetTypes()
            .Where(type => CallsMadeBy(type, includeNested: false).Any(m => Same(m, GetAppointmentForUpdate)))
            .Select(TopLevel)
            .Distinct()
            .ToList();

        callers.Should().Equal([typeof(AppointmentRepositoryExtensions)],
            "an appointment loaded for update anywhere else would skip the restricted and anonymised check");
    }

    [Test]
    public void EveryAppointmentCommandHandler_ThatChangesOne_LoadsItThroughAGuard()
    {
        var guards = new[]
        {
            typeof(AppointmentRepositoryExtensions).GetMethod(nameof(AppointmentRepositoryExtensions.GetForUpdateOrThrowAsync))!,
            typeof(AppointmentRepositoryExtensions).GetMethod(nameof(AppointmentRepositoryExtensions.GetForCancelOrThrowAsync))!,
        };
        var handlers = Application.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => RequestTypes(type).Any(request => request.Namespace?.StartsWith("MedApp.Application.Appointments.Commands") == true))
            .ToList();

        handlers.Should().HaveCountGreaterThanOrEqualTo(5, "the scan must find the appointment commands");
        foreach (var handler in handlers)
        {
            var calls = CallsMadeBy(handler, includeNested: true).ToList();
            var isGuarded = calls.Any(m => guards.Any(g => Same(m, g)))
                            || (CheckTheirOwnPatient.Contains(handler) && calls.Any(m => Same(m, EnsureCanChange)));
            isGuarded.Should().BeTrue($"{handler.Name} changes an appointment, so it must check the patient's record first");
        }
    }

    [Test]
    public void TheScan_SeesCallsInsideAsyncMethods()
    {
        // The guards are async, so their call to GetForUpdateAsync is in a compiler-generated state machine.
        CallsMadeBy(typeof(PathwayRepositoryExtensions), includeNested: true).Should().Contain(m => Same(m, GetForUpdate));
    }

    private static IEnumerable<Type> RequestTypes(Type handler) =>
        handler.GetInterfaces()
            .Where(i => i.IsGenericType &&
                        (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) || i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)))
            .Select(i => i.GetGenericArguments()[0]);

    private static Type TopLevel(Type type)
    {
        while (type.DeclaringType is not null)
            type = type.DeclaringType;
        return type;
    }

    private static bool Same(MethodBase called, MethodBase expected) =>
        called.Module == expected.Module && called.MetadataToken == expected.MetadataToken;

    /// <summary>Every method a type's methods call (call and callvirt), and its nested types' too when asked.</summary>
    private static IEnumerable<MethodBase> CallsMadeBy(Type type, bool includeNested)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var types = includeNested ? Nested(type).Prepend(type) : [type];

        foreach (var t in types)
        foreach (var method in t.GetMethods(all).Cast<MethodBase>().Concat(t.GetConstructors(all)))
        {
            var il = method.GetMethodBody()?.GetILAsByteArray();
            if (il is null)
                continue;

            for (var i = 0; i + 4 < il.Length; i++)
            {
                // call is 0x28, callvirt 0x6F; each is followed by a 4-byte method token.
                if (il[i] != 0x28 && il[i] != 0x6F)
                    continue;

                MethodBase? called = null;
                try
                {
                    called = t.Module.ResolveMethod(
                        BitConverter.ToInt32(il, i + 1),
                        t.IsGenericType ? t.GetGenericArguments() : null,
                        method.IsGenericMethod ? method.GetGenericArguments() : null);
                }
                catch (ArgumentException)
                {
                    // Not a method token: the byte was part of another instruction's operand.
                }
                catch (BadImageFormatException)
                {
                }

                if (called is not null)
                    yield return called;
            }
        }
    }

    private static IEnumerable<Type> Nested(Type type) =>
        type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).SelectMany(n => Nested(n).Prepend(n));
}
