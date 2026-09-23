using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.RiskEngine.Returns;
using RiskAnalysis.RiskEngine.Statistics;

namespace RiskAnalysis.Infrastructure.Services;

/// <summary>
/// Статистический анализ доходностей.
///
/// Служба выполняет только преобразование данных и не содержит расчётных
/// формул: цены поступают от поставщика ценовых рядов, вычисления выполняются
/// расчётным ядром. Такое разделение позволяет проверять математические модели
/// модульными тестами без обращения к базе данных.
/// </summary>
public class ReturnAnalysisService : IReturnAnalysisService
{
    private readonly IPriceSeriesProvider _priceSeries;

    public ReturnAnalysisService(IPriceSeriesProvider priceSeries) => _priceSeries = priceSeries;

    /// <inheritdoc />
    public async Task<ReturnAnalysisResult> AnalyzeAsync(
        int instrumentId,
        DateOnly from,
        DateOnly to,
        ReturnType returnType = ReturnType.Logarithmic,
        ReturnFrequency frequency = ReturnFrequency.Daily,
        bool includeReturns = false,
        CancellationToken cancellationToken = default)
    {
        var series = await _priceSeries.GetSeriesAsync(instrumentId, from, to, cancellationToken);

        if (series.Points.Count < 2)
        {
            throw new InvalidOperationException(
                $"По инструменту {series.Ticker} за период с {from:dd.MM.yyyy} по {to:dd.MM.yyyy} " +
                "отсутствует достаточное число котировок. Выполните загрузку истории.");
        }

        // Цены хранятся в виде десятичных чисел для точного представления
        // денежных величин; расчёты выполняются в числах с плавающей запятой
        // двойной точности, принятых в вычислительных библиотеках.
        var observations = new PriceObservation[series.Points.Count];

        for (var i = 0; i < series.Points.Count; i++)
        {
            var point = series.Points[i];
            observations[i] = new PriceObservation(point.Date, (double)point.AdjustedClose);
        }

        var returns = ReturnCalculator.Calculate(observations, returnType, frequency);

        if (returns.Length < 8)
        {
            throw new InvalidOperationException(
                $"По инструменту {series.Ticker} за указанный период получено {returns.Length} " +
                "значений доходности. Для статистического анализа требуется не менее восьми " +
                "наблюдений; расширьте период либо измените периодичность.");
        }

        var values = ReturnCalculator.Values(returns);
        var periodsPerYear = ReturnCalculator.PeriodsPerYear(frequency);

        var statistics = DescriptiveStatisticsCalculator.Calculate(values, periodsPerYear);

        // Коэффициенты формы уже рассчитаны, поэтому используется перегрузка,
        // не выполняющая повторного расчёта моментов.
        var normality = NormalityTests.JarqueBera(
            statistics.Count, statistics.Skewness, statistics.ExcessKurtosis);

        var histogram = HistogramBuilder.Build(values);

        return new ReturnAnalysisResult(
            InstrumentId: series.InstrumentId,
            Ticker: series.Ticker,
            ShortName: series.Ticker,
            From: series.Points[0].Date,
            To: series.Points[^1].Date,
            ReturnType: returnType,
            Frequency: frequency,
            PriceCount: series.Points.Count,
            ReturnCount: returns.Length,
            AppliedAdjustments: series.AppliedAdjustments,
            Statistics: statistics,
            Normality: normality,
            Histogram: histogram,
            Returns: includeReturns ? returns : null);
    }
}
