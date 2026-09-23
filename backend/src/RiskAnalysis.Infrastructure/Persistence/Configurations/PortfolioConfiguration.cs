using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskAnalysis.Domain.Entities;

namespace RiskAnalysis.Infrastructure.Persistence.Configurations;

public class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(EntityTypeBuilder<Portfolio> builder)
    {
        builder.ToTable("portfolios", t =>
            t.HasComment("Инвестиционные портфели, выступающие объектом анализа риска"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2048);
        builder.Property(x => x.BaseCurrency).HasMaxLength(8).IsRequired();

        builder.HasOne(x => x.BenchmarkInstrument)
               .WithMany()
               .HasForeignKey(x => x.BenchmarkInstrumentId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.OwnerUserId);
    }
}
