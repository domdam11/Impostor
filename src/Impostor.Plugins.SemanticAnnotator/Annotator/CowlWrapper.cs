using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Runtime.InteropServices;
using cowl;
using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;
using CppSharp.Passes;
using CppSharp.Types.Std;
using Impostor.Api.Events;
using Impostor.Api.Events.Meeting;
using Impostor.Api.Events.Player;
using Impostor.Api.Events.Ship;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Innersloth.Maps;
using Impostor.Api.Net.Inner.Objects;


namespace Impostor.Plugins.SemanticAnnotator.Utils
{
    public class CowlWrapper
    {
        
        /// <summary>
        /// Retrieves the annotation.
        /// </summary>
        /// <returns>The annotation.</returns>

        public (Dictionary<byte, string>, string) Annotate(IGame game, List<IEvent>? events, Dictionary<byte, string> playerStates, string gameState, int num_annot)
        {
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

            if (num_annot == 7) {
                Console.WriteLine("Hello");
            }

            var instancesToRelease = new List<nint>();

            // Instantiate a manager and deserialize an ontology from file.
            CowlManager manager = cowl_manager.CowlManager();

            var onto = cowl_manager.CowlManagerGetOntology(manager, cowl_ontology_id.CowlOntologyIdAnonymous());
            
            // note: the game passed as argument represent the last status of the game to which events passed as argument are "applied"   
            // Classes
            var crewmateClass = CreateClassFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/CrewMateAlive", instancesToRelease);
            var crewmateDeadClass = CreateClassFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/CrewMateDead", instancesToRelease);
            var impostorClass = CreateClassFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/ImpostorAlive", instancesToRelease);
            var impostorDeadClass = CreateClassFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/ImpostorDead", instancesToRelease);
            var shapeshifterClass = CreateClassFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Shapeshifter", instancesToRelease);
            var gameClass = CreateClassFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Game", instancesToRelease);
            var genericRoomClass = CreateClassFromIri("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Room", instancesToRelease);

            // create a list to hold the players
            List<Player> players = new List<Player>();
            if (playerStates.Count == 0)
            {
                foreach(var p in game.Players) {
                // Aggiungi una nuova coppia chiave/valore se il dizionario è vuoto 
                    playerStates[p.Character.PlayerId] = "none";
                }
            }
            foreach (var p in game.Players)
            {
                //assign class to players involved in the game
                CowlClass cls = crewmateClass;
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
                    case RoleTypes.Shapeshifter:
                        cls = shapeshifterClass;
                        break;
                    default:
                        break;
                }
                // instantiate player: id, name, class, status, list of positions, lists of obj and data properties 
                Player player = new Player(p.Character.PlayerId, p.Character.PlayerInfo.PlayerName.Replace(" ", ""), cls, p.Character.NetworkTransform.Position, playerStates[p.Character.PlayerId]); 
                players.Add(player);
            }

            // game

            // Mostrare tutti gli eventi nella lista
            Console.WriteLine("Lista degli Eventi:");
            foreach (var evento in events)
            {
                Console.WriteLine(evento);
            }

            // scan player events
            foreach (var ev in events) 
            {
                // identify the player the event refers to by Id
                Player player = null;
                if (ev is IPlayerEvent playerEvent) {
                    player = players.Find(p => p.Id == playerEvent.ClientPlayer.Character.PlayerId);
                }
                switch (ev)
                {   
                    // EnterVent
                    case IPlayerEnterVentEvent enterVentEvent:
                        var enterVentIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{enterVentEvent.Vent.Name}";                                                       
                        var objQuantEnterVent = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/EnterVent", new[] {enterVentIri}, instancesToRelease);
                        player.objQuantRestrictionsPlayer.Add(objQuantEnterVent);
                        break;

                    // ExitVent
                    case IPlayerExitVentEvent exitVentEvent:
                        var exitVentIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{exitVentEvent.Vent.Name}";                                                            
                        var objQuantExitVent = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/ExitVent", new[] {exitVentIri}, instancesToRelease);
                        // add movement to track player's path 
                        player.Movements.Add(exitVentEvent.Vent.Position);
                        // with a mapping VentName - room we can infer the room  
                        player.objQuantRestrictionsPlayer.Add(objQuantExitVent);
                        break;

                    // Murder
                    case IPlayerMurderEvent murderEvent:
                        var victimIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{murderEvent.Victim.PlayerInfo.PlayerName.Replace(" ", "")}";  
                        var objHasValueKill = CreateObjHasValue("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Kills", victimIri, instancesToRelease);

                        player.objHasValueRestrictionsPlayer.Add(objHasValueKill);
                        // update state of the victim
                        Player playerKilled = players.Find(p => p.Id == murderEvent.Victim.PlayerId);
                        playerKilled.Cls = crewmateDeadClass;
                        break;

                    // CompletedTask
                    case IPlayerCompletedTaskEvent completedTaskEvent: 
                        var taskIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{completedTaskEvent.Task.Task.Type}";  

                        var objQuantCompletedTask = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Completed",  new[] { taskIri }, instancesToRelease);
                        
                        // player is trusted if: task is visual + there is a crewmate inFOV 
                        if (completedTaskEvent.Task.Task.IsVisual) {
                            var coords = completedTaskEvent.PlayerControl.NetworkTransform.Position;
                            foreach (var p in completedTaskEvent.Game.Players) {
                                // an impostor wouldn't witness a crewmate 
                                if (p != player && p.Character.PlayerInfo.RoleType.ToString() == "crewmate") {
                                    var coordsP = p.Character.NetworkTransform.Position; 
                                    // euclidean distance
                                    var dist = calcDistance(coords, coordsP); 
                                    // if distance < threshold then IsInFOV for both players involved 
                                    if (dist < 3) {
                                        //so there is at least one crewmate who have seen the player doing the task
                                        player.State = "trusted";
                                        var dataQuantPlayerState =  CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasStatus", "trusted", "http://www.w3.org/2001/XMLSchema#string", "", instancesToRelease);
                                        player.dataQuantRestrictionsPlayer.Add(dataQuantPlayerState);  
                                    }
                                }
                            }                      
                        }
                        player.objQuantRestrictionsPlayer.Add(objQuantCompletedTask);
                        break;

                    // Vote
                    case IPlayerVotedEvent votedEvent:
                        if (votedEvent.VotedFor.PlayerInfo.PlayerName != "" && votedEvent.VotedFor != null){
                            var votedIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{votedEvent.VotedFor.PlayerInfo.PlayerName.Replace(" ", "")}";  
                            var objHasValueVote = CreateObjHasValue("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Voted", votedIri, instancesToRelease);
                            
                            player.objHasValueRestrictionsPlayer.Add(objHasValueVote);
                            //update count of votes got by player in current round
                            Player votedPlayer = players.Find(ot => ot.Id == votedEvent.VotedFor.PlayerId);
                            votedPlayer.IncrementScore();
                        }
                        break;

                    // StartMeeting
                    case IPlayerStartMeetingEvent startMeetingEvent: 
                        // if no body reported is an emergency meeting call
                        if (startMeetingEvent.Body is null || startMeetingEvent.Body.PlayerInfo.PlayerName == "")  {
                            var emergencyCallIri = "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/EmergencyCall";
                            var objQuantEmergencyMeeting = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Calls", new[] { emergencyCallIri }, instancesToRelease);
                            
                            player.objQuantRestrictionsPlayer.Add(objQuantEmergencyMeeting);
                        } else {
                            var deadBodyIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{startMeetingEvent.Body.PlayerInfo.PlayerName.Replace(" ", "")}";  
                            var objHasValueReport = CreateObjHasValue("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Reports", deadBodyIri, instancesToRelease);
                            
                            player.objHasValueRestrictionsPlayer.Add(objHasValueReport);

                            var emergencyCallIri = "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/EmergencyCall";
                            var objQuantEmergencyMeeting = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Calls", new[] { emergencyCallIri }, instancesToRelease);
                            
                            player.objQuantRestrictionsPlayer.Add(objQuantEmergencyMeeting);
                        }
                        break;

                    // Exile
                    case IPlayerExileEvent exileEvent:
                        var exiledIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{exileEvent.PlayerControl.PlayerInfo.PlayerName.Replace(" ", "")}";  
                        var dataQuantExiled =  CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/gotExiled", "true", "http://www.w3.org/2001/XMLSchema#boolean", "", instancesToRelease);
                        
                        Player exiledPlayer = players.Find(ex => ex.Id == exileEvent.PlayerControl.PlayerId);
                        if (exiledPlayer.Cls == crewmateClass) {
                            exiledPlayer.Cls = crewmateDeadClass;
                        } else {
                            exiledPlayer.Cls = impostorDeadClass;
                        }  
                        exiledPlayer.dataQuantRestrictionsPlayer.Add(dataQuantExiled);
                        break;
                    
                    // RepairSystem
                     case IPlayerRepairSystemEvent repairSystemEvent:
                        var repairediri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{repairSystemEvent.SystemType}";
                        var objQuantRepairSystem = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Repairs", new[] { repairediri }, instancesToRelease);
                        
                        player.objQuantRestrictionsPlayer.Add(objQuantRepairSystem);
                        // update game state
                        gameState = "none";
                        break;

                    // Movement
                    case IPlayerMovementEvent movementEvent:
                        var coordsPlayer = movementEvent.PlayerControl.NetworkTransform.Position;
                        // add movement to track path of the player
                        player.Movements.Add(coordsPlayer);
                        
                        // check if player is near other players
                        foreach (var p in movementEvent.Game.Players) {
                            if (p != player) {
                                var coordsP = p.Character.NetworkTransform.Position; 
                                // euclidean distance
                                var dist = calcDistance(coordsPlayer, coordsP); 
                                // if distance < threshold then IsInFOV for both players 
                                if (dist < 3) {
                                    var objHasValueIsInFOV1 = CreateObjHasValue("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/IsInFOV", $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{p.Character.PlayerInfo.PlayerName.Replace(" ", "")}", instancesToRelease);                        
                                    player.objHasValueRestrictionsPlayer.Add(objHasValueIsInFOV1);
                                    var objHasValueIsInFOV2 = CreateObjHasValue("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/IsInFOV", $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{player.Name}", instancesToRelease);                        
                                    Player other = players.Find(ot => ot.Id == p.Character.PlayerId);
                                    player.objHasValueRestrictionsPlayer.Add(objHasValueIsInFOV2);
                                    //question: defining isinfov as symmetric avoid us to write for both players involved?  
                                }
                            }
                        }
                        // check if player is nextTo a vent
                        foreach (var v in MapData.Maps[game.Options.Map].Vents) {
                            var coordsVent = v.Value.Position;
                            var dist = calcDistance(coordsPlayer, coordsVent);
                            if (dist < 0.5) {
                                var vent = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{v.Value.Name}";  
                                var objQuantNextToVent = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/NextTo", new[] { vent },  instancesToRelease);

                                player.objQuantRestrictionsPlayer.Add(objQuantNextToVent);
                                break; //a player can't be next to more than 1 vent
                            }
                        }
                        break;

                    //scan ship events

                    // shipSabotage 
                    
                    case IShipSabotageEvent shipSabotageEvent:
                        player = players.Find(p => p.Id == shipSabotageEvent.ClientPlayer.Character.PlayerId);
                        var sabotageIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{shipSabotageEvent.SystemType}";
                        var objQuantSabotage = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Sabotages", new[] { sabotageIri }, instancesToRelease);
                        //PER SAPERE STANZA BISOGNA INDAGARE SYSTEMTYPE?
                        player.objQuantRestrictionsPlayer.Add(objQuantSabotage);
                        gameState = "sabotage";
                        break;

                    // shipDoorsClose
                    case IShipDoorsCloseEvent shipDoorsCloseEvent:
                        /*var roomIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{shipDoorsCloseEvent.SystemType}"; //which door/room?
                        var dataQuantShipClose =  CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/areDoorsClosed", "true", "http://www.w3.org/2001/XMLSchema#boolean", "", instancesToRelease);
                        */
                        //dataQuantRestrictionsRoom.Add(dataQuantShipClose);
                        break;

                    // shipDecontamDoorOpen
                    case IShipDecontamDoorOpenEvent shipDecontamDoorOpenEvent:
                    /*
                        var decontamIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{shipDecontamDoorOpenEvent.DecontamDoor}"; //decontamination room
                        var dataQuantDecontamOpen =  CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/areDoorsClosed", "false", "http://www.w3.org/2001/XMLSchema#boolean", "", instancesToRelease);
                        *///QUI SAPPIAMO LA STANZA
                        //dataQuantRestrictionsRoom.Add(dataQuantDecontamOpen);
                        break;
                    
                    // shipPolusDoorOpen
                    case IShipPolusDoorOpenEvent shipPolusDoorOpenEvent:
                        /*var openDoorIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{shipPolusDoorOpenEvent.Door}"; //which door/room?
                        var dataQuantPolusOpen =  CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/areDoorsClosed", "false", "http://www.w3.org/2001/XMLSchema#boolean", "", instancesToRelease);
*/
                        //dataQuantRestrictionsRoom.Add(dataQuantPolusOpen);
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
                            if (p.voteCount == (nAlivePlayers/2)-1) {
                                p.State = "suspected";
                                var dataQuantPlayerState =  CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasStatus", "suspected", "http://www.w3.org/2001/XMLSchema#string", "", instancesToRelease);
                                p.dataQuantRestrictionsPlayer.Add(dataQuantPlayerState); 
                            }
                            p.resetVoteCount();
                        }
                        // if no tie update state of the exiled player
                        if (meetingEndedEvent.IsTie == false)
                        {
                            Player playerExiled = players.Find(p => p.Id == meetingEndedEvent.Exiled.PlayerId);
                            if (meetingEndedEvent.Exiled.PlayerInfo.IsImpostor == false) {
                                playerExiled.Cls = crewmateDeadClass;
                            } else {
                                playerExiled.Cls = impostorDeadClass;
                            }
                        }
                        break;


                }
            } 

            // when all events have been analyzed, for each player create the individual with all collected properties
            foreach (var player in players)
            {
                //counter of players in FOV
                int count = 0;
                
                foreach (var obj in player.objHasValueRestrictionsPlayer)
                {   
                    var temp = cowl_obj_prop_exp.CowlObjPropExpGetProp(cowl_obj_has_value.CowlObjHasValueGetProp(obj).__Instance);
                    if (cowl_obj_prop.CowlObjPropGetIri(temp).ToString() == "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/IsInFOV")
                    {
                        count++;
                    }
                }
                var dataQuantNPlayersFOV =  CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/hasNPlayersInFOV", count.ToString(), "http://www.w3.org/2001/XMLSchema#integer", "", instancesToRelease);
                player.dataQuantRestrictionsPlayer.Add(dataQuantNPlayersFOV);


                var dim = player.Movements.Count();
                
                // if no movement, player is in a fixed position (maybe AFK?)
                if (player.Movements.Count() == 1) {                     
                    var dataQuantPos =  CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/isFixed", "true", "http://www.w3.org/2001/XMLSchema#boolean", "", instancesToRelease);
                    player.dataQuantRestrictionsPlayer.Add(dataQuantPos);
                } else { 
                    // check movement trajectories to see if he's getting near someone. THIS METHOD DOESN'T CONSIDER WALLS
                    foreach (var p in players) {
                        var near = 0;
                        if (p != player) {
                            var initDistance = calcDistance(player.Movements[0], p.Movements[0]);
                            for (var i=1; i < dim; i++) {
                                var newDistance=0.0;
                                if (i < p.Movements.Count()) {
                                    // the other player is moving too
                                    newDistance = calcDistance(player.Movements[i], p.Movements[i]);
                                } else { 
                                    // the other player is fixed
                                    newDistance = calcDistance(player.Movements[i], p.Movements[player.Movements.Count()-1]);
                                }
                                //FIND A THRESHOLD
                                if ((newDistance+5) < initDistance) {
                                    //we should have coordinates of walls without doors(lines:start-end point) and if there is intersection between positions of players and a wall then they are not near
                                    //OR think at rooms and define which room is near to the other
                                    near++;
                                }
                            }
                            if (near >= dim/2) {
                                var objHasValueGetClose = CreateObjHasValue("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/getCloseTo", $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{p.Name}", instancesToRelease);                        
                                player.objHasValueRestrictionsPlayer.Add(objHasValueGetClose);
                            }
                        }
                    }
                    // check if player is nextTo a task
                    foreach (var t in MapData.Maps[game.Options.Map].Tasks) {
                        var nextTo = false;
                        var counter = 0;
                        // there are task with more positions
                        for (var i=0; i < t.Value.Position.Count(); i++) {
                            var coordsTask = t.Value.Position[i];
                            counter = 0;
                            var lastDist = 0.0;
                            for (var j=0; j < player.Movements.Count(); j++) {
                                // euclidean distance
                                var dist = calcDistance(player.Movements[i], coordsTask); 
                                // set a threshold
                                if (dist < 0.5) {
                                    nextTo = true;
                                    if (dist == lastDist) {
                                        counter++;
                                    } else {
                                        counter--; //player moved
                                    } 
                                    lastDist = dist;
                                }
                            }
                        }
                        //check if player is nextTo a task and if he's doing that task
                        if (nextTo == true) {
                            var task = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{t.Value.Type}";  
                            var objQuantNextToTask = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/NextTo", new[] { task }, instancesToRelease);
                            player.objQuantRestrictionsPlayer.Add(objQuantNextToTask);
                            // if player is nextTo a task and he's not moving for a period proportional to time to complete the task, it means he is doing that
                            if (t.Value.Category == TaskCategories.ShortTask && counter == 1) { 
                                //if crewmate does
                                var objQuantDoesTask = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Does",  new[] { task },  instancesToRelease);
                                //if impostor fake
                                if (player.Cls == impostorClass) { 
                                    objQuantDoesTask = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Fake",  new[] { task }, instancesToRelease);
                                }
                                if (player.Cls != impostorDeadClass) { 
                                    player.objQuantRestrictionsPlayer.Add(objQuantDoesTask);
                                }
                            } else if (t.Value.Category == TaskCategories.CommonTask && counter == 2) { 
                                //if crewmate does
                                var objQuantDoesTask = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Does",  new[] { task }, instancesToRelease);
                                //if impostor fake
                                if (player.Cls == impostorClass) { 
                                    objQuantDoesTask = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Fake",  new[] { task }, instancesToRelease);
                                }
                                if (player.Cls != impostorDeadClass) { 
                                    player.objQuantRestrictionsPlayer.Add(objQuantDoesTask);
                                }
                            } else if (t.Value.Category ==  TaskCategories.LongTask && counter == 3) {
                                //if crewmate does
                                var objQuantDoesTask = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Does",  new[] { task }, instancesToRelease);
                                //if impostor fake
                                if (player.Cls == impostorClass) { 
                                    objQuantDoesTask = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Fake",  new[] { task }, instancesToRelease);
                                }
                                if (player.Cls != impostorDeadClass) { 
                                    player.objQuantRestrictionsPlayer.Add(objQuantDoesTask);
                                }
                            }
                            break; //a player can't be next to more than 1 task
                        }
                    }
                }
                var resultCreatePlayer  = CreateIndividual(onto,$"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{player.Name}", new[] { player.Cls }, player.objHasValueRestrictionsPlayer.Distinct().ToArray(), player.objQuantRestrictionsPlayer.Distinct().ToArray(), player.dataQuantRestrictionsPlayer.Distinct().ToArray(), instancesToRelease);
            }
            
            var dataQuantRestrictionsGame = CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/CurrentState", gameState, "http://www.w3.org/2001/XMLSchema#string", "", instancesToRelease);
            var resultCreateGame = CreateIndividual(onto,$"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{game.Code.Code}", new[] { gameClass }, null, null, new[] { dataQuantRestrictionsGame }, instancesToRelease, false);
            //there are MANY ROOMS SO SAME AS PLAYERS
            //var resultRoom = CreateIndividual(onto,"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/ImpostorAnnotatedRoom", new[] { genericRoomClass }, objHasValueRestrictionsRoom.ToArray(), objQuantRestrictionsRoom.ToArray(), dataQuantRestrictionsRoom.ToArray());

            //write to file
            string folderPath = $"gameSession{game.Code}";
            if (!Directory.Exists(folderPath)) {
                Directory.CreateDirectory(folderPath); 
            }
            string absoluteHeaderDirectory = Path.Combine(folderPath, $"amongus{num_annot}.owl");
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

            foreach (var instance in instancesToRelease)
            {
                cowl_object.CowlRelease(instance);
            }
  
            //ConsoleDriver.Run(new CowlWrapper());
            cowl_object.CowlRelease(onto.__Instance);
            cowl_object.CowlRelease(manager.__Instance);
            
            // create a dictionary of player states updates and return that
            Dictionary<byte, string> pStates = new Dictionary<byte, string>();
            foreach (var p in players) {
                pStates.Add(p.Id, p.State);
            }
            return (pStates, gameState);
        }

        public static void Main(string[] args)
        {
            // Call the GetAnnotation method
            //new CowlWrapper();
        }


        public static List<nint> instancesToRelease = new List<nint>();

        //e.g. :Reports
        public static CowlObjProp CreateObjPropFromIri(string propertyIri, List<nint> instancesToRelease)
        {
            var taskRole = cowl_obj_prop.CowlObjPropFromString(UString.UstringCopyBuf(propertyIri));
            instancesToRelease.Add(taskRole.__Instance);
            
            return taskRole;
        }    
        
        //e.g. :CrewMateAlive
        public static CowlClass CreateClassFromIri(string classIri, List<nint> instancesToRelease)
        {
            var classObj = cowl_class.CowlClassFromString(UString.UstringCopyBuf(classIri));
            instancesToRelease.Add(classObj.__Instance);
            
            return classObj;
        }

        //e.g :CrewmateName
        public static CowlDataProp CreateDataPropFromIri(string dataPropIri, List<nint> instancesToRelease)
        {
            var dataPropObj = cowl_data_prop.CowlDataPropFromString(UString.UstringCopyBuf(dataPropIri));
            instancesToRelease.Add(dataPropObj.__Instance);
            
            return dataPropObj;
        }

        //e.g ObjHasValue(:hasParent :John)
        public static CowlObjHasValue CreateObjHasValue(string propertyIri, string individualIri, List<nint> instancesToRelease)
        {
            var property = cowl_obj_prop.CowlObjPropFromString(UString.UstringCopyBuf(propertyIri));
            instancesToRelease.Add(property.__Instance);

            var ind = cowl_named_ind.CowlNamedIndFromString(UString.UstringCopyBuf(individualIri));
            instancesToRelease.Add(ind.__Instance);

            var objRestr = cowl_obj_has_value.CowlObjHasValue(property.__Instance, ind.__Instance);
            instancesToRelease.Add(objRestr.__Instance);

            return objRestr;
        }
        
        //e.g. ObjectSome/AllValuesFrom(:Does ObjectIntersection/UnionOf(:Electrical_Sabotage)))
        public static CowlObjQuant CreateObjValuesRestriction(string propertyIri, IEnumerable<string> fillerClassesIri, List<nint> instancesToRelease)
        {
            var fillerVector = cowl_vector.UvecCowlObjectPtr();

            // Add the filler classes to the vector
            foreach (var fillerClassIri in fillerClassesIri)
            {
                var fillerClass = cowl_class.CowlClassFromString(UString.UstringCopyBuf(fillerClassIri));
                instancesToRelease.Add(fillerClass.__Instance);
                cowl_vector.UvecPushCowlObjectPtr(fillerVector, fillerClass.__Instance);
            }
   

            var operandsRole = cowl_vector.CowlVector(fillerVector);

            // Check the number of operands
            if (cowl_vector.CowlVectorCount(operandsRole) > 1)
            {
                // Create the closure
                var closure = cowl_nary_bool.CowlNaryBool(CowlNAryType.COWL_NT_INTERSECT, operandsRole);
                instancesToRelease.Add(closure.__Instance);

                // Create the task role
                var taskRole = cowl_obj_prop.CowlObjPropFromString(UString.UstringCopyBuf(propertyIri));
                instancesToRelease.Add(taskRole.__Instance);

                // Create the object quantifier
                var obj_quant = cowl_obj_quant.CowlObjQuant(CowlQuantType.COWL_QT_ALL, taskRole.__Instance, closure.__Instance);
                instancesToRelease.Add(obj_quant.__Instance);
                instancesToRelease.Add(operandsRole.__Instance);

                return obj_quant;
            }
            else
            {
                // Create the task role
                var taskRole = cowl_obj_prop.CowlObjPropFromString(UString.UstringCopyBuf(propertyIri));
                instancesToRelease.Add(taskRole.__Instance);

                // Create the object quantifier
                var obj_quant = cowl_obj_quant.CowlObjQuant(CowlQuantType.COWL_QT_ALL, taskRole.__Instance, operandsRole.__Instance);
                instancesToRelease.Add(obj_quant.__Instance);
                instancesToRelease.Add(operandsRole.__Instance);

                return obj_quant;
            }
        }


        //e.g. DataSome/AllValuesFrom(:has2MoreAlivePlayers DatatypeRestriction( xsd:integer xsd:minInclusive "2"^^xsd:integer )))

        public static CowlDataQuant CreateDataValuesRestriction(string propertyIri, string fillerLiteralIri, string dt, string lang, List<nint> instancesToRelease)
        {
            var fillerVector = cowl_vector.UvecCowlObjectPtr();

            // Add the filler literal to the vector
            var fillerLiteral = cowl_literal.CowlLiteralFromString(UString.UstringCopyBuf(dt), UString.UstringCopyBuf(fillerLiteralIri), UString.UstringCopyBuf(lang));
            instancesToRelease.Add(fillerLiteral.__Instance);
            cowl_vector.UvecPushCowlObjectPtr(fillerVector, fillerLiteral.__Instance);
   
            var operandsRole = cowl_vector.CowlVector(fillerVector);
            
            // Create the task role
            var taskRole = cowl_data_prop.CowlDataPropFromString(UString.UstringCopyBuf(propertyIri));
            instancesToRelease.Add(taskRole.__Instance);

            // Create the object quantifier
            var data_quant = cowl_data_quant.CowlDataQuant(CowlQuantType.COWL_QT_ALL, taskRole.__Instance, operandsRole.__Instance);
            instancesToRelease.Add(data_quant.__Instance);
            instancesToRelease.Add(operandsRole.__Instance);

            return data_quant;
        }

        
        //e.g. ClassAssertion(ObjectIntersectionOf(:CrewMateAlive ObjectAllValuesFrom(...) :Player1)
        public static CowlRet CreateIndividual(CowlOntology onto, string individualIri, IEnumerable<CowlClass> classesIri,  IEnumerable<CowlObjHasValue>? objHasValues, IEnumerable<CowlObjQuant> objQuantsIri, IEnumerable<CowlDataQuant>? dataQuantsIri, List<nint> instancesToRelease, Boolean isPlayer=true)
        {
            var operands = cowl_vector.UvecCowlObjectPtr();

            // Add the classes to the operands
            foreach (var classIri in classesIri)
            {
                cowl_vector.UvecPushCowlObjectPtr(operands, classIri.__Instance);
            }

            if (isPlayer) {
                // Add the object quantifiers to the operands
                foreach (var objQuant in objQuantsIri)
                {
                    cowl_vector.UvecPushCowlObjectPtr(operands, objQuant.__Instance);
                }

                foreach (var objVal in objHasValues)
                {
                    cowl_vector.UvecPushCowlObjectPtr(operands, objVal.__Instance);
                }
            }

            foreach (var dataQuant in dataQuantsIri)
            {
                cowl_vector.UvecPushCowlObjectPtr(operands, dataQuant.__Instance);
            }

            CowlVector vec = cowl_vector.CowlVector(operands);
            instancesToRelease.Add(vec.__Instance);

            // Create the expression
            var exp = cowl_nary_bool.CowlNaryBool(CowlNAryType.COWL_NT_INTERSECT, vec);
            instancesToRelease.Add(exp.__Instance);

            // Create the individual
            var ind = cowl_named_ind.CowlNamedIndFromString(UString.UstringCopyBuf(individualIri));
            instancesToRelease.Add(ind.__Instance);

            // Create the axiom
            var axiom = cowl_cls_assert_axiom.CowlClsAssertAxiom(exp.__Instance, ind.__Instance, null);
            instancesToRelease.Add(axiom.__Instance);

            return cowl_ontology.CowlOntologyAddAxiom(onto, axiom.__Instance);
        }
        

        public static double calcDistance(System.Numerics.Vector2 posPlayer1, System.Numerics.Vector2 posPlayer2)
        {
            return Math.Sqrt(Math.Pow(posPlayer1.X - posPlayer2.X, 2) + Math.Pow(posPlayer1.Y - posPlayer2.Y, 2));
        }

    }

    public class Player
    {
        public byte Id { get; set; }
        public string Name { get; set; }
        public string State { get; set; }
        public CowlClass Cls { get; set; }
        public List<System.Numerics.Vector2> Movements { get; set; }
        public int voteCount { get; set; }
        public List<CowlObjHasValue> objHasValueRestrictionsPlayer { get; set; }
        public List<CowlObjQuant> objQuantRestrictionsPlayer { get; set; }
        public List<CowlDataQuant> dataQuantRestrictionsPlayer { get; set; }

        public Player(byte id, string name, CowlClass cls, System.Numerics.Vector2 initialPos, string state)
        {
            Id = id; 
            Name = name.Replace(" ", "");
            State = state;
            Cls = cls; //class of the player
            Movements = new List<System.Numerics.Vector2>{initialPos};
            voteCount = 0; 
            // lists to store characteristics inferred from events
            objHasValueRestrictionsPlayer = new List<CowlObjHasValue>();
            objQuantRestrictionsPlayer = new List<CowlObjQuant>();
            dataQuantRestrictionsPlayer = new List<CowlDataQuant>();
        }
        public void IncrementScore()
        {
            voteCount += 1; // Increment vote count
        }
        public void resetVoteCount()
        {
            voteCount = 0; // reset after meeting ended
        }
    }
}
