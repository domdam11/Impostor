using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Impostor.Api.Events.Managers;
using Impostor.Api.Games;
using Impostor.Api.Games.Managers;
using Impostor.Api.Innersloth;
using Impostor.Api.Innersloth.GameOptions;
using Impostor.Api.Net;
using Impostor.Api.Net.Custom;
using Impostor.Api.Net.Messages;
using Impostor.Api.Net.Messages.C2S;
using Impostor.Api.Net.Manager;
using Impostor.Api.Utils;
using Impostor.Hazel.Abstractions;
using Impostor.Hazel;
using Impostor.Hazel.Extensions;
using Impostor.Server;
using Impostor.Server.Events;
using Impostor.Server.Net;
using Impostor.Server.Net.Custom;
using Impostor.Server.Net.Factories;
using Impostor.Server.Net.Manager;
using Impostor.Server.Recorder;
using Impostor.Server.Utils;
using Impostor.Tools.ServerReplay.Mocks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Serilog;
using ILogger = Serilog.ILogger;
// Import namespaces for accessing the Semantic Annotator plugin
using Impostor.Plugins.SemanticAnnotator;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Coravel;
using Microsoft.Extensions.Configuration;
using Impostor.Plugins.SemanticAnnotator.Models;
using Impostor.Plugins.SemanticAnnotator.Ports;

namespace Impostor.Tools.ServerReplay
{
    internal static class Program
    {
        // Serilog logger for logging application activity
        private static readonly ILogger Logger = Log.ForContext(typeof(Program));

        // Dictionaries mapping client IDs to necessary session objects
        private static readonly Dictionary<int, IHazelConnection> Connections = new Dictionary<int, IHazelConnection>();
        private static readonly Dictionary<int, IGameOptions> GameOptions = new Dictionary<int, IGameOptions>();

        // Dependency Injection ServiceProvider
        private static ServiceProvider _serviceProvider;

        // Other required objects for handling messages
        private static ObjectPool<MessageReader> _readerPool;
        private static MockGameCodeFactory _gameCodeFactory;
        private static ClientManager _clientManager;
        private static GameManager _gameManager;

        // Fake DateTimeProvider to simulate time progression in recorded sessions
        public static FakeDateTimeProvider _fakeDateTimeProvider;

        /// <summary>
        /// Entry point of the application that processes and replays .dat session files.
        /// </summary>
        private static async Task Main(string[] args)
        {
            // Configure Serilog for logging
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger();

            // Measure execution time
            var stopwatch = Stopwatch.StartNew();

            // Build our DI service provider
           // _serviceProvider = BuildServices();

           // var semanticAnnotatorPlugin = _serviceProvider.GetRequiredService<SemanticAnnotatorPlugin>();
            //await semanticAnnotatorPlugin.EnableAsync(); // Schedules the periodic annotation task

            // Read all .dat files from the "sessions" directory
            foreach (var file in Directory.GetFiles("..\\" +
                "..\\..\\sessions\\", "*.dat"))
            {
                // Clear dictionaries at the start of each session
                Connections.Clear();
                GameOptions.Clear();

                // Create a new ServiceProvider (DI) for each session file
                _serviceProvider = BuildServices();

                // Retrieve shared services
                _readerPool = _serviceProvider.GetRequiredService<ObjectPool<MessageReader>>();
                _gameCodeFactory = _serviceProvider.GetRequiredService<MockGameCodeFactory>();
                _clientManager = _serviceProvider.GetRequiredService<ClientManager>();
                _gameManager = _serviceProvider.GetRequiredService<GameManager>();
                _fakeDateTimeProvider = _serviceProvider.GetRequiredService<FakeDateTimeProvider>();

                // Open the file and start reading
                await using (var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream))
                {
                    // Begin parsing the session
                    await ParseSession(reader);
                }

            }

            // Log total processing time
            var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            Logger.Information($"Took {elapsedMilliseconds}ms.");
        }

        /// <summary>
        /// Sets up and configures required services, including:
        /// - Impostor core classes
        /// - Semantic Annotator plugin
        /// - Coravel scheduling services
        /// </summary>
        private static ServiceProvider BuildServices()
        {
            var configurationBuilder = new ConfigurationBuilder();

            configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());
            configurationBuilder.AddJsonFile("properties.json", true);
            var configuration = configurationBuilder.Build();

            var services = new ServiceCollection();

            services.AddSingleton<IConfiguration>(configuration);
            services.Configure<Thresholds>(configuration); // Registra la configurazione

            // Set up a mock ServerEnvironment
            services.AddSingleton(new ServerEnvironment
            {
                IsReplay = true,
            });

            // Add a FakeDateTimeProvider to handle artificial time management
            services.AddSingleton<FakeDateTimeProvider>();
            services.AddSingleton<IDateTimeProvider>(p => p.GetRequiredService<FakeDateTimeProvider>());

            // Configure logging
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog();
            });

            // Register necessary Impostor services
            services.AddSingleton<GameManager>();
            services.AddSingleton<IGameManager>(p => p.GetRequiredService<GameManager>());
            services.AddSingleton<MockGameCodeFactory>();
            services.AddSingleton<IGameCodeFactory>(p => p.GetRequiredService<MockGameCodeFactory>());
            services.AddSingleton<ICompatibilityManager, CompatibilityManager>();
            services.AddSingleton<ClientManager>();
            services.AddSingleton<IClientFactory, ClientFactory<Client>>();
            services.AddSingleton<IEventManager, EventManager>();

            // Register custom message managers
            services.AddEventPools();
            services.AddHazel();
            services.AddSingleton<ICustomMessageManager<ICustomRootMessage>, CustomMessageManager<ICustomRootMessage>>();
            services.AddSingleton<ICustomMessageManager<ICustomRpc>, CustomMessageManager<ICustomRpc>>();

            // Add Coravel's scheduler services
            services.AddScheduler();
            var serviceProvider = services.BuildServiceProvider();
            // Crea un'istanza di SemanticAnnotatorPluginStartup iniettando IConfiguration
            var semanticAnnotatorPluginStartup = ActivatorUtilities.CreateInstance<SemanticAnnotatorPluginStartup>(serviceProvider);

            // Configura i servizi
            semanticAnnotatorPluginStartup.ConfigureServices(services);

            // Registrazioni aggiuntive (se serve)
            services.AddSingleton<SemanticAnnotatorPlugin>();

            // Build and return the service provider
            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Parses the entire session (.dat file)
        /// </summary>
        private static async Task ParseSession(BinaryReader reader)
        {
            // Read the version of the recording protocol
            var protocolVersion = (ServerReplayVersion)reader.ReadUInt32();
            if (protocolVersion < ServerReplayVersion.Initial || protocolVersion > ServerReplayVersion.Latest)
            {
                throw new NotSupportedException("Session's protocol version is unsupported");
            }

            // Retrieve the initial timestamp of the session
            var startTime = _fakeDateTimeProvider.UtcNow = DateTimeOffset.FromUnixTimeMilliseconds(reader.ReadInt64());

            // Read the server version
            var serverVersion = reader.ReadString();
            Logger.Information("Loaded session (server: {ServerVersion}, recorded at {StartTime})", serverVersion, startTime);
            var totalTimeframe = 0;
            // Read all packets in the session
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                var dataLength = reader.ReadInt32();
                var data = reader.ReadBytes(dataLength - 4);

                await using (var stream = new MemoryStream(data))
                using (var readerInner = new BinaryReader(stream))
                {
                    var nextTimeFrame = TimeSpan.FromMilliseconds(readerInner.ReadUInt32());
                    // Sleep for the entire duration
                    await Task.Delay(Math.Min((int)nextTimeFrame.TotalMilliseconds, 500));
                    // Update the simulated time for each block
                    _fakeDateTimeProvider.UtcNow += nextTimeFrame;
                    totalTimeframe += nextTimeFrame.Milliseconds;
                    // Interpret the individual packet
                    await ParsePacket(readerInner);
                    if (totalTimeframe > 3000) {
                       
                        var decisionSupport = _serviceProvider.GetRequiredService<IDecisionSupportService>();
                        var gameCacheManager = _serviceProvider.GetRequiredService<GameEventCacheManager>();
                        await decisionSupport.ProcessMultipleAsync(gameCacheManager.GetActiveSessions());
                    
                        totalTimeframe = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Decodes and processes a single packet.
        /// </summary>
        private static async Task ParsePacket(BinaryReader reader)
        {
            var dataType = (RecordedPacketType)reader.ReadByte();
            // The client ID is used as a key in the dictionary
            var clientId = reader.ReadInt32();

            switch (dataType)
            {
                case RecordedPacketType.Connect:
                {
                    // Read connection data
                    var addressLength = reader.ReadByte();
                    var addressBytes = reader.ReadBytes(addressLength);
                    var addressPort = reader.ReadUInt16();
                    var address = new IPEndPoint(new IPAddress(addressBytes), addressPort);
                    var name = reader.ReadString();
                    var gameVersion = new GameVersion(reader.ReadInt32());

                    // Create a mock connection and register the client
                    var connection = new MockHazelConnection(address);
                    await _clientManager.RegisterConnectionAsync(
                        connection,
                        name,
                        gameVersion,
                        Language.English,
                        QuickChatModes.FreeChatOrQuickChat,
                        new PlatformSpecificData(Platforms.Unknown, "ServerReplay")
                    );

                    // 3. Store the connection in the dictionary
                    Connections.Add(clientId, connection);
                    break;
                }
                case RecordedPacketType.Disconnect:
                {
                    // If the packet contains a disconnection reason, read it
                    string reason = null;
                    if (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        reason = reader.ReadString();
                    }

                    // Send the disconnection signal to the client (and to Impostor)
                    await Connections[clientId].Client!.HandleDisconnectAsync(reason);
                    Connections.Remove(clientId);
                    break;
                }
                case RecordedPacketType.Message:
                {
                    // 1. Retrieve message type and length
                    var messageType = (MessageType)reader.ReadByte();
                    var tag = reader.ReadByte();
                    var length = reader.ReadInt32();
                    var buffer = reader.ReadBytes(length);

                    // 2. Retrieve a MessageReader object from the pool
                    using var message = _readerPool.Get();
                    message.Update(buffer, tag: tag);

                    // 3. If this is a HostGame request, store the game options
                    if (tag == MessageFlags.HostGame)
                    {
                        Message00HostGameC2S.Deserialize(message, out var gameOptions, out _, out _);
                        GameOptions.Add(clientId, gameOptions);
                    }
                    else if (Connections.TryGetValue(clientId, out var client))
                    {
                        // Route the message to the Impostor Client
                        await client.Client!.HandleMessageAsync(message, messageType);
                    }
                    break;
                }
                case RecordedPacketType.GameCreated:
                {
                    // 1. Read the game code
                    _gameCodeFactory.Result = GameCode.From(reader.ReadString());

                    // 2. Create the game
                    await _gameManager.CreateAsync(
                        Connections[clientId].Client,
                        GameOptions[clientId],
                        GameFilterOptions.CreateDefault()
                    );

                    // 3. Remove the used game options from the dictionary
                    GameOptions.Remove(clientId);

                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
