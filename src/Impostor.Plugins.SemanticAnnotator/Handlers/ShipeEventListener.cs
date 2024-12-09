using System;
using System.Security;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Impostor.Api.Events.Ship;
using Impostor.Api.Innersloth;
<<<<<<< HEAD
=======
using Impostor.Plugins.SemanticAnnotator.Annotator;
>>>>>>> 78f1e2eb8a16ecbc059c7d2e709b50a9de97723d

namespace Impostor.Plugins.Example.Handlers
{
    public class ShipEventListener : IEventListener
    {
        [EventListener(EventPriority.Monitor)]
        public void OnGame(IShipEvent e)
        {
            Console.WriteLine(e.GetType().Name + " triggered");
        }

        [EventListener]
        public void OnSabotage(IShipSabotageEvent e)
        {
            Console.WriteLine("Game: {0}, Ship > sabotage {1} by {2}", e.ClientPlayer.Game.Code, e.SystemType, e.ClientPlayer.Character.PlayerInfo.PlayerName);
<<<<<<< HEAD
   
=======
            // add event in order to annotate
            EventUtility.SaveEvent(e);
>>>>>>> 78f1e2eb8a16ecbc059c7d2e709b50a9de97723d
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
