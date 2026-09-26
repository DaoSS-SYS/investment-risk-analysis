using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.Infrastructure.Identity;

namespace RiskAnalysis.WebApi.Controllers;

/// <summary>
/// Проверка подлинности пользователей и управление учётными записями.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ICurrentUser _currentUser;

    public AuthController(IAuthService auth, ICurrentUser currentUser)
    {
        _auth = auth;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Выполняет вход в систему и выпускает маркер доступа.
    ///
    /// Полученный маркер передаётся в последующих запросах в заголовке
    /// Authorization со схемой Bearer.
    /// </summary>
    /// <param name="request">Имя пользователя и пароль.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResult>> Login(
        [FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Не указано имя пользователя либо пароль." });
        }

        var result = await _auth.LoginAsync(
            request, HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);

        // Сообщение не различает отсутствие учётной записи и неверный пароль:
        // различие позволило бы установить, какие имена пользователей
        // существуют в системе.
        return result is null
            ? Unauthorized(new { error = "Неверное имя пользователя либо пароль." })
            : Ok(result);
    }

    /// <summary>Возвращает сведения о пользователе, выполняющем запрос.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserView>> Me(CancellationToken cancellationToken)
    {
        var users = await _auth.GetUsersAsync(cancellationToken);
        var current = users.FirstOrDefault(user => user.Id == _currentUser.Id);

        return current is null ? NotFound() : Ok(current);
    }

    /// <summary>Возвращает перечень учётных записей.</summary>
    [Authorize(Policy = AuthorizationPolicies.ManageUsers)]
    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<UserView>>> GetUsers(
        CancellationToken cancellationToken)
    {
        return Ok(await _auth.GetUsersAsync(cancellationToken));
    }

    /// <summary>Создаёт учётную запись.</summary>
    /// <param name="request">Реквизиты учётной записи.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    [Authorize(Policy = AuthorizationPolicies.ManageUsers)]
    [HttpPost("users")]
    public async Task<ActionResult<UserView>> CreateUser(
        [FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _auth.CreateUserAsync(request, cancellationToken));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Переводит учётную запись в действующие либо недействующие.
    /// Учётные записи не удаляются: удаление нарушило бы связность
    /// журнала действий.
    /// </summary>
    /// <param name="id">Идентификатор учётной записи.</param>
    /// <param name="isActive">Требуемое состояние.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    [Authorize(Policy = AuthorizationPolicies.ManageUsers)]
    [HttpPut("users/{id:guid}/active")]
    public async Task<IActionResult> SetUserActive(
        Guid id, [FromQuery] bool isActive, CancellationToken cancellationToken)
    {
        if (id == _currentUser.Id && !isActive)
        {
            return BadRequest(new
            {
                error = "Нельзя перевести в недействующие собственную учётную запись."
            });
        }

        return await _auth.SetUserActiveAsync(id, isActive, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    /// <summary>Возвращает перечень ролей с описанием полномочий.</summary>
    [HttpGet("roles")]
    public ActionResult<object> GetRoles()
    {
        return Ok(ApplicationRoles.All.Select(role => new
        {
            Name = role.Name,
            Description = role.Description
        }));
    }
}

/// <summary>
/// Журнал действий пользователей.
/// </summary>
[ApiController]
[Route("api/audit")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.ManageUsers)]
public class AuditController : ControllerBase
{
    private readonly IAuditService _audit;

    public AuditController(IAuditService audit) => _audit = audit;

    /// <summary>Возвращает записи журнала действий.</summary>
    /// <param name="limit">Максимальное число записей.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditRecordView>>> GetRecords(
        [FromQuery] int limit = 100, CancellationToken cancellationToken = default)
    {
        return Ok(await _audit.GetRecordsAsync(limit, cancellationToken));
    }
}
