using MedApp.Domain.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations;

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("UserSessions");

        builder.HasKey(session => session.Id);

        builder.Property(session => session.UserId)
            .IsRequired();

        builder.Property(session => session.CreatedAtUtc)
            .IsRequired();

        builder.Property(session => session.ExpiresAtUtc)
            .IsRequired();

        builder.Property(session => session.CreatedByIp)
            .HasMaxLength(200);

        builder.Property(session => session.UserAgent)
            .HasMaxLength(512);

        builder.HasIndex(session => session.UserId);
        builder.HasIndex(session => session.ExpiresAtUtc);
    }
}