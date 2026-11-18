using FluentValidation;
using MedApp.Application.Sites.Common;

namespace MedApp.Application.Sites.Commands.CreateRoom;

public sealed class CreateRoomValidator : AbstractValidator<CreateRoomCommand>
{
    public CreateRoomValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty().WithMessage("Site is required.");
        RuleFor(x => x.Name).Name(SiteLimits.MaxRoomNameLength);
    }
}
