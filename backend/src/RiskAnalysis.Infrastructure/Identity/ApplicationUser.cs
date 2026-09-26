using Microsoft.AspNetCore.Identity;

namespace RiskAnalysis.Infrastructure.Identity;

/// <summary>
/// Пользователь системы.
///
/// Расширяет стандартную сущность подсистемы удостоверения личности
/// реквизитами, необходимыми в предметной области: фамилией с инициалами
/// и должностью. Они выводятся в журнале действий и при указании лица,
/// выполнившего расчёт.
///
/// Класс размещён в слое инфраструктуры, а не в слое предметной области,
/// поскольку наследуется от типа подсистемы удостоверения личности.
/// Требование об отсутствии внешних зависимостей у слоя предметной области
/// установлено в ADR-0002.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Фамилия, имя и отчество.</summary>
    public string FullName { get; set; } = null!;

    /// <summary>Должность в организации.</summary>
    public string? Position { get; set; }

    /// <summary>
    /// Признак действующей учётной записи. Учётные записи не удаляются,
    /// а помечаются недействующими: удаление нарушило бы связность
    /// журнала действий и сведений о владельцах портфелей.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Время последнего входа в систему.</summary>
    public DateTimeOffset? LastLoginAt { get; set; }
}

/// <summary>Роль пользователя системы.</summary>
public class ApplicationRole : IdentityRole<Guid>
{
    /// <summary>Описание полномочий роли на русском языке.</summary>
    public string? Description { get; set; }
}

/// <summary>
/// Наименования ролей.
///
/// Состав ролей отражает распределение обязанностей в организации:
/// ведение портфелей и выполнение расчётов, ознакомление с результатами
/// без права изменения, сопровождение системы.
/// </summary>
public static class ApplicationRoles
{
    /// <summary>
    /// Администратор: ведение справочника инструментов, загрузка данных
    /// из внешних источников, управление учётными записями.
    /// </summary>
    public const string Administrator = "Administrator";

    /// <summary>
    /// Аналитик: ведение собственных портфелей, выполнение расчётов
    /// по любым портфелям, формирование отчётов.
    /// </summary>
    public const string Analyst = "Analyst";

    /// <summary>
    /// Наблюдатель: ознакомление со всеми портфелями, результатами расчётов
    /// и отчётами без права изменения данных.
    /// </summary>
    public const string Viewer = "Viewer";

    /// <summary>Все роли системы с описанием полномочий.</summary>
    public static readonly (string Name, string Description)[] All =
    [
        (Administrator,
            "Администратор. Ведение справочника инструментов, загрузка данных " +
            "из внешних источников, управление учётными записями пользователей."),
        (Analyst,
            "Аналитик. Ведение собственных портфелей, выполнение расчётов " +
            "по любым портфелям, формирование отчётов."),
        (Viewer,
            "Наблюдатель. Ознакомление со всеми портфелями, результатами расчётов " +
            "и отчётами без права изменения данных.")
    ];
}

/// <summary>Наименования политик разграничения доступа.</summary>
public static class AuthorizationPolicies
{
    /// <summary>Изменение состава портфелей и позиций.</summary>
    public const string ManagePortfolios = "ManagePortfolios";

    /// <summary>Ведение справочника инструментов и загрузка данных.</summary>
    public const string ManageReferenceData = "ManageReferenceData";

    /// <summary>Управление учётными записями пользователей.</summary>
    public const string ManageUsers = "ManageUsers";
}
