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
using MedApp.Application.Procedures.Common;
using MedApp.Application.Procedures.PatientProcedures.Commands.CreatePatientProcedure;
using MedApp.Application.Procedures.Repositories;
using MediatR;

namespace MedApp.UnitTests.Application.Procedures.Common;

/// <summary>
/// A restricted or anonymised patient's procedures can't be changed because every change loads its procedure through
/// <see cref="ProcedureRepositoryExtensions.GetForUpdateOrThrowAsync"/> (or, only to block or cancel it,
/// <see cref="ProcedureRepositoryExtensions.GetForPauseOrThrowAsync"/>), which check the patient. These tests read the
/// compiled Application assembly's IL, so a new handler that loads a procedure for update some other way, and so
/// would skip the check, fails here. Appointments are guarded the same way
/// (<see cref="AppointmentRepositoryExtensions.GetForUpdateOrThrowAsync"/>, or, only to cancel one,
/// <see cref="AppointmentRepositoryExtensions.GetForCancelOrThrowAsync"/>).
/// </summary>
[TestFixture]
public sealed class ProcedureRestrictionGuardConventionTests
{
    private static readonly Assembly Application = typeof(ProcedureMapper).Assembly;

    private static readonly MethodInfo GetForUpdate =
        typeof(IPatientProcedureRepository).GetMethod(nameof(IPatientProcedureRepository.GetForUpdateAsync))!;

    private static readonly MethodInfo[] Guards =
    [
        typeof(ProcedureRepositoryExtensions).GetMethod(nameof(ProcedureRepositoryExtensions.GetForUpdateOrThrowAsync))!,
        typeof(ProcedureRepositoryExtensions).GetMethod(nameof(ProcedureRepositoryExtensions.GetForPauseOrThrowAsync))!,
    ];

    private static readonly MethodInfo EnsureCanChange = typeof(PatientRules).GetMethod(nameof(PatientRules.EnsureCanChange))!;

    /// <summary>
    /// Commands that take the procedure repository only to read a procedure, and change nothing about the patient's
    /// record, so a restriction doesn't stop them: a download (audited, but nothing changes).
    /// </summary>
    /// <summary>
    /// Commands that read a procedure without changing it: downloading a file, and withdrawing a walk-in as entered in
    /// error (it reads the stage to ask for its visit again; the appointment goes through its own guard).
    /// </summary>
    private static readonly Type[] ReadOnlyCommands = [typeof(DownloadAttachmentHandler), typeof(MarkAppointmentEnteredInErrorHandler)];

    /// <summary>
    /// Commands that read the procedure (or patient) rather than loading it for update, and check the patient's record
    /// themselves with <see cref="PatientRules.EnsureCanChange"/>: creating a procedure or an appointment, and attaching
    /// or deleting a file.
    /// </summary>
    private static readonly Type[] CheckTheirOwnPatient =
    [
        typeof(CreatePatientProcedureHandler), typeof(CreateAppointmentHandler), typeof(UploadAttachmentHandler),
        typeof(DeleteAttachmentHandler), typeof(RecordPastAppointmentHandler),
    ];

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

        callers.Should().Equal([typeof(ProcedureRepositoryExtensions)],
            "a procedure loaded for update anywhere else would skip the restricted and anonymised check");
    }

    [Test]
    public void EveryProcedureCommandHandler_LoadsThroughAGuard()
    {
        var handlers = Application.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => RequestTypes(type).Any(request => request.Namespace?.Contains(".Commands.") == true))
            .Where(type => type.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IPatientProcedureRepository))))
            .ToList();

        handlers.Should().HaveCountGreaterThanOrEqualTo(10, "the scan must find the procedure commands");
        foreach (var handler in handlers)
        {
            var calls = CallsMadeBy(handler, includeNested: true).ToList();
            var isGuarded = calls.Any(m => Guards.Any(g => Same(m, g)))
                            || (CheckTheirOwnPatient.Contains(handler) && calls.Any(m => Same(m, EnsureCanChange)))
                            || ReadOnlyCommands.Contains(handler);
            isGuarded.Should().BeTrue($"{handler.Name} changes a procedure, so it must load it through a guard that checks the patient");
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
        CallsMadeBy(typeof(ProcedureRepositoryExtensions), includeNested: true).Should().Contain(m => Same(m, GetForUpdate));
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
