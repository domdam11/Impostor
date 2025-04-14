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

namespace Impostor.Plugins.SemanticAnnotator
{
    public class SemanticAnnotatorPluginStartup : IPluginStartup
    {
        private readonly IConfiguration _configuration;

        public SemanticAnnotatorPluginStartup()
        {
        }

        public SemanticAnnotatorPluginStartup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureHost(IHostBuilder host)
        {
            // Nothing extra
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Coravel
            services.AddScheduler();

            // Binding dei Thresholds
            var thresholds = new Thresholds();
            _configuration.GetSection("Thresholds").Bind(thresholds);
            services.AddSingleton(thresholds);

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
            bool useBuffer = _configuration.GetValue<bool>("UseBufferMode", true);

            if (useBuffer)
            {
                // Buffer e orchestrazione asincrona
                services.AddSingleton<IAnnotationBuffer, InMemoryAnnotationBuffer>();
                services.AddSingleton<IArgumentationResultBuffer, InMemoryArgumentationResultBuffer>();

                services.AddSingleton<IAnnotator, AnnotatorService>();
                services.AddSingleton<IArgumentationService, ArgumentationApiAdapter>();
                services.AddSingleton<INotarizationService, NotarizationAdapter>();
                services.AddSingleton<ITransactionManager, TransactionManager>();

                // Job asincroni Coravel
                services.AddTransient<AnnotationJob>();
                services.AddTransient<GameNotarizationJob>();

            }
            else
            {
                // Modalità diretta con orchestrazione coordinata
                services.AddSingleton<IAnnotator, AnnotatorService>();
                services.AddSingleton<IArgumentationService, ArgumentationApiAdapter>();
                services.AddSingleton<INotarizationService, NotarizationAdapter>();
                services.AddSingleton<ITransactionManager, TransactionManager>();
                services.AddSingleton<IDecisionSupportService, DecisionSupportService>();

            }
        }
    }
}
