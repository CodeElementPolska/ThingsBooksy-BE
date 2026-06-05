using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThingsBooksy.Modules.ManagementGroups.Core.Domain;

namespace ThingsBooksy.Modules.ManagementGroups.Core.DAL.Configurations;

internal class ManagementGroupConfiguration : IEntityTypeConfiguration<ManagementGroup>
{
    public void Configure(EntityTypeBuilder<ManagementGroup> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.HasIndex(g => new { g.OwnerId, g.Name }).IsUnique().HasFilter("\"DeletedAt\" IS NULL");
        builder.HasQueryFilter(x => x.DeletedAt == null);

        builder.HasMany(x => x.Members)
            .WithOne(x => x.Group)
            .HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
