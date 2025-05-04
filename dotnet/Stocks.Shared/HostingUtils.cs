using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Stocks.Shared;

public class HostingUtils {
    public static ILogger GetBootstrapLogger<T>() {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        var serviceCollection = new ServiceCollection();
        _ = serviceCollection.AddLogging(builder => {
            _ = builder.ClearProviders();
            _ = builder.AddSerilog(Log.Logger);
        });

        ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();
        ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        return loggerFactory.CreateLogger<T>();
    }
}
