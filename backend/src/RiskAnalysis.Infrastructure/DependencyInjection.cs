using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RiskAnalysis.Infrastructure.Persistence;

namespace RiskAnalysis.Infrastructure;

/// <summary>
/// Регистрация компонентов слоя инфраструктуры в контейнере внедрения зависимостей.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Не задана строка подключения ConnectionStrings:Default. " +
                "Для среды разработки задайте её командой: dotnet user-secrets set \"ConnectionStrings:Default\" \"...\"");

        services.AddDbContext<RiskAnalysisDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(RiskAnalysisDbContext).Assembly.FullName);
                // Повторные попытки при кратковременной недоступности СУБД.
                npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
            });

            // Имена таблиц и столбцов формируются в стиле snake_case —
            // общепринятом соглашении об именовании в PostgreSQL.
            options.UseSnakeCaseNamingConvention();
        });

        return services;
    }
}
