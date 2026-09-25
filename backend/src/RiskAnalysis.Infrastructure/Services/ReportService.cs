using Microsoft.Extensions.Logging;
using QuestPDF.Infrastructure;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.Infrastructure.Reporting;

namespace RiskAnalysis.Infrastructure.Services;

/// <summary>
/// Формирование отчётов по результатам анализа риска.
///
/// Служба собирает исходные сведения от расчётных подсистем и передаёт их
/// построителям отчётов. Сами построители не обращаются ни к базе данных,
/// ни к расчётным службам: они получают готовые объекты и занимаются
/// исключительно разметкой.
/// </summary>
public class ReportService : IReportService
{
    private readonly IPortfolioService _portfolios;
    private readonly IPortfolioRiskService _risk;
    private readonly IStressTestService _stressTest;
    private readonly ILogger<ReportService> _logger;

    static ReportService()
    {
        // Библиотека QuestPDF распространяется на условиях, допускающих
        // безвозмездное применение организациями с оборотом ниже
        // установленного предела. Тип лицензии указывается явно.
        QuestPDF.Settings.License = LicenseType.Community;

        // Начиная с версии 2026.9 библиотека не использует установленные
        // в системе шрифты по умолчанию, а поставляемый с ней шрифт Lato
        // не содержит начертаний букв кириллического алфавита. Обращение
        // к системным шрифтам включается явно.
        //
        // Известное ограничение: при размещении приложения в контейнере
        // требуемые шрифты должны быть установлены в образе либо
        // поставлены вместе с приложением.
        QuestPDF.Settings.UseSystemFonts = true;
    }

    public ReportService(
        IPortfolioService portfolios,
        IPortfolioRiskService risk,
        IStressTestService stressTest,
        ILogger<ReportService> logger)
    {
        _portfolios = portfolios;
        _risk = risk;
        _stressTest = stressTest;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GeneratedReport> BuildPortfolioPdfAsync(
        int portfolioId,
        double confidenceLevel = 0.99,
        int horizonDays = 1,
        bool includeStressTest = true,
        CancellationToken cancellationToken = default)
    {
        var (portfolio, risk, stress) = await CollectAsync(
            portfolioId, confidenceLevel, horizonDays, includeStressTest, cancellationToken);

        var content = PortfolioReportBuilder.Build(portfolio, risk, stress);

        _logger.LogInformation(
            "Сформирован отчёт по портфелю {PortfolioId} в формате PDF, {Size} байт",
            portfolioId, content.Length);

        return new GeneratedReport(
            FileName: BuildFileName(portfolio.Name, "pdf"),
            ContentType: "application/pdf",
            Content: content);
    }

    /// <inheritdoc />
    public async Task<GeneratedReport> BuildPortfolioWorkbookAsync(
        int portfolioId,
        double confidenceLevel = 0.99,
        int horizonDays = 1,
        bool includeStressTest = true,
        CancellationToken cancellationToken = default)
    {
        var (portfolio, risk, stress) = await CollectAsync(
            portfolioId, confidenceLevel, horizonDays, includeStressTest, cancellationToken);

        var content = PortfolioWorkbookBuilder.Build(portfolio, risk, stress);

        _logger.LogInformation(
            "Сформирован отчёт по портфелю {PortfolioId} в формате электронной таблицы, {Size} байт",
            portfolioId, content.Length);

        return new GeneratedReport(
            FileName: BuildFileName(portfolio.Name, "xlsx"),
            ContentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            Content: content);
    }

    /// <summary>
    /// Собирает сведения, необходимые для построения отчёта.
    /// </summary>
    private async Task<(PortfolioView Portfolio, PortfolioRiskReport Risk, StressTestReport? Stress)>
        CollectAsync(
            int portfolioId,
            double confidenceLevel,
            int horizonDays,
            bool includeStressTest,
            CancellationToken cancellationToken)
    {
        var portfolio = await _portfolios.GetAsync(portfolioId, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Портфель с идентификатором {portfolioId} не найден.");

        if (portfolio.Positions.Count == 0)
        {
            throw new InvalidOperationException(
                $"Портфель «{portfolio.Name}» не содержит позиций: отчёт не может быть сформирован.");
        }

        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = to.AddYears(-10);

        var risk = await _risk.CalculateAsync(
            portfolioId,
            new RiskCalculationParameters(confidenceLevel, horizonDays, from, to),
            cancellationToken);

        StressTestReport? stress = null;

        if (includeStressTest)
        {
            try
            {
                // Стресс-тестирование выполняется на горизонте, кратном
                // горизонту оценки риска, но не менее десяти торговых дней:
                // на однодневном горизонте исторические сценарии
                // малосодержательны.
                stress = await _stressTest.RunAsync(
                    portfolioId, from, to, confidenceLevel,
                    Math.Max(horizonDays, 10), cancellationToken);
            }
            catch (InvalidOperationException exception)
            {
                // Невозможность выполнить стресс-тестирование не должна
                // препятствовать формированию отчёта: соответствующий раздел
                // в него не включается.
                _logger.LogWarning(exception,
                    "Стресс-тестирование портфеля {PortfolioId} не выполнено, " +
                    "раздел не включён в отчёт", portfolioId);
            }
        }

        return (portfolio, risk, stress);
    }

    /// <summary>
    /// Формирует имя файла отчёта. Из наименования портфеля исключаются
    /// символы, недопустимые в именах файлов.
    /// </summary>
    private static string BuildFileName(string portfolioName, string extension)
    {
        var invalid = Path.GetInvalidFileNameChars();

        var sanitized = new string(portfolioName
            .Select(character => invalid.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();

        if (sanitized.Length > 60)
        {
            sanitized = sanitized[..60];
        }

        return $"Отчёт о рисках. {sanitized}. {DateTime.Now:yyyy-MM-dd}.{extension}";
    }
}
