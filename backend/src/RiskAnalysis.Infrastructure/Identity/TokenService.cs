using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace RiskAnalysis.Infrastructure.Identity;

/// <summary>
/// Параметры выпуска маркеров доступа.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Издатель маркера.</summary>
    public string Issuer { get; set; } = "RiskAnalysis";

    /// <summary>Потребитель маркера.</summary>
    public string Audience { get; set; } = "RiskAnalysis";

    /// <summary>
    /// Ключ подписи. Задаётся вне репозитория — в хранилище секретов среды
    /// разработки либо в переменных окружения при размещении приложения.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Срок действия маркера, ч.</summary>
    public int LifetimeHours { get; set; } = 12;
}

/// <summary>
/// Выпуск маркеров доступа.
///
/// Применяется схема с маркером в формате JWT. Основание выбора: клиентское
/// приложение выполняется в браузере и обращается к программному интерфейсу
/// с другого сетевого адреса, поэтому проверка подлинности не может
/// опираться на состояние сеанса на стороне сервера. Маркер содержит
/// сведения о пользователе и его ролях, подписан на стороне сервера
/// и проверяется без обращения к базе данных.
///
/// Сведения, помещаемые в маркер, не являются тайной: он подписан, но
/// не зашифрован. В маркер включаются идентификатор, имя пользователя,
/// фамилия с инициалами и перечень ролей — то, что требуется клиентскому
/// приложению для отображения интерфейса и разграничения доступа.
/// </summary>
public class TokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Не задан либо задан слишком коротким ключ подписи маркеров доступа " +
                "(Jwt:SigningKey). Длина ключа должна составлять не менее 32 символов. " +
                "Для среды разработки задайте его командой: " +
                "dotnet user-secrets set \"Jwt:SigningKey\" \"...\"");
        }
    }

    /// <summary>
    /// Выпускает маркер доступа для пользователя.
    /// </summary>
    /// <param name="user">Пользователь.</param>
    /// <param name="roles">Роли пользователя.</param>
    public (string Token, DateTimeOffset ExpiresAt) Issue(
        ApplicationUser user, IEnumerable<string> roles)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(_options.LifetimeHours);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new("fullName", user.FullName)
        };

        if (!string.IsNullOrWhiteSpace(user.Position))
        {
            claims.Add(new Claim("position", user.Position));
        }

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
