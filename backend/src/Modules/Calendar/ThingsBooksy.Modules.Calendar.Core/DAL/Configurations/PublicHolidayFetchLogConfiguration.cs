using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ThingsBooksy.Modules.Calendar.Core.Domain;

namespace ThingsBooksy.Modules.Calendar.Core.DAL.Configurations;

internal class PublicHolidayFetchLogConfiguration : IEntityTypeConfiguration<PublicHolidayFetchLog>
{
    public void Configure(EntityTypeBuilder<PublicHolidayFetchLog> builder)
    {
        builder.ToTable("public_holiday_fetch_logs");

        builder.HasKey(x => x.Year);

        builder.Property(x => x.Year)
            .ValueGeneratedNever();

        builder.Property(x => x.FetchedAt)
            .IsRequired();

        builder.Property(x => x.HolidayCount)
            .IsRequired();
    }
}
