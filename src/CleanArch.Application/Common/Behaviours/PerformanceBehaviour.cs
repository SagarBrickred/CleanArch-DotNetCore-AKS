using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CleanArch.Application.Common.Behaviours;

/// <summary>Flags slow requests (>500ms) as warnings so they surface in App Insights / Log Analytics alerts.</summary>
public class PerformanceBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private const int SlowRequestThresholdMs = 500;
    private readonly ILogger<PerformanceBehaviour<TRequest, TResponse>> _logger;

    public PerformanceBehaviour(ILogger<PerformanceBehaviour<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > SlowRequestThresholdMs)
        {
            _logger.LogWarning("Long running request: {RequestName} ({ElapsedMilliseconds}ms)",
                typeof(TRequest).Name, sw.ElapsedMilliseconds);
        }
        return response;
    }
}
