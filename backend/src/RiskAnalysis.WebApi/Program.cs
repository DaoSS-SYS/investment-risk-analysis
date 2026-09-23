using Microsoft.EntityFrameworkCore;
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

builder.Services.AddControllers();

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
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
