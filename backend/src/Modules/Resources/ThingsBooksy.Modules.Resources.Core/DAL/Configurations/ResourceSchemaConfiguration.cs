using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThingsBooksy.Modules.Resources.Core.Domain;

namespace ThingsBooksy.Modules.Resources.Core.DAL.Configurations;

internal class ResourceSchemaConfiguration : IEntityTypeConfiguration<ResourceSchema>
{
    public void Configure(EntityTypeBuilder<ResourceSchema> builder)
    {
        builder.ToTable("resource_types");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.HasQueryFilter(x => x.DeletedAt == null);

        builder.HasIndex(t => new { t.GroupId, t.Name }).IsUnique().HasFilter("\"DeletedAt\" IS NULL");

        builder.HasMany(x => x.PropertyDefinitions)
            .WithOne()
            .HasForeignKey(x => x.ResourceSchemaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
