using MedApp.Domain.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Authentication;

public sealed class SignInChallengeConfiguration : IEntityTypeConfiguration<SignInChallenge>
{
    public void Configure(EntityTypeBuilder<SignInChallenge> builder)
    {
        builder.ToTable("SignInChallenges");

        builder.HasKey(challenge => challenge.Id);

        builder.Property(challenge => challenge.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(challenge => challenge.TokenHash)
            .IsUnique();

        builder.HasIndex(challenge => challenge.UserId);
        builder.HasIndex(challenge => challenge.ExpiresAtUtc);
    }
}
