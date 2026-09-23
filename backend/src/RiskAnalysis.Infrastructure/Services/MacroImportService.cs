using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.Domain.Entities;
using RiskAnalysis.Domain.Enums;
using RiskAnalysis.Infrastructure.Persistence;

namespace RiskAnalysis.Infrastructure.Services;

/// <summary>
/// Загрузка макроэкономических показателей и определение безрисковой ставки.
/// </summary>
public class MacroImportService : IMacroImportService
{
    /// <summary>
    /// Значение безрисковой ставки, применяемое при отсутствии данных
    /// Банка России в базе. Наличие запасного значения обеспечивает
    /// работоспособность расчётной части при недоступности внешнего
    /// источника, что является требованием к надёжности системы.
    /// </summary>
    private const double FallbackAnnualRate = 0.15;

    private readonly RiskAnalysisDbContext _db;
    private readonly IMacroDataClient _macroData;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MacroImportService> _logger;

    public MacroImportService(
        RiskAnalysisDbContext db,
        IMacroDataClient macroData,
        IConfiguration configuration,
        ILogger<MacroImportService> logger)
    {
        _db = db;
        _macroData = macroData;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ImportResult> ImportKeyRateAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var startedAt = DateTimeOffset.UtcNow;

        var log = new ImportLog
        {
            Source = _macroData.SourceName,
            DateFrom = from,
            DateTo = to,
            StartedAt = startedAt
        };

        try
        {
            var received = await _macroData.GetKeyRateAsync(from, to, cancellationToken);

            var existing = await _db.MacroIndicators
                .Where(m => m.Code == MacroIndicatorCode.KeyRate
                            && m.Date >= from && m.Date <= to)
                .ToDictionaryAsync(m => m.Date, cancellationToken);

            var inserted = 0;
            var updated = 0;

            foreach (var observation in received)
            {
                if (existing.TryGetValue(observation.Date, out var stored))
                {
                    if (stored.Value == observation.Value)
                    {
                        continue;
                    }

                    stored.Value = observation.Value;
                    updated++;
                }
                else
                {
                    _db.MacroIndicators.Add(new MacroIndicator
                    {
                        Code = observation.Code,
                        Date = observation.Date,
                        Value = observation.Value
                    });
                    inserted++;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            stopwatch.Stop();

            log.RowsReceived = received.Count;
            log.RowsInserted = inserted;
            log.RowsUpdated = updated;
            log.Status = received.Count > 0 ? ImportStatus.Success : ImportStatus.PartialSuccess;
            log.FinishedAt = DateTimeOffset.UtcNow;
            log.DurationMs = (int)stopwatch.ElapsedMilliseconds;

            _db.ImportLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken);

            return new ImportResult(
                0, "KEY_RATE", from, to,
                received.Count, inserted, updated,
                log.Status, null, log.DurationMs);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();

            log.Status = ImportStatus.Failed;
            log.Message = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
            log.FinishedAt = DateTimeOffset.UtcNow;
            log.DurationMs = (int)stopwatch.ElapsedMilliseconds;

            _db.ChangeTracker.Clear();
            _db.ImportLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Загрузка ключевой ставки за период {From}–{To} не выполнена", from, to);

            return new ImportResult(
                0, "KEY_RATE", from, to, 0, 0, 0,
                ImportStatus.Failed, log.Message, log.DurationMs);
        }
    }

    /// <inheritdoc />
    public async Task<RiskFreeRate> GetRiskFreeRateAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        // Ключевая ставка изменяется по решениям Совета директоров Банка
        // России и остаётся постоянной между заседаниями. За период,
        // охватывающий несколько лет, в качестве безрисковой ставки
        // принимается среднее значение по всем рабочим дням периода:
        // именно такая величина соответствует средней доходности
        // безрискового вложения на протяжении периода.
        var values = await _db.MacroIndicators
            .AsNoTracking()
            .Where(m => m.Code == MacroIndicatorCode.KeyRate && m.Date >= from && m.Date <= to)
            .Select(m => m.Value)
            .ToListAsync(cancellationToken);

        if (values.Count == 0)
        {
            var configured = _configuration.GetValue<double?>("RiskFree:DefaultAnnualRate")
                             ?? FallbackAnnualRate;

            _logger.LogWarning(
                "Ключевая ставка за период {From}–{To} отсутствует в базе данных. " +
                "Применено значение по умолчанию {Rate:P2}", from, to, configured);

            return new RiskFreeRate(configured, "значение по умолчанию", 0, configured, configured);
        }

        // Значения хранятся в процентах годовых, расчёты ведутся в долях единицы.
        var rates = values.Select(v => (double)v / 100.0).ToList();

        return new RiskFreeRate(
            AnnualRate: rates.Average(),
            Source: "Ключевая ставка Банка России",
            ObservationCount: rates.Count,
            Minimum: rates.Min(),
            Maximum: rates.Max());
    }
}
