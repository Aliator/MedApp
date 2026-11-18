using MedApp.Domain.Referrals;

namespace MedApp.Application.Referrals.Common;

/// <summary>A referral in a list, with its patient and team loaded, and whether a file (its letter) is attached.</summary>
public sealed record ReferralRow(Referral Referral, bool HasLetter);
