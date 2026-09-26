using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RiskAnalysis.Domain.Entities;
using RiskAnalysis.Infrastructure.Identity;

namespace RiskAnalysis.Infrastructure.Persistence;

/// <summary>
/// Контекст базы данных информационной системы количественного анализа
/// рисков инвестиционной деятельности.
/// </summary>
public class RiskAnalysisDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
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
    public DbSet<CorporateAction> CorporateActions => Set<CorporateAction>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Базовая реализация настраивает сущности подсистемы удостоверения
        // личности и должна вызываться до применения собственных конфигураций.
        base.OnModelCreating(modelBuilder);

        // Конфигурации сущностей вынесены в отдельные классы,
        // реализующие IEntityTypeConfiguration<T>.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RiskAnalysisDbContext).Assembly);

        // Наименования вспомогательных таблиц подсистемы удостоверения
        // личности приводятся к соглашению, принятому в базе данных.
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
    }
}
