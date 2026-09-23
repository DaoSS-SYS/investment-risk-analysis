using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskAnalysis.Domain.Entities;

namespace RiskAnalysis.Infrastructure.Persistence.Configurations;

public class ImportLogConfiguration : IEntityTypeConfiguration<ImportLog>
{
    public void Configure(EntityTypeBuilder<ImportLog> builder)
    {
        builder.ToTable("import_logs", t =>
            t.HasComment("Журнал загрузки данных из внешних источников"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Source).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(4096);

        builder.HasOne(x => x.Instrument)
               .WithMany()
               .HasForeignKey(x => x.InstrumentId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.StartedAt);
    }
}
