using MedApp.Application.Procedures.Common;
using MediatR;

namespace MedApp.Application.Attachments.Commands.DeleteAttachment;

/// <summary>
/// Deletes a file: hides it (a soft delete, audited), leaving its bytes for the purge after the retention window.
/// Staff may delete their own files while what they're attached to allows changes; admins may delete any, at any time
/// (to remove a file attached to the wrong patient from a finished stage, say), except while the patient's record is
/// restricted or anonymised, when nobody can.
/// </summary>
public sealed record DeleteAttachmentCommand(Guid Id, ProcedureActor Actor, bool IsAdmin) : IRequest;
