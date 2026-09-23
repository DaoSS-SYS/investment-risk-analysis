using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.Domain.Enums;

namespace RiskAnalysis.Infrastructure.ExternalData.Cbr;

/// <summary>
/// Клиент открытых сервисов Банка России.
///
/// Ключевая ставка Банка России применяется в системе в качестве безрисковой
/// ставки при расчёте коэффициентов Шарпа, Сортино, Трейнора и параметров
/// модели CAPM. Выбор обоснован тем, что ключевая ставка является
/// официальным ориентиром стоимости денег в рублях и публикуется Банком
/// России в открытом доступе.
///
/// Данные предоставляются службой DailyInfoWebServ по протоколу SOAP.
/// Значение ставки публикуется на каждый рабочий день и остаётся неизменным
/// между заседаниями Совета директоров Банка России.
/// </summary>
public class CbrClient : IMacroDataClient
{
    private const string ServiceUrl = "https://www.cbr.ru/DailyInfoWebServ/DailyInfo.asmx";
    private const string KeyRateAction = "http://web.cbr.ru/KeyRate";

    private readonly HttpClient _httpClient;
    private readonly ILogger<CbrClient> _logger;

    public CbrClient(HttpClient httpClient, ILogger<CbrClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public string SourceName => "CBR";

    /// <inheritdoc />
    public async Task<IReadOnlyList<MacroObservation>> GetKeyRateAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var envelope =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                           xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                           xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <KeyRate xmlns="http://web.cbr.ru/">
                  <fromDate>{from}</fromDate>
                  <ToDate>{to}</ToDate>
                </KeyRate>
              </soap:Body>
            </soap:Envelope>
            """
            .Replace("{from}", from.ToString("yyyy-MM-dd") + "T00:00:00")
            .Replace("{to}", to.ToString("yyyy-MM-dd") + "T00:00:00");

        using var request = new HttpRequestMessage(HttpMethod.Post, ServiceUrl)
        {
            Content = new StringContent(envelope, Encoding.UTF8, "text/xml")
        };

        request.Headers.Add("SOAPAction", KeyRateAction);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Сервис Банка России вернул код {(int)response.StatusCode} " +
                "при запросе ключевой ставки.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);

        var result = new List<MacroObservation>();

        // Ответ содержит набор элементов KR, каждый из которых включает
        // дату действия ставки DT и её значение Rate.
        foreach (var element in document.Descendants().Where(e => e.Name.LocalName == "KR"))
        {
            var dateText = element.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "DT")?.Value;

            var rateText = element.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "Rate")?.Value;

            if (dateText is null || rateText is null)
            {
                continue;
            }

            if (!DateTimeOffset.TryParse(
                    dateText, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
            {
                continue;
            }

            if (!decimal.TryParse(
                    rateText.Replace(',', '.'), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var rate))
            {
                continue;
            }

            result.Add(new MacroObservation(
                MacroIndicatorCode.KeyRate,
                DateOnly.FromDateTime(date.Date),
                rate));
        }

        _logger.LogInformation(
            "Загружена ключевая ставка Банка России за период {From}–{To}: {Count} значений",
            from, to, result.Count);

        return result.OrderBy(r => r.Date).ToList();
    }
}
