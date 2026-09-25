using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RiskAnalysis.Application.Models;

namespace RiskAnalysis.Infrastructure.Reporting;

/// <summary>
/// Построение отчёта по риску портфеля в формате переносимого документа.
///
/// Отчёт строится средствами библиотеки QuestPDF с описанием разметки
/// непосредственно в коде. Такой способ предпочтён шаблонам, заполняемым
/// подстановкой значений: состав разделов отчёта зависит от наличия
/// результатов стресс-тестирования, а число строк таблиц — от состава
/// портфеля.
///
/// Начертание шрифта выбирается из числа установленных в системе: указанные
/// начертания содержат начертания букв кириллического алфавита. При отсутствии
/// первого из них применяется следующее.
/// </summary>
public static class PortfolioReportBuilder
{
    private static readonly Color AccentColor = Color.FromHex("#1d4ed8");
    private static readonly Color DangerColor = Color.FromHex("#b91c1c");
    private static readonly Color MutedColor = Color.FromHex("#64748b");
    private static readonly Color HeaderBackground = Color.FromHex("#eff6ff");

    /// <summary>
    /// Формирует документ отчёта.
    /// </summary>
    /// <param name="portfolio">Портфель с текущей переоценкой позиций.</param>
    /// <param name="risk">Результат оценки риска.</param>
    /// <param name="stress">Результат стресс-тестирования, если выполнялось.</param>
    public static byte[] Build(
        PortfolioView portfolio,
        PortfolioRiskReport risk,
        StressTestReport? stress)
    {
        var generatedAt = DateTimeOffset.Now;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                // Семейство шрифта определяется при первом обращении
                // из числа фактически установленных: см. ReportFonts.
                page.DefaultTextStyle(style => style
                    .FontFamily(ReportFonts.Family)
                    .FontSize(9));

                page.Header().Element(header => ComposeHeader(header, portfolio, generatedAt));

                page.Content().PaddingVertical(12).Column(column =>
                {
                    column.Spacing(14);

                    column.Item().Element(item => ComposeSummary(item, portfolio, risk));
                    column.Item().Element(item => ComposePositions(item, portfolio));
                    column.Item().Element(item => ComposeRiskEstimates(item, risk));
                    column.Item().Element(item => ComposeContributions(item, risk));

                    if (stress is not null)
                    {
                        column.Item().PageBreak();
                        column.Item().Element(item => ComposeStressTest(item, stress));
                    }

                    column.Item().Element(item => ComposeConclusions(item, risk, stress));
                });

                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
    }

    // -----------------------------------------------------------------------
    // Разделы отчёта
    // -----------------------------------------------------------------------

    private static void ComposeHeader(
        IContainer container, PortfolioView portfolio, DateTimeOffset generatedAt)
    {
        container.BorderBottom(1).BorderColor(AccentColor).PaddingBottom(8).Column(column =>
        {
            column.Item().Text("Отчёт о количественной оценке рисков инвестиционной деятельности")
                .FontSize(13).SemiBold().FontColor(AccentColor);

            column.Item().PaddingTop(2).Row(row =>
            {
                row.RelativeItem().Text($"Портфель: {portfolio.Name}").FontSize(9);

                row.ConstantItem(180).AlignRight()
                    .Text($"Сформирован {generatedAt:dd.MM.yyyy HH:mm}")
                    .FontSize(8).FontColor(MutedColor);
            });
        });
    }

    private static void ComposeSummary(
        IContainer container, PortfolioView portfolio, PortfolioRiskReport risk)
    {
        container.Column(column =>
        {
            column.Item().Element(SectionTitle).Text("1. Общие сведения");

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                });

                Pair(table, "Период выборки", $"{risk.From:dd.MM.yyyy} — {risk.To:dd.MM.yyyy}");
                Pair(table, "Наблюдений", Count(risk.ObservationCount));
                Pair(table, "Стоимость портфеля", Money(portfolio.CurrentValue));
                Pair(table, "Вложено средств", Money(portfolio.PurchaseValue));
                Pair(table, "Финансовый результат",
                    $"{Money(portfolio.ProfitLoss)} ({Percent(portfolio.ProfitLossPercent, 1, true)})");
                Pair(table, "Эталонный портфель", portfolio.BenchmarkTicker ?? "не задан");
                Pair(table, "Уровень доверия", Percent(risk.ConfidenceLevel, 0));
                Pair(table, "Горизонт оценки", $"{risk.HorizonDays} торг. дн.");
                Pair(table, "Волатильность портфеля",
                    $"{Percent(risk.PortfolioVolatilityAnnualized, 2)} годовых");
                Pair(table, "Эффект диверсификации", Percent(risk.DiversificationEffect, 1));
            });
        });
    }

    private static void ComposePositions(IContainer container, PortfolioView portfolio)
    {
        container.Column(column =>
        {
            column.Item().Element(SectionTitle).Text("2. Состав портфеля");

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(50);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.ConstantColumn(50);
                });

                HeaderRow(table,
                    "Тикер", "Наименование", "Количество", "Цена покупки",
                    "Текущая цена", "Стоимость", "Доля");

                foreach (var position in portfolio.Positions)
                {
                    table.Cell().Element(Cell).Text(position.Ticker).SemiBold();
                    table.Cell().Element(Cell).Text(position.ShortName);
                    table.Cell().Element(CellRight).Text(Count((int)position.Quantity));
                    table.Cell().Element(CellRight).Text(Money(position.PurchasePrice));
                    table.Cell().Element(CellRight).Text(Money(position.LastPrice ?? 0m));
                    table.Cell().Element(CellRight).Text(Money(position.CurrentValue));
                    table.Cell().Element(CellRight).Text(Percent(position.Weight, 1));
                }

                table.Cell().ColumnSpan(5).Element(TotalCell).Text("Итого").SemiBold();
                table.Cell().Element(TotalCellRight).Text(Money(portfolio.CurrentValue)).SemiBold();
                table.Cell().Element(TotalCellRight).Text("100,0 %").SemiBold();
            });
        });
    }

    private static void ComposeRiskEstimates(IContainer container, PortfolioRiskReport risk)
    {
        container.Column(column =>
        {
            column.Item().Element(SectionTitle).Text("3. Оценка риска портфеля");

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(5);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });

                HeaderRow(table, "Метод", "VaR, ₽", "VaR, %", "CVaR, ₽", "CVaR, %");

                foreach (var estimate in risk.Estimates)
                {
                    table.Cell().Element(Cell).Text(MethodName(estimate.Description));
                    table.Cell().Element(CellRight).Text(Money((decimal)estimate.ValueAtRiskAbsolute));
                    table.Cell().Element(CellRight).Text(Percent(estimate.ValueAtRiskRelative, 2));
                    table.Cell().Element(CellRight)
                        .Text(Money((decimal)estimate.ExpectedShortfallAbsolute)).SemiBold();
                    table.Cell().Element(CellRight)
                        .Text(Percent(estimate.ExpectedShortfallRelative, 2));
                }
            });

            column.Item().PaddingTop(6).Text(text =>
            {
                text.Span("Стоимостная мера риска (VaR) — величина потерь, которая не будет " +
                          "превышена с заданной вероятностью. Ожидаемые потери (CVaR) — средняя " +
                          "величина потерь при условии превышения этой границы.")
                    .FontSize(8).FontColor(MutedColor);
            });
        });
    }

    private static void ComposeContributions(IContainer container, PortfolioRiskReport risk)
    {
        container.Column(column =>
        {
            column.Item().Element(SectionTitle).Text("4. Разложение риска по позициям");

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(55);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(3);
                });

                HeaderRow(table,
                    "Тикер", "Доля", "Вклад в риск",
                    "Компонентная VaR", "Обособленная VaR", "Выигрыш от дивер.");

                foreach (var contribution in risk.Contributions)
                {
                    var exceeds = contribution.ContributionShare > contribution.Weight;

                    table.Cell().Element(Cell).Text(contribution.Ticker).SemiBold();
                    table.Cell().Element(CellRight).Text(Percent(contribution.Weight, 1));
                    table.Cell().Element(CellRight)
                        .Text(Percent(contribution.ContributionShare, 1))
                        .FontColor(exceeds ? DangerColor : Colors.Black).SemiBold();
                    table.Cell().Element(CellRight).Text(Money((decimal)contribution.ComponentVar));
                    table.Cell().Element(CellRight).Text(Money((decimal)contribution.StandaloneVar));
                    table.Cell().Element(CellRight)
                        .Text(Money((decimal)contribution.DiversificationBenefit));
                }

                table.Cell().ColumnSpan(3).Element(TotalCell).Text("Итого").SemiBold();
                table.Cell().Element(TotalCellRight)
                    .Text(Money((decimal)risk.Contributions.Sum(c => c.ComponentVar))).SemiBold();
                table.Cell().Element(TotalCellRight)
                    .Text(Money((decimal)risk.SumOfStandaloneVar)).SemiBold();
                table.Cell().Element(TotalCellRight)
                    .Text(Money((decimal)risk.Contributions.Sum(c => c.DiversificationBenefit)))
                    .SemiBold();
            });

            column.Item().PaddingTop(6).Text(text =>
            {
                text.Span("Сумма компонентных мер риска в точности равна стоимостной мере риска " +
                          "портфеля (тождество Эйлера). Позиции, вклад которых в риск превышает " +
                          "их долю в портфеле, выделены цветом: их сокращение снижает риск " +
                          "сильнее прочих.")
                    .FontSize(8).FontColor(MutedColor);
            });
        });
    }

    private static void ComposeStressTest(IContainer container, StressTestReport stress)
    {
        container.Column(column =>
        {
            column.Item().Element(SectionTitle).Text("5. Стресс-тестирование");

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(5);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });

                HeaderRow(table, "Сценарий", "Период", "Потери, ₽", "% стоим.", "К VaR");

                foreach (var scenario in stress.Scenarios)
                {
                    var period = scenario.From is null
                        ? "гипотетический"
                        : $"{scenario.From:dd.MM.yyyy} — {scenario.To:dd.MM.yyyy}";

                    table.Cell().Element(Cell).Text(scenario.Name);
                    table.Cell().Element(Cell).Text(period).FontSize(8);
                    table.Cell().Element(CellRight)
                        .Text(Money((decimal)scenario.LossAmount)).FontColor(DangerColor);
                    table.Cell().Element(CellRight).Text(Percent(scenario.PortfolioReturn, 1, true));
                    table.Cell().Element(CellRight)
                        .Text(scenario.LossToVarRatio.ToString("F2")).SemiBold();
                }
            });

            column.Item().PaddingTop(6).Text(text =>
            {
                text.Span($"Стоимостная мера риска на горизонте {stress.HorizonDays} торг. дн. " +
                          $"при уровне доверия {Percent(stress.ConfidenceLevel, 0)} составляет " +
                          $"{Money((decimal)stress.ValueAtRisk)}. Периоды исторических сценариев " +
                          "определены по фактическим данным как периоды наибольших потерь " +
                          "рассматриваемого портфеля.")
                    .FontSize(8).FontColor(MutedColor);
            });
        });
    }

    private static void ComposeConclusions(
        IContainer container, PortfolioRiskReport risk, StressTestReport? stress)
    {
        container.Column(column =>
        {
            column.Item().Element(SectionTitle).Text(stress is null ? "5. Выводы" : "6. Выводы");

            column.Item().PaddingTop(4).Background(HeaderBackground).Padding(8).Column(inner =>
            {
                inner.Spacing(6);

                inner.Item().Text(risk.Conclusion).FontSize(9);

                if (stress is not null)
                {
                    inner.Item().Text(stress.Conclusion).FontSize(9);
                }
            });
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(
                "Информационная система количественного анализа рисков " +
                "инвестиционной деятельности. Источники данных: Московская Биржа, Банк России.")
                .FontSize(7).FontColor(MutedColor);

            row.ConstantItem(70).AlignRight().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(7).FontColor(MutedColor));
                text.CurrentPageNumber();
                text.Span(" из ");
                text.TotalPages();
            });
        });
    }

    // -----------------------------------------------------------------------
    // Вспомогательные элементы разметки
    // -----------------------------------------------------------------------

    private static IContainer SectionTitle(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(3)
            .DefaultTextStyle(style => style.FontSize(11).SemiBold().FontColor(AccentColor));

    private static IContainer Cell(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(3)
            .PaddingHorizontal(2);

    private static IContainer CellRight(IContainer container) =>
        Cell(container).AlignRight();

    private static IContainer TotalCell(IContainer container) =>
        container.BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingVertical(4)
            .PaddingHorizontal(2);

    private static IContainer TotalCellRight(IContainer container) =>
        TotalCell(container).AlignRight();

    private static void HeaderRow(TableDescriptor table, params string[] titles)
    {
        for (var i = 0; i < titles.Length; i++)
        {
            var cell = table.Cell().Background(HeaderBackground).PaddingVertical(4)
                .PaddingHorizontal(2);

            (i == 0 ? cell : cell.AlignRight())
                .Text(titles[i]).FontSize(8).SemiBold().FontColor(AccentColor);
        }
    }

    private static void Pair(TableDescriptor table, string label, string value)
    {
        table.Cell().PaddingVertical(2).Text(label).FontSize(8).FontColor(MutedColor);
        table.Cell().PaddingVertical(2).Text(value).FontSize(9).SemiBold();
    }

    private static string MethodName(string description) => description.Split('.')[0].Split(',')[0];

    private static string Money(decimal value) =>
        $"{value.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"))} ₽";

    private static string Count(int value) =>
        value.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));

    private static string Percent(double value, int digits, bool withSign = false)
    {
        var format = withSign ? $"+0.{new string('0', digits)};-0.{new string('0', digits)}" : $"F{digits}";

        return $"{(value * 100).ToString(format, System.Globalization.CultureInfo.GetCultureInfo("ru-RU"))} %";
    }
}
