using MedApp.Contracts.Practices.Responses;
using MediatR;

namespace MedApp.Application.Practices.Commands.SetPracticeArchived;

/// <summary>
/// Archives a practice (it can't be chosen for a patient or referral any more; those that have it keep it) or restores
/// it, from the version the caller saw (<paramref name="ExpectedVersion"/>; 409 if it has changed since).
/// </summary>
public sealed record SetPracticeArchivedCommand(Guid PracticeId, bool IsArchived, int ExpectedVersion)
    : IRequest<PracticeResponse>;
