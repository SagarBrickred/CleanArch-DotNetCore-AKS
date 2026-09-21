using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CleanArch.Application.Common.Behaviours;

/// <summary>Structured request/response logging for every MediatR request — feeds Application Insights via ILogger.</summary>
public class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;

    public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger) => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Handling {RequestName}", requestName);
        try
        {
            var response = await next();
            _logger.LogInformation("Handled {RequestName} in {ElapsedMilliseconds}ms", requestName, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {RequestName} after {ElapsedMilliseconds}ms", requestName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
