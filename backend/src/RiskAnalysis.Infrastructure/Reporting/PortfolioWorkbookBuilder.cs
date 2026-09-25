using ClosedXML.Excel;
using RiskAnalysis.Application.Models;

namespace RiskAnalysis.Infrastructure.Reporting;

/// <summary>
/// Построение отчёта по риску портфеля в формате электронной таблицы.
///
/// В отличие от переносимого документа, предназначенного для чтения,
/// электронная таблица предназначена для дальнейшей обработки: числовые
/// величины записываются числами, а не строками, и снабжаются форматами
/// отображения. Это позволяет пользователю строить собственные диаграммы
/// и выполнять дополнительные расчёты без повторного ввода данных.
/// </summary>
public static class PortfolioWorkbookBuilder
{
    private static readonly XLColor HeaderBackground = XLColor.FromHtml("#eff6ff");
    private static readonly XLColor HeaderText = XLColor.FromHtml("#1d4ed8");
    private static readonly XLColor DangerText = XLColor.FromHtml("#b91c1c");

    private const string MoneyFormat = "# ##0 \"₽\"";
    private const string PercentFormat = "0.00 %";
    private const string NumberFormat = "# ##0.0000";

    /// <summary>
    /// Формирует книгу электронной таблицы.
    /// </summary>
    public static byte[] Build(
        PortfolioView portfolio,
        PortfolioRiskReport risk,
        StressTestReport? stress)
    {
        using var workbook = new XLWorkbook();

        BuildSummarySheet(workbook, portfolio, risk);
        BuildPositionsSheet(workbook, portfolio);
        BuildRiskSheet(workbook, risk);
        BuildContributionsSheet(workbook, risk);

        if (stress is not null)
        {
            BuildStressSheet(workbook, stress);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return stream.ToArray();
    }

    private static void BuildSummarySheet(
        XLWorkbook workbook, PortfolioView portfolio, PortfolioRiskReport risk)
    {
        var sheet = workbook.Worksheets.Add("Общие сведения");

        sheet.Cell(1, 1).Value = "Отчёт о количественной оценке рисков инвестиционной деятельности";
        sheet.Range(1, 1, 1, 3).Merge();
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(13).Font.SetFontColor(HeaderText);

        var row = 3;

        void Pair(string label, XLCellValue value, string? format = null)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.SetFontColor(XLColor.FromHtml("#64748b"));
            sheet.Cell(row, 2).Value = value;

            if (format is not null)
            {
                sheet.Cell(row, 2).Style.NumberFormat.Format = format;
            }

            sheet.Cell(row, 2).Style.Font.SetBold();
            row++;
        }

        Pair("Портфель", portfolio.Name);
        Pair("Сформирован", DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
        Pair("Период выборки", $"{risk.From:dd.MM.yyyy} — {risk.To:dd.MM.yyyy}");
        Pair("Наблюдений", risk.ObservationCount);
        Pair("Эталонный портфель", portfolio.BenchmarkTicker ?? "не задан");
        row++;
        Pair("Вложено средств", portfolio.PurchaseValue, MoneyFormat);
        Pair("Текущая стоимость", portfolio.CurrentValue, MoneyFormat);
        Pair("Финансовый результат", portfolio.ProfitLoss, MoneyFormat);
        Pair("Результат, доля", portfolio.ProfitLossPercent, PercentFormat);
        row++;
        Pair("Уровень доверия", risk.ConfidenceLevel, PercentFormat);
        Pair("Горизонт оценки, торг. дн.", risk.HorizonDays);
        Pair("Волатильность портфеля, годовых", risk.PortfolioVolatilityAnnualized, PercentFormat);
        Pair("Средневзвешенная волатильность", risk.WeightedAverageVolatility, PercentFormat);
        Pair("Эффект диверсификации", risk.DiversificationEffect, PercentFormat);
        row++;

        sheet.Cell(row, 1).Value = "Вывод";
        sheet.Cell(row, 1).Style.Font.SetBold().Font.SetFontColor(HeaderText);
        row++;

        sheet.Cell(row, 1).Value = risk.Conclusion;
        sheet.Range(row, 1, row, 6).Merge();
        sheet.Cell(row, 1).Style.Alignment.SetWrapText();
        sheet.Row(row).Height = 60;

        sheet.Column(1).Width = 34;
        sheet.Column(2).Width = 28;
    }

    private static void BuildPositionsSheet(XLWorkbook workbook, PortfolioView portfolio)
    {
        var sheet = workbook.Worksheets.Add("Состав портфеля");

        WriteHeader(sheet,
            "Тикер", "Наименование", "Количество", "Цена покупки", "Дата покупки",
            "Текущая цена", "Дата котировки", "Стоимость покупки", "Текущая стоимость",
            "Финансовый результат", "Результат, доля", "Доля в портфеле");

        var row = 2;

        foreach (var position in portfolio.Positions)
        {
            sheet.Cell(row, 1).Value = position.Ticker;
            sheet.Cell(row, 2).Value = position.ShortName;
            sheet.Cell(row, 3).Value = position.Quantity;
            sheet.Cell(row, 4).Value = position.PurchasePrice;
            sheet.Cell(row, 5).Value = position.PurchaseDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 6).Value = position.LastPrice ?? 0m;
            sheet.Cell(row, 7).Value = position.LastPriceDate?.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 8).Value = position.PurchaseValue;
            sheet.Cell(row, 9).Value = position.CurrentValue;
            sheet.Cell(row, 10).Value = position.ProfitLoss;
            sheet.Cell(row, 11).Value = position.ProfitLossPercent;
            sheet.Cell(row, 12).Value = position.Weight;

            sheet.Cell(row, 5).Style.DateFormat.Format = "dd.mm.yyyy";
            sheet.Cell(row, 7).Style.DateFormat.Format = "dd.mm.yyyy";
            sheet.Range(row, 4, row, 4).Style.NumberFormat.Format = MoneyFormat;
            sheet.Range(row, 6, row, 6).Style.NumberFormat.Format = MoneyFormat;
            sheet.Range(row, 8, row, 10).Style.NumberFormat.Format = MoneyFormat;
            sheet.Range(row, 11, row, 12).Style.NumberFormat.Format = PercentFormat;

            if (position.ProfitLoss < 0m)
            {
                sheet.Cell(row, 10).Style.Font.SetFontColor(DangerText);
                sheet.Cell(row, 11).Style.Font.SetFontColor(DangerText);
            }

            row++;
        }

        sheet.Cell(row, 1).Value = "Итого";
        sheet.Cell(row, 8).Value = portfolio.PurchaseValue;
        sheet.Cell(row, 9).Value = portfolio.CurrentValue;
        sheet.Cell(row, 10).Value = portfolio.ProfitLoss;
        sheet.Range(row, 8, row, 10).Style.NumberFormat.Format = MoneyFormat;
        sheet.Range(row, 1, row, 12).Style.Font.SetBold().Border.TopBorder = XLBorderStyleValues.Thin;

        sheet.Columns().AdjustToContents();
    }

    private static void BuildRiskSheet(XLWorkbook workbook, PortfolioRiskReport risk)
    {
        var sheet = workbook.Worksheets.Add("Оценка риска");

        WriteHeader(sheet,
            "Метод", "VaR, руб.", "VaR, доля", "CVaR, руб.", "CVaR, доля",
            "Наблюдений", "Описание");

        var row = 2;

        foreach (var estimate in risk.Estimates)
        {
            sheet.Cell(row, 1).Value = estimate.Description.Split('.')[0].Split(',')[0];
            sheet.Cell(row, 2).Value = estimate.ValueAtRiskAbsolute;
            sheet.Cell(row, 3).Value = estimate.ValueAtRiskRelative;
            sheet.Cell(row, 4).Value = estimate.ExpectedShortfallAbsolute;
            sheet.Cell(row, 5).Value = estimate.ExpectedShortfallRelative;
            sheet.Cell(row, 6).Value = estimate.ObservationCount;
            sheet.Cell(row, 7).Value = estimate.Description;

            sheet.Range(row, 2, row, 2).Style.NumberFormat.Format = MoneyFormat;
            sheet.Range(row, 4, row, 4).Style.NumberFormat.Format = MoneyFormat;
            sheet.Range(row, 3, row, 3).Style.NumberFormat.Format = PercentFormat;
            sheet.Range(row, 5, row, 5).Style.NumberFormat.Format = PercentFormat;

            row++;
        }

        sheet.Column(7).Width = 80;
        sheet.Column(7).Style.Alignment.SetWrapText();
        sheet.Columns(1, 6).AdjustToContents();
    }

    private static void BuildContributionsSheet(XLWorkbook workbook, PortfolioRiskReport risk)
    {
        var sheet = workbook.Worksheets.Add("Разложение риска");

        WriteHeader(sheet,
            "Тикер", "Доля в портфеле", "Вклад в риск", "Предельная VaR",
            "Компонентная VaR", "Обособленная VaR", "Выигрыш от диверсификации");

        var row = 2;

        foreach (var contribution in risk.Contributions)
        {
            sheet.Cell(row, 1).Value = contribution.Ticker;
            sheet.Cell(row, 2).Value = contribution.Weight;
            sheet.Cell(row, 3).Value = contribution.ContributionShare;
            sheet.Cell(row, 4).Value = contribution.MarginalVar;
            sheet.Cell(row, 5).Value = contribution.ComponentVar;
            sheet.Cell(row, 6).Value = contribution.StandaloneVar;
            sheet.Cell(row, 7).Value = contribution.DiversificationBenefit;

            sheet.Range(row, 2, row, 3).Style.NumberFormat.Format = PercentFormat;
            sheet.Range(row, 4, row, 7).Style.NumberFormat.Format = MoneyFormat;

            if (contribution.ContributionShare > contribution.Weight)
            {
                sheet.Cell(row, 3).Style.Font.SetBold().Font.SetFontColor(DangerText);
            }

            row++;
        }

        sheet.Cell(row, 1).Value = "Итого";
        sheet.Cell(row, 2).FormulaA1 = $"SUM(B2:B{row - 1})";
        sheet.Cell(row, 3).FormulaA1 = $"SUM(C2:C{row - 1})";
        sheet.Cell(row, 5).FormulaA1 = $"SUM(E2:E{row - 1})";
        sheet.Cell(row, 6).FormulaA1 = $"SUM(F2:F{row - 1})";
        sheet.Cell(row, 7).FormulaA1 = $"SUM(G2:G{row - 1})";

        sheet.Range(row, 2, row, 3).Style.NumberFormat.Format = PercentFormat;
        sheet.Range(row, 5, row, 7).Style.NumberFormat.Format = MoneyFormat;
        sheet.Range(row, 1, row, 7).Style.Font.SetBold().Border.TopBorder = XLBorderStyleValues.Thin;

        row += 2;

        sheet.Cell(row, 1).Value =
            "Сумма компонентных мер риска в точности равна стоимостной мере риска портфеля " +
            "(тождество Эйлера). Суммы в строке «Итого» рассчитаны формулами и могут быть " +
            "проверены средствами табличного процессора.";
        sheet.Range(row, 1, row, 7).Merge();
        sheet.Cell(row, 1).Style.Alignment.SetWrapText();
        sheet.Row(row).Height = 30;

        sheet.Columns(1, 7).AdjustToContents();
    }

    private static void BuildStressSheet(XLWorkbook workbook, StressTestReport stress)
    {
        var sheet = workbook.Worksheets.Add("Стресс-тестирование");

        sheet.Cell(1, 1).Value = "Стоимостная мера риска";
        sheet.Cell(1, 2).Value = stress.ValueAtRisk;
        sheet.Cell(1, 2).Style.NumberFormat.Format = MoneyFormat;

        sheet.Cell(2, 1).Value = "Ожидаемые потери";
        sheet.Cell(2, 2).Value = stress.ExpectedShortfall;
        sheet.Cell(2, 2).Style.NumberFormat.Format = MoneyFormat;

        sheet.Range(1, 1, 2, 1).Style.Font.SetFontColor(XLColor.FromHtml("#64748b"));
        sheet.Range(1, 2, 2, 2).Style.Font.SetBold();

        WriteHeader(sheet, 4,
            "Сценарий", "Вид", "Начало", "Конец", "Торг. дн.",
            "Потери, руб.", "Доля стоимости", "Стоимость после", "Отношение к VaR");

        var row = 5;

        foreach (var scenario in stress.Scenarios)
        {
            sheet.Cell(row, 1).Value = scenario.Name;
            sheet.Cell(row, 2).Value = scenario.Kind.ToString() == "Historical"
                ? "исторический"
                : "гипотетический";

            if (scenario.From is not null)
            {
                sheet.Cell(row, 3).Value = scenario.From.Value.ToDateTime(TimeOnly.MinValue);
                sheet.Cell(row, 4).Value = scenario.To!.Value.ToDateTime(TimeOnly.MinValue);
                sheet.Range(row, 3, row, 4).Style.DateFormat.Format = "dd.mm.yyyy";
            }

            sheet.Cell(row, 5).Value = scenario.TradingDays;
            sheet.Cell(row, 6).Value = scenario.LossAmount;
            sheet.Cell(row, 7).Value = scenario.PortfolioReturn;
            sheet.Cell(row, 8).Value = scenario.ValueAfter;
            sheet.Cell(row, 9).Value = scenario.LossToVarRatio;

            sheet.Range(row, 6, row, 6).Style.NumberFormat.Format = MoneyFormat;
            sheet.Range(row, 8, row, 8).Style.NumberFormat.Format = MoneyFormat;
            sheet.Range(row, 7, row, 7).Style.NumberFormat.Format = PercentFormat;
            sheet.Range(row, 9, row, 9).Style.NumberFormat.Format = NumberFormat;

            sheet.Cell(row, 6).Style.Font.SetFontColor(DangerText);

            if (scenario.LossToVarRatio >= 2.0)
            {
                sheet.Cell(row, 9).Style.Font.SetBold().Font.SetFontColor(DangerText);
            }

            row++;
        }

        row += 1;
        sheet.Cell(row, 1).Value = stress.Conclusion;
        sheet.Range(row, 1, row, 9).Merge();
        sheet.Cell(row, 1).Style.Alignment.SetWrapText();
        sheet.Row(row).Height = 45;

        sheet.Columns(1, 9).AdjustToContents();
        sheet.Column(1).Width = 44;
    }

    private static void WriteHeader(IXLWorksheet sheet, params string[] titles) =>
        WriteHeader(sheet, 1, titles);

    private static void WriteHeader(IXLWorksheet sheet, int row, params string[] titles)
    {
        for (var i = 0; i < titles.Length; i++)
        {
            var cell = sheet.Cell(row, i + 1);

            cell.Value = titles[i];
            cell.Style.Font.SetBold().Font.SetFontColor(HeaderText);
            cell.Style.Fill.SetBackgroundColor(HeaderBackground);
            cell.Style.Alignment.SetWrapText();
        }

        sheet.SheetView.FreezeRows(row);
    }
}
