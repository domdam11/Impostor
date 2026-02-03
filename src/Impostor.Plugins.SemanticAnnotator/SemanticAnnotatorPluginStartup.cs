using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Coravel;
using Impostor.Api.Events;
using Impostor.Api.Plugins;
using Impostor.Plugins.SemanticAnnotator.Adapters;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Application;
using Impostor.Plugins.SemanticAnnotator.Controllers;
using Impostor.Plugins.SemanticAnnotator.Handlers;
using Impostor.Plugins.SemanticAnnotator.Infrastructure;
using Impostor.Plugins.SemanticAnnotator.Jobs;
using Impostor.Plugins.SemanticAnnotator.Models.Options;
using Impostor.Plugins.SemanticAnnotator.Ports;
using IO.Swagger.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using StackExchange.Redis;
using TransactionHandler.Tasks;

namespace Impostor.Plugins.SemanticAnnotator

{
    public class PrometheusExporterServer : IHostedService
    {
        private readonly IServiceProvider _root;
        private WebApplication? _app;

        public PrometheusExporterServer(IServiceProvider root)
        {
            _root = root;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://*:5001");

            // ===== metrics / prometheus =====
            builder.Services.AddOpenTelemetry().WithMetrics(metrics =>
            {
                var histogramBoundaries = new double[] { 0, 5, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110 };
                var histogramBoundariesHigh = new double[] { 0, 500, 1000, 1500, 2000, 2500, 3000 };
                metrics
                    .AddMeter("SemanticAnnotator.DSS")
                    .AddView("dss_total_duration_ms", new ExplicitBucketHistogramConfiguration { Boundaries = histogramBoundaries })
                    .AddView("dss_annotation_duration_ms", new ExplicitBucketHistogramConfiguration { Boundaries = histogramBoundaries })
                    .AddView("dss_argumentation_duration_ms", new ExplicitBucketHistogramConfiguration { Boundaries = histogramBoundaries })
                    .AddView("dss_notarization_duration_ms", new ExplicitBucketHistogramConfiguration { Boundaries = histogramBoundariesHigh })
                    .AddPrometheusExporter();
            });

            _app = builder.Build();

            // Espone solo endpoint /metrics
            _app.MapPrometheusScrapingEndpoint();

            _ = _app.RunAsync(cancellationToken);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_app is not null)
            {
                await _app.StopAsync(cancellationToken);
                _app = null;
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
            //services.AddScheduler(); // Coravel scheduler
            services.AddSingleton<DecisionSupportScheduler>();
            services.AddTransient<DecisionSupportJob>();
            services.AddScoped<IDecisionSupportService, DecisionSupportService>();
      

            // Binding dei Thresholds

            services.Configure<AnnotatorServiceOptions>(_configuration.GetSection("AnnotatorService"));
            services.Configure<RedisStorageOptions>(_configuration.GetSection("RedisStorage"));

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
            var useBuffer = _configuration.GetValue("SemanticPlugin:UseBufferMode", false);
            services.AddSingleton<IAnnotator, AnnotatorService>();
            services.AddSingleton<IArgumentationService, ArgumentationApiAdapter>();
            services.AddSingleton<IConnectionMultiplexer>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<RedisStorageOptions>>().Value;


                if (options?.Enabled == true)
                {
                    try
                    {
                        return ConnectionMultiplexer.Connect(options.ConnectionString);

                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Unable to connect to Redis at {options.ConnectionString}", ex);
                    }
                }
                else
                {
                    return null;
                }
            });


            services.AddSingleton<IGameEventStorage, RedisGameEventStorage>();
            services.AddSingleton<ITransactionManager, TransactionManager>();

            services.AddSingleton<ISemanticEventRecorder, SemanticEventRecorderService>();

            services.Configure<SemanticPluginOptions>(_configuration.GetSection("SemanticPlugin"));
            services.AddSingleton<KeyedTaskQueue>();

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
            services.AddHostedService<PrometheusExporterServer>();
        }
    }
}
