using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThingsBooksy.Modules.Availability.Core.Domain;

namespace ThingsBooksy.Modules.Availability.Core.DAL.Configurations;

internal class ResourceRuleSetConfiguration : IEntityTypeConfiguration<ResourceRuleSet>
{
    public void Configure(EntityTypeBuilder<ResourceRuleSet> builder)
    {
        builder.ToTable("resource_rule_sets");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.ResourceId).IsUnique();

        builder.HasMany(x => x.Rules)
            .WithOne()
            .HasForeignKey(x => x.ResourceRuleSetId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
