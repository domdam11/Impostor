using Impostor.Api.Events;
using Impostor.Api.Plugins;
using Impostor.Plugins.SemanticAnnotator.Adapters;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Application;
using Impostor.Plugins.SemanticAnnotator.Handlers;
using Impostor.Plugins.SemanticAnnotator.Jobs;
using Impostor.Plugins.SemanticAnnotator.Models;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Coravel;
using System;
using TransactionHandler.Tasks;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using IO.Swagger.Api;

namespace Impostor.Plugins.SemanticAnnotator
{
    public class SemanticAnnotatorPluginStartup : IPluginStartup
    {
        private readonly IConfiguration _configuration;


        public SemanticAnnotatorPluginStartup()
        {
    
            var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory()) // or use Path.Combine for plugin folder
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            _configuration = builder.Build();
    
        }

        public void ConfigureHost(IHostBuilder host)
        {
            // Nothing extra
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Coravel
            services.AddScheduler();
            services.AddSingleton(_configuration);
            // Binding dei Thresholds
   
            services.Configure<AnnotatorServiceOptions>(_configuration.GetSection("AnnotatorService"));
      

            // Event listeners
            services.AddSingleton<IEventListener, GameEventListener>();
            services.AddSingleton<IEventListener, PlayerEventListener>();
            services.AddSingleton<IEventListener, MeetingEventListener>();
            services.AddSingleton<IEventListener, ShipEventListener>();
            services.AddSingleton<IEventListener, ClientEventListener>();

            // Core
            services.AddSingleton<GameEventCacheManager>();
            services.AddSingleton<AnnotatorEngine>();

            // Modalità buffer: true -> uso di buffer + job Coravel
            bool useBuffer = _configuration.GetValue<bool>("UseBufferMode", false);
            services.AddSingleton<IAnnotator, AnnotatorService>();
            services.AddSingleton<IArgumentationService, ArgumentationApiAdapter>();
            services.AddSingleton<INotarizationService, NotarizationAdapter>();
            services.AddSingleton<ITransactionManager, TransactionManager>();
   
            services.Configure<ArgumentationServiceOptions>(_configuration.GetSection("ArgumentationService"));

            services.AddHttpClient<IArgumentationService, ArgumentationApiAdapter>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<ArgumentationServiceOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
            });

            services.Configure<BlockchainApiOptions>(_configuration.GetSection("BlockchainApi"));

            services.AddSingleton<IBlockchainReSTAPIApi>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<BlockchainApiOptions>>().Value;
                return new BlockchainReSTAPIApi(options.BaseUrl);
            });

            if (useBuffer)
            {
                // Buffer e orchestrazione asincrona
                services.AddSingleton<IAnnotationBuffer, InMemoryAnnotationBuffer>();
                services.AddSingleton<IArgumentationResultBuffer, InMemoryArgumentationResultBuffer>();

                // Job asincroni Coravel
                services.AddTransient<AnnotationJob>();
                services.AddTransient<GameNotarizationJob>();

            }
            else
            {
                services.AddSingleton<IDecisionSupportService, DecisionSupportService>();

            }
        }
    }
}
