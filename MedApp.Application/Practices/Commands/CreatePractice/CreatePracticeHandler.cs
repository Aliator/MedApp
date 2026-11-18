using MedApp.Application.Common.Exceptions;
using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using MedApp.Contracts.Practices.Responses;
using MedApp.Domain.Practices;
using MediatR;

namespace MedApp.Application.Practices.Commands.CreatePractice;

/// <summary>Adds a practice. Another with the same ODS code is a 409. The directory holds no patient data, so nothing is logged.</summary>
public sealed class CreatePracticeHandler(IPracticeRepository repository, TimeProvider timeProvider)
    : IRequestHandler<CreatePracticeCommand, PracticeResponse>
{
    public async Task<PracticeResponse> Handle(CreatePracticeCommand request, CancellationToken cancellationToken)
    {
        var details = PracticeDetails.From(
            request.Name, request.OdsCode, request.AddressLines, request.Postcode, request.Phone, request.SecureEmail);
        if (details.OdsCode is { } odsCode && await repository.OdsCodeExistsAsync(odsCode, null, cancellationToken))
            throw new ConflictException(PracticeRules.OdsCodeTakenMessage(odsCode));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var practice = new Practice
        {
            Id = Guid.NewGuid(),
            Version = 1,
            CreatedAt = now,
            LastUpdated = now
        };
        details.ApplyTo(practice);

        await repository.AddAsync(practice, cancellationToken);

        return PracticeMapper.ToResponse(practice, PracticeUsage.None);
    }
}
