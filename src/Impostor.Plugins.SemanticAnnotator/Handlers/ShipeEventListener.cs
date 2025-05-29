using System;
using System.Security;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Impostor.Api.Events.Ship;
using Impostor.Api.Innersloth;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.SemanticAnnotator.Handlers
{
    public class ShipEventListener : IEventListener
    {
        private readonly ILogger<ShipEventListener> _logger;
        private readonly GameEventCacheManager _eventCacheManager;

        public ShipEventListener(ILogger<ShipEventListener> logger, GameEventCacheManager eventCacheManager)
        {
            _logger = logger;
            _eventCacheManager = eventCacheManager;
        }

        [EventListener(EventPriority.Monitor)]
        public void OnGame(IShipEvent e)
        {
            Console.WriteLine(e.GetType().Name + " triggered");
        }

        [EventListener]
        public void OnSabotage(IShipSabotageEvent e)
        {
            Console.WriteLine("Game: {0}, Ship > sabotage {1} by {2}", e.ClientPlayer.Game.Code, e.SystemType, e.ClientPlayer.Character.PlayerInfo.PlayerName);
            // add event in order to annotate
            _eventCacheManager.SaveEvent(e.Game.Code, e);
        }

        [EventListener]
        public void OnDoorsClosed(IShipDoorsCloseEvent e)
        {
            Console.WriteLine("Ship > doors closed - " + e.SystemType);
            //e.ShipStatus.Sabotage(SystemTypes.Electrical);
        }

        [EventListener]
        public void OnPolusDoorOpened(IShipPolusDoorOpenEvent e)
        {
            Console.WriteLine("Ship - door opened - " + e.Door);

        }

        [EventListener]
        public void OnDecontamDoorOpened(IShipDecontamDoorOpenEvent e)
        {
            Console.WriteLine("Ship - decontam door opened - " + e.DecontamDoor);
        }
    }
}
