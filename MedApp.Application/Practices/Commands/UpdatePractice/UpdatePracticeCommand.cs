using MedApp.Contracts.Practices.Responses;
using MediatR;

namespace MedApp.Application.Practices.Commands.UpdatePractice;

/// <summary>
/// Replaces a practice's details, from the version the caller saw (<paramref name="ExpectedVersion"/>; 409 if it has
/// changed since). Patients and referrals that have it show the new details.
/// </summary>
public sealed record UpdatePracticeCommand(
    Guid PracticeId,
    string Name,
    string? OdsCode,
    IReadOnlyList<string>? AddressLines,
    string? Postcode,
    string? Phone,
    string? SecureEmail,
    int ExpectedVersion) : IRequest<PracticeResponse>;
