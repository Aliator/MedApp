using System.Reflection;
using FluentAssertions;
using MedApp.Application.Attachments.Repositories;
using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Teams;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Domain.Pathways.PatientPathways;
using MediatR;

namespace MedApp.UnitTests.Application.Common.Teams;

/// <summary>
/// The team walls are enforced in code, not by convention (decisions 192 and 203 of <c>docs/multi-clinic-plan.md</c>):
/// <list type="bullet">
/// <item>every list of pathways the repository gives takes a <see cref="TeamScope"/>, so a forgotten filter doesn't
/// compile (the unscoped ones are named here, each for system work or a check that follows);</item>
/// <item>every pathway command loads its pathway through a guard with the team walls;</item>
/// <item>every query of the walled areas carries the viewer's scope.</item>
/// </list>
/// MC5 adds the other areas' repositories (referrals, appointments, letters, results, consent, MDT, samples).
/// </summary>
[TestFixture]
public sealed class TeamScopeConventionTests
{
    private static readonly Assembly Application = typeof(PathwayMapper).Assembly;

    /// <summary>
    /// The repository methods that give pathways without a scope, and why: a single pathway loaded by ID, which the
    /// handler then checks with <see cref="ITeamBoundary"/>; and a patient's every pathway, for system work and the
    /// other-team summaries (which show none of a hidden pathway's details).
    /// </summary>
    private static readonly string[] UnscopedOnPurpose =
    [
        nameof(IPatientPathwayRepository.GetByIdAsync),
        nameof(IPatientPathwayRepository.GetForUpdateAsync),
        nameof(IPatientPathwayRepository.GetByPatientAsync),
    ];

    [Test]
    public void PathwayRepository_EveryListOfPathways_TakesAScope()
    {
        var lists = typeof(IPatientPathwayRepository).GetMethods()
            .Where(m => GivesSeveral(m.ReturnType))
            .ToList();

        lists.Should().NotBeEmpty();
        foreach (var method in lists)
        {
            var scoped = method.GetParameters().Any(p => p.ParameterType == typeof(TeamScope));
            (scoped || (UnscopedOnPurpose.Contains(method.Name) && HasScopedOverload(method)))
                .Should().BeTrue($"{method.Name} lists pathways, so it must take the viewer's TeamScope");
        }

        typeof(IPatientPathwayRepository).GetMethods()
            .Where(m => m.Name == nameof(IPatientPathwayRepository.GetByPatientAsync))
            .Should().Contain(m => m.GetParameters().Any(p => p.ParameterType == typeof(TeamScope)),
                "a patient's pathways for a viewer come through the scoped overload");
    }

    [Test]
    public void EveryPathwayCommand_LoadsItsPathwayThroughAGuardWithTheTeamWalls()
    {
        var guards = typeof(PathwayRepositoryExtensions).GetMethods()
            .Where(m => m.Name is nameof(PathwayRepositoryExtensions.GetForUpdateOrThrowAsync)
                or nameof(PathwayRepositoryExtensions.GetForPauseOrThrowAsync)
                or nameof(PathwayRepositoryExtensions.GetForCorrectionOrThrowAsync)
                or nameof(PathwayRepositoryExtensions.GetForStageWorkOrThrowAsync)
                or nameof(PathwayRepositoryExtensions.GetForNotesOrThrowAsync))
            .ToList();
        var walled = guards.Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(ITeamBoundary))).ToList();
        var unwalled = guards.Except(walled).ToList();

        var handlers = Application.GetTypes()
            .Where(t => t.Namespace?.StartsWith("MedApp.Application.Pathways.", StringComparison.Ordinal) == true)
            .Where(t => t is { IsClass: true, IsAbstract: false } && Handles(t))
            .Where(t => CallsMadeBy(t).Any(m => guards.Any(g => Same(m, g))))
            .ToList();

        handlers.Should().HaveCountGreaterThanOrEqualTo(15, "the scan must find the pathway commands");
        foreach (var handler in handlers)
        {
            var calls = CallsMadeBy(handler).ToList();
            calls.Should().Contain(m => walled.Any(g => Same(m, g)), $"{handler.Name} changes a team's pathway");
            calls.Should().NotContain(m => unwalled.Any(g => Same(m, g)), $"{handler.Name} must not skip the team walls");
        }
    }

    [Test]
    public void EveryQueryOfTheWalledAreas_CarriesTheViewersScope()
    {
        var queries = Application.GetTypes()
            .Where(t => t.Namespace is { } ns
                        && (ns.StartsWith("MedApp.Application.Pathways.PatientPathways.Queries.", StringComparison.Ordinal)
                            || ns.StartsWith("MedApp.Application.Pathways.Timeline.Queries.", StringComparison.Ordinal)
                            || ns.StartsWith("MedApp.Application.Attachments.Queries.", StringComparison.Ordinal)
                            || ns == "MedApp.Application.Patients.Queries.GetPatientById"))
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)))
            .ToList();

        queries.Should().HaveCountGreaterThanOrEqualTo(8);
        foreach (var query in queries)
            query.GetProperty("Scope")?.PropertyType.Should().Be(typeof(TeamScope), $"{query.Name} reads team data");
        queries.Where(q => q.GetProperty("Scope") is null).Should().BeEmpty("every query of a walled area takes the viewer's scope");
    }

    [Test]
    public void AttachmentRepository_ItsListTakesTheQuery_WhichCarriesTheScope() =>
        typeof(IAttachmentRepository).GetMethod(nameof(IAttachmentRepository.ListAsync))!.GetParameters()[0].ParameterType
            .GetProperty("Scope")!.PropertyType.Should().Be(typeof(TeamScope));

    private static bool GivesSeveral(Type returnType)
    {
        var inner = returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)
            ? returnType.GetGenericArguments()[0]
            : returnType;
        if (!inner.IsGenericType)
            return false;
        var definition = inner.GetGenericTypeDefinition();
        return (definition == typeof(IReadOnlyList<>) || definition == typeof(PagedResult<>))
               && inner.GetGenericArguments()[0] is var item
               && (item == typeof(PatientPathway) || item == typeof(PathwayLabel));
    }

    private static bool HasScopedOverload(MethodInfo method) =>
        method.DeclaringType!.GetMethods().Any(m => m.Name == method.Name
                                                    && m.GetParameters().Any(p => p.ParameterType == typeof(TeamScope)));

    private static bool Handles(Type type) =>
        type.GetInterfaces().Any(i => i.IsGenericType
                                      && (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
                                          || i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)));

    private static bool Same(MethodBase called, MethodBase expected) =>
        called.Module == expected.Module && called.MetadataToken == expected.MetadataToken;

    /// <summary>Every method a type and its nested types (async state machines) call.</summary>
    private static IEnumerable<MethodBase> CallsMadeBy(Type type)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        foreach (var t in Nested(type).Prepend(type))
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
