using MedApp.Application.Pathways.Common;
using MediatR;

namespace MedApp.Application.Pathways.PatientPathways.Commands.DeletePatientPathway;

/// <summary>Deletes a Cancelled or Archived pathway (admins only). Its audit history is kept.</summary>
public sealed record DeletePatientPathwayCommand(Guid Id, PathwayActor Actor) : IRequest;
