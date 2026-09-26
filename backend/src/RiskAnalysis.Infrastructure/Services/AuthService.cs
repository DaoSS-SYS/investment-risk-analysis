using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Application.Models;
using RiskAnalysis.Domain.Entities;
using RiskAnalysis.Domain.Enums;
using RiskAnalysis.Infrastructure.Identity;
using RiskAnalysis.Infrastructure.Persistence;

namespace RiskAnalysis.Infrastructure.Services;

/// <summary>
/// Проверка подлинности пользователей и управление учётными записями.
/// </summary>
public class AuthService : IAuthService
{
    private const string DefaultAdministratorUserName = "admin";

    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly TokenService _tokens;
    private readonly IAuditService _audit;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles,
        TokenService tokens,
        IAuditService audit,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _users = users;
        _roles = roles;
        _tokens = tokens;
        _audit = audit;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LoginResult?> LoginAsync(
        LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByNameAsync(request.UserName);

        // Сообщение об ошибке не различает отсутствие учётной записи и
        // неверный пароль: различие позволило бы установить, какие имена
        // пользователей существуют в системе.
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning(
                "Неудачная попытка входа с именем {UserName} с адреса {Address}",
                request.UserName, ipAddress);

            return null;
        }

        if (!await _users.CheckPasswordAsync(user, request.Password))
        {
            await _users.AccessFailedAsync(user);

            _logger.LogWarning(
                "Неверный пароль для пользователя {UserName} с адреса {Address}",
                request.UserName, ipAddress);

            return null;
        }

        await _users.ResetAccessFailedCountAsync(user);

        var roles = await _users.GetRolesAsync(user);
        var (token, expiresAt) = _tokens.Issue(user, roles);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _users.UpdateAsync(user);

        await _audit.RecordAsync(
            user.Id, user.UserName ?? string.Empty,
            AuditAction.Login, "User", user.Id.ToString(),
            $"Вход в систему, роли: {string.Join(", ", roles)}", cancellationToken);

        _logger.LogInformation(
            "Вход в систему: {UserName}, роли: {Roles}", user.UserName, string.Join(", ", roles));

        return new LoginResult(token, expiresAt, Map(user, roles));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserView>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await _users.Users
            .AsNoTracking()
            .OrderBy(user => user.FullName)
            .ToListAsync(cancellationToken);

        var result = new List<UserView>(users.Count);

        foreach (var user in users)
        {
            result.Add(Map(user, await _users.GetRolesAsync(user)));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<UserView> CreateUserAsync(
        CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        if (!ApplicationRoles.All.Any(role => role.Name == request.Role))
        {
            throw new ArgumentException(
                $"Роль «{request.Role}» не определена в системе.", nameof(request));
        }

        var existing = await _users.FindByNameAsync(request.UserName);

        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"Пользователь с именем «{request.UserName}» уже существует.");
        }

        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            FullName = request.FullName,
            Position = request.Position,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var created = await _users.CreateAsync(user, request.Password);

        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                "Учётная запись не создана: " +
                string.Join("; ", created.Errors.Select(error => error.Description)));
        }

        await _users.AddToRoleAsync(user, request.Role);

        await _audit.RecordAsync(
            AuditAction.Create, "User", user.Id.ToString(),
            $"Создана учётная запись {user.UserName} ({user.FullName}), роль: {request.Role}",
            cancellationToken);

        return Map(user, [request.Role]);
    }

    /// <inheritdoc />
    public async Task<bool> SetUserActiveAsync(
        Guid userId, bool isActive, CancellationToken cancellationToken = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return false;
        }

        user.IsActive = isActive;
        await _users.UpdateAsync(user);

        await _audit.RecordAsync(
            AuditAction.Update, "User", user.Id.ToString(),
            isActive
                ? $"Учётная запись {user.UserName} переведена в действующие"
                : $"Учётная запись {user.UserName} переведена в недействующие",
            cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task EnsureSeedDataAsync(CancellationToken cancellationToken = default)
    {
        foreach (var (name, description) in ApplicationRoles.All)
        {
            if (await _roles.RoleExistsAsync(name))
            {
                continue;
            }

            await _roles.CreateAsync(new ApplicationRole
            {
                Name = name,
                Description = description
            });

            _logger.LogInformation("Создана роль {Role}", name);
        }

        if (await _users.FindByNameAsync(DefaultAdministratorUserName) is not null)
        {
            return;
        }

        // Пароль учётной записи администратора, создаваемой при первом
        // запуске, задаётся вне репозитория. При его отсутствии учётная
        // запись не создаётся: значение по умолчанию в общедоступном
        // репозитории было бы известно всем.
        var password = _configuration["Seed:AdministratorPassword"];

        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning(
                "Учётная запись администратора не создана: не задан параметр " +
                "Seed:AdministratorPassword. Задайте его командой " +
                "dotnet user-secrets set \"Seed:AdministratorPassword\" \"...\"");

            return;
        }

        var administrator = new ApplicationUser
        {
            UserName = DefaultAdministratorUserName,
            FullName = "Администратор системы",
            Position = "Сопровождение информационной системы",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var created = await _users.CreateAsync(administrator, password);

        if (created.Succeeded)
        {
            await _users.AddToRoleAsync(administrator, ApplicationRoles.Administrator);

            _logger.LogInformation(
                "Создана учётная запись администратора {UserName}", administrator.UserName);
        }
        else
        {
            _logger.LogError(
                "Учётная запись администратора не создана: {Errors}",
                string.Join("; ", created.Errors.Select(error => error.Description)));
        }
    }

    private static UserView Map(ApplicationUser user, IEnumerable<string> roles) => new(
        Id: user.Id,
        UserName: user.UserName ?? string.Empty,
        FullName: user.FullName,
        Position: user.Position,
        Email: user.Email,
        Roles: roles.ToList(),
        IsActive: user.IsActive,
        CreatedAt: user.CreatedAt,
        LastLoginAt: user.LastLoginAt);
}

/// <summary>
/// Ведение журнала действий пользователей.
/// </summary>
public class AuditService : IAuditService
{
    private readonly RiskAnalysisDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        RiskAnalysisDbContext db, ICurrentUser currentUser, ILogger<AuditService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task RecordAsync(
        AuditAction action,
        string entityType,
        string? entityId,
        string description,
        CancellationToken cancellationToken = default)
    {
        return RecordAsync(
            _currentUser.Id, _currentUser.UserName,
            action, entityType, entityId, description, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RecordAsync(
        Guid? userId,
        string userName,
        AuditAction action,
        string entityType,
        string? entityId,
        string description,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _db.AuditRecords.Add(new AuditRecord
            {
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Description = description.Length > 1024 ? description[..1024] : description,
                IpAddress = _currentUser.IpAddress,
                Timestamp = DateTimeOffset.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Отказ при записи в журнал не должен приводить к отказу самой
            // операции: журнал ведётся для последующего разбора, а не для
            // управления выполнением.
            _logger.LogError(exception,
                "Не удалось записать в журнал действие {Action} над {EntityType}",
                action, entityType);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditRecordView>> GetRecordsAsync(
        int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _db.AuditRecords
            .AsNoTracking()
            .OrderByDescending(record => record.Timestamp)
            .Take(Math.Clamp(limit, 1, 1000))
            .Select(record => new AuditRecordView(
                record.Id,
                record.UserName,
                record.Action.ToString(),
                record.EntityType,
                record.EntityId,
                record.Description,
                record.IpAddress,
                record.Timestamp))
            .ToListAsync(cancellationToken);
    }
}
