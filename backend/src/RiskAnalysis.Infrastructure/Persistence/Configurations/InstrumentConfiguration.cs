using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskAnalysis.Domain.Entities;

namespace RiskAnalysis.Infrastructure.Persistence.Configurations;

public class InstrumentConfiguration : IEntityTypeConfiguration<Instrument>
{
    public void Configure(EntityTypeBuilder<Instrument> builder)
    {
        builder.ToTable("instruments", t =>
            t.HasComment("Справочник финансовых инструментов"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Ticker).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ShortName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(512);
        builder.Property(x => x.Engine).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Market).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Board).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(8).IsRequired();
        builder.Property(x => x.Isin).HasMaxLength(16);
        builder.Property(x => x.Sector).HasMaxLength(128);

        // Перечисления хранятся строками: это делает содержимое таблиц
        // читаемым при анализе данных непосредственно в СУБД.
        builder.Property(x => x.SecurityType).HasConversion<string>().HasMaxLength(16).IsRequired();

        // Один и тот же тикер может торговаться в разных режимах торгов,
        // поэтому уникальность обеспечивается парой значений.
        builder.HasIndex(x => new { x.Ticker, x.Board }).IsUnique();
        builder.HasIndex(x => x.IsActive);
    }
}
