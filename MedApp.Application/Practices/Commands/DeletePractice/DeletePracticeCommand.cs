using MediatR;

namespace MedApp.Application.Practices.Commands.DeletePractice;

/// <summary>Deletes a practice no patient or referral has; one that's in use is archived instead (409).</summary>
public sealed record DeletePracticeCommand(Guid PracticeId) : IRequest;
