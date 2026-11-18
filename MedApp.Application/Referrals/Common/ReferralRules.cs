using FluentValidation;
using MedApp.Application.Pathways.Common;
using MedApp.Domain.Referrals;

namespace MedApp.Application.Referrals.Common;

/// <summary>The checks registering and editing a referral share, for their validators.</summary>
public static class ReferralRules
{
    /// <summary>How far back a referral's received date may be: a year.</summary>
    public const int MaxDaysBack = 365;

    public const string ReceivedInFutureMessage = "The date received can't be in the future.";

    public static readonly string ReceivedTooLongAgoMessage =
        $"The date received can't be more than {MaxDaysBack} days ago. Check the date on the letter.";

    public static readonly string OdsCodeMessage =
        $"The ODS code must be {OdsCodes.MinLength} to {OdsCodes.MaxLength} letters and digits (a GP practice's is a letter and five digits, e.g. A12345).";

    public static readonly string DetailRequiredMessage = "Say what happened (who withdrew it, or what was wrong).";

    /// <summary>Whether a received date is allowed today (UK): not in the future, at most a year back.</summary>
    public static string? WhyReceivedOnNotAllowed(DateOnly receivedOn, DateOnly today) =>
        receivedOn > today ? ReceivedInFutureMessage
        : receivedOn < today.AddDays(-MaxDaysBack) ? ReceivedTooLongAgoMessage
        : null;

    public static void SpecialtyId<T>(this IRuleBuilder<T, Guid> rule) =>
        rule.NotEmpty().WithMessage("Choose the specialty asked for.");

    public static void ReceivedOn<T>(this IRuleBuilder<T, DateOnly> rule, TimeProvider timeProvider) =>
        rule.Custom((receivedOn, context) =>
        {
            if (WhyReceivedOnNotAllowed(receivedOn, DueDates.ToUkDate(timeProvider.GetUtcNow().UtcDateTime)) is { } why)
                context.AddFailure(why);
        });

    public static void ReferrerPractice<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(text => !string.IsNullOrWhiteSpace(text))
            .WithMessage("Give the referring practice.")
            .Must(text => text is null || text.Trim().Length <= Referral.MaxPracticeLength)
            .WithMessage($"The referring practice must be at most {Referral.MaxPracticeLength} characters.");

    public static void ReferrerClinician<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(text => text is null || text.Trim().Length <= Referral.MaxClinicianLength)
            .WithMessage($"The referring clinician must be at most {Referral.MaxClinicianLength} characters.");

    public static void ReferrerOdsCode<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(OdsCodes.IsValidOrEmpty).WithMessage(OdsCodeMessage);

    public static void RequestedPriority<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(PathwayEnums.IsPriority)
            .WithMessage($"The requested priority must be one of: {PathwayEnums.PriorityNames}.");

    public static void Reason<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(text => !string.IsNullOrWhiteSpace(text))
            .WithMessage("Give the reason for referral, from the letter.")
            .Must(text => text is null || text.Trim().Length <= Referral.MaxReasonLength)
            .WithMessage($"The reason for referral must be at most {Referral.MaxReasonLength:N0} characters.");

    public static void Detail<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(text => !string.IsNullOrWhiteSpace(text))
            .WithMessage(DetailRequiredMessage)
            .Must(text => text is null || text.Trim().Length <= Referral.MaxDetailLength)
            .WithMessage($"The detail must be at most {Referral.MaxDetailLength} characters.");
}
