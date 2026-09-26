namespace RiskAnalysis.Application.Abstractions;

/// <summary>
/// Отказ в выполнении действия по причине недостаточности полномочий.
///
/// Выделен в отдельный вид исключения, поскольку отказ такого рода
/// требует иного ответа, нежели ошибка в исходных данных: запрос составлен
/// верно и был бы выполнен, будь он направлен другим пользователем.
/// Веб-приложение отвечает на него состоянием 403.
/// </summary>
public class AccessDeniedException : Exception
{
    public AccessDeniedException(string message) : base(message)
    {
    }
}
