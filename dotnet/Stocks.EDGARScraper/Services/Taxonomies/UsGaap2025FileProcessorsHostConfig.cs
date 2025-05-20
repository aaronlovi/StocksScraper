using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stocks.EDGARScraper.Models.Taxonomies;

namespace Stocks.EDGARScraper.Services.Taxonomies;

public static class UsGaap2025FileProcessorsHostConfig {
    public static IServiceCollection ConfigureTaxonomyConceptsFileProcessor(this IServiceCollection services, HostBuilderContext context) {
        IConfigurationSection conceptOptionsSection = context.Configuration.GetSection(nameof(UsGaap2025ConceptsFileProcessorOptions));
        IConfigurationSection presentationOptionsSection = context.Configuration.GetSection(nameof(UsGaap2025PresentationFileProcessorOptions));
        return services.
            Configure<UsGaap2025ConceptsFileProcessorOptions>(conceptOptionsSection).
            Configure<UsGaap2025PresentationFileProcessorOptions>(presentationOptionsSection).
            AddSingleton<UsGaap2025ConceptsFileProcessor>().
            AddSingleton<UsGaap2025PresentationFileProcessor>();
    }
}
