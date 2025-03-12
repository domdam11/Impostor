using Impostor.Api.Events;
using Impostor.Api.Events.Client;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Impostor.Plugins.SemanticAnnotator.Annotator;

namespace Impostor.Plugins.SemanticAnnotator.Handlers
{
    public class ClientEventListener : IEventListener
    {
        // Private field to log information related to listener activity
        private readonly ILogger<ClientEventListener> _logger;

        // Private field to manage the cache of collected events
        private readonly GameEventCacheManager _eventCacheManager;

        // Dependency injection for logging and event cache management
        public ClientEventListener(ILogger<ClientEventListener> logger, GameEventCacheManager eventCacheManager)
        {
            _logger = logger;
            _eventCacheManager = eventCacheManager;
        }

        [EventListener]
        public async ValueTask OnClientConnected(IClientConnectedEvent e)
        {
            // Determine the GameCode based on the available context (currently set as "unassigned")
            string gameCode = "unassigned";

            // Create a dictionary to store event details
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Use a GUID as a unique EventId
                { "GameCode", gameCode },                  // Store the gameCode (currently defaulted)
                { "EventType", "ClientConnected" },        // Specify the type of event
                { "Timestamp", DateTime.UtcNow },          // Capture the event timestamp in UTC
                { "ClientName", e.Client.Name },           // Store the client's username
                { "ClientLanguage", e.Client.Language },   // Store the client's language setting
                { "ChatMode", e.Client.ChatMode}           // Store the chat mode preference of the client
            };

            // Store the event data in the cache
            await _eventCacheManager.AddEventAsync(gameCode, eventData);

            // Log the event, including client name, language, and chat mode
            _logger.LogInformation(
                "Client {name} > connected (language: {language}, chat mode: {chatMode})",
                e.Client.Name, e.Client.Language, e.Client.ChatMode);
        }
    }
}
