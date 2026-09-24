using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RiskAnalysis.Application.Abstractions;

namespace RiskAnalysis.Infrastructure.BackgroundProcessing;

/// <summary>
/// Очередь асинхронных расчётов, реализованная на основе канала
/// <see cref="Channel{T}"/>.
///
/// Очередь ограничена по длине: при её заполнении постановка нового расчёта
/// приостанавливается до освобождения места. Ограничение предотвращает
/// неограниченный рост потребления памяти при массовой постановке расчётов.
/// </summary>
public class CalculationQueue : ICalculationQueue
{
    private const int Capacity = 256;

    private readonly Channel<Guid> _channel;
    private int _count;

    public CalculationQueue()
    {
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <inheritdoc />
    public int Count => Volatile.Read(ref _count);

    /// <inheritdoc />
    public async ValueTask EnqueueAsync(Guid calculationId, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(calculationId, cancellationToken);
        Interlocked.Increment(ref _count);
    }

    /// <inheritdoc />
    public async ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        var id = await _channel.Reader.ReadAsync(cancellationToken);
        Interlocked.Decrement(ref _count);

        return id;
    }
}

/// <summary>
/// Фоновая служба обработки очереди расчётов.
///
/// Служба последовательно извлекает расчёты из очереди и выполняет их.
/// Для каждого расчёта создаётся отдельная область внедрения зависимостей:
/// контекст базы данных регистрируется с временем жизни, ограниченным
/// областью, и не может использоваться фоновой службой напрямую.
///
/// Отказ отдельного расчёта не должен останавливать обработку очереди,
/// поэтому исключения перехватываются и фиксируются в журнале.
/// </summary>
public class CalculationWorker : BackgroundService
{
    private readonly ICalculationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CalculationWorker> _logger;

    public CalculationWorker(
        ICalculationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<CalculationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Фоновая служба обработки расчётов запущена");

        while (!stoppingToken.IsCancellationRequested)
        {
            Guid calculationId;

            try
            {
                calculationId = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();

                var service = scope.ServiceProvider.GetRequiredService<ICalculationService>();

                await service.ExecuteAsync(calculationId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Необработанная ошибка при выполнении расчёта {CalculationId}", calculationId);
            }
        }

        _logger.LogInformation("Фоновая служба обработки расчётов остановлена");
    }
}
