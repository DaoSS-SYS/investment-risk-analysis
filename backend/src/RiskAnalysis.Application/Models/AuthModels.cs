namespace RiskAnalysis.Application.Models;

/// <summary>Запрос на вход в систему.</summary>
/// <param name="UserName">Имя пользователя.</param>
/// <param name="Password">Пароль.</param>
public sealed record LoginRequest(string UserName, string Password);

/// <summary>Сведения о пользователе.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="UserName">Имя пользователя.</param>
/// <param name="FullName">Фамилия, имя и отчество.</param>
/// <param name="Position">Должность.</param>
/// <param name="Email">Адрес электронной почты.</param>
/// <param name="Roles">Роли.</param>
/// <param name="IsActive">Признак действующей учётной записи.</param>
/// <param name="CreatedAt">Дата создания.</param>
/// <param name="LastLoginAt">Время последнего входа.</param>
public sealed record UserView(
    Guid Id,
    string UserName,
    string FullName,
    string? Position,
    string? Email,
    IReadOnlyList<string> Roles,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

/// <summary>Результат успешного входа в систему.</summary>
/// <param name="Token">Маркер доступа.</param>
/// <param name="ExpiresAt">Время истечения срока действия маркера.</param>
/// <param name="User">Сведения о пользователе.</param>
public sealed record LoginResult(string Token, DateTimeOffset ExpiresAt, UserView User);

/// <summary>Запрос на создание учётной записи.</summary>
/// <param name="UserName">Имя пользователя.</param>
/// <param name="Password">Пароль.</param>
/// <param name="FullName">Фамилия, имя и отчество.</param>
/// <param name="Position">Должность.</param>
/// <param name="Email">Адрес электронной почты.</param>
/// <param name="Role">Роль.</param>
public sealed record CreateUserRequest(
    string UserName,
    string Password,
    string FullName,
    string? Position,
    string? Email,
    string Role);

/// <summary>Запись журнала действий.</summary>
/// <param name="Id">Идентификатор записи.</param>
/// <param name="UserName">Имя пользователя.</param>
/// <param name="Action">Вид действия.</param>
/// <param name="EntityType">Вид объекта.</param>
/// <param name="EntityId">Идентификатор объекта.</param>
/// <param name="Description">Описание действия.</param>
/// <param name="IpAddress">Сетевой адрес.</param>
/// <param name="Timestamp">Время совершения действия.</param>
public sealed record AuditRecordView(
    long Id,
    string UserName,
    string Action,
    string EntityType,
    string? EntityId,
    string Description,
    string? IpAddress,
    DateTimeOffset Timestamp);
