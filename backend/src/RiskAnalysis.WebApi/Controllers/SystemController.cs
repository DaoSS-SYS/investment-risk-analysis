using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiskAnalysis.Infrastructure.Persistence;

namespace RiskAnalysis.WebApi.Controllers;

/// <summary>
/// Служебные методы: сведения о системе и состоянии базы данных.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly RiskAnalysisDbContext _db;

    public SystemController(RiskAnalysisDbContext db) => _db = db;

    /// <summary>Возвращает сведения о версии системы и состоянии хранилища данных.</summary>
    [HttpGet("info")]
    public async Task<ActionResult<object>> GetInfo(CancellationToken cancellationToken)
    {
        var canConnect = await _db.Database.CanConnectAsync(cancellationToken);

        var appliedMigrations = canConnect
            ? (await _db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray()
            : [];

        return Ok(new
        {
            Application = "Информационная система количественного анализа рисков инвестиционной деятельности",
            Version = typeof(SystemController).Assembly.GetName().Version?.ToString(),
            Environment.MachineName,
            DatabaseAvailable = canConnect,
            AppliedMigrations = appliedMigrations,
            InstrumentCount = canConnect ? await _db.Instruments.CountAsync(cancellationToken) : 0,
            QuoteCount = canConnect ? await _db.Quotes.CountAsync(cancellationToken) : 0,
            PortfolioCount = canConnect ? await _db.Portfolios.CountAsync(cancellationToken) : 0
        });
    }
}
