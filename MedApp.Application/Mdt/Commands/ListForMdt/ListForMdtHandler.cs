using MedApp.Application.Common.Exceptions;
using MedApp.Application.Mdt.Common;
using MedApp.Application.Mdt.Repositories;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Patients.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Contracts.Mdt.Responses;
using MedApp.Domain.Mdt;
using MediatR;

namespace MedApp.Application.Mdt.Commands.ListForMdt;

public sealed class ListForMdtHandler(
    IMdtItemRepository repository,
    IPatientPathwayRepository pathways,
    ISpecialtyRepository specialties,
    TimeProvider timeProvider)
    : IRequestHandler<ListForMdtCommand, MdtItemResponse>
{
    public async Task<MdtItemResponse> Handle(ListForMdtCommand request, CancellationToken cancellationToken)
    {
        var pathway = await pathways.GetByIdAsync(request.PathwayId, cancellationToken)
                      ?? throw new NotFoundException($"Pathway '{request.PathwayId}' was not found.");

        // New work: refused for a restricted or anonymised record (decision 75).
        PatientRules.EnsureCanChange(pathway.Patient);
        MdtRules.EnsurePathwayCanBeListed(pathway);

        var meeting = await repository.GetMeetingAsync(request.MeetingId, cancellationToken)
                      ?? throw new NotFoundException($"Meeting '{request.MeetingId}' was not found.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        MdtRules.EnsureCanListFor(meeting, now);

        if (await repository.IsOpenOnMeetingAsync(pathway.Id, meeting.Id, cancellationToken))
            throw new ConflictException(MdtRules.AlreadyListedMessage);

        var specialty = await specialties.GetAsync(meeting.SpecialtyId!.Value, cancellationToken);
        var item = new MdtItem
        {
            Id = Guid.NewGuid(),
            PatientId = pathway.PatientId,
            PathwayId = pathway.Id,
            SpecialtyId = meeting.SpecialtyId.Value,
            MeetingId = meeting.Id,
            Question = request.Question.Trim(),
            Status = MdtItemStatus.Listed,
            ListedByUserId = request.Actor.UserId,
            ListedByUsername = request.Actor.Username,
            ListedAtUtc = now,
            Version = 1
        };

        await repository.AddAsync(
            item, MdtAudit.Listed(item, pathway, specialty?.Name ?? string.Empty, meeting, request.Actor, now), cancellationToken);

        var saved = await repository.GetAsync(item.Id, cancellationToken)
                    ?? throw new NotFoundException($"MDT item '{item.Id}' was not found.");
        return MdtMapper.ToResponse(saved, request.Actor, now);
    }
}
