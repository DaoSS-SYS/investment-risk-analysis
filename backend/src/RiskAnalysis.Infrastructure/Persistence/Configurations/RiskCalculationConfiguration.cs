using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskAnalysis.Domain.Entities;

namespace RiskAnalysis.Infrastructure.Persistence.Configurations;

public class RiskCalculationConfiguration : IEntityTypeConfiguration<RiskCalculation>
{
    public void Configure(EntityTypeBuilder<RiskCalculation> builder)
    {
        builder.ToTable("risk_calculations", t =>
            t.HasComment("Журнал расчётов: параметры запуска и результаты"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CalculationType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();

        // Состав параметров и результатов различается по видам расчёта,
        // поэтому применяется тип jsonb СУБД PostgreSQL. Это позволяет
        // добавлять новые методы анализа без изменения схемы данных.
        builder.Property(x => x.ParametersJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ResultJson).HasColumnType("jsonb");
        builder.Property(x => x.ErrorMessage).HasMaxLength(4096);

        builder.HasOne(x => x.Portfolio)
               .WithMany(x => x.Calculations)
               .HasForeignKey(x => x.PortfolioId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.PortfolioId, x.CreatedAt });
        builder.HasIndex(x => x.Status);
    }
}
