using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.Domain.Entities;
using RiskAnalysis.Domain.Enums;
using RiskAnalysis.Infrastructure.Persistence;

namespace RiskAnalysis.Infrastructure.Services;

/// <summary>
/// Приём, выполнение и хранение асинхронных расчётов.
///
/// Порядок работы. Запрос на расчёт не выполняется немедленно: создаётся
/// запись в таблице расчётов со статусом «в очереди», её идентификатор
/// передаётся в очередь и возвращается клиенту. Фоновая служба извлекает
/// идентификатор, выполняет расчёт и сохраняет результат в той же записи.
/// Клиент получает результат последующими обращениями по идентификатору.
///
/// Параметры запуска и результат сохраняются в полях типа jsonb, что
/// обеспечивает воспроизводимость расчёта и позволяет добавлять новые виды
/// расчётов без изменения схемы данных (см. ADR-0005).
/// </summary>
public class CalculationService : ICalculationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly RiskAnalysisDbContext _db;
    private readonly ICalculationQueue _queue;
    private readonly IPortfolioRiskService _riskService;
    private readonly ILogger<CalculationService> _logger;

    public CalculationService(
        RiskAnalysisDbContext db,
        ICalculationQueue queue,
        IPortfolioRiskService riskService,
        ILogger<CalculationService> logger)
    {
        _db = db;
        _queue = queue;
        _riskService = riskService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CalculationView> EnqueueAsync(
        int portfolioId,
        RiskCalculationParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var exists = await _db.Portfolios.AnyAsync(p => p.Id == portfolioId, cancellationToken);

        if (!exists)
        {
            throw new InvalidOperationException(
                $"Портфель с идентификатором {portfolioId} не найден.");
        }

        var calculation = new RiskCalculation
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolioId,
            CalculationType = CalculationType.RiskAssessment,
            Status = CalculationStatus.Queued,
            ParametersJson = JsonSerializer.Serialize(parameters, SerializerOptions),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.RiskCalculations.Add(calculation);
        await _db.SaveChangesAsync(cancellationToken);

        await _queue.EnqueueAsync(calculation.Id, cancellationToken);

        _logger.LogInformation(
            "Расчёт {CalculationId} по портфелю {PortfolioId} поставлен в очередь, " +
            "длина очереди {QueueLength}",
            calculation.Id, portfolioId, _queue.Count);

        return Map(calculation, includeResult: false);
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(Guid calculationId, CancellationToken cancellationToken = default)
    {
        var calculation = await _db.RiskCalculations
            .FirstOrDefaultAsync(c => c.Id == calculationId, cancellationToken);

        if (calculation is null)
        {
            _logger.LogWarning("Расчёт {CalculationId} не найден в базе данных", calculationId);
            return;
        }

        if (calculation.Status != CalculationStatus.Queued)
        {
            _logger.LogWarning(
                "Расчёт {CalculationId} имеет статус {Status} и повторно не выполняется",
                calculationId, calculation.Status);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        calculation.Status = CalculationStatus.Running;
        calculation.StartedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var parameters =
                JsonSerializer.Deserialize<RiskCalculationParameters>(
                    calculation.ParametersJson, SerializerOptions)
                ?? new RiskCalculationParameters();

            var report = await _riskService.CalculateAsync(
                calculation.PortfolioId, parameters, cancellationToken);

            stopwatch.Stop();

            calculation.ResultJson = JsonSerializer.Serialize(report, SerializerOptions);
            calculation.Status = CalculationStatus.Succeeded;
            calculation.FinishedAt = DateTimeOffset.UtcNow;
            calculation.DurationMs = (int)stopwatch.ElapsedMilliseconds;

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Расчёт {CalculationId} завершён за {Duration} мс", calculationId, calculation.DurationMs);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();

            calculation.Status = CalculationStatus.Failed;
            calculation.ErrorMessage = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
            calculation.FinishedAt = DateTimeOffset.UtcNow;
            calculation.DurationMs = (int)stopwatch.ElapsedMilliseconds;

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Расчёт {CalculationId} завершился ошибкой", calculationId);
        }
    }

    /// <inheritdoc />
    public async Task<CalculationView?> GetAsync(
        Guid calculationId, CancellationToken cancellationToken = default)
    {
        var calculation = await _db.RiskCalculations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == calculationId, cancellationToken);

        return calculation is null ? null : Map(calculation, includeResult: true);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalculationView>> GetHistoryAsync(
        int portfolioId, int limit = 50, CancellationToken cancellationToken = default)
    {
        var calculations = await _db.RiskCalculations
            .AsNoTracking()
            .Where(c => c.PortfolioId == portfolioId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);

        // История возвращается без результатов: при десятке инструментов
        // результат одного расчёта занимает десятки килобайт.
        return calculations.Select(c => Map(c, includeResult: false)).ToList();
    }

    private static CalculationView Map(RiskCalculation calculation, bool includeResult)
    {
        RiskCalculationParameters? parameters = null;
        PortfolioRiskReport? result = null;

        try
        {
            parameters = JsonSerializer.Deserialize<RiskCalculationParameters>(
                calculation.ParametersJson, SerializerOptions);

            if (includeResult && calculation.ResultJson is not null)
            {
                result = JsonSerializer.Deserialize<PortfolioRiskReport>(
                    calculation.ResultJson, SerializerOptions);
            }
        }
        catch (JsonException)
        {
            // Формат сохранённого результата мог измениться после доработки
            // системы. Запись остаётся доступной, результат не возвращается.
        }

        return new CalculationView(
            Id: calculation.Id,
            PortfolioId: calculation.PortfolioId,
            CalculationType: calculation.CalculationType,
            Status: calculation.Status,
            Parameters: parameters,
            CreatedAt: calculation.CreatedAt,
            StartedAt: calculation.StartedAt,
            FinishedAt: calculation.FinishedAt,
            DurationMs: calculation.DurationMs,
            ErrorMessage: calculation.ErrorMessage,
            Result: result);
    }
}
