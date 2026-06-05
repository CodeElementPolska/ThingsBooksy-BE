using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThingsBooksy.Modules.Availability.Core.Domain;

namespace ThingsBooksy.Modules.Availability.Core.DAL.Configurations;

internal class AvailabilityRuleConfiguration : IEntityTypeConfiguration<AvailabilityRule>
{
    public void Configure(EntityTypeBuilder<AvailabilityRule> builder)
    {
        builder.ToTable("availability_rules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DaysOfWeek)
            .HasColumnType("integer[]");

        builder.Property(x => x.StartTime)
            .HasColumnType("time without time zone");

        builder.Property(x => x.EndTime)
            .HasColumnType("time without time zone");

        builder.Property(x => x.StartDate)
            .HasColumnType("date");

        builder.Property(x => x.EndDate)
            .HasColumnType("date");

        builder.Property(x => x.SchemaRuleSetId).IsRequired(false);
        builder.Property(x => x.ResourceRuleSetId).IsRequired(false);
    }
}
