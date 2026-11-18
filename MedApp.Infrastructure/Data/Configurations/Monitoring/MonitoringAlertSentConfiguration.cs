using MedApp.Domain.Monitoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedApp.Infrastructure.Data.Configurations.Monitoring;

public sealed class MonitoringAlertSentConfiguration : IEntityTypeConfiguration<MonitoringAlertSent>
{
    public void Configure(EntityTypeBuilder<MonitoringAlertSent> builder)
    {
        builder.ToTable("MonitoringAlertsSent");

        // One row per rule and item: an item is reported once under each rule (decision 135).
        builder.HasKey(sent => new { sent.RuleId, sent.SubjectId });

        // Deleting a rule deletes what it reported. No key to the item: it's a referral, result, appointment, specimen
        // or clock pause, by the rule's event, and the row outlives none of them in any way that matters.
        builder.HasOne<MonitoringRule>()
            .WithMany()
            .HasForeignKey(sent => sent.RuleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
