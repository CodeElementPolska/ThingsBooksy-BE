using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThingsBooksy.Modules.Availability.Core.Domain;

namespace ThingsBooksy.Modules.Availability.Core.DAL.Configurations;

internal class SchemaRuleSetConfiguration : IEntityTypeConfiguration<SchemaRuleSet>
{
    public void Configure(EntityTypeBuilder<SchemaRuleSet> builder)
    {
        builder.ToTable("schema_rule_sets");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.SchemaId).IsUnique();

        builder.HasMany(x => x.Rules)
            .WithOne()
            .HasForeignKey(x => x.SchemaRuleSetId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
