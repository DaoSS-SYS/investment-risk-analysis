using Microsoft.EntityFrameworkCore;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.Domain.Entities;
using RiskAnalysis.Infrastructure.Persistence;

namespace RiskAnalysis.Infrastructure.Services;

/// <summary>
/// Управление портфелями и их позициями.
///
/// Текущая оценка позиций выполняется по последней имеющейся в базе данных
/// котировке. Обращение к бирже при этом не производится: система сохраняет
/// работоспособность при недоступности внешнего источника.
/// </summary>
public class PortfolioService : IPortfolioService
{
    private readonly RiskAnalysisDbContext _db;

    public PortfolioService(RiskAnalysisDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PortfolioView>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var ids = await _db.Portfolios
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var result = new List<PortfolioView>(ids.Count);

        foreach (var id in ids)
        {
            var view = await GetAsync(id, cancellationToken);

            if (view is not null)
            {
                result.Add(view);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<PortfolioView?> GetAsync(
        int portfolioId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _db.Portfolios
            .AsNoTracking()
            .Include(p => p.Positions)
                .ThenInclude(position => position.Instrument)
            .Include(p => p.BenchmarkInstrument)
            .FirstOrDefaultAsync(p => p.Id == portfolioId, cancellationToken);

        if (portfolio is null)
        {
            return null;
        }

        var instrumentIds = portfolio.Positions.Select(p => p.InstrumentId).Distinct().ToList();

        // Последние котировки по всем инструментам портфеля выбираются
        // одним запросом с использованием оконной функции.
        var lastQuotes = await _db.Quotes
            .AsNoTracking()
            .Where(q => instrumentIds.Contains(q.InstrumentId))
            .GroupBy(q => q.InstrumentId)
            .Select(g => new
            {
                InstrumentId = g.Key,
                TradeDate = g.Max(q => q.TradeDate)
            })
            .ToListAsync(cancellationToken);

        var keys = lastQuotes.Select(q => q.InstrumentId).ToList();
        var dates = lastQuotes.Select(q => q.TradeDate).ToList();

        var prices = await _db.Quotes
            .AsNoTracking()
            .Where(q => keys.Contains(q.InstrumentId) && dates.Contains(q.TradeDate))
            .Select(q => new { q.InstrumentId, q.TradeDate, q.Close })
            .ToListAsync(cancellationToken);

        var lastPrice = lastQuotes
            .Select(last => prices.FirstOrDefault(
                p => p.InstrumentId == last.InstrumentId && p.TradeDate == last.TradeDate))
            .Where(p => p is not null)
            .ToDictionary(p => p!.InstrumentId, p => (p!.TradeDate, p.Close));

        var positions = new List<PositionView>(portfolio.Positions.Count);

        decimal totalPurchase = 0m;
        decimal totalCurrent = 0m;

        foreach (var position in portfolio.Positions.OrderBy(p => p.Instrument!.Ticker))
        {
            var purchaseValue = position.Quantity * position.PurchasePrice;

            decimal? price = null;
            DateOnly? priceDate = null;

            if (lastPrice.TryGetValue(position.InstrumentId, out var quote))
            {
                price = quote.Close;
                priceDate = quote.TradeDate;
            }

            var currentValue = price is null ? purchaseValue : position.Quantity * price.Value;

            totalPurchase += purchaseValue;
            totalCurrent += currentValue;

            positions.Add(new PositionView(
                Id: position.Id,
                InstrumentId: position.InstrumentId,
                Ticker: position.Instrument!.Ticker,
                ShortName: position.Instrument.ShortName,
                Quantity: position.Quantity,
                PurchasePrice: position.PurchasePrice,
                PurchaseDate: position.PurchaseDate,
                LastPrice: price,
                LastPriceDate: priceDate,
                PurchaseValue: purchaseValue,
                CurrentValue: currentValue,
                ProfitLoss: currentValue - purchaseValue,
                ProfitLossPercent: purchaseValue > 0
                    ? (double)((currentValue - purchaseValue) / purchaseValue)
                    : 0.0,
                Weight: 0.0));
        }

        // Доли позиций рассчитываются от текущей стоимости портфеля.
        if (totalCurrent > 0m)
        {
            for (var i = 0; i < positions.Count; i++)
            {
                positions[i] = positions[i] with
                {
                    Weight = (double)(positions[i].CurrentValue / totalCurrent)
                };
            }
        }

        return new PortfolioView(
            Id: portfolio.Id,
            Name: portfolio.Name,
            Description: portfolio.Description,
            BaseCurrency: portfolio.BaseCurrency,
            BenchmarkInstrumentId: portfolio.BenchmarkInstrumentId,
            BenchmarkTicker: portfolio.BenchmarkInstrument?.Ticker,
            PurchaseValue: totalPurchase,
            CurrentValue: totalCurrent,
            ProfitLoss: totalCurrent - totalPurchase,
            ProfitLossPercent: totalPurchase > 0
                ? (double)((totalCurrent - totalPurchase) / totalPurchase)
                : 0.0,
            Positions: positions,
            CreatedAt: portfolio.CreatedAt,
            UpdatedAt: portfolio.UpdatedAt);
    }

    /// <inheritdoc />
    public async Task<PortfolioView> CreateAsync(
        PortfolioRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Не указано наименование портфеля.", nameof(request));
        }

        await ValidateBenchmarkAsync(request.BenchmarkInstrumentId, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        var portfolio = new Portfolio
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            BaseCurrency = request.BaseCurrency,
            BenchmarkInstrumentId = request.BenchmarkInstrumentId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Portfolios.Add(portfolio);
        await _db.SaveChangesAsync(cancellationToken);

        return (await GetAsync(portfolio.Id, cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task<PortfolioView?> UpdateAsync(
        int portfolioId, PortfolioRequest request, CancellationToken cancellationToken = default)
    {
        var portfolio = await _db.Portfolios
            .FirstOrDefaultAsync(p => p.Id == portfolioId, cancellationToken);

        if (portfolio is null)
        {
            return null;
        }

        await ValidateBenchmarkAsync(request.BenchmarkInstrumentId, cancellationToken);

        portfolio.Name = request.Name.Trim();
        portfolio.Description = request.Description;
        portfolio.BaseCurrency = request.BaseCurrency;
        portfolio.BenchmarkInstrumentId = request.BenchmarkInstrumentId;
        portfolio.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(portfolioId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int portfolioId, CancellationToken cancellationToken = default)
    {
        var portfolio = await _db.Portfolios
            .FirstOrDefaultAsync(p => p.Id == portfolioId, cancellationToken);

        if (portfolio is null)
        {
            return false;
        }

        _db.Portfolios.Remove(portfolio);
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<PortfolioView?> AddPositionAsync(
        int portfolioId, PositionRequest request, CancellationToken cancellationToken = default)
    {
        var portfolio = await _db.Portfolios
            .FirstOrDefaultAsync(p => p.Id == portfolioId, cancellationToken);

        if (portfolio is null)
        {
            return null;
        }

        if (request.Quantity <= 0m)
        {
            throw new ArgumentException(
                "Количество единиц инструмента должно быть положительным.", nameof(request));
        }

        if (request.PurchasePrice <= 0m)
        {
            throw new ArgumentException(
                "Цена приобретения должна быть положительной.", nameof(request));
        }

        var instrumentExists = await _db.Instruments
            .AnyAsync(i => i.Id == request.InstrumentId, cancellationToken);

        if (!instrumentExists)
        {
            throw new ArgumentException(
                $"Инструмент с идентификатором {request.InstrumentId} не найден в справочнике.",
                nameof(request));
        }

        _db.Positions.Add(new Position
        {
            PortfolioId = portfolioId,
            InstrumentId = request.InstrumentId,
            Quantity = request.Quantity,
            PurchasePrice = request.PurchasePrice,
            PurchaseDate = request.PurchaseDate,
            Note = request.Note
        });

        portfolio.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(portfolioId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PortfolioView?> RemovePositionAsync(
        int portfolioId, int positionId, CancellationToken cancellationToken = default)
    {
        var position = await _db.Positions
            .FirstOrDefaultAsync(
                p => p.Id == positionId && p.PortfolioId == portfolioId, cancellationToken);

        if (position is null)
        {
            return null;
        }

        _db.Positions.Remove(position);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(portfolioId, cancellationToken);
    }

    private async Task ValidateBenchmarkAsync(int? benchmarkId, CancellationToken cancellationToken)
    {
        if (benchmarkId is null)
        {
            return;
        }

        var exists = await _db.Instruments.AnyAsync(i => i.Id == benchmarkId, cancellationToken);

        if (!exists)
        {
            throw new ArgumentException(
                $"Инструмент-бенчмарк с идентификатором {benchmarkId} не найден в справочнике.",
                nameof(benchmarkId));
        }
    }
}
