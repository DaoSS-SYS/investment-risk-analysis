using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskAnalysis.Domain.Entities;

namespace RiskAnalysis.Infrastructure.Persistence.Configurations;

public class CorporateActionConfiguration : IEntityTypeConfiguration<CorporateAction>
{
    public void Configure(EntityTypeBuilder<CorporateAction> builder)
    {
        builder.ToTable("corporate_actions", t =>
            t.HasComment("Аномалии ценовых рядов и их классификация: корпоративные действия и рыночные события"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActionType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(16).IsRequired();

        builder.Property(x => x.Ratio).HasPrecision(18, 8).IsRequired();
        builder.Property(x => x.AdjustmentFactor).HasPrecision(18, 8).IsRequired();
        builder.Property(x => x.ObservedRatio).HasPrecision(18, 8).IsRequired();
        builder.Property(x => x.ObservedLogReturn).HasPrecision(18, 8).IsRequired();

        builder.Property(x => x.Comment).HasMaxLength(512);

        builder.HasOne(x => x.Instrument)
               .WithMany()
               .HasForeignKey(x => x.InstrumentId)
               .OnDelete(DeleteBehavior.Cascade);

        // По одному инструменту на одну дату может существовать только одна
        // запись: повторный запуск выявления не создаёт дубликатов.
        builder.HasIndex(x => new { x.InstrumentId, x.ActionDate }).IsUnique();
    }
}
