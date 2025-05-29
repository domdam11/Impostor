using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;

namespace Impostor.Plugins.SemanticAnnotator.Models
{
    public class CustomPlayerMovementEvent : IPlayerEvent
    {
        public IGame? Game { get; }
        public IClientPlayer? ClientPlayer { get; }
        public IInnerPlayerControl? PlayerControl { get; }
        public System.Numerics.Vector2 Position { get; }
        public DateTimeOffset Timestamp { get; set; }

        // Costruttore
        public CustomPlayerMovementEvent(IGame? game, IClientPlayer? clientPlayer, IInnerPlayerControl? playerControl, DateTimeOffset timestamp)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
            Position = playerControl.NetworkTransform.Position;
            Timestamp = timestamp;
        }
    }
}
