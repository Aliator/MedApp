using FluentValidation;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Referrals.Common;
using MedApp.Domain.Referrals;

namespace MedApp.Application.Referrals.Commands.PauseClock;

public sealed class PauseClockValidator : AbstractValidator<PauseClockCommand>
{
    public static readonly string ReasonMessage =
        $"Choose why the clock is paused: one of {string.Join(", ", Enum.GetNames<ClockPauseReason>())}.";

    public const string DetailRequiredMessage = "Say why the clock is paused.";

    public static readonly string ReviewOnMessage =
        $"Choose a review date from tomorrow to {ReferralClockPause.MaxReviewDays} days ahead.";

    public PauseClockValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.ReferralId).NotEmpty().WithMessage("Referral id is required.");

        RuleFor(x => x.Reason)
            .Must(reason => PathwayEnums.TryParse<ClockPauseReason>(reason, out _))
            .WithMessage(ReasonMessage);

        RuleFor(x => x.Detail)
            .MaximumLength(ReferralClockPause.MaxDetailLength)
            .WithMessage($"The detail must not exceed {ReferralClockPause.MaxDetailLength} characters.");

        RuleFor(x => x.Detail)
            .NotEmpty()
            .When(x => PathwayEnums.TryParse<ClockPauseReason>(x.Reason, out var reason) && reason == ClockPauseReason.Other)
            .WithMessage(DetailRequiredMessage);

        RuleFor(x => x.ReviewOn)
            .Must(reviewOn =>
            {
                var today = DueDates.ToUkDate(timeProvider.GetUtcNow().UtcDateTime);
                return reviewOn > today && reviewOn <= WaitingClock.LatestReviewOn(today);
            })
            .WithMessage(ReviewOnMessage);

        RuleFor(x => x.ExpectedVersion).GreaterThan(0).WithMessage("Give the version of the referral you changed.");
    }
}
