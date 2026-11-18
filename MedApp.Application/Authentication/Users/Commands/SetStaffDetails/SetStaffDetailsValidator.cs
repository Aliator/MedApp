using FluentValidation;
using MedApp.Application.Common.Identity;

namespace MedApp.Application.Authentication.Users.Commands.SetStaffDetails;

public sealed class SetStaffDetailsValidator : AbstractValidator<SetStaffDetailsCommand>
{
    public SetStaffDetailsValidator()
    {
        RuleFor(x => x.Username).NotEmpty().WithMessage("Username must not be empty.");
        RuleFor(x => x.FullName).StaffDetail("Full name");
        RuleFor(x => x.JobTitle).StaffDetail("Job title");
    }
}
