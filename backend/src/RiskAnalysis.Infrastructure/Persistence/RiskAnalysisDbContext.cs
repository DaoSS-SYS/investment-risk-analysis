using Microsoft.EntityFrameworkCore;
using RiskAnalysis.Domain.Entities;

namespace RiskAnalysis.Infrastructure.Persistence;

/// <summary>
/// Контекст базы данных информационной системы количественного анализа
/// рисков инвестиционной деятельности.
/// </summary>
public class RiskAnalysisDbContext : DbContext
{
    public RiskAnalysisDbContext(DbContextOptions<RiskAnalysisDbContext> options)
        : base(options)
    {
    }

    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<MacroIndicator> MacroIndicators => Set<MacroIndicator>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<RiskCalculation> RiskCalculations => Set<RiskCalculation>();
    public DbSet<ImportLog> ImportLogs => Set<ImportLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Конфигурации сущностей вынесены в отдельные классы,
        // реализующие IEntityTypeConfiguration<T>.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RiskAnalysisDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
