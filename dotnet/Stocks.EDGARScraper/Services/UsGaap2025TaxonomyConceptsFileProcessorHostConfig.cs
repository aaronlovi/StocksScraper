using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stocks.EDGARScraper.Models;

namespace Stocks.EDGARScraper.Services;

public static class UsGaap2025TaxonomyConceptsFileProcessorHostConfig {
    public static IServiceCollection ConfigureTaxonomyConceptsFileProcessor(this IServiceCollection services, HostBuilderContext context) {
        IConfigurationSection section = context.Configuration.GetSection(nameof(UsGaap2025TaxonomyConceptsFileProcessorOptions));
        return services.
            Configure<UsGaap2025TaxonomyConceptsFileProcessorOptions>(section).
            AddSingleton<UsGaap2025TaxonomyConceptsFileProcessor>();
    }
}
