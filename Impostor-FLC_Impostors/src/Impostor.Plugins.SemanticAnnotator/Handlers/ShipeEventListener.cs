using System;
using System.Security;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Impostor.Api.Events.Ship;
using Impostor.Api.Innersloth;

namespace Impostor.Plugins.Example.Handlers 
{
    public class ShipEventListener : IEventListener
    {
        [EventListener(EventPriority.Monitor)]
        public void OnGame(IShipEvent e)
        {
            Console.WriteLine($"{(e?.GetType()?.Name ?? "UnknownEvent")} triggered");
        }

        [EventListener]
        public void OnSabotage(IShipSabotageEvent e)
        {
            string gameCode = e.ClientPlayer?.Game.Code.ToString() ?? "UnknownGame"; 
            string playerName = e.ClientPlayer?.Character?.PlayerInfo?.PlayerName ?? "UnknownPlayer";
            var systemType = e.SystemType; 

            Console.WriteLine($"Game: {gameCode}, Ship > sabotage {systemType} by {playerName}");
        }

        [EventListener]
        public void OnDoorsClosed(IShipDoorsCloseEvent e)
        {
            
            Console.WriteLine($"Ship > doors closed - {e.SystemType}");
        }

        [EventListener]
        public void OnPolusDoorOpened(IShipPolusDoorOpenEvent e)
        {
            Console.WriteLine($"Ship - door opened - {e.Door}");
        }

        [EventListener]
        public void OnDecontamDoorOpened(IShipDecontamDoorOpenEvent e)
        {
            Console.WriteLine($"Ship - decontam door opened - {e.DecontamDoor}");
        }
    }
}
