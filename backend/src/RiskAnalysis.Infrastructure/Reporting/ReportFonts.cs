using QuestPDF.Drawing;

namespace RiskAnalysis.Infrastructure.Reporting;

/// <summary>
/// Определение начертания шрифта для отчётов.
///
/// Поставляемый с библиотекой QuestPDF шрифт Lato не содержит начертаний
/// букв кириллического алфавита, а начиная с версии 2026.9 библиотека
/// не обращается к установленным в системе шрифтам по умолчанию и прерывает
/// построение документа при указании недоступного семейства — даже если
/// в перечне запасных вариантов присутствуют доступные.
///
/// Поэтому применяется явная регистрация: при первом обращении
/// просматриваются известные расположения файлов шрифтов, и регистрируется
/// первое найденное семейство, содержащее кириллические начертания.
/// Такой порядок делает построение отчёта воспроизводимым и в операционных
/// системах семейства Windows, и в дистрибутивах Linux, включая размещение
/// приложения в контейнере.
/// </summary>
public static class ReportFonts
{
    /// <summary>
    /// Перечень проверяемых семейств. Для каждого указаны файлы обычного
    /// и полужирного начертаний: полужирное требуется для заголовков
    /// и итоговых строк таблиц.
    /// </summary>
    private static readonly (string Family, string[] Files)[] Candidates =
    [
        ("Segoe UI",
        [
            @"C:\Windows\Fonts\segoeui.ttf",
            @"C:\Windows\Fonts\segoeuib.ttf",
            @"C:\Windows\Fonts\segoeuisl.ttf"
        ]),
        ("Arial",
        [
            @"C:\Windows\Fonts\arial.ttf",
            @"C:\Windows\Fonts\arialbd.ttf"
        ]),
        ("DejaVu Sans",
        [
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
            "/usr/share/fonts/TTF/DejaVuSans.ttf",
            "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf"
        ]),
        ("Liberation Sans",
        [
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"
        ])
    ];

    private static readonly Lock SyncRoot = new();
    private static string? _family;

    /// <summary>
    /// Наименование зарегистрированного семейства шрифта.
    ///
    /// При отсутствии всех проверяемых семейств возвращается поставляемый
    /// с библиотекой шрифт Lato. Кириллические начертания в нём отсутствуют,
    /// поэтому такой случай означает необходимость установки шрифтов
    /// в среде размещения приложения.
    /// </summary>
    public static string Family
    {
        get
        {
            if (_family is not null)
            {
                return _family;
            }

            lock (SyncRoot)
            {
                _family ??= RegisterFirstAvailable();
            }

            return _family;
        }
    }

    private static string RegisterFirstAvailable()
    {
        foreach (var (family, files) in Candidates)
        {
            var existing = files.Where(File.Exists).ToArray();

            if (existing.Length == 0)
            {
                continue;
            }

            foreach (var path in existing)
            {
                using var stream = File.OpenRead(path);
                FontManager.RegisterFont(stream);
            }

            return family;
        }

        return "Lato";
    }
}
