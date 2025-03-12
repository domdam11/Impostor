using System;
using Impostor.Api.Games;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Impostor.Api.Events.Ship;
using Impostor.Api.Innersloth;
using Impostor.Plugins.SemanticAnnotator;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.SemanticAnnotator.Handlers
{
    /// <summary>
    /// Event listener for handling ship-related events in the game.
    /// It tracks game-wide ship events, sabotage, door control, and other ship-related interactions.
    /// </summary>
    public class ShipEventListener : IEventListener
    {
        private readonly ILogger<ShipEventListener> _logger;
        private readonly GameEventCacheManager _eventCacheManager;

        public ShipEventListener(ILogger<ShipEventListener> logger, GameEventCacheManager eventCacheManager)
        {
            _logger = logger;
            _eventCacheManager = eventCacheManager;
        }

        /// <summary>
        /// Handles any general ship-related event.
        /// </summary>
        [EventListener(EventPriority.Monitor)]
        public void OnGame(IShipEvent e)
        {
            _logger.LogInformation("{eventName} triggered for Game {code}", e.GetType().Name, e.Game.Code);

            // Create a dictionary to store event details
            var shipEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },        // Use a GUID as a unique EventId
                { "GameCode", e.Game.Code },                    // Store the game code
                { "EventType", e.GetType().Name },              // Specify the type of event
                { "Timestamp", DateTime.UtcNow },               // Capture the event timestamp in UTC
                { "Details", e.GetType().Name }                 // Additional details, can be customized
            };

            // Store the event in the cache
            _eventCacheManager.AddEventAsync(e.Game.Code, shipEventData);
        }

        /// <summary>
        /// Handles the event when a sabotage occurs on the ship.
        /// </summary>
        [EventListener]
        public void OnSabotage(IShipSabotageEvent e)
        {
            var message = $"Game: {e.ClientPlayer.Game.Code}, Ship > sabotage {e.SystemType} by {e.ClientPlayer.Character.PlayerInfo.PlayerName}";
            _logger.LogInformation(message);

            // Create a dictionary to store sabotage event details
            var sabotageEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },           // Use a GUID as a unique EventId
                { "GameCode", e.Game.Code },                       // Store the game code
                { "EventType", "ShipSabotage" },                   // Specify the type of event
                { "Timestamp", DateTime.UtcNow },                   // Capture the event timestamp in UTC
                { "SystemType", e.SystemType },                    // Store the sabotaged system
                { "PlayerName", e.ClientPlayer.Character.PlayerInfo.PlayerName } // Store the player's name who performed the sabotage
            };

            // Store the event in the cache
            _eventCacheManager.AddEventAsync(e.Game.Code, sabotageEventData);
        }

        /// <summary>
        /// Handles the event when a ship's doors are closed.
        /// </summary>
        [EventListener]
        public void OnDoorsClosed(IShipDoorsCloseEvent e)
        {
            _logger.LogInformation("Ship > doors closed - {door}", e.SystemType);

            // Create a dictionary to store the door closure event details
            var doorsClosedEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },           // Use a GUID as a unique EventId
                { "GameCode", e.Game.Code },                       // Store the game code
                { "EventType", "ShipDoorsClosed" },                 // Specify the type of event
                { "Timestamp", DateTime.UtcNow },                   // Capture the event timestamp in UTC
                { "SystemType", e.SystemType }                     // Store the system type related to the closed door
            };

            // Store the event in the cache
            _eventCacheManager.AddEventAsync(e.Game.Code, doorsClosedEventData);
        }

        /// <summary>
        /// Handles the event when a door is opened on the Polus map.
        /// </summary>
        [EventListener]
        public void OnPolusDoorOpened(IShipPolusDoorOpenEvent e)
        {
            _logger.LogInformation("Ship > doors opened - {door}", e.Door);

            // Create a dictionary to store Polus door opening event details
            var doorsOpenedEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },           // Use a GUID as a unique EventId
                { "GameCode", e.Game.Code },                       // Store the game code
                { "EventType", "ShipDoorsOpened" },                // Specify the type of event
                { "Timestamp", DateTime.UtcNow },                   // Capture the event timestamp in UTC
                { "Door", e.Door }                                 // Store the door identifier
            };

            // Store the event in the cache
            _eventCacheManager.AddEventAsync(e.Game.Code, doorsOpenedEventData);
        }

        /// <summary>
        /// Handles the event when a decontamination door is opened.
        /// </summary>
        [EventListener]
        public void OnDecontamDoorOpened(IShipDecontamDoorOpenEvent e)
        {
            _logger.LogInformation("Ship - decontam door opened - {decontamDoor}", e.DecontamDoor);

            // Create a dictionary to store decontamination door opening event details
            var decontamDoorOpenEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },               // Use a GUID as a unique EventId
                { "GameCode", e.Game.Code },                           // Store the game code
                { "EventType", "DecontamDoorOpened" },                 // Specify the type of event
                { "Timestamp", DateTime.UtcNow },                       // Capture the event timestamp in UTC
                { "DecontamDoor", e.DecontamDoor }                     // Store the decontamination door identifier
            };

            // Store the event in the cache
            _eventCacheManager.AddEventAsync(e.Game.Code, decontamDoorOpenEventData);
        }
    }
}
