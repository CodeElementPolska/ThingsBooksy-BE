using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThingsBooksy.Modules.Availability.Core.ReadModels;

namespace ThingsBooksy.Modules.Availability.Core.DAL.Configurations;

internal class GroupReadModelConfiguration : IEntityTypeConfiguration<GroupReadModel>
{
    public void Configure(EntityTypeBuilder<GroupReadModel> builder)
    {
        builder.ToTable("group_read_models");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TimeZoneId).HasMaxLength(100).IsRequired();
    }
}
