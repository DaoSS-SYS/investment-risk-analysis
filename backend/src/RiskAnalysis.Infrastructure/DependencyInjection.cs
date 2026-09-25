using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Infrastructure.BackgroundProcessing;
using RiskAnalysis.Infrastructure.ExternalData.Cbr;
using RiskAnalysis.Infrastructure.ExternalData.Moex;
using RiskAnalysis.Infrastructure.Persistence;
using RiskAnalysis.Infrastructure.Services;

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
        AddPersistence(services, configuration);
        AddExternalDataSources(services, configuration);

        services.AddScoped<IQuoteImportService, QuoteImportService>();
        services.AddScoped<ICorporateActionService, CorporateActionService>();
        services.AddScoped<IPriceSeriesProvider, PriceSeriesService>();
        services.AddScoped<IReturnAnalysisService, ReturnAnalysisService>();
        services.AddScoped<IVarAnalysisService, VarAnalysisService>();
        services.AddScoped<IMacroImportService, MacroImportService>();
        services.AddScoped<IPerformanceAnalysisService, PerformanceAnalysisService>();
        services.AddScoped<IPortfolioService, PortfolioService>();
        services.AddScoped<IPortfolioRiskService, PortfolioRiskService>();
        services.AddScoped<ICalculationService, CalculationService>();
        services.AddScoped<IBacktestService, BacktestService>();
        services.AddScoped<IStressTestService, StressTestService>();
        services.AddScoped<IPortfolioOptimizationService, PortfolioOptimizationService>();

        // Очередь асинхронных расчётов существует в единственном экземпляре
        // на всё приложение; обработчик очереди — фоновая служба.
        services.AddSingleton<ICalculationQueue, CalculationQueue>();
        services.AddHostedService<CalculationWorker>();

        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
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
    }

    private static void AddExternalDataSources(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MoexIssOptions>(configuration.GetSection(MoexIssOptions.SectionName));

        // Типизированный клиент: время ожидания и базовый адрес берутся
        // из конфигурации, управление временем жизни соединений выполняет
        // фабрика IHttpClientFactory.
        services.AddHttpClient<IMarketDataClient, MoexIssClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<MoexIssOptions>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RiskAnalysis/1.0 (VKR)");
        });

        // Открытые сервисы Банка России: ключевая ставка как безрисковая ставка.
        services.AddHttpClient<IMacroDataClient, CbrClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RiskAnalysis/1.0 (VKR)");
        });
    }
}
