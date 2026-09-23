using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskAnalysis.Domain.Entities;

namespace RiskAnalysis.Infrastructure.Persistence.Configurations;

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("positions", t =>
            t.HasComment("Позиции портфеля по финансовым инструментам"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity).HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.PurchasePrice).HasPrecision(18, 6).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(512);

        builder.HasOne(x => x.Portfolio)
               .WithMany(x => x.Positions)
               .HasForeignKey(x => x.PortfolioId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Instrument)
               .WithMany()
               .HasForeignKey(x => x.InstrumentId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PortfolioId);
    }
}
