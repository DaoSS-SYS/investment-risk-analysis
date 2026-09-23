using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RiskAnalysis.Infrastructure.Persistence;

/// <summary>
/// Фабрика контекста данных для средств проектирования EF Core.
/// Применяется утилитой dotnet-ef при создании и применении миграций,
/// благодаря чему операции с миграциями не требуют запуска веб-приложения.
/// Строка подключения берётся из переменной окружения RISKANALYSIS_CONNECTION;
/// при её отсутствии используется значение по умолчанию, достаточное
/// для генерации кода миграции без обращения к СУБД.
/// </summary>
public class RiskAnalysisDbContextFactory : IDesignTimeDbContextFactory<RiskAnalysisDbContext>
{
    private const string DefaultConnection =
        "Host=localhost;Port=5432;Database=riskanalysis;Username=postgres;Password=postgres";

    public RiskAnalysisDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("RISKANALYSIS_CONNECTION") ?? DefaultConnection;

        var options = new DbContextOptionsBuilder<RiskAnalysisDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new RiskAnalysisDbContext(options);
    }
}
