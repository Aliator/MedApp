using MedApp.Contracts.Practices.Responses;
using MediatR;

namespace MedApp.Application.Practices.Commands.CreatePractice;

/// <summary>Adds a GP practice to the directory: a name, and optionally its ODS code (unique), address, phone and secure email.</summary>
public sealed record CreatePracticeCommand(
    string Name,
    string? OdsCode,
    IReadOnlyList<string>? AddressLines,
    string? Postcode,
    string? Phone,
    string? SecureEmail) : IRequest<PracticeResponse>;
