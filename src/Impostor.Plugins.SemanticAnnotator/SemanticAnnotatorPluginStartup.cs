using System;
using System.IO;
using Coravel;
using Impostor.Api.Events;
using Impostor.Api.Plugins;
using Impostor.Plugins.SemanticAnnotator.Adapters;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Application;
using Impostor.Plugins.SemanticAnnotator.Handlers;
using Impostor.Plugins.SemanticAnnotator.Jobs;
using Impostor.Plugins.SemanticAnnotator.Models;
using Impostor.Plugins.SemanticAnnotator.Ports;
using IO.Swagger.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using TransactionHandler.Tasks;

namespace Impostor.Plugins.SemanticAnnotator 

{
    public static class PrometheusExporterServer
    {
        private static bool _started;

        public static void Start()
        {
            if (!_started)
            {
                var builder = WebApplication.CreateBuilder();

                builder.Services.AddOpenTelemetry().WithMetrics(metrics =>
                {
                    var histogramBoundaries = new double[] { 0, 5, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110 };
                    var histogramBoundariesHigh = new double[] { 0, 500, 1000, 1500, 2000, 2500, 3000 };
                    metrics
                        .AddMeter("SemanticAnnotator.DSS")
                        .AddView("dss_total_duration_ms", new ExplicitBucketHistogramConfiguration
                        {
                            Boundaries = histogramBoundaries
                        })
                        .AddView("dss_annotation_duration_ms", new ExplicitBucketHistogramConfiguration
                        {
                            Boundaries = histogramBoundaries
                        })
                        .AddView("dss_argumentation_duration_ms", new ExplicitBucketHistogramConfiguration
                        {
                            Boundaries = histogramBoundaries
                        })
                        .AddView("dss_notarization_duration_ms", new ExplicitBucketHistogramConfiguration
                        {
                            Boundaries = histogramBoundariesHigh
                        })
                        .AddPrometheusExporter();
                });


                var app = builder.Build();
                app.MapPrometheusScrapingEndpoint(); // espone /metrics
                app.RunAsync(); // non blocca il thread
                _started = true;
            }
        }
    }
    public class SemanticAnnotatorPluginStartup : IPluginStartup
    {
        private readonly IConfiguration _configuration;


        public SemanticAnnotatorPluginStartup()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            _configuration = builder.Build();
        }

        public void ConfigureHost(IHostBuilder host)
        {
            // Nothing extra
        }

        public void ConfigureServices(IServiceCollection services)
        {

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
                client.BaseAddress = new Uri(options.ArgumentationEndpointUrl);
            });

            services.Configure<NotarizationServiceOptions>(_configuration.GetSection("NotarizationService"));

            services.AddSingleton<IBlockchainReSTAPIApi>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<NotarizationServiceOptions>>().Value;
                return new BlockchainReSTAPIApi(options.BlockchainEndpointUrl);
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
            PrometheusExporterServer.Start();
        }
    }
}
