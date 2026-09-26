using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RiskAnalysis.Application.Abstractions;
using RiskAnalysis.Infrastructure.Identity;
using RiskAnalysis.WebApi.Filters;
using RiskAnalysis.WebApi.Services;
using Microsoft.OpenApi;
using RiskAnalysis.Infrastructure;
using RiskAnalysis.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Журналирование. Serilog пишет структурированный журнал в консоль и в файл,
// что используется при диагностике отказов внешних источников данных.
// ---------------------------------------------------------------------------
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/riskanalysis-.log", rollingInterval: Serilog.RollingInterval.Day));

// ---------------------------------------------------------------------------
// Состав сервисов приложения
// ---------------------------------------------------------------------------
builder.Services.AddInfrastructure(builder.Configuration);

// ---------------------------------------------------------------------------
// Проверка подлинности и разграничение доступа
// ---------------------------------------------------------------------------
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var signingKey = jwtSection["SigningKey"];

if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
{
    throw new InvalidOperationException(
        "Не задан либо задан слишком коротким ключ подписи маркеров доступа " +
        "(Jwt:SigningKey). Длина ключа должна составлять не менее 32 символов. " +
        "Для среды разработки задайте его командой: " +
        "dotnet user-secrets set \"Jwt:SigningKey\" \"...\"");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"] ?? "RiskAnalysis",
            ValidAudience = jwtSection["Audience"] ?? "RiskAnalysis",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),

            // Допуск на расхождение часов устанавливается нулевым: при
            // значении по умолчанию маркер оставался бы действующим ещё
            // пять минут после истечения срока.
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Изменение состава портфелей и позиций доступно аналитику
    // и администратору; наблюдатель имеет доступ только на чтение.
    options.AddPolicy(AuthorizationPolicies.ManagePortfolios, policy =>
        policy.RequireRole(ApplicationRoles.Analyst, ApplicationRoles.Administrator));

    // Ведение справочника инструментов и загрузка данных из внешних
    // источников отнесены к обязанностям сопровождения системы.
    options.AddPolicy(AuthorizationPolicies.ManageReferenceData, policy =>
        policy.RequireRole(ApplicationRoles.Administrator));

    options.AddPolicy(AuthorizationPolicies.ManageUsers, policy =>
        policy.RequireRole(ApplicationRoles.Administrator));
});

builder.Services.AddControllers(options =>
    {
        // Требование подлинности применяется ко всем методам интерфейса.
        // Исключения обозначаются явно признаком AllowAnonymous: такой
        // порядок исключает случайное оставление метода без защиты.
        options.Filters.Add(new AuthorizeFilter(
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()));

        // Отказ по принадлежности объекта пользователю обнаруживается
        // в службах слоя приложения и переводится в состояние 403.
        options.Filters.Add<AccessDeniedExceptionFilter>();
    })
    .AddJsonOptions(options =>
    {
        // Перечисления передаются клиенту строковыми значениями:
        // это делает ответы интерфейса самодокументируемыми.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Информационная система количественного анализа рисков инвестиционной деятельности",
        Version = "v1",
        Description = "Программный интерфейс системы: справочник инструментов, загрузка котировок, " +
                      "портфели и расчёт показателей инвестиционного риска."
    });

    // Описание схемы передачи маркера доступа: позволяет обращаться
    // к защищённым методам непосредственно из страницы документации.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Маркер доступа, полученный методом /api/auth/login"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", document), new List<string>() }
    });

    // Подключение XML-комментариев к описанию методов интерфейса.
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Проверка работоспособности: доступность СУБД.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RiskAnalysisDbContext>("database");

// Политика CORS для клиентского приложения на React.
const string CorsPolicy = "frontend";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Конвейер обработки запросов
// ---------------------------------------------------------------------------
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "RiskAnalysis API v1");
        options.DocumentTitle = "RiskAnalysis API";
    });
}

app.UseCors(CorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

// Создание ролей и учётной записи администратора при первом запуске.
using (var scope = app.Services.CreateScope())
{
    var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
    await auth.EnsureSeedDataAsync();
}

app.Run();
