using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThingsBooksy.Modules.Availability.Core.ReadModels;

namespace ThingsBooksy.Modules.Availability.Core.DAL.Configurations;

internal class PublicHolidayReadModelConfiguration : IEntityTypeConfiguration<PublicHolidayReadModel>
{
    public void Configure(EntityTypeBuilder<PublicHolidayReadModel> builder)
    {
        builder.ToTable("public_holiday_read_models");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Date).HasColumnType("date");
        builder.Property(x => x.LocalName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.Year, x.Date }).IsUnique();
    }
}
