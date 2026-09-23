using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.Domain.Enums;

namespace RiskAnalysis.Infrastructure.ExternalData.Moex;

/// <summary>
/// Клиент информационно-статистического сервера Московской Биржи (MOEX ISS).
/// Источник является открытым и не требует аутентификации.
///
/// Ответы сервера имеют табличную структуру: каждый блок данных содержит
/// массив имён столбцов columns и массив строк data. Разбор выполняется
/// по именам столбцов, поскольку их состав и порядок различаются для разных
/// рынков: например, для рынка акций и для рынка индексов.
/// </summary>
public class MoexIssClient : IMarketDataClient
{
    private readonly HttpClient _httpClient;
    private readonly MoexIssOptions _options;
    private readonly ILogger<MoexIssClient> _logger;

    public MoexIssClient(
        HttpClient httpClient,
        IOptions<MoexIssOptions> options,
        ILogger<MoexIssClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public string SourceName => "MOEX_ISS";

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecurityInfo>> SearchSecuritiesAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var url = $"securities.json?q={Uri.EscapeDataString(query)}&iss.meta=off&limit=50";

        using var document = await GetJsonAsync(url, cancellationToken);
        var block = document.RootElement.GetProperty("securities");
        var columns = BuildColumnIndex(block.GetProperty("columns"));

        var result = new List<SecurityInfo>();

        foreach (var row in block.GetProperty("data").EnumerateArray())
        {
            var ticker = ReadString(row, columns, "secid");
            var board = ReadString(row, columns, "primary_boardid");
            var group = ReadString(row, columns, "group");
            var type = ReadString(row, columns, "type");
            var isTraded = ReadInt32(row, columns, "is_traded");

            // Инструменты, снятые с торгов, в справочник системы не добавляются:
            // по ним невозможно получить актуальную историю котировок.
            if (ticker is null || board is null || group is null || isTraded == 0)
            {
                continue;
            }

            var (engine, market) = MapGroupToMarket(group);
            if (engine is null || market is null)
            {
                continue;
            }

            result.Add(new SecurityInfo(
                Ticker: ticker,
                ShortName: ReadString(row, columns, "shortname") ?? ticker,
                FullName: ReadString(row, columns, "name"),
                SecurityType: MapSecurityType(type, group),
                Engine: engine,
                Market: market,
                Board: board,
                Isin: ReadString(row, columns, "isin"),
                Currency: "RUB",
                LotSize: null));
        }

        _logger.LogInformation(
            "Поиск в справочнике MOEX по запросу {Query}: найдено {Count} инструментов",
            query, result.Count);

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MarketQuote>> GetHistoryAsync(
        string engine,
        string market,
        string board,
        string ticker,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var quotes = new List<MarketQuote>();
        var start = 0;
        var pageNumber = 0;

        // Сервер выдаёт историю постранично. Размер страницы и общее число
        // записей сообщаются в служебном блоке history.cursor.
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url =
                $"history/engines/{engine}/markets/{market}/boards/{board}/securities/{ticker}.json" +
                $"?from={from:yyyy-MM-dd}&till={to:yyyy-MM-dd}&start={start}&iss.meta=off";

            using var document = await GetJsonAsync(url, cancellationToken);

            var history = document.RootElement.GetProperty("history");
            var columns = BuildColumnIndex(history.GetProperty("columns"));
            var rows = history.GetProperty("data");

            var rowsOnPage = 0;

            foreach (var row in rows.EnumerateArray())
            {
                rowsOnPage++;

                var tradeDate = ReadDate(row, columns, "TRADEDATE");
                var close = ReadDecimal(row, columns, "CLOSE");

                // Записи без цены закрытия соответствуют дням без торгов
                // по инструменту и в расчётах доходности участвовать не могут.
                if (tradeDate is null || close is null)
                {
                    continue;
                }

                // Для отдельных дней биржа не публикует цену открытия,
                // максимум и минимум. В этом случае используется цена закрытия.
                var open = ReadDecimal(row, columns, "OPEN") ?? close.Value;
                var high = ReadDecimal(row, columns, "HIGH") ?? close.Value;
                var low = ReadDecimal(row, columns, "LOW") ?? close.Value;

                quotes.Add(new MarketQuote(
                    TradeDate: tradeDate.Value,
                    Open: open,
                    High: high,
                    Low: low,
                    Close: close.Value,
                    Volume: ReadInt64(row, columns, "VOLUME"),
                    Turnover: ReadDecimal(row, columns, "VALUE")));
            }

            var (total, pageSize) = ReadCursor(document.RootElement);

            pageNumber++;
            start += pageSize > 0 ? pageSize : Math.Max(rowsOnPage, 1);

            if (rowsOnPage == 0 || start >= total)
            {
                break;
            }

            if (_options.PageDelayMs > 0)
            {
                await Task.Delay(_options.PageDelayMs, cancellationToken);
            }
        }

        _logger.LogInformation(
            "Загружена история {Ticker} за период {From}–{To}: {Count} котировок, страниц: {Pages}",
            ticker, from, to, quotes.Count, pageNumber);

        return quotes.OrderBy(q => q.TradeDate).ToList();
    }

    // -----------------------------------------------------------------------
    // Обращение к источнику
    // -----------------------------------------------------------------------

    /// <summary>
    /// Выполняет запрос с повторными попытками при временных отказах источника.
    /// Повтор выполняется при ошибках передачи, превышении времени ожидания
    /// и при кодах ответа 5xx и 429.
    /// </summary>
    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= _options.RetryCount; attempt++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var isTransient =
                        (int)response.StatusCode >= 500 ||
                        (int)response.StatusCode == 429;

                    var message =
                        $"MOEX ISS вернул код {(int)response.StatusCode} для запроса {url}";

                    if (!isTransient)
                    {
                        throw new HttpRequestException(message);
                    }

                    lastError = new HttpRequestException(message);
                }
                else
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                       && !cancellationToken.IsCancellationRequested)
            {
                lastError = ex;
            }

            _logger.LogWarning(
                "Попытка {Attempt} из {Total} обращения к MOEX ISS не удалась: {Message}",
                attempt, _options.RetryCount, lastError?.Message);

            if (attempt < _options.RetryCount)
            {
                await Task.Delay(_options.RetryDelayMs * attempt, cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Источник рыночных данных недоступен после {_options.RetryCount} попыток. " +
            $"Запрос: {url}", lastError);
    }

    // -----------------------------------------------------------------------
    // Разбор табличного ответа
    // -----------------------------------------------------------------------

    private static Dictionary<string, int> BuildColumnIndex(JsonElement columns)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var position = 0;

        foreach (var column in columns.EnumerateArray())
        {
            var name = column.GetString();
            if (name is not null)
            {
                index[name] = position;
            }

            position++;
        }

        return index;
    }

    private static (int Total, int PageSize) ReadCursor(JsonElement root)
    {
        if (!root.TryGetProperty("history.cursor", out var cursor))
        {
            return (0, 0);
        }

        var columns = BuildColumnIndex(cursor.GetProperty("columns"));
        var data = cursor.GetProperty("data");

        foreach (var row in data.EnumerateArray())
        {
            var total = ReadInt32(row, columns, "TOTAL") ?? 0;
            var pageSize = ReadInt32(row, columns, "PAGESIZE") ?? 0;
            return (total, pageSize);
        }

        return (0, 0);
    }

    private static JsonElement? Cell(JsonElement row, Dictionary<string, int> columns, string name)
    {
        if (!columns.TryGetValue(name, out var position) || position >= row.GetArrayLength())
        {
            return null;
        }

        var cell = row[position];
        return cell.ValueKind == JsonValueKind.Null ? null : cell;
    }

    private static string? ReadString(JsonElement row, Dictionary<string, int> columns, string name)
    {
        var cell = Cell(row, columns, name);
        return cell?.ValueKind == JsonValueKind.String ? cell.Value.GetString() : null;
    }

    private static decimal? ReadDecimal(JsonElement row, Dictionary<string, int> columns, string name)
    {
        var cell = Cell(row, columns, name);

        if (cell is null)
        {
            return null;
        }

        return cell.Value.ValueKind switch
        {
            JsonValueKind.Number => cell.Value.TryGetDecimal(out var value) ? value : null,
            JsonValueKind.String when decimal.TryParse(
                cell.Value.GetString(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null
        };
    }

    private static long? ReadInt64(JsonElement row, Dictionary<string, int> columns, string name)
    {
        var value = ReadDecimal(row, columns, name);
        return value is null ? null : (long)decimal.Truncate(value.Value);
    }

    private static int? ReadInt32(JsonElement row, Dictionary<string, int> columns, string name)
    {
        var value = ReadDecimal(row, columns, name);
        return value is null ? null : (int)decimal.Truncate(value.Value);
    }

    private static DateOnly? ReadDate(JsonElement row, Dictionary<string, int> columns, string name)
    {
        var text = ReadString(row, columns, name);

        return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    // -----------------------------------------------------------------------
    // Соответствие классификации биржи и классификации системы
    // -----------------------------------------------------------------------

    /// <summary>
    /// Определяет торговую систему и рынок по коду группы инструментов биржи.
    /// </summary>
    private static (string? Engine, string? Market) MapGroupToMarket(string group) => group switch
    {
        "stock_shares" => ("stock", "shares"),
        "stock_dr" => ("stock", "shares"),
        "stock_etf" => ("stock", "shares"),
        "stock_ppif" => ("stock", "shares"),
        "stock_bonds" => ("stock", "bonds"),
        "stock_index" => ("stock", "index"),
        "currency_selt" => ("currency", "selt"),
        _ => (null, null)
    };

    /// <summary>
    /// Определяет тип инструмента по коду типа и группы инструментов биржи.
    /// </summary>
    private static SecurityType MapSecurityType(string? type, string group)
    {
        if (type is not null)
        {
            if (type.Contains("bond", StringComparison.OrdinalIgnoreCase))
            {
                return SecurityType.Bond;
            }

            if (type.Contains("index", StringComparison.OrdinalIgnoreCase))
            {
                return SecurityType.Index;
            }

            if (type.Contains("etf", StringComparison.OrdinalIgnoreCase) ||
                type.Contains("ppif", StringComparison.OrdinalIgnoreCase))
            {
                return SecurityType.Etf;
            }

            if (type.Contains("share", StringComparison.OrdinalIgnoreCase) ||
                type.Contains("dr", StringComparison.OrdinalIgnoreCase))
            {
                return SecurityType.Share;
            }
        }

        return group switch
        {
            "stock_bonds" => SecurityType.Bond,
            "stock_index" => SecurityType.Index,
            "currency_selt" => SecurityType.Currency,
            _ => SecurityType.Share
        };
    }
}
