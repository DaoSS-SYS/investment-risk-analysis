using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RiskAnalysis.Application.Abstractions;

namespace RiskAnalysis.WebApi.Filters;

/// <summary>
/// Преобразует отказ по недостаточности полномочий в ответ с состоянием 403.
///
/// Разграничение доступа выполняется на двух уровнях. Принадлежность
/// пользователя к роли проверяется до выполнения метода — средствами
/// политик доступа. Принадлежность конкретного объекта пользователю может
/// быть проверена только после обращения к базе данных, то есть внутри
/// службы слоя приложения. Службы не зависят от веб-приложения и не могут
/// формировать ответ самостоятельно: они сообщают об отказе исключением,
/// а настоящий перехватчик переводит его в состояние протокола.
///
/// Перехватчик зарегистрирован глобально: отказ такого рода обрабатывается
/// единообразно во всех контроллерах и не требует повторения обработки
/// в каждом методе.
/// </summary>
public class AccessDeniedExceptionFilter : IExceptionFilter
{
    private readonly ILogger<AccessDeniedExceptionFilter> _logger;

    public AccessDeniedExceptionFilter(ILogger<AccessDeniedExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not AccessDeniedException exception)
        {
            return;
        }

        _logger.LogWarning(
            "Отказано в выполнении действия {Action}: {Reason}",
            context.ActionDescriptor.DisplayName, exception.Message);

        context.Result = new ObjectResult(new { error = exception.Message })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };

        context.ExceptionHandled = true;
    }
}
