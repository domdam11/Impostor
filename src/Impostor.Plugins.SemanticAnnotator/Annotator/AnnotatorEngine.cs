using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using cowl;
using CowlSharp.Wrapper;
using Impostor.Api.Events;
using Impostor.Api.Events.Meeting;
using Impostor.Api.Events.Player;
using Impostor.Api.Events.Ship;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Innersloth.GameOptions;
using Impostor.Api.Innersloth.Maps;

using Impostor.Plugins.SemanticAnnotator.Models;
using Microsoft.Extensions.Options;


namespace Impostor.Plugins.SemanticAnnotator.Annotator
{

    public class AnnotatorEngine
    {
        private readonly Thresholds _thresholds;

        public AnnotatorEngine(IOptions<AnnotatorServiceOptions> options)
        {
            _thresholds = options.Value.Thresholds;
        }

        /// <summary>
        /// Retrieves the annotation.
        /// </summary>
        /// <returns>The annotation.</returns>

        public (Dictionary<byte, PlayerStruct>, string, string) Annotate(IGame game, List<IEvent>? events, Dictionary<byte, PlayerStruct> playerStates, string gameState, int numAnnot, int numRestarts, DateTimeOffset timestamp)
        {
           // string filePath = "../../../../Impostor.Plugins.SemanticAnnotator/Annotator/properties.json";  // JSON file with thresholds
            string nameSpace = "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/";
            // load existing thresholds
           

            // You must always initialize the library before use.
            try
            {
                // Initialize the Cowl library
                cowl_config.CowlInit();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }


            var instancesToRelease = new List<nint>();

            // Instantiate a manager and deserialize an ontology from file.
            CowlManager manager = cowl_manager.CowlManager();

            var onto = cowl_manager.CowlManagerGetOntology(manager, cowl_ontology_id.CowlOntologyIdAnonymous());

            // note: the game passed as argument represent the last status of the game to which events passed as argument are "applied"   
            // Classes and objects to use throughout the process
            var crewmateClass = CowlWrapper.CreateClassFromIri(CowlWrapper.GetFullIri(nameSpace, "CrewMateAlive"), instancesToRelease);
            var crewmateDeadClass = CowlWrapper.CreateClassFromIri(CowlWrapper.GetFullIri(nameSpace, "CrewMateDead"), instancesToRelease);
            var playerClass = "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Player";
            var impostorClass = CowlWrapper.CreateClassFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/ImpostorAlive", instancesToRelease);
            var impostorDeadClass = CowlWrapper.CreateClassFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/ImpostorExiled", instancesToRelease);
            var gameClass = CowlWrapper.CreateClassFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Game", instancesToRelease);

            // create a list to hold the players
            List<Player> players = new List<Player>();

            foreach (var p in game.Players)
            {
                if (p is null)
                {
                    break;
                }
                else if (p.Character is null)
                {
                    break;
                }
                else if (p.Character.PlayerInfo is null)
                {
                    break;
                }
                else if (p.Character.PlayerInfo.PlayerName is null)
                {
                    break;
                }
                else if (p.Character.PlayerInfo.PlayerName.Replace(" ", "") == "")
                {
                    break;
                }
                else
                {
                    //assign class to players involved in the game
                    var cls = crewmateClass;
                    switch (p.Character.PlayerInfo.RoleType)
                    {
                        case RoleTypes.CrewmateGhost:
                            cls = crewmateDeadClass;
                            break;
                        case RoleTypes.ImpostorGhost:
                            cls = impostorDeadClass;
                            break;
                        case RoleTypes.Impostor:
                            cls = impostorClass;
                            break;
                        default:
                            break;
                    }
                    // instantiate player: id, name, class, status, list of positions, lists of obj and data properties 
                    var spawnMov = new CustomMovement(p.Character.NetworkTransform.Position, timestamp);
                    if (playerStates.ContainsKey(p.Character.PlayerId))
                    {
                        Player player = new Player(p.Character.PlayerId, p.Character.PlayerInfo.PlayerName.Replace(" ", ""), cls, playerStates[p.Character.PlayerId].SessionCls, playerStates[p.Character.PlayerId].Movements, spawnMov, playerStates[p.Character.PlayerId].State, playerStates[p.Character.PlayerId].VoteCount);
                        players.Add(player);
                    }
                    else
                    {
                        throw new Exception($"Player with ID {p.Character.PlayerId} not found in player states dictionary.");
                    }
                }
            }

            // game

            // scan player events
            foreach (var ev in events)
            {
                // identify the player the event refers to by Id
                Player player = null;
                if (ev is IPlayerEvent playerEvent)
                {
                    if (playerEvent is null)
                    {
                        break;
                    }
                    else if (playerEvent.ClientPlayer is null)
                    {
                        break;
                    }
                    else if (playerEvent.ClientPlayer.Character is null)
                    {
                        break;
                    }
                    else if (playerEvent.ClientPlayer.Character.PlayerInfo is null)
                    {
                        break;
                    }
                    else if (playerEvent.ClientPlayer.Character.PlayerInfo.PlayerName is null)
                    {
                        break;
                    }
                    else if (playerEvent.ClientPlayer.Character.PlayerInfo.PlayerName.Replace(" ", "") == "")
                    {
                        break;
                    }
                    else
                    {
                        //Cerca il player a cui si riferisce l'evento analizzato
                        player = players.Find(p => p.Id == playerEvent.ClientPlayer.Character.PlayerId);
                    }
                }
                switch (ev)
                {
                    // EnterVent: mi segno un player è entrato in una vent => per ora la può fare solo l'impostore(non c'è l'ingegnere)
                    case IPlayerEnterVentEvent enterVentEvent:
                        var enterVentIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{enterVentEvent.Vent.Name}";
                        var objQuantEnterVent = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/EnterVent", new[] { enterVentIri }, instancesToRelease);
                        player.objQuantRestrictionsPlayer.Add(objQuantEnterVent);
                        break;

                    // VentTo: andare verso un altro luogo dopo che sei entrato in una ventola
                    case IPlayerVentEvent VentEvent:
                        var VentIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{VentEvent.NewVent.Name}";
                        var objQuantVent = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/VentTo", new[] { VentIri }, instancesToRelease);
                        var ventMov = new CustomMovement(VentEvent.NewVent.Position, player.Movements[player.Movements.Count - 1].Timestamp);
                        player.Movements.Add(ventMov);
                        player.objQuantRestrictionsPlayer.Add(objQuantVent);
                        break;

                    // ExitVent: sei uscito definitivamente da un condotto
                    case IPlayerExitVentEvent exitVentEvent:
                        var exitVentIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{exitVentEvent.Vent.Name}";
                        var objQuantExitVent = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/ExitVent", new[] { exitVentIri }, instancesToRelease);
                        // add movement to track player's path 
                        var exitMov = new CustomMovement(exitVentEvent.Vent.Position, player.Movements[player.Movements.Count - 1].Timestamp);
                        player.Movements.Add(exitMov);
                        // with a mapping VentName - room we can infer the room  
                        player.objQuantRestrictionsPlayer.Add(objQuantExitVent);
                        break;

                    // Murder
                    case IPlayerMurderEvent murderEvent:
                        if (murderEvent.Victim is null)
                        {
                            break;
                        }
                        else if (murderEvent.Victim.PlayerInfo.PlayerName.ToString().Replace(" ", "") == "")
                        {
                            break;
                        }
                        else if (player.Cls == impostorDeadClass)
                        {
                            break;
                        }
                        else
                        {
                            var playerKilled = players.Find(p => p.Id == murderEvent.Victim.PlayerId);
                            var victimIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{playerKilled.SessionCls}";
                            var objQuantKill = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Kills", new[] { victimIri }, instancesToRelease);
                            player.objQuantRestrictionsPlayer.Add(objQuantKill);
                            // update state of the victim
                            playerKilled.Cls = crewmateDeadClass;
                            break;
                        }

                    // CompletedTask: come rendere permanente l'informazione al player che ha visto, del tipo: X Know that Y is trusted
                    case IPlayerCompletedTaskEvent completedTaskEvent:
                        var taskIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{completedTaskEvent.Task.Task.Type}";

                        var objQuantCompletedTask = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Completed", new[] { taskIri }, instancesToRelease);

                        // player is trusted if: task is visual + there is a crewmate inFOV 
                        if (completedTaskEvent.Task.Task.IsVisual)
                        {
                            var coords = completedTaskEvent.PlayerControl.NetworkTransform.Position;
                            foreach (var p in completedTaskEvent.Game.Players)
                            {
                                // an impostor wouldn't witness a crewmate 
                                if (p != player && p.Character.PlayerInfo.RoleType.ToString() == "crewmate" && p.Character.PlayerInfo.IsDead == false)
                                {
                                    var coordsP = p.Character.NetworkTransform.Position;
                                    // euclidean distance
                                    var dist = CalcDistance(coords, coordsP);
                                    // if distance <= threshold then IsInFOV for both players involved 
                                    if (dist <= _thresholds.FOV)
                                    {
                                        //so there is at least one crewmate who have seen the player doing the task
                                        player.State = "trusted";
                                        var dataQuantPlayerState = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasStatus", new[] { "trusted" }, "http://www.w3.org/2001/XMLSchema#string", "", instancesToRelease);
                                        player.dataQuantRestrictionsPlayer.Add(dataQuantPlayerState);
                                    }
                                }
                            }
                        }
                        player.objQuantRestrictionsPlayer.Add(objQuantCompletedTask);
                        break;

                    // Vote
                    case IPlayerVotedEvent votedEvent:
                        if (votedEvent.VotedFor is null)
                        {
                            break;
                        }
                        else if (votedEvent.VotedFor.PlayerInfo.PlayerName.ToString().Replace(" ", "") == "")
                        {
                            break;
                        }
                        else
                        {
                            var votedPlayer = players.Find(ot => ot.Id == votedEvent.VotedFor.PlayerId);
                            var votedIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{votedPlayer.SessionCls}";
                            var objQuantVote = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Votes", new[] { votedIri }, instancesToRelease);

                            player.objQuantRestrictionsPlayer.Add(objQuantVote);
                            //update count of votes got by player in current round
                            votedPlayer.IncrementScore();
                            break;
                        }

                    // StartMeeting
                    case IPlayerStartMeetingEvent startMeetingEvent:
                        // if no body reported is an emergency meeting call
                        if (startMeetingEvent.Body is null || startMeetingEvent.Body.PlayerInfo.PlayerName == "")
                        {
                            var emergencyCallIri = "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/EmergencyCall";
                            var objQuantEmergencyMeeting = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Calls", new[] { emergencyCallIri }, instancesToRelease);

                            player.objQuantRestrictionsPlayer.Add(objQuantEmergencyMeeting);
                        }
                        else
                        {
                            var deadPlayer = players.Find(ot => ot.Id == startMeetingEvent.Body.PlayerId);
                            var deadBodyIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{deadPlayer.SessionCls}";
                            var objQuantReport = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Reports", new[] { deadBodyIri }, instancesToRelease);

                            player.objQuantRestrictionsPlayer.Add(objQuantReport);

                            var emergencyCallIri = "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/EmergencyCall";
                            var objQuantEmergencyMeeting = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Calls", new[] { emergencyCallIri }, instancesToRelease);

                            player.objQuantRestrictionsPlayer.Add(objQuantEmergencyMeeting);
                        }
                        break;

                    // RepairSystem
                    case IPlayerRepairSystemEvent repairSystemEvent:
                        //mapping to adapt to the ontology
                        string sabotageTask = repairSystemEvent.SystemType.ToString() switch
                        {
                            "Electrical" => "FixLight",
                            "Comms" => "CommsSabotaged",
                            "Reactor" => "ReactorMeltdown",
                            "LifeSupp" => "O2OxygenDepleted",
                            "Admin" => "AdminOxygenDepleted",
                            _ => "Another type of sabotage task",
                        };
                        var repairediri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{sabotageTask}";
                        var objQuantRepairSystem = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Repairs", new[] { repairediri }, instancesToRelease);

                        player.objQuantRestrictionsPlayer.Add(objQuantRepairSystem);
                        // update game state
                        gameState = "none";
                        break;

                    // Movement
                    case CustomPlayerMovementEvent movementEvent:
                        var coordsPlayer = movementEvent.Position;
                        var mov = new CustomMovement(coordsPlayer, movementEvent.Timestamp);
                        // add movement to track path of the player
                        
                        player?.Movements.Add(mov);

                        break;

                    //scan ship events

                    // shipSabotage => informazione generale disponibile a tutti i player => valutare di mettere una restr cardinale
                    case IShipSabotageEvent shipSabotageEvent:
                        player = players.Find(p => p.Id == shipSabotageEvent.ClientPlayer.Character.PlayerId);
                        //mapping to adapt to the ontology
                        string sabotage = shipSabotageEvent.SystemType.ToString() switch
                        {
                            "Electrical" => "SabotageFixLights",
                            "MedBay" => "SabotageMedScan",
                            "Doors" => "SabotageDoorSabotage",
                            "Comms" => "CommsSabotaged",
                            "Security" => "SabotageSecurity",
                            "Reactor" => "SabotageReactorMeltdown",
                            "LifeSupp" => "SabotageOxygenDepleted",
                            "Ventilation" => "SabotageVentilation",
                            _ => "Another type of sabotage",
                        };

                        var sabotageIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{sabotage}";
                        var objQuantSabotage = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Sabotages", new[] { sabotageIri }, instancesToRelease);
                        player.objQuantRestrictionsPlayer.Add(objQuantSabotage);
                        gameState = "sabotage";
                        break;

                    // scan meeting events
                    //---------------------------Quando inizia un meeting l'annotazione o dev'essere bloccata o deve durare quanto il meeting stesso----------------------
                    //meetingStarted
                    case IMeetingStartedEvent meetingStartedEvent:
                        gameState = "meeting";
                        break;

                    // meetingEnded
                    case IMeetingEndedEvent meetingEndedEvent:
                        gameState = "none";

                        // count number of alive players in game

                        var nAlivePlayers = 0;
                        foreach (var p in meetingEndedEvent.Game.Players)
                        {
                            if (p.Character.PlayerInfo.IsDead == false)
                            {
                                nAlivePlayers++;
                            }
                        }
                        //------------------Questa info è interessante perchè tutti i player vedono le votazioni per cui questa potrebbe essere una info valido fino al prox meeting--------------
                        // set a status for each player (trusted/suspected/none)
                        foreach (var p in players)
                        {
                            if (p.VoteCount == (nAlivePlayers / 2) - 1 && p.State != "trusted")
                            {
                                p.State = "suspected";
                                var dataQuantPlayerState = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasStatus", new[] { "suspected" }, "http://www.w3.org/2001/XMLSchema#string", "", instancesToRelease);
                                p.dataQuantRestrictionsPlayer.Add(dataQuantPlayerState);
                            }
                            p.resetVoteCount();
                            p.resetMovements();
                        }
                        // if no tie update state of the exiled player
                        if (meetingEndedEvent.IsTie == false)
                        {
                            var dataQuantExiled = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/GotExiled", new[] { "true" }, "http://www.w3.org/2001/XMLSchema#boolean", "", instancesToRelease);

                            if (meetingEndedEvent.Exiled is null)
                            {
                                break;
                            }
                            else if (meetingEndedEvent.Exiled.PlayerInfo.PlayerName.Replace(" ", "") == "")
                            {
                                break;
                            }
                            var playerExiled = players.Find(p => p.Id == meetingEndedEvent.Exiled.PlayerId);
                            if (meetingEndedEvent.Exiled.PlayerInfo.IsImpostor == false)
                            {
                                playerExiled.Cls = crewmateDeadClass;
                            }
                            else
                            {
                                playerExiled.Cls = impostorDeadClass;
                            }
                            playerExiled.dataQuantRestrictionsPlayer.Add(dataQuantExiled);
                        }
                        break;


                }
                //Console.WriteLine(ev);
            }

            // when all events have been analyzed, for each player create the individual with all collected properties: esclusedere player non in FOV
            // create a list to hold the players List<Player> players = new List<Player>();
            //Infomrazioni utili: -cooldown -telecamere attive -altreinfo -Eliminare casi finali in cui l'impostore viene espulso o muore -Ferma annotatore nei meeting
            
            //--------------------Filtraggio dei player attorno all'impostore(in generale attorno al target)-----------------------------------------
            //Dovrebbe tener conto dei muri
            List<Player> playersInFOVImpostor = new List<Player>();
            var impostorPlayer = players.Find(p => p.Cls == impostorClass || p.Cls == impostorDeadClass);
            if (impostorPlayer != null && impostorPlayer.Movements != null)
            {
                var dimFuori = impostorPlayer.Movements.Count();
                foreach (var op in players)
                {
                    if (op.Cls != crewmateDeadClass && op.Cls != impostorClass)
                    {
                        var dimOp = op.Movements.Count();
                        var dist = CalcDistance(impostorPlayer.Movements[dimFuori - 1].Position, op.Movements[dimOp - 1].Position);
                        if (dist <= _thresholds.FOV)
                        {
                            playersInFOVImpostor.Add(op);
                        }
                    }
                }
                playersInFOVImpostor.Add(impostorPlayer);
            }
            //--------------------Filtraggio dei player attorno all'impostore-----------------------------------------
            //Ipotesi di conoscenza generale dei player su quanti rimangono vivi => per attivare KillToWin
            int numeroPlayerVivi = 0;
            foreach (var p in players) {
                if (p.Cls == impostorClass || p.Cls == crewmateClass) {
                    //he's alive
                    numeroPlayerVivi++;
                }
            }
            //Qui dovresti considerare solo le informazioni a cui può accedere l'impostore: -non puoi vedere ciò che è oltre la tua visuale => se un player vede qualcuno che tu non vedi non lo sai fattualmente
            foreach (var player in playersInFOVImpostor)
            {

                var dim = player.Movements.Count();
                var nCrewmatesFOV = 0;
                var nCrewMatesHasImpostorsFOV = 0;//numero di crewmate che vedo un impostor
                var nPlayerVeryNear = 0;
                var nImpostorsFOV = 0;

                // check if player fixed or moving towards someone

                // if no movement, player is in a fixed position (maybe AFK?)
                if (dim == 1)
                {
                    var dataQuantPos = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/IsFixed", new[] { "true" }, "http://www.w3.org/2001/XMLSchema#boolean", "", instancesToRelease);
                    player.dataQuantRestrictionsPlayer.Add(dataQuantPos);
                }
                else
                {
                    // check movement trajectories to see if he's getting near someone. This method doesn't consider walls
                    foreach (var p in players)
                    {
                        if (player.Cls == crewmateDeadClass || player.Cls == impostorDeadClass) continue;
                        if (p == player) continue;
                        if (p.Cls == crewmateDeadClass || p.Cls == impostorDeadClass) continue;

                        var near = 0;
                        var initDistance = CalcDistance(player.Movements[0].Position, p.Movements[0].Position);

                        for (var i = 1; i < dim; i++)
                        {
                            var newDistance = 0.0;

                            if (i < p.Movements.Count)
                            {
                                // player p is moving
                                newDistance = CalcDistance(player.Movements[i].Position, p.Movements[i].Position);
                            }
                            else
                            {
                                // player p is fixed
                                newDistance = CalcDistance(player.Movements[i].Position, p.Movements[p.Movements.Count - 1].Position);
                            }

                            if (newDistance < initDistance)
                            {
                                near++;
                            }
                        }

                        if (near >= dim / 2)
                        {
                            var getClosePlayer = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{p.SessionCls}";
                            var objQuantGetClose = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/GetCloseTo", new[] { getClosePlayer }, instancesToRelease);

                            //player.objQuantRestrictionsPlayer.Add(objQuantGetClose);
                        }
                    }
                }

                // check if player is InFOV other players
                bool IsInFOV = false;
                //Lista di player che vedono l'impostore: utile per capire quali fra questi sta svolgendo un task anche
                foreach (var op in players) {
                    //if dead player, skip
                    if (op != player && op.Cls != crewmateDeadClass && op.Cls != impostorDeadClass) {
                        var dimOp = op.Movements.Count();
                        // euclidean distance: potrei mettere la posizione dell'impostore => tu vedi i player che vede l'impostore
                        var dist = CalcDistance(player.Movements[dim-1].Position, op.Movements[dimOp-1].Position); 
                        // if distance < threshold then IsInFOV for both players 
                        if (dist <= _thresholds.FOV) {
                            var InFovIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{op.SessionCls}";  
                            if(player.Cls == impostorClass){
                                var objQuantIsInFOV = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/ImpostorIsInFOV", new[] {InFovIri}, instancesToRelease);
                                player.objQuantRestrictionsPlayer.Add(objQuantIsInFOV);
                                nCrewMatesHasImpostorsFOV++;
                            } else {
                                var objQuantIsInFOV = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/IsInFOV", new[] {InFovIri}, instancesToRelease);
                                player.objQuantRestrictionsPlayer.Add(objQuantIsInFOV);
                                IsInFOV = true;//serve per capire quante persone vede il player che sta facendo un task
                                nCrewmatesFOV++;
                            }

                            if (dist < _thresholds.FOV/3)
                            {
                                nPlayerVeryNear++; 
                            }
                        }
                    }
                }

                //counter of players in FOV: cambia l'object property in base al fatto che sia un impostore oppure no => serve a distinguere nelle strategie impostori da crewmate
                if(player.Cls == impostorClass){
                    var objproperty = CowlWrapper.CreateObjPropFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/ImpostorIsInFOV", instancesToRelease);
                    var cardexact = CowlWrapper.CreateCardTypeExactly(objproperty, nCrewMatesHasImpostorsFOV, instancesToRelease);
                    player.objCardRestrictionsPlayer.Add(cardexact);
                }else {
                    var objproperty = CowlWrapper.CreateObjPropFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/IsInFOV", instancesToRelease);
                    var cardexact = CowlWrapper.CreateCardTypeExactly(objproperty, nCrewmatesFOV, instancesToRelease);
                    player.objCardRestrictionsPlayer.Add(cardexact);
                
                }
                
                //counter of players in FOVVeryNear => da definire meglio (thresholds.FOV/3)
                var objQuantVeryNear = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/VeryNear", new[] {playerClass}, instancesToRelease);
                var objPropVeryNear = CowlWrapper.CreateObjPropFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/VeryNear", instancesToRelease);
                var exactVeryNear = CowlWrapper.CreateCardTypeExactly(objPropVeryNear, nPlayerVeryNear, instancesToRelease);
                if (nPlayerVeryNear == 0)
                {
                    player.objCardRestrictionsPlayer.Add(exactVeryNear);
                }else{
                    player.objQuantRestrictionsPlayer.Add(objQuantVeryNear);
                    player.objCardRestrictionsPlayer.Add(exactVeryNear);
                }

                // check if player is nextTo a vent
                foreach (var v in MapData.Maps[game.Options.Map].Vents)
                {
                    var coordsVent = v.Value.Position;
                    var dist = CalcDistance(player.Movements[dim - 1].Position, coordsVent);
                    if (dist <= _thresholds.NextToVent)
                    {
                        var vent = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{v.Value.Name}";
                        var objQuantNextToVent = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/NextTo", new[] { vent }, instancesToRelease);

                        player.objQuantRestrictionsPlayer.Add(objQuantNextToVent);
                        break; //a player can't be next to more than 1 vent
                    }
                }

                // check if player is nextTo a task
                foreach (var t in MapData.Maps[game.Options.Map].Tasks)
                {
                    var nextTo = false;
                    for (var i = 0; i < t.Value.Position.Count(); i++)
                    {
                        var coordsTask = t.Value.Position[i];
                        var dist = CalcDistance(player.Movements[dim - 1].Position, coordsTask);
                        if (dist <= _thresholds.NextToTask)
                        {
                            var task = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{t.Value.Type}";
                            var objQuantNextToTask = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/NextTo", new[] { task }, instancesToRelease);

                            player.objQuantRestrictionsPlayer.Add(objQuantNextToTask);
                            nextTo = true; //a player can't be next to more than 1 task
                            break;
                        }
                    }
                    if (nextTo) break;
                }
                
                bool DoesGeneric = false;
                // loop through player positions
                for (var j = 0; j < dim; j++)
                {
                    var does = false;
                    var playerMovement = player.Movements[j];
                    // check if player is nextTo a task and does a task
                    foreach (var t in MapData.Maps[game.Options.Map].Tasks)
                    {
                        var timeThreshold = _thresholds.TimeShort;
                        switch (t.Value.Type)
                        {
                            case TaskTypes.InspectSample:
                                timeThreshold = _thresholds.TimeInspectSample;
                                break;
                            case TaskTypes.UnlockManifolds:
                                timeThreshold = _thresholds.TimeUnlockManifolds;
                                break;
                            case TaskTypes.CalibrateDistributor:
                                timeThreshold = _thresholds.TimeCalibratedDistributor;
                                break;
                            case TaskTypes.ClearAsteroids:
                                timeThreshold = _thresholds.TimeClearAsteroids;
                                break;
                            case TaskTypes.StartReactor:
                                timeThreshold = _thresholds.TimeStartReactor;
                                break;
                            default:
                                break;
                        }

                        var task = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{t.Value.Type}";

                        // there are tasks with more positions
                        for (var i = 0; i < t.Value.Position.Count(); i++)
                        {
                            var coordsTask = t.Value.Position[i];

                            // distance between player pos and task pos
                            var dist = CalcDistance(playerMovement.Position, coordsTask);

                            // if dist < threshold, check time of staying fixed
                            if (dist <= _thresholds.NextToTask)
                            {

                                // check timespan between movements
                                if (j < dim - 1) 
                                {
                                    var nextMovement = player.Movements[j + 1];
                                    var timeDiff = (nextMovement.Timestamp - playerMovement.Timestamp).TotalSeconds;
                                        
                                    // if timespan compatible with time to perform task: ipotesi attinente di svolgimento del task
                                    if (timeDiff >= timeThreshold)  
                                    {
                                        if (player.Cls == impostorClass) {
                                            //annota cosa fa l'impostore
                                            var objQuantFake = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Fake", new[] { task },  instancesToRelease);
                                            var objPropFake = CowlWrapper.CreateObjPropFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Fake", instancesToRelease);
                                            var ExactFake = CowlWrapper.CreateCardTypeExactly(objPropFake, 1, instancesToRelease);
                                            player.objQuantRestrictionsPlayer.Add(objQuantFake);
                                            player.objCardRestrictionsPlayer.Add(ExactFake);
                                        } else {
                                            //annota cosa fa il crewmate
                                            var objQuantDoes = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Does", new[] { task },  instancesToRelease);
                                            var propertyDoes = CowlWrapper.CreateObjPropFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Does", instancesToRelease);
                                            var ExactDoes = CowlWrapper.CreateCardTypeExactly(propertyDoes, 1, instancesToRelease);
                                            player.objCardRestrictionsPlayer.Add(ExactDoes);
                                            player.objQuantRestrictionsPlayer.Add(objQuantDoes);
                                            //annota chi vede il crewmate mentre fa il task:utilizzi IsInFOV per dire quanti ne guardi mentre fai il task
                                            //IsInFOV se esiste => prelevi la restr cardinali per capire come costruire HasInFOVFromTask
                                            if(IsInFOV){
                                                var objQuantHasInFOVFromTask = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasInFOVFromTask", new[] {playerClass},  instancesToRelease);
                                                var propertyHasInFOVFromTask = CowlWrapper.CreateObjPropFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasInFOVFromTask", instancesToRelease);
                                                var ExactHasInFOVFromTask = CowlWrapper.CreateCardTypeExactly(propertyHasInFOVFromTask, nCrewmatesFOV, instancesToRelease);
                                                player.objCardRestrictionsPlayer.Add(ExactHasInFOVFromTask);
                                                player.objQuantRestrictionsPlayer.Add(objQuantHasInFOVFromTask);
                                            }
                                            else
                                            {
                                                var propertyHasInFOVFromTask = CowlWrapper.CreateObjPropFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasInFOVFromTask", instancesToRelease);
                                                var ExactHasInFOVFromTask = CowlWrapper.CreateCardTypeExactly(propertyHasInFOVFromTask, 0, instancesToRelease);
                                                player.objCardRestrictionsPlayer.Add(ExactHasInFOVFromTask);  
                                            }
                                            //Controllo se un player generico viene visto da un altro che fa un task(escluso sè stesso)
                                            foreach (var op in playersInFOVImpostor.Where(op => op.SessionCls != player.SessionCls)) {
                                                var dimOp = op.Movements.Count();
                                                var dist_1 = CalcDistance(player.Movements[dim-1].Position, op.Movements[dimOp-1].Position); 
                                                if (dist_1 <= _thresholds.FOV)
                                                {
                                                    if (op.Cls == impostorClass & player.Cls != crewmateDeadClass) {
                                                        op.objCardRestricCodependent["ImpostorIsInFOVFromTask"] = op.objCardRestricCodependent.TryGetValue("ImpostorIsInFOVFromTask", out var val) ? val + 1 : 1;
                                                    }else if(op.Cls == crewmateClass & player.Cls != crewmateDeadClass)
                                                    {
                                                        op.objCardRestricCodependent["IsInFOVFromTask"] = op.objCardRestricCodependent.TryGetValue("IsInFOVFromTask", out var val) ? val + 1 : 1;
                                                    }
                                                }
                                                
                                            }   
                                        }
                                        does = true;
                                        DoesGeneric = does;
                                        break;
                                    }
                                } else {
                                    // if last position, check time from last movement and now
                                    var timeDiff = (timestamp - playerMovement.Timestamp).TotalSeconds;
                                    if (timeDiff >= timeThreshold) {
                                        if (player.Cls == impostorClass) {
                                            var objQuantFake = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Fake", new[] { task },  instancesToRelease);
                                            var objPropFake = CowlWrapper.CreateObjPropFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Fake", instancesToRelease);
                                            var ExactFake = CowlWrapper.CreateCardTypeExactly(objPropFake, 1, instancesToRelease);
                                            player.objQuantRestrictionsPlayer.Add(objQuantFake);
                                            player.objCardRestrictionsPlayer.Add(ExactFake);
                                        } else {
                                            var objQuantDoes = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Does", new[] { task },  instancesToRelease);
                                            var propertyDoes = CowlWrapper.CreateObjPropFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Does", instancesToRelease);
                                            var ExactDoes = CowlWrapper.CreateCardTypeExactly(propertyDoes, 1, instancesToRelease);
                                            player.objCardRestrictionsPlayer.Add(ExactDoes);
                                            player.objQuantRestrictionsPlayer.Add(objQuantDoes);
                                            //potremmo aggiustarla nel caso restringendo a solo quello che l'impostore vede => il player è un contenitore associato al player target
                                            if(IsInFOV){
                                                var objQuantHasInFOVFromTask = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasInFOVFromTask", new[] {playerClass},  instancesToRelease);
                                                var propertyHasInFOVFromTask = CowlWrapper.CreateObjPropFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasInFOVFromTask", instancesToRelease);
                                                var ExactHasInFOVFromTask = CowlWrapper.CreateCardTypeExactly(propertyHasInFOVFromTask, nCrewmatesFOV, instancesToRelease);
                                                player.objCardRestrictionsPlayer.Add(ExactHasInFOVFromTask);
                                                player.objQuantRestrictionsPlayer.Add(objQuantHasInFOVFromTask);
                                            }
                                            else
                                            {
                                                var propertyHasInFOVFromTask = CowlWrapper.CreateObjPropFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasInFOVFromTask", instancesToRelease);
                                                var ExactHasInFOVFromTask = CowlWrapper.CreateCardTypeExactly(propertyHasInFOVFromTask, 0, instancesToRelease);
                                                player.objCardRestrictionsPlayer.Add(ExactHasInFOVFromTask);  
                                            }
                                            //Siccome i player sono tutti nella visuale qui puoi semplificare
                                            foreach (var op in playersInFOVImpostor.Where(op => op.SessionCls != player.SessionCls)) {
                                                    var dimOp = op.Movements.Count();
                                                    var dist_1 = CalcDistance(player.Movements[dim-1].Position, op.Movements[dimOp-1].Position); 
                                                    if (dist_1 <= _thresholds.FOV)
                                                    {
                                                        if (op.Cls == impostorClass & player.Cls != crewmateDeadClass) {
                                                            op.objCardRestricCodependent["ImpostorIsInFOVFromTask"] = op.objCardRestricCodependent.TryGetValue("ImpostorIsInFOVFromTask", out var val) ? val + 1 : 1;
                                                        }else if(op.Cls == crewmateClass & player.Cls != crewmateDeadClass)
                                                        {
                                                            op.objCardRestricCodependent["IsInFOVFromTask"] = op.objCardRestricCodependent.TryGetValue("IsInFOVFromTask", out var val) ? val + 1 : 1;
                                                        }
                                                    }
                                                
                                            } 
                                        }
                                        does=true;
                                        DoesGeneric = does;
                                        break;
                                    } 
                                }
                            }
                            if (does) break;
                        }
                        if (does) break;
                    }
                }
                if(DoesGeneric == false & player.Cls == crewmateClass) {
                    var propertyDoes = CowlWrapper.CreateObjPropFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Does", instancesToRelease);
                    var ExactDoes = CowlWrapper.CreateCardTypeExactly(propertyDoes, 0, instancesToRelease);
                    player.objCardRestrictionsPlayer.Add(ExactDoes);
                }else if (DoesGeneric == false & player.Cls == impostorClass)
                {
                    var objPropFake = CowlWrapper.CreateObjPropFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Fake", instancesToRelease);
                    var ExactFake = CowlWrapper.CreateCardTypeExactly(objPropFake, 0, instancesToRelease);
                    player.objCardRestrictionsPlayer.Add(ExactFake);
                }
                //var dataQuantHasCoordinates = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasCoordinates", new[] { player.Movements[dim-1].Position.ToString() }, "http://www.w3.org/2001/XMLSchema#string", "", instancesToRelease);
                //player.dataQuantRestrictionsPlayer.Add(dataQuantHasCoordinates);
                var objPropRestrictionNPlayers = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasNAlivePlayers", new[] { playerClass }, instancesToRelease);
                var propertyRestrictionNPlayers = CowlWrapper.CreateObjPropFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasNAlivePlayers", instancesToRelease);
                var ExactRestrictionNPlayers = CowlWrapper.CreateCardTypeExactly(propertyRestrictionNPlayers, numeroPlayerVivi, instancesToRelease);
                player.objQuantRestrictionsPlayer.Add(objPropRestrictionNPlayers);
                player.objCardRestrictionsPlayer.Add(ExactRestrictionNPlayers);

            }
            //Inserimento dei player all'interno dell'ontologia => post iterazione su tutti i player per memorizzare informazioni codipendenti tra di loro
            foreach (var player in playersInFOVImpostor)
            {
                //Mi segno da quante persone che fanno un task l'impostore è visto in un certo momento
                if(player.Cls == impostorClass) {
                    var propertyImpostorIsInFOVFromTask = CowlWrapper.CreateObjPropFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/ImpostorIsInFOVFromTask", instancesToRelease);
                    var ExactImpostorIsInFOVFromTask = CowlWrapper.CreateCardTypeExactly(propertyImpostorIsInFOVFromTask, player.objCardRestricCodependent.GetValueOrDefault("ImpostorIsInFOVFromTask", 0), instancesToRelease);
                    player.objCardRestrictionsPlayer.Add(ExactImpostorIsInFOVFromTask);
                }else if(player.Cls == crewmateClass) {
                    var propertyIsInFOVFromTask = CowlWrapper.CreateObjPropFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/IsInFOVFromTask", instancesToRelease);
                    var ExactImpostorIsInFOVFromTask = CowlWrapper.CreateCardTypeExactly(propertyIsInFOVFromTask, player.objCardRestricCodependent.GetValueOrDefault("IsInFOVFromTask", 0), instancesToRelease);
                    player.objCardRestrictionsPlayer.Add(ExactImpostorIsInFOVFromTask);
                }
                var sessionClass = CowlWrapper.CreateClassFromIri($"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{player.SessionCls}", instancesToRelease);
                CowlWrapper.CreateIndividual(onto,$"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{player.Name}", new[] { player.Cls, sessionClass }, player.objQuantRestrictionsPlayer.ToArray(), player.dataQuantRestrictionsPlayer.ToArray(), instancesToRelease, true, player.objCardRestrictionsPlayer.ToArray());
            }

            var alive = 0;
            foreach (var p in players)
            {
                if (p.Cls == impostorClass || p.Cls == crewmateClass)
                {
                    //he's alive
                    alive++;
                }
            }

            var map = "";
            var anonVotes = "";
            var visualTasks = "";
            var confirmEjects = "";
            if (game.Options is NormalGameOptions normalGameOptions)
            {
                map = normalGameOptions.Map.ToString();
                anonVotes = normalGameOptions.AnonymousVotes.ToString();
                visualTasks = normalGameOptions.VisualTasks.ToString();
                confirmEjects = normalGameOptions.ConfirmImpostor.ToString();
            }
            else if (game.Options is LegacyGameOptionsData legacyGameOptionsData)
            {
                map = legacyGameOptionsData.Map.ToString();
                anonVotes = legacyGameOptionsData.AnonymousVotes.ToString();
                visualTasks = legacyGameOptionsData.VisualTasks.ToString();
                confirmEjects = legacyGameOptionsData.ConfirmImpostor.ToString();
            }

            var dataQuantRestrictionMap = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/UseMap", new[] { map }, "http://www.w3.org/2001/XMLSchema#string", "", instancesToRelease);
            var dataQuantRestrictionAnonymVotes = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/AnonymousVotesEnabled", new[] { anonVotes }, "http://www.w3.org/2001/XMLSchema#boolean", "", instancesToRelease);
            var dataQuantRestrictionVisualTasks = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/VisualTasksEnabled", new[] { visualTasks }, "http://www.w3.org/2001/XMLSchema#boolean", "", instancesToRelease);
            var dataQuantRestrictionConfirmEjects = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/ConfirmEjects", new[] { confirmEjects }, "http://www.w3.org/2001/XMLSchema#boolean", "", instancesToRelease);
            var dataQuantRestrictionState = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/CurrentState", new[] { gameState }, "http://www.w3.org/2001/XMLSchema#string", "", instancesToRelease);
            var dataQuantRestrictionNPlayers = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasNAlivePlayers", new[] { alive.ToString() }, "http://www.w3.org/2001/XMLSchema#integer", "", instancesToRelease);

            //Individuo game può essere utile => metto solo 2 atomi per evitare sballamento punteggi in altre strategie
            //CowlWrapper.CreateIndividual(onto, $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{game.Code.Code}", new[] { gameClass }, null, new[] { dataQuantRestrictionState, dataQuantRestrictionNPlayers, dataQuantRestrictionMap, dataQuantRestrictionAnonymVotes, dataQuantRestrictionVisualTasks, dataQuantRestrictionConfirmEjects }, instancesToRelease, false);

            //write to file
            /*string folderPath = $"gameSession{game.Code}";
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            else if (numRestarts != 0)
            {
                folderPath = $"gameSession{game.Code}_{numRestarts}";
                Directory.CreateDirectory(folderPath);
            }
            string absoluteHeaderDirectory = Path.Combine(folderPath, $"amongus{numAnnot}.owl");
            
            var string3 = UString.UstringCopyBuf(absoluteHeaderDirectory);
            */
            cowl_sym_table.CowlSymTableRegisterPrefixRaw(cowl_ontology.CowlOntologyGetSymTable(onto), UString.UstringCopyBuf(""), UString.UstringCopyBuf("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/"), false);
            //cowl_manager.CowlManagerWritePath(manager, onto, string3);

            // Write the ontology to a string
            UVec_char chars = uvec_builtin.UvecChar();
            cowl_manager.CowlManagerWriteStrbuf(manager, onto, chars);
            var sbyteArray = new sbyte[uvec_builtin.UvecCountChar(chars)];
            uvec_builtin.UvecCopyToArrayChar(chars, sbyteArray);
            byte[] byteArray = Array.ConvertAll(sbyteArray, b => (byte)b);
            string result = playersInFOVImpostor.Any() ? System.Text.Encoding.UTF8.GetString(byteArray) : null;

            //Console.WriteLine(result2.Result);

            foreach (var instance in instancesToRelease)
            {
                cowl_object.CowlRelease(instance);
            }

            //ConsoleDriver.Run(new CowlWrapper());
            cowl_object.CowlRelease(onto.__Instance);
            cowl_object.CowlRelease(manager.__Instance);

            // create a dictionary of player states updates and return that
            Dictionary<byte, PlayerStruct> pStates = new Dictionary<byte, PlayerStruct>();
            foreach (var p in players)
            {
                var dim = p.Movements.Count();
                var ps = new PlayerStruct { State = p.State, SessionCls = p.SessionCls, Movements = new List<CustomMovement> { p.Movements[dim - 1] }, VoteCount = p.VoteCount };

                pStates.Add(p.Id, ps);
            }
            //Console.WriteLine(absoluteHeaderDirectory);
            return (pStates, gameState, result);
        }


        private static double CalcDistance(System.Numerics.Vector2 posPlayer1, System.Numerics.Vector2 posPlayer2)
        {
            return Math.Sqrt(Math.Pow(posPlayer1.X - posPlayer2.X, 2) + Math.Pow(posPlayer1.Y - posPlayer2.Y, 2));
        }




    }




}
