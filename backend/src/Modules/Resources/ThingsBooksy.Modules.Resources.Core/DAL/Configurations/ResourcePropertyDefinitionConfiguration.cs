using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThingsBooksy.Modules.Resources.Core.Domain;

namespace ThingsBooksy.Modules.Resources.Core.DAL.Configurations;

internal class ResourcePropertyDefinitionConfiguration : IEntityTypeConfiguration<ResourcePropertyDefinition>
{
    public void Configure(EntityTypeBuilder<ResourcePropertyDefinition> builder)
    {
        builder.ToTable("resource_property_definitions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DataType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();
    }
}
