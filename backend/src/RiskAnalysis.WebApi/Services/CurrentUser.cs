using System.Security.Claims;
using RiskAnalysis.Application.Abstractions;

namespace RiskAnalysis.WebApi.Services;

/// <summary>
/// Сведения о пользователе, выполняющем текущий запрос.
///
/// Извлекаются из удостоверения, восстановленного по маркеру доступа.
/// Для неаутентифицированного запроса идентификатор не определён, а имя
/// принимает значение, обозначающее отсутствие пользователя: журнал действий
/// должен оставаться заполненным и в этом случае.
/// </summary>
public class CurrentUser : ICurrentUser
{
    private const string AnonymousName = "не определён";

    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    /// <inheritdoc />
    public Guid? Id
    {
        get
        {
            var value = Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    /// <inheritdoc />
    public string UserName =>
        Principal?.FindFirstValue(ClaimTypes.Name) is { Length: > 0 } name ? name : AnonymousName;

    /// <inheritdoc />
    public string? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    /// <inheritdoc />
    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
