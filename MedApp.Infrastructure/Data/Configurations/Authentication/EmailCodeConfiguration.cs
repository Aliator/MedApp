using MedApp.Domain.Authentication;
using MedApp.Infrastructure.Common.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Authentication;

/// <summary>
/// Emailed two-factor codes (only their hashes). A sign-in's code goes with its challenge, and every code with its
/// account (cascading deletes), so nothing outlives what it was for; the rest expire within minutes and are deleted when
/// the next code is sent.
/// </summary>
public sealed class EmailCodeConfiguration : IEntityTypeConfiguration<EmailCode>
{
    public void Configure(EntityTypeBuilder<EmailCode> builder)
    {
        builder.ToTable("EmailCodes");

        builder.HasKey(code => code.Id);

        builder.Property(code => code.CodeHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasOne<SignInChallenge>()
            .WithMany()
            .HasForeignKey(code => code.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(code => code.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(code => new { code.UserId, code.Purpose });
        builder.HasIndex(code => code.ExpiresAtUtc);
    }
}
