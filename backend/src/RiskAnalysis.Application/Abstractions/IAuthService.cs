using RiskAnalysis.Application.Models;
using RiskAnalysis.Domain.Enums;

namespace RiskAnalysis.Application.Abstractions;

/// <summary>
/// Проверка подлинности пользователей и управление учётными записями.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Выполняет вход в систему. При успешной проверке выпускает маркер
    /// доступа и фиксирует вход в журнале действий.
    /// </summary>
    Task<LoginResult?> LoginAsync(
        LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Возвращает перечень учётных записей.</summary>
    Task<IReadOnlyList<UserView>> GetUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>Создаёт учётную запись.</summary>
    Task<UserView> CreateUserAsync(
        CreateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Изменяет признак действующей учётной записи. Учётные записи
    /// не удаляются: удаление нарушило бы связность журнала действий.
    /// </summary>
    Task<bool> SetUserActiveAsync(
        Guid userId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт роли и учётную запись администратора при первом запуске
    /// приложения, если они отсутствуют.
    /// </summary>
    Task EnsureSeedDataAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Ведение журнала действий пользователей.
/// </summary>
public interface IAuditService
{
    /// <summary>Фиксирует действие в журнале.</summary>
    Task RecordAsync(
        AuditAction action,
        string entityType,
        string? entityId,
        string description,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Фиксирует действие в журнале с явным указанием пользователя.
    ///
    /// Применяется при записи входа в систему: на момент проверки
    /// подлинности запрос ещё не содержит удостоверения, и сведения
    /// о пользователе не могут быть получены из него.
    /// </summary>
    Task RecordAsync(
        Guid? userId,
        string userName,
        AuditAction action,
        string entityType,
        string? entityId,
        string description,
        CancellationToken cancellationToken = default);

    /// <summary>Возвращает записи журнала.</summary>
    Task<IReadOnlyList<AuditRecordView>> GetRecordsAsync(
        int limit = 100, CancellationToken cancellationToken = default);
}

/// <summary>
/// Сведения о пользователе, выполняющем текущий запрос.
///
/// Абстракция введена для того, чтобы службы слоя приложения не зависели
/// от способа передачи сведений о пользователе, принятого в веб-приложении.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Идентификатор пользователя. Не определён для неаутентифицированного запроса.</summary>
    Guid? Id { get; }

    /// <summary>Имя пользователя.</summary>
    string UserName { get; }

    /// <summary>Сетевой адрес, с которого выполняется запрос.</summary>
    string? IpAddress { get; }

    /// <summary>Проверяет принадлежность пользователя к роли.</summary>
    bool IsInRole(string role);
}
