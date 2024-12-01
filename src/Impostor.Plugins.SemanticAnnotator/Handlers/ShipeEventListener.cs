using System;
using Impostor.Api.Games;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Impostor.Api.Events.Ship;
using Impostor.Api.Innersloth;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using System.Collections.Generic;

namespace Impostor.Plugins.SemanticAnnotator.Handlers
{
    public class ShipEventListener : IEventListener
    {
        private readonly GameEventCacheManager _eventCacheManager;

        public ShipEventListener(GameEventCacheManager eventCacheManager)
        {
            _eventCacheManager = eventCacheManager;
        }

        [EventListener(EventPriority.Monitor)]
        public void OnGame(IShipEvent e)
        {
            Console.WriteLine(e.GetType().Name + " triggered");

            // Crea un dizionario per rappresentare le informazioni sull'evento
            var shipEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },        // Usa un GUID come EventId
                { "GameCode", e.Game.Code },                    // Codice del gioco
                { "EventType", "ShipEvent" },                    // Tipo di evento
                { "Timestamp", DateTime.UtcNow },                // Timestamp dell'evento
                { "GetType", e.GetType().Name }  
            };

            _eventCacheManager.AddEventAsync(e.Game.Code, shipEventData);
        }

        [EventListener]
        public void OnSabotage(IShipSabotageEvent e)
        {
            var message = $"Game: {e.ClientPlayer.Game.Code}, Ship > sabotage {e.SystemType} by {e.ClientPlayer.Character.PlayerInfo.PlayerName}";
            Console.WriteLine(message);

            // Crea un dizionario per rappresentare le informazioni sull'evento di sabotaggio
            var sabotageEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },           // Usa un GUID come EventId
                { "GameCode", e.Game.Code },                       // Codice del gioco
                { "EventType", "ShipSabotage" },                    // Tipo di evento
                { "Timestamp", DateTime.UtcNow },                   // Timestamp dell'evento
                { "ShipSabotage", e.SystemType },
                { "PlayerName", e.ClientPlayer.Character.PlayerInfo.PlayerName }
            };

            _eventCacheManager.AddEventAsync(e.Game.Code, sabotageEventData);
        }

        [EventListener]
        public void OnDoorsClosed(IShipDoorsCloseEvent e)
        {
            Console.WriteLine("Ship > doors closed - " + e.SystemType);

            // Crea un dizionario per rappresentare le informazioni sull'evento di chiusura delle porte
            var doorsClosedEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },           // Usa un GUID come EventId
                { "GameCode", e.Game.Code },                       // Codice del gioco
                { "EventType", "ShipDoorsClosed" },                 // Tipo di evento
                { "Timestamp", DateTime.UtcNow },                   // Timestamp dell'evento
                { "SystemType",e.SystemType} 
            };

            _eventCacheManager.AddEventAsync(e.Game.Code, doorsClosedEventData);
        }

        [EventListener]
        public void OnPolusDoorOpened(IShipPolusDoorOpenEvent e)
        {
            Console.WriteLine("Ship - door opened - " + e.Door);

            // Crea un dizionario per rappresentare le informazioni sull'evento di apertura della porta Polus
            var polusDoorOpenEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },           // Usa un GUID come EventId
                { "GameCode", e.Game.Code },                       // Codice del gioco
                { "EventType", "PolusDoorOpened" },                 // Tipo di evento
                { "Timestamp", DateTime.UtcNow },                   // Timestamp dell'evento
                { "Door", e.Door } 
            };

            _eventCacheManager.AddEventAsync(e.Game.Code, polusDoorOpenEventData);
        }

        [EventListener]
        public void OnDecontamDoorOpened(IShipDecontamDoorOpenEvent e)
        {
            Console.WriteLine("Ship - decontam door opened - " + e.DecontamDoor);

            // Crea un dizionario per rappresentare le informazioni sull'evento di apertura della porta decontam
            var decontamDoorOpenEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },               // Usa un GUID come EventId
                { "GameCode", e.Game.Code },                           // Codice del gioco
                { "EventType", "DecontamDoorOpened" },                  // Tipo di evento
                { "Timestamp", DateTime.UtcNow },                       // Timestamp dell'evento
                { "DecontamDoor", e.DecontamDoor} 
            };

            _eventCacheManager.AddEventAsync(e.Game.Code, decontamDoorOpenEventData);
        }
    }
}
