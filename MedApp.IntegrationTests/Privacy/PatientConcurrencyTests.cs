using FluentAssertions;
using MedApp.Application.Activity.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Domain.Activity;
using MedApp.IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace MedApp.IntegrationTests.Privacy;

/// <summary>
/// A patient's row version stops a save made from a copy read before someone else's save from writing old values
/// back, such as undoing a restriction or an anonymisation: the stale save is a 409 and changes nothing.
/// </summary>
[TestFixture]
public sealed class PatientConcurrencyTests : IntegrationTest
{
    private static ActivityEntry Entry(Guid patientId) => new()
    {
        Id = Guid.NewGuid(),
        AtUtc = DateTime.UtcNow,
        Action = ActivityAction.PatientUpdated,
        Username = "integration-test",
        PatientId = patientId,
        Details = "{}",
    };

    [Test]
    public async Task UpdateAsync_FromACopyReadBeforeAnotherSave_IsAConflict_AndKeepsTheOtherSave()
    {
        var admin = await SignInAsAdminAsync();
        var created = await CreatePatientAsync(admin);

        await using var first = Api.Services.CreateAsyncScope();
        await using var second = Api.Services.CreateAsyncScope();
        var firstRepository = first.ServiceProvider.GetRequiredService<IPatientRepository>();
        var secondRepository = second.ServiceProvider.GetRequiredService<IPatientRepository>();
        var firstCopy = (await firstRepository.GetByIdAsync(created.Id, CancellationToken.None))!;
        var staleCopy = (await secondRepository.GetByIdAsync(created.Id, CancellationToken.None))!;

        firstCopy.RestrictedAtUtc = DateTime.UtcNow;
        firstCopy.RestrictionReason = "Synthetic restriction";
        await firstRepository.UpdateAsync(firstCopy, Entry(created.Id), CancellationToken.None);

        staleCopy.FirstName = "Stale";
        var act = () => secondRepository.UpdateAsync(staleCopy, Entry(created.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.ChangedMeanwhileMessage);
        var stored = await RunInScopeAsync(services =>
            services.GetRequiredService<IPatientRepository>().GetByIdAsync(created.Id, CancellationToken.None));
        stored!.FirstName.Should().Be(created.FirstName);
        stored.RestrictionReason.Should().Be("Synthetic restriction");
        stored.RowVersion.Should().NotBeEmpty();
    }

    [Test]
    public async Task UpdateAsync_FromAFreshCopy_Succeeds()
    {
        var admin = await SignInAsAdminAsync();
        var created = await CreatePatientAsync(admin);

        await RunInScopeAsync(async services =>
        {
            var repository = services.GetRequiredService<IPatientRepository>();
            var patient = (await repository.GetByIdAsync(created.Id, CancellationToken.None))!;
            patient.FirstName = "Fresh";
            await repository.UpdateAsync(patient, Entry(created.Id), CancellationToken.None);
        });

        var stored = await RunInScopeAsync(services =>
            services.GetRequiredService<IPatientRepository>().GetByIdAsync(created.Id, CancellationToken.None));
        stored!.FirstName.Should().Be("Fresh");
    }
}
