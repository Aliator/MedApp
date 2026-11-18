using FluentValidation;
using MedApp.Application.Sites.Common;

namespace MedApp.Application.Sites.Commands.UpdateRoom;

public sealed class UpdateRoomValidator : AbstractValidator<UpdateRoomCommand>
{
    public UpdateRoomValidator()
    {
        RuleFor(x => x.RoomId).NotEmpty().WithMessage("Room id is required.");
        RuleFor(x => x.Name).Name(SiteLimits.MaxRoomNameLength);
    }
}
