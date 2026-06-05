using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThingsBooksy.Modules.Availability.Core.ReadModels;

namespace ThingsBooksy.Modules.Availability.Core.DAL.Configurations;

internal class ResourceReadModelConfiguration : IEntityTypeConfiguration<ResourceReadModel>
{
    public void Configure(EntityTypeBuilder<ResourceReadModel> builder)
    {
        builder.ToTable("resource_read_models");
        builder.HasKey(x => x.Id);
    }
}
