using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskAnalysis.Domain.Entities;

namespace RiskAnalysis.Infrastructure.Persistence.Configurations;

public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("quotes", t =>
            t.HasComment("История дневных котировок финансовых инструментов"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Open).HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.High).HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.Low).HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.Close).HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.Turnover).HasPrecision(20, 2);

        builder.HasOne(x => x.Instrument)
               .WithMany(x => x.Quotes)
               .HasForeignKey(x => x.InstrumentId)
               .OnDelete(DeleteBehavior.Cascade);

        // Ключевое ограничение подсистемы загрузки данных: оно делает импорт
        // идемпотентным — повторная загрузка того же периода не создаёт дубликатов.
        builder.HasIndex(x => new { x.InstrumentId, x.TradeDate }).IsUnique();
        builder.HasIndex(x => x.TradeDate);
    }
}
