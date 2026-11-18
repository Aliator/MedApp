using MedApp.Application.Common.Exceptions;
using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using MedApp.Contracts.Practices.Responses;
using MediatR;

namespace MedApp.Application.Practices.Commands.UpdatePractice;

/// <summary>
/// Edits a practice (archived or not), from the caller's version (409 if it changed since). A code another practice has
/// is a 409. Nothing is saved if nothing changed.
/// </summary>
public sealed class UpdatePracticeHandler(IPracticeRepository repository, TimeProvider timeProvider)
    : IRequestHandler<UpdatePracticeCommand, PracticeResponse>
{
    public async Task<PracticeResponse> Handle(UpdatePracticeCommand request, CancellationToken cancellationToken)
    {
        var practice = await repository.GetOrThrowAsync(request.PracticeId, cancellationToken);
        PracticeRules.EnsureVersion(practice, request.ExpectedVersion);

        var details = PracticeDetails.From(
            request.Name, request.OdsCode, request.AddressLines, request.Postcode, request.Phone, request.SecureEmail);
        if (details.Matches(practice))
            return await PracticeMapper.ToResponseAsync(practice, repository, cancellationToken);

        if (details.OdsCode is { } odsCode
            && odsCode != practice.OdsCode
            && await repository.OdsCodeExistsAsync(odsCode, practice.Id, cancellationToken))
            throw new ConflictException(PracticeRules.OdsCodeTakenMessage(odsCode));

        var originalVersion = practice.Version;
        details.ApplyTo(practice);
        practice.Version++;
        practice.LastUpdated = timeProvider.GetUtcNow().UtcDateTime;
        await repository.UpdateAsync(practice, originalVersion, cancellationToken);

        return await PracticeMapper.ToResponseAsync(practice, repository, cancellationToken);
    }
}
