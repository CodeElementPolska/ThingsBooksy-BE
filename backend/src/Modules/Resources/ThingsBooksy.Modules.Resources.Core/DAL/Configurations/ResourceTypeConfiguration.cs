using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThingsBooksy.Modules.Resources.Core.Domain;

namespace ThingsBooksy.Modules.Resources.Core.DAL.Configurations;

internal class ResourceTypeConfiguration : IEntityTypeConfiguration<ResourceType>
{
    public void Configure(EntityTypeBuilder<ResourceType> builder)
    {
        builder.ToTable("resource_types");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.BufferMinutes).HasDefaultValue(0).IsRequired();
        builder.HasQueryFilter(x => x.DeletedAt == null);

        builder.HasIndex(t => new { t.GroupId, t.Name }).IsUnique();

        builder.HasMany(x => x.PropertyDefinitions)
            .WithOne()
            .HasForeignKey(x => x.ResourceTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
