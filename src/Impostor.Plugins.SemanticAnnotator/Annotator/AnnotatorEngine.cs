using cowl;
using Impostor.Api.Events;
using Impostor.Api.Events.Meeting;
using Impostor.Api.Events.Player;
using Impostor.Api.Events.Ship;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Innersloth.Maps;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using System.Text.Json;
using Impostor.Api.Innersloth.GameOptions;
using CowlSharp.Wrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;


namespace Impostor.Plugins.SemanticAnnotator.Utils
{
   
    public class AnnotatorEngine
    {
    

        /// <summary>
        /// Retrieves the annotation.
        /// </summary>
        /// <returns>The annotation.</returns>

        public (Dictionary<byte, PlayerStruct>, string) Annotate(IGame game, List<IEvent>? events, Dictionary<byte, PlayerStruct> playerStates, string gameState, int numAnnot, int numRestarts, DateTimeOffset timestamp)
        {
            string filePath = "../../../../Impostor.Plugins.SemanticAnnotator/Annotator/properties.json";  // JSON file with thresholds
            string nameSpace = "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/";
            // load existing thresholds
            var thresholds = LoadThresholds(filePath);

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
            // Classes
            var crewmateClass = CowlWrapper.CreateClassFromIri(CowlWrapper.GetFullIri(nameSpace, "CrewMateAlive"), instancesToRelease);
            var crewmateDeadClass = CowlWrapper.CreateClassFromIri(CowlWrapper.GetFullIri(nameSpace, "CrewMateDead"), instancesToRelease);
            var impostorClass = CowlWrapper.CreateClassFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/ImpostorAlive", instancesToRelease);
            var impostorDeadClass = CowlWrapper.CreateClassFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/ImpostorExiled", instancesToRelease);
            var gameClass = CowlWrapper.CreateClassFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Game", instancesToRelease);

            // create a list to hold the players
            List<Player> players = new List<Player>();
            
            foreach (var p in game.Players)
            {
                if (p is null) {
                    break;
                } else if (p.Character is null) {
                    break;
                } else if (p.Character.PlayerInfo is null) {
                    break;
                } else if (p.Character.PlayerInfo.PlayerName is null) {
                    break;
                }else if (p.Character.PlayerInfo.PlayerName.Replace(" ", "") == "") {
                    break;
                } else {
                    //assign class to players involved in the game
                    var cls = crewmateClass;
                    switch(p.Character.PlayerInfo.RoleType) {
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
                    Player player = new Player(p.Character.PlayerId, p.Character.PlayerInfo.PlayerName.Replace(" ", ""), cls, playerStates[p.Character.PlayerId].SessionCls, playerStates[p.Character.PlayerId].Movements, spawnMov, playerStates[p.Character.PlayerId].State, playerStates[p.Character.PlayerId].VoteCount); 
                    players.Add(player);
                }
            }

            // game

            // scan player events
            foreach (var ev in events) 
            {
                // identify the player the event refers to by Id
                Player player = null;
                if (ev is IPlayerEvent playerEvent) {
                    if (playerEvent is null){
                        break;
                    } else if (playerEvent.ClientPlayer is null) {
                        break;
                    } else if (playerEvent.ClientPlayer.Character is null) {
                        break;
                    } else if (playerEvent.ClientPlayer.Character.PlayerInfo is null) {
                        break;
                    } else if (playerEvent.ClientPlayer.Character.PlayerInfo.PlayerName is null) {
                        break;
                    } else if (playerEvent.ClientPlayer.Character.PlayerInfo.PlayerName.Replace(" ", "") == "") {
                        break;
                    } else {
                        player = players.Find(p => p.Id == playerEvent.ClientPlayer.Character.PlayerId);
                    }
                }
                switch (ev)
                {   
                    // EnterVent
                    case IPlayerEnterVentEvent enterVentEvent:
                        var enterVentIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{enterVentEvent.Vent.Name}";                                                       
                        var objQuantEnterVent = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/EnterVent", new[] {enterVentIri}, instancesToRelease);
                        player.objQuantRestrictionsPlayer.Add(objQuantEnterVent);
                        break;

                    // VentTo
                    case IPlayerVentEvent VentEvent:
                        var VentIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{VentEvent.NewVent.Name}";                                                       
                        var objQuantVent = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/VentTo", new[] {VentIri}, instancesToRelease);
                        var ventMov = new CustomMovement(VentEvent.NewVent.Position, player.Movements[player.Movements.Count - 1].Timestamp);
                        player.Movements.Add(ventMov);
                        player.objQuantRestrictionsPlayer.Add(objQuantVent);
                        break;

                    // ExitVent
                    case IPlayerExitVentEvent exitVentEvent:
                        var exitVentIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{exitVentEvent.Vent.Name}";                                                            
                        var objQuantExitVent = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/ExitVent", new[] {exitVentIri}, instancesToRelease);
                        // add movement to track player's path 
                        var exitMov = new CustomMovement(exitVentEvent.Vent.Position, player.Movements[player.Movements.Count - 1].Timestamp);
                        player.Movements.Add(exitMov);
                        // with a mapping VentName - room we can infer the room  
                        player.objQuantRestrictionsPlayer.Add(objQuantExitVent);
                        break;

                    // Murder
                    case IPlayerMurderEvent murderEvent:
                        if (murderEvent.Victim is null) {
                            break;
                        } else if (murderEvent.Victim.PlayerInfo.PlayerName.ToString().Replace(" ", "") == "") {
                            break;
                        } else if (player.Cls == impostorDeadClass) {
                            break;
                        } else {
                            var playerKilled = players.Find(p => p.Id == murderEvent.Victim.PlayerId);
                            var victimIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{playerKilled.SessionCls}";  
                            var objQuantKill = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Kills", new[] { victimIri }, instancesToRelease);
                            player.objQuantRestrictionsPlayer.Add(objQuantKill);
                            // update state of the victim
                            playerKilled.Cls = crewmateDeadClass;
                            break;
                        }

                    // CompletedTask
                    case IPlayerCompletedTaskEvent completedTaskEvent: 
                        var taskIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{completedTaskEvent.Task.Task.Type}";  

                        var objQuantCompletedTask = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Completed",  new[] { taskIri }, instancesToRelease);
                        
                        // player is trusted if: task is visual + there is a crewmate inFOV 
                        if (completedTaskEvent.Task.Task.IsVisual) {
                            var coords = completedTaskEvent.PlayerControl.NetworkTransform.Position;
                            foreach (var p in completedTaskEvent.Game.Players) {
                                // an impostor wouldn't witness a crewmate 
                                if (p != player && p.Character.PlayerInfo.RoleType.ToString() == "crewmate" && p.Character.PlayerInfo.IsDead == false) {
                                    var coordsP = p.Character.NetworkTransform.Position; 
                                    // euclidean distance
                                    var dist = CalcDistance(coords, coordsP); 
                                    // if distance <= threshold then IsInFOV for both players involved 
                                    if (dist <= thresholds.FOV) {
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
                        if (votedEvent.VotedFor is null) {
                            break;
                        } else if (votedEvent.VotedFor.PlayerInfo.PlayerName.ToString().Replace(" ", "") == "") {
                            break;
                        } else {
                            var votedPlayer = players.Find(ot => ot.Id == votedEvent.VotedFor.PlayerId);
                            var votedIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{votedPlayer.SessionCls}";  
                            var objQuantVote = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Votes", new[] {votedIri}, instancesToRelease);
                            
                            player.objQuantRestrictionsPlayer.Add(objQuantVote);
                            //update count of votes got by player in current round
                            votedPlayer.IncrementScore();
                            break;
                        }

                    // StartMeeting
                    case IPlayerStartMeetingEvent startMeetingEvent: 
                        // if no body reported is an emergency meeting call
                        if (startMeetingEvent.Body is null || startMeetingEvent.Body.PlayerInfo.PlayerName == "")  {
                            var emergencyCallIri = "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/EmergencyCall";
                            var objQuantEmergencyMeeting = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Calls", new[] { emergencyCallIri }, instancesToRelease);
                            
                            player.objQuantRestrictionsPlayer.Add(objQuantEmergencyMeeting);
                        } else {
                            var deadPlayer = players.Find(ot => ot.Id == startMeetingEvent.Body.PlayerId);
                            var deadBodyIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{deadPlayer.SessionCls}";  
                            var objQuantReport = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Reports", new[] {deadBodyIri}, instancesToRelease);
                            
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
                        player.Movements.Add(mov);
                        
                        break;

                    //scan ship events

                    // shipSabotage 
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
                    
                    //meetingStarted
                    case IMeetingStartedEvent meetingStartedEvent:
                        gameState = "meeting";
                        break;

                    // meetingEnded
                    case IMeetingEndedEvent meetingEndedEvent:
                        gameState = "none";

                        // count number of alive players in game
                        
                        var nAlivePlayers = 0;
                        foreach (var p in meetingEndedEvent.Game.Players) {
                            if (p.Character.PlayerInfo.IsDead == false) {
                                nAlivePlayers++;
                            }
                        }
                        // set a status for each player (trusted/suspected/none)
                        foreach (var p in players) {
                            if (p.VoteCount == (nAlivePlayers/2)-1 && p.State != "trusted") {
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

                            if (meetingEndedEvent.Exiled is null) {
                                break;
                            } else if (meetingEndedEvent.Exiled.PlayerInfo.PlayerName.Replace(" ", "") == "") {
                                break;
                            }
                            var playerExiled = players.Find(p => p.Id == meetingEndedEvent.Exiled.PlayerId);
                            if (meetingEndedEvent.Exiled.PlayerInfo.IsImpostor == false) {
                                playerExiled.Cls = crewmateDeadClass;  
                            } else {
                                playerExiled.Cls = impostorDeadClass;
                            }
                            playerExiled.dataQuantRestrictionsPlayer.Add(dataQuantExiled);
                        }
                        break;


                }
                //Console.WriteLine(ev);
            } 

            // when all events have been analyzed, for each player create the individual with all collected properties
            foreach (var player in players)
            {
                
                var dim = player.Movements.Count();
                var nCrewmatesFOV = 0;
                var nImpostorsFOV = 0;

                // check if player fixed or moving towards someone
                
                // if no movement, player is in a fixed position (maybe AFK?)
                if (dim == 1) {                     
                    var dataQuantPos = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/IsFixed", new[] { "true" }, "http://www.w3.org/2001/XMLSchema#boolean", "", instancesToRelease);
                    player.dataQuantRestrictionsPlayer.Add(dataQuantPos);
                } else { 
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

                            if (newDistance < initDistance )
                            {
                                near++;
                            }
                        }

                        if (near >= dim / 2)
                        {
                            var getClosePlayer = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{p.SessionCls}";
                            var objQuantGetClose = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/GetCloseTo",new[] {getClosePlayer},instancesToRelease);

                            player.objQuantRestrictionsPlayer.Add(objQuantGetClose);
                        }
                    }
                }

                // check if player is InFOV other players
                foreach (var op in players) {
                    //if dead player, skip
                    if (op != player && op.Cls != crewmateDeadClass && op.Cls != impostorDeadClass) {
                        var dimOp = op.Movements.Count();
                        // euclidean distance
                        var dist = CalcDistance(player.Movements[dim-1].Position, op.Movements[dimOp-1].Position); 
                        // if distance < threshold then IsInFOV for both players 
                        if (dist <= thresholds.FOV) {
                            var InFovIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{op.SessionCls}";                      
                            var objQuantIsInFOV = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/IsInFOV", new[] {InFovIri}, instancesToRelease);
                            player.objQuantRestrictionsPlayer.Add(objQuantIsInFOV);
                            if (op.Cls == crewmateClass) {
                                nCrewmatesFOV++;
                            } else {
                                nImpostorsFOV++;
                            }
                        }
                    }
                }

                //counter of players in FOV
                var dataQuantNCrewmatesFOV = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasNCrewmatesInFOV", new [] { nCrewmatesFOV.ToString() }, "http://www.w3.org/2001/XMLSchema#integer", "", instancesToRelease);
                player.dataQuantRestrictionsPlayer.Add(dataQuantNCrewmatesFOV);
                var dataQuantNImpostorsFOV = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasNImpostorsInFOV", new [] { nImpostorsFOV.ToString() }, "http://www.w3.org/2001/XMLSchema#integer", "", instancesToRelease);
                player.dataQuantRestrictionsPlayer.Add(dataQuantNImpostorsFOV);

                // check if player is nextTo a vent
                foreach (var v in MapData.Maps[game.Options.Map].Vents) {
                    var coordsVent = v.Value.Position;
                    var dist = CalcDistance(player.Movements[dim-1].Position, coordsVent);
                    if (dist <= thresholds.NextToVent) {
                        var vent = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{v.Value.Name}";  
                        var objQuantNextToVent = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/NextTo", new[] { vent },  instancesToRelease);

                        player.objQuantRestrictionsPlayer.Add(objQuantNextToVent);
                        break; //a player can't be next to more than 1 vent
                    }                        
                }

                // check if player is nextTo a task
                foreach (var t in MapData.Maps[game.Options.Map].Tasks) {
                    var nextTo = false;
                    for (var i=0; i < t.Value.Position.Count(); i++) {
                        var coordsTask = t.Value.Position[i];
                        var dist = CalcDistance(player.Movements[dim-1].Position, coordsTask);
                        if (dist <= thresholds.NextToVent) {
                            var task = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{t.Value.Type}";  
                            var objQuantNextToTask = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/NextTo", new[] { task },  instancesToRelease);

                            player.objQuantRestrictionsPlayer.Add(objQuantNextToTask);
                            nextTo = true; //a player can't be next to more than 1 task
                            break;
                        }         
                    } if (nextTo) break;                
                }
            
                // loop through player positions
                for (var j = 0; j < dim; j++)
                {
                    var does = false;
                    var playerMovement = player.Movements[j];
                    // check if player is nextTo a task and does a task
                    foreach (var t in MapData.Maps[game.Options.Map].Tasks) {
                        var timeThreshold = thresholds.TimeShort;
                        switch (t.Value.Type) {
                             case TaskTypes.InspectSample:
                                timeThreshold = thresholds.TimeInspectSample;
                                break;
                            case TaskTypes.UnlockManifolds:
                                timeThreshold = thresholds.TimeUnlockManifolds;
                                break;
                            case TaskTypes.CalibrateDistributor:
                                timeThreshold = thresholds.TimeCalibratedDistributor;
                                break;
                            case TaskTypes.ClearAsteroids:
                                timeThreshold = thresholds.TimeClearAsteroids;
                                break;
                            case TaskTypes.StartReactor:
                                timeThreshold = thresholds.TimeStartReactor;
                                break;
                            default:
                                break;
                        }

                        var task = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{t.Value.Type}"; 
                         
                        // there are tasks with more positions
                        for (var i=0; i < t.Value.Position.Count(); i++) {
                            var coordsTask = t.Value.Position[i];
                            
                            // distance between player pos and task pos
                            var dist = CalcDistance(playerMovement.Position, coordsTask);

                            // if dist < threshold, check time of staying fixed
                            if (dist <= thresholds.NextToTask) {

                                // check timespan between movements
                                if (j < dim - 1) 
                                {
                                    var nextMovement = player.Movements[j + 1];
                                    var timeDiff = (nextMovement.Timestamp - playerMovement.Timestamp).TotalSeconds;
                                        
                                    // if timespan compatible with time to perform task
                                    if (timeDiff >= timeThreshold)  
                                    {
                                        if (player.Cls == impostorClass) {
                                            var objQuantFake = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Fake", new[] { task },  instancesToRelease);

                                            player.objQuantRestrictionsPlayer.Add(objQuantFake);
                                        } else {
                                            var objQuantDoes = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Does", new[] { task },  instancesToRelease);

                                            player.objQuantRestrictionsPlayer.Add(objQuantDoes);
                                        }
                                        does = true;
                                        break;
                                    }
                                } else {
                                    // if last position, check time from last movement and now
                                    var timeDiff = (timestamp - playerMovement.Timestamp).TotalSeconds;
                                    if (timeDiff >= timeThreshold) {
                                        if (player.Cls == impostorClass) {
                                            var objQuantFake = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Fake", new[] { task },  instancesToRelease);

                                            player.objQuantRestrictionsPlayer.Add(objQuantFake);
                                        } else {
                                            var objQuantDoes = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Does", new[] { task },  instancesToRelease);

                                            player.objQuantRestrictionsPlayer.Add(objQuantDoes);
                                        }
                                        does=true;
                                        break;
                                    } 
                                }
                            }
                            if(does) break;
                        } if(does) break;
                    } 
                }

                var dataQuantHasCoordinates = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasCoordinates", new[] { player.Movements[dim-1].Position.ToString() }, "http://www.w3.org/2001/XMLSchema#string", "", instancesToRelease);
                player.dataQuantRestrictionsPlayer.Add(dataQuantHasCoordinates);
                var sessionClass = CowlWrapper.CreateClassFromIri($"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{player.SessionCls}", instancesToRelease);

                var resultCreatePlayer  = CowlWrapper.CreateIndividual(onto,$"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{player.Name}", new[] { player.Cls, sessionClass }, player.objQuantRestrictionsPlayer.ToArray(), player.dataQuantRestrictionsPlayer.ToArray(), instancesToRelease);
            
            }
            
            var alive = 0;
            foreach (var p in players) {
                if (p.Cls == impostorClass || p.Cls == crewmateClass) {
                    //he's alive
                    alive++;
                }
            }

            var map = "";
            var anonVotes = "";
            var visualTasks = "";
            var confirmEjects = "";
            if (game.Options is NormalGameOptions normalGameOptions) {
                map = normalGameOptions.Map.ToString();
                anonVotes = normalGameOptions.AnonymousVotes.ToString();
                visualTasks = normalGameOptions.VisualTasks.ToString();
                confirmEjects = normalGameOptions.ConfirmImpostor.ToString();
            } else if (game.Options is LegacyGameOptionsData legacyGameOptionsData) {
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
            

            var resultCreateGame = CowlWrapper.CreateIndividual(onto,$"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{game.Code.Code}", new[] { gameClass }, null, new[] { dataQuantRestrictionState, dataQuantRestrictionNPlayers, dataQuantRestrictionMap, dataQuantRestrictionAnonymVotes, dataQuantRestrictionVisualTasks, dataQuantRestrictionConfirmEjects }, instancesToRelease, false);
            
            //write to file
            string folderPath = $"gameSession{game.Code}";
            if (!Directory.Exists(folderPath)) {
                Directory.CreateDirectory(folderPath); 
            } else if (numRestarts != 0) {
                folderPath = $"gameSession{game.Code}_{numRestarts}";
                Directory.CreateDirectory(folderPath); 
            }
            string absoluteHeaderDirectory = Path.Combine(folderPath, $"amongus{numAnnot}.owl");
            var string3 = UString.UstringCopyBuf(absoluteHeaderDirectory);
            cowl_sym_table.CowlSymTableRegisterPrefixRaw(cowl_ontology.CowlOntologyGetSymTable(onto), UString.UstringCopyBuf(""), UString.UstringCopyBuf("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/"), false);
            cowl_manager.CowlManagerWritePath(manager, onto, string3);

            // Write the ontology to a string
            UVec_char chars = uvec_builtin.UvecChar();
            cowl_manager.CowlManagerWriteStrbuf(manager, onto, chars);
            var sbyteArray = new sbyte[uvec_builtin.UvecCountChar(chars)];
            uvec_builtin.UvecCopyToArrayChar(chars, sbyteArray);
            byte[] byteArray = Array.ConvertAll(sbyteArray, b => (byte)b);
            string result = System.Text.Encoding.UTF8.GetString(byteArray);

            var result2 = Task.Run(async () => await CallArgumentationAsync(result));
            result2.Wait(); 
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
            foreach (var p in players) {
                var dim = p.Movements.Count();
                var ps = new PlayerStruct { State = p.State, SessionCls = p.SessionCls, Movements = new List<CustomMovement> { p.Movements[dim - 1] }, VoteCount = p.VoteCount};

                pStates.Add(p.Id, ps);
            }
            return (pStates, gameState);
        }

        public static async Task<string> CallArgumentationAsync(string annotations)
        {
            string url_owl = "http://127.0.0.1:18080/update";
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    var StringContent = new StringContent(annotations, System.Text.Encoding.UTF8);
                    HttpResponseMessage response_1 = await client.PostAsync(url_owl, StringContent);
                    string result = await response_1.Content.ReadAsStringAsync();
                    return result;
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"Errore nella richiesta: {e.Message}");
                    return "Errore nella richiesta";
                }
            }
        }
        

        private static double CalcDistance(System.Numerics.Vector2 posPlayer1, System.Numerics.Vector2 posPlayer2)
        {
            return Math.Sqrt(Math.Pow(posPlayer1.X - posPlayer2.X, 2) + Math.Pow(posPlayer1.Y - posPlayer2.Y, 2));
        }

        public static Thresholds LoadThresholds(string filePath)
        {
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<Thresholds>(json);
            }
            else
            {
                Console.WriteLine("JSON file not found");
                return new Thresholds() { FOV = 3.0, NextToTask = 1.0, NextToVent = 1.0, TimeShort = 2.0, TimeInspectSample = 3.0, TimeUnlockManifolds = 5.0, TimeCalibratedDistributor = 9.0, TimeClearAsteroids = 11.0, TimeStartReactor = 28.0 };  // default threhsolds
            }
        }


    }

    public class Player
    {
        public byte Id { get; set; }
        public string Name { get; set; }
        public string State { get; set; }
        public CowlClass Cls { get; set; }
        public string SessionCls { get; set; }
        public List<CustomMovement> Movements { get; set; }
        public int VoteCount { get; set; }
        public List<CowlObjHasValue> objHasValueRestrictionsPlayer { get; set; }
        public List<CowlObjQuant> objQuantRestrictionsPlayer { get; set; }
        public List<CowlDataQuant> dataQuantRestrictionsPlayer { get; set; }

        public Player(byte id, string name, CowlClass cls, string sesCls, List<CustomMovement> movements, CustomMovement initialMov, string state, int voteCount)
        {
            Id = id; 
            Name = name.Replace(" ", "");
            State = state;
            Cls = cls; //class of the player
            SessionCls = sesCls; //class of the player for the session
            if (movements.Count() == 0) {
                Movements = new List<CustomMovement>{initialMov};
            } else {
                Movements = movements;
            }
            VoteCount = voteCount; 
            // lists to store characteristics inferred from events
            objHasValueRestrictionsPlayer = new List<CowlObjHasValue>();
            objQuantRestrictionsPlayer = new List<CowlObjQuant>();
            dataQuantRestrictionsPlayer = new List<CowlDataQuant>();
        }
        public void IncrementScore()
        {
            VoteCount ++; // Increment vote count
        }
        public void resetVoteCount()
        {
            VoteCount = 0; // reset after meeting ended
        }
        public void resetMovements()
        {
            var timestamp = Movements[Movements.Count - 1].Timestamp;
            Movements.Clear(); // reset after meeting ended
            System.Numerics.Vector2 meetingSpawnCenter = new System.Numerics.Vector2(-0.72f, 0.62f); //meetingSpawnCenter for Skeld Map
            CustomMovement spawnMov = new CustomMovement(meetingSpawnCenter, timestamp);
            Movements.Add(spawnMov);
        }
    }

    public class CustomMovement
    {
        public System.Numerics.Vector2 Position { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public CustomMovement(System.Numerics.Vector2 position, DateTimeOffset timestamp)
        {
            Position = position;
            Timestamp = timestamp;
        }
    }

    public class Thresholds
    {
        public double FOV { get; set; }
        public double NextToTask { get; set; }
        public double NextToVent { get; set; }
        public double TimeShort { get; set; }
        public double TimeInspectSample { get; set; }
        public double TimeUnlockManifolds { get; set; }
        public double TimeCalibratedDistributor { get; set; }
        public double TimeClearAsteroids { get; set; }
        public double TimeStartReactor { get; set; }
    }
}
