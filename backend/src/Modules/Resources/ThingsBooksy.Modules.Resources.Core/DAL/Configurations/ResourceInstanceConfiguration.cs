using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThingsBooksy.Modules.Resources.Core.Domain;

namespace ThingsBooksy.Modules.Resources.Core.DAL.Configurations;

internal class ResourceInstanceConfiguration : IEntityTypeConfiguration<ResourceInstance>
{
    public void Configure(EntityTypeBuilder<ResourceInstance> builder)
    {
        builder.ToTable("resource_instances");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.HasQueryFilter(x => x.DeletedAt == null);

        builder.HasIndex(i => new { i.GroupId, i.Id })
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasMany(x => x.PropertyValues)
            .WithOne()
            .HasForeignKey(x => x.ResourceInstanceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
