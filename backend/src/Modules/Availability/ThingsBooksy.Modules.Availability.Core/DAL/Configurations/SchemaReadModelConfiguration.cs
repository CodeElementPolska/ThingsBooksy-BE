using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThingsBooksy.Modules.Availability.Core.ReadModels;

namespace ThingsBooksy.Modules.Availability.Core.DAL.Configurations;

internal class SchemaReadModelConfiguration : IEntityTypeConfiguration<SchemaReadModel>
{
    public void Configure(EntityTypeBuilder<SchemaReadModel> builder)
    {
        builder.ToTable("schema_read_models");
        builder.HasKey(x => x.Id);
    }
}
