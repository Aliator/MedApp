using MedApp.Application.Pathways.Common;
using MedApp.Contracts.Specimens.Responses;
using MediatR;

namespace MedApp.Application.Specimens.Commands.EditSpecimen;

/// <summary>Corrects a specimen's details before it's sent, from the version the caller saw.</summary>
public sealed record EditSpecimenCommand(
    Guid SpecimenId,
    Guid SpecimenTypeId,
    Guid LaboratoryId,
    DateTime TakenAtUtc,
    string? Site,
    string? RequestNotes,
    Guid RequestingUserId,
    int ExpectedVersion,
    PathwayActor Actor)
    : IRequest<SpecimenResponse>;
