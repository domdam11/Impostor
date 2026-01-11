using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Impostor.Api;
using Impostor.Api.Events.Managers;
using Impostor.Api.Events.Ship;
using Impostor.Api.Innersloth;
using Impostor.Api.Innersloth.Maps;
using Impostor.Api.Net;
using Impostor.Api.Net.Custom;
using Impostor.Api.Net.Inner;
using Impostor.Api.Net.Inner.Objects.ShipStatus;
using Impostor.Api.Net.Messages.Rpcs;
using Impostor.Server.Events;
using Impostor.Server.Events.Player;
using Impostor.Server.Events.Ship;
using Impostor.Server.Net.Inner.Objects.Systems;
using Impostor.Server.Net.Inner.Objects.Systems.ShipStatus;
using Impostor.Server.Net.State;

namespace Impostor.Server.Net.Inner.Objects.ShipStatus
{
    internal abstract partial class InnerShipStatus : InnerNetObject, IInnerShipStatus
    {
        private readonly Dictionary<SystemTypes, ISystemType> _systems = new Dictionary<SystemTypes, ISystemType>();
        private readonly IEventManager _eventManager;
        protected InnerShipStatus(ICustomMessageManager<ICustomRpc> customMessageManager, Game game, MapTypes mapType, IEventManager eventManager) : base(customMessageManager, game)
        {
            Components.Add(this);
            _eventManager = eventManager;
            MapType = mapType;
            Data = MapData.Maps[mapType];
            Doors = new Dictionary<int, bool>(Data.Doors.Count);
        }

        public MapTypes MapType { get; }

        public MapData Data { get; }

        public Dictionary<int, bool> Doors { get; }

        internal override ValueTask OnSpawnAsync()
        {
            for (var i = 0; i < Doors.Count; i++)
            {
                Doors.Add(i, false);
            }

            AddSystems(_systems);
            _systems.Add(SystemTypes.Sabotage, new SabotageSystemType(_systems.Values.OfType<IActivatable>().ToArray()));

            return base.OnSpawnAsync();
        }

        public override ValueTask<bool> SerializeAsync(IMessageWriter writer, bool initialState)
        {
            throw new NotImplementedException();
        }

        public override async ValueTask DeserializeAsync(IClientPlayer sender, IClientPlayer? target, IMessageReader reader, bool initialState)
        {
            if (!await ValidateHost(CheatContext.Deserialize, sender) || !await ValidateBroadcast(CheatContext.Deserialize, sender, target))
            {
                return;
            }

            while (reader.Position < reader.Length)
            {
                var messageReader = reader.ReadMessage();
                var type = (SystemTypes)messageReader.Tag;
                if (_systems.TryGetValue(type, out var value))
                {
                    value.Deserialize(messageReader, initialState);
                }
            }
        }

        public override async ValueTask<bool> HandleRpcAsync(ClientPlayer sender, ClientPlayer? target, RpcCalls call, IMessageReader reader)
        {
            if (!await ValidateCmd(call, sender, target))
            {
                return false;
            }

            switch (call)
            {
                case RpcCalls.CloseDoorsOfType:
                {
                    if (!await ValidateImpostor(call, sender, sender.Character!.PlayerInfo))
                    {
                        return false;
                    }

                    Rpc27CloseDoorsOfType.Deserialize(reader, out var systemType);
                    break;
                }


                case RpcCalls.UpdateSystem:
                {
                    // TODO: properly deserialize this RPC
                    Rpc35UpdateSystem.Deserialize(reader, Game, out var systemType, out var playerControl, out var amount);

                    if (playerControl == null)
                    {
                        if (await sender.Client.ReportCheatAsync(RpcCalls.UpdateSystem, CheatCategory.Ownership, $"Client sent {nameof(RpcCalls.UpdateSystem)} with an null player control {nameof(InnerPlayerControl)}"))
                        {
                            return false;
                        }

                        return false;
                    }

                    if (playerControl.PlayerId != sender.Character!.PlayerId)
                    {
                        if (await sender.Client.ReportCheatAsync(RpcCalls.UpdateSystem, CheatCategory.Ownership, $"Client sent {nameof(RpcCalls.UpdateSystem)} with an unowned {nameof(InnerPlayerControl)}"))
                        {
                            return false;
                        }
                    }
                    var flag = (byte)amount;

                    Console.WriteLine(systemType + " " + flag);
          
                    switch (systemType)
                    {
                        case SystemTypes.Sabotage:
                            case SystemTypes.HeliSabotage:
                            case SystemTypes.MushroomMixupSabotage:

                            

                            await _eventManager.CallAsync(new ShipSabotageEvent(Game, this, sender, (SystemTypes)flag));
                            break;

                        case SystemTypes.Doors:

                            await _eventManager.CallAsync(new ShipPolusDoorOpenEvent(Game, this, sender, (PolusDoors)flag));

                            break;
            

                        case SystemTypes.Decontamination:
                        case SystemTypes.TopDecontaminationPolus:

                            await _eventManager.CallAsync(new ShipDecontamDoorOpenEvent(Game, this, sender, systemType, flag));

                            break;
                    }

                    break;

                }

               

                default:
                    return await base.HandleRpcAsync(sender, target, call, reader);
            }

            return true;
        }

        public virtual Vector2 GetSpawnLocation(InnerPlayerControl player, int numPlayers, bool initialSpawn)
        {
            var vector = new Vector2(0, 1);
            vector = Rotate(vector, (player.PlayerId - 1) * (360f / numPlayers));
            vector *= Data.SpawnRadius;
            return (initialSpawn ? Data.InitialSpawnCenter : Data.MeetingSpawnCenter) + vector + new Vector2(0f, 0.3636f);
        }

        protected virtual void AddSystems(Dictionary<SystemTypes, ISystemType> systems)
        {
            systems.Add(SystemTypes.Electrical, new SwitchSystem());
            systems.Add(SystemTypes.MedBay, new MedScanSystem());
        }

        private static Vector2 Rotate(Vector2 self, float degrees)
        {
            var f = 0.017453292f * degrees;
            var cos = MathF.Cos(f);
            var sin = MathF.Sin(f);

            return new Vector2((self.X * cos) - (sin * self.Y), (self.X * sin) + (cos * self.Y));
        }

        public async ValueTask Sabotage(SystemTypes systemType)
        {
            

            // Is this ok? Sabotages can only be triggered by impostors.
            // The anticheat will need to be modified in the future to ignore packets sent/modified by plugins
            var netId = Game.Players.First(p => p.Character.PlayerInfo.IsImpostor).Character.NetId;
            Console.WriteLine(netId);

            using var writer = Game.StartRpc(NetId, RpcCalls.UpdateSystem);
            writer.Write((byte)SystemTypes.Sabotage);
            writer.WritePacked(netId);
            writer.Write((byte)systemType);
            await Game.FinishRpcAsync(writer);
        }
    }
}
