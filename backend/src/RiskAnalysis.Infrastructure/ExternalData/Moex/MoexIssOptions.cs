namespace RiskAnalysis.Infrastructure.ExternalData.Moex;

/// <summary>
/// Параметры подключения к информационно-статистическому серверу
/// Московской Биржи (MOEX ISS).
/// </summary>
public class MoexIssOptions
{
    public const string SectionName = "MoexIss";

    /// <summary>Базовый адрес информационно-статистического сервера.</summary>
    public string BaseUrl { get; set; } = "https://iss.moex.com/iss/";

    /// <summary>Предельное время ожидания ответа, с.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Число повторных попыток при временной недоступности источника.
    /// Обеспечивает устойчивость подсистемы загрузки к кратковременным отказам.
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>Пауза между повторными попытками, мс.</summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Пауза между запросами страниц при постраничной выдаче, мс.
    /// Ограничивает интенсивность обращений к публичному источнику.
    /// </summary>
    public int PageDelayMs { get; set; } = 100;
}
