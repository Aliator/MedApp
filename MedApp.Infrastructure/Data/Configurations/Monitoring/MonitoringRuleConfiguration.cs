using MedApp.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Monitoring;

public sealed class MonitoringRuleConfiguration : IEntityTypeConfiguration<MonitoringRule>
{
    public void Configure(EntityTypeBuilder<MonitoringRule> builder)
    {
        builder.ToTable("MonitoringRules");

        builder.HasKey(rule => rule.Id);

        // No foreign keys (to users, templates or stage definitions), as with notifications and the audit log: a rule
        // naming something deleted stays, shows it as deleted, and matches nothing more. The enums are stored as numbers.

        // An admin's own rules, for their Monitoring page.
        builder.HasIndex(rule => rule.OwnerUserId);

        // The switched-on rules for a change's events, read on every procedure change that has one.
        builder.HasIndex(rule => new { rule.Event, rule.IsEnabled });
    }
}
