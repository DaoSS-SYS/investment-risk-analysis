using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskAnalysis.Domain.Entities;

namespace RiskAnalysis.Infrastructure.Persistence.Configurations;

public class MacroIndicatorConfiguration : IEntityTypeConfiguration<MacroIndicator>
{
    public void Configure(EntityTypeBuilder<MacroIndicator> builder)
    {
        builder.ToTable("macro_indicators", t =>
            t.HasComment("Макроэкономические показатели Банка России"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Value).HasPrecision(18, 6).IsRequired();

        builder.HasIndex(x => new { x.Code, x.Date }).IsUnique();
    }
}
