using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Channels;
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
using Impostor.Plugins.SemanticAnnotator.Annotator;


namespace Impostor.Plugins.SemanticAnnotator.Utils
{
    public class CowlWrapper
    {
        
        /// <summary>
        /// Retrieves the annotation.
        /// </summary>
        /// <returns>The annotation.</returns>

        public (Dictionary<byte, PlayerStruct>, string) Annotate(IGame game, List<IEvent>? events, Dictionary<byte, PlayerStruct> playerStates, string gameState, int num_annot, int numRestarts)
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

           foreach (var p in game.Players)
            {
                if (p == null) break;  // Se un giocatore è null, saltiamo l'iterazione
                if (p.Character == null) break;
                // Se il giocatore non è già nel dizionario, lo aggiungiamo
                if (!playerStates.ContainsKey(p.Character.PlayerId))
                {
                    PlayerStruct playerStruct = new PlayerStruct
                    {
                        State = "none",  // Stato iniziale del giocatore
                        Movements = new List<System.Numerics.Vector2> { p.Character.NetworkTransform.Position }, // Aggiungi la posizione iniziale
                        VoteCount = 0 // Contatore dei voti iniziale
                    };

                    playerStates.Add(p.Character.PlayerId, playerStruct);
                }
            }
            // Rimuovere giocatori che non sono più nel gioco
            var playerIdsInGame = new HashSet<byte>(
                game.Players
                    .Where(p => p != null && p.Character != null)  // Verifica che il giocatore e il suo Character non siano null
                    .Select(p => p.Character.PlayerId)
            );

            // Trova gli ID dei giocatori che sono nel dizionario ma non più nel gioco
            var playerIdsToRemove = playerStates.Keys
                .Where(playerId => !playerIdsInGame.Contains(playerId))
                .ToList();

            // Rimuovi i giocatori che non sono più nel gioco
            foreach (var playerId in playerIdsToRemove)
            {
                playerStates.Remove(playerId);
            }

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
                    Player player = new Player(p.Character.PlayerId, p.Character.PlayerInfo.PlayerName.Replace(" ", ""), cls, playerStates[p.Character.PlayerId].Movements, p.Character.NetworkTransform.Position, playerStates[p.Character.PlayerId].State, playerStates[p.Character.PlayerId].VoteCount); 
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
                        var objQuantEnterVent = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/EnterVent", new[] {enterVentIri}, instancesToRelease);
                        player.objQuantRestrictionsPlayer.Add(objQuantEnterVent);
                        break;

                    // VentTo
                    case IPlayerVentEvent VentEvent:
                        var VentIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{VentEvent.NewVent.Name}";                                                       
                        var objQuantVent = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/VentTo", new[] {VentIri}, instancesToRelease);
                        player.Movements.Add(VentEvent.NewVent.Position);
                        player.objQuantRestrictionsPlayer.Add(objQuantVent);
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
                        if (murderEvent.Victim is null) {
                            break;
                        } else if (murderEvent.Victim.PlayerInfo.PlayerName.ToString().Replace(" ", "") == "") {
                            break;
                        } else {
                            var victimIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{murderEvent.Victim.PlayerInfo.PlayerName.Replace(" ", "")}";  
                            var objHasValueKill = CreateObjHasValue("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Kills", victimIri, instancesToRelease);
                            
                            player.objHasValueRestrictionsPlayer.Add(objHasValueKill);
                            // update state of the victim
                            Player playerKilled = players.Find(p => p.Id == murderEvent.Victim.PlayerId);
                            playerKilled.Cls = crewmateDeadClass;
                            break;
                        }

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
                                    if (dist < 4.0) {
                                        //so there is at least one crewmate who have seen the player doing the task
                                        player.State = "trusted";
                                        var dataQuantPlayerState =  CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasStatus", new[] { "trusted" }, "http://www.w3.org/2001/XMLSchema#string", "", instancesToRelease);
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
                            var votedIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{votedEvent.VotedFor.PlayerInfo.PlayerName.Replace(" ", "")}";  
                            var objHasValueVote = CreateObjHasValue("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Voted", votedIri, instancesToRelease);
                            
                            player.objHasValueRestrictionsPlayer.Add(objHasValueVote);
                            //update count of votes got by player in current round
                            Player votedPlayer = players.Find(ot => ot.Id == votedEvent.VotedFor.PlayerId);
                            votedPlayer.IncrementScore();
                            break;
                        }

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

                    // RepairSystem
                     case IPlayerRepairSystemEvent repairSystemEvent:
                        var repairediri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{repairSystemEvent.SystemType}";
                        var objQuantRepairSystem = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Repairs", new[] { repairediri }, instancesToRelease);
                        
                        player.objQuantRestrictionsPlayer.Add(objQuantRepairSystem);
                        // update game state
                        gameState = "none";
                        break;

                    // Movement
                    case CustomPlayerMovementEvent movementEvent:
                        var coordsPlayer = movementEvent.PlayerControl.NetworkTransform.Position;
                        // add movement to track path of the player
                        player.Movements.Add(coordsPlayer);
                        
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
                                var dataQuantPlayerState =  CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasStatus", new[] { "suspected" }, "http://www.w3.org/2001/XMLSchema#string", "", instancesToRelease);
                                p.dataQuantRestrictionsPlayer.Add(dataQuantPlayerState); 
                            }
                            p.resetVoteCount();
                            p.resetMovements();
                        }
                        // if no tie update state of the exiled player
                        if (meetingEndedEvent.IsTie == false)
                        {
                            var dataQuantExiled =  CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/gotExiled", new[] { "true" }, "http://www.w3.org/2001/XMLSchema#boolean", "", instancesToRelease);

                            if (meetingEndedEvent.Exiled is null) {
                                break;
                            } else if (meetingEndedEvent.Exiled.PlayerInfo.PlayerName.Replace(" ", "") == "") {
                                break;
                            }
                            Player playerExiled = players.Find(p => p.Id == meetingEndedEvent.Exiled.PlayerId);
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
                
                // if no movement, player is in a fixed position (maybe AFK?)
                if (dim == 1) {                     
                    var dataQuantPos =  CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/isFixed", new[] { "true" }, "http://www.w3.org/2001/XMLSchema#boolean", "", instancesToRelease);
                    player.dataQuantRestrictionsPlayer.Add(dataQuantPos);
                } else { 
                    // check movement trajectories to see if he's getting near someone. THIS METHOD DOESN'T CONSIDER WALLS
                    foreach (var p in players)
                    {
                        if (player.Cls == crewmateDeadClass || player.Cls == impostorDeadClass) continue; // Salta il confronto sen player morto
                        if (p == player) continue; // Salta il confronto con se stesso
                        if (p.Cls == crewmateDeadClass || p.Cls == impostorDeadClass) continue; // Salta il confronto con player morti

                        int near = 0;
                        var initDistance = calcDistance(player.Movements[0], p.Movements[0]);

                        // Cicla sulle posizioni dei giocatori
                        for (int i = 1; i < dim; i++)
                        {
                            double newDistance = 0.0;
                            
                            // Verifica se il giocatore "p" ha una posizione per questo passo
                            if (i < p.Movements.Count)
                            {
                                // Il giocatore si sta muovendo
                                newDistance = calcDistance(player.Movements[i], p.Movements[i]);
                            }
                            else
                            {
                                // Il giocatore "p" è fermo, quindi calcola la distanza con l'ultima posizione
                                newDistance = calcDistance(player.Movements[i], p.Movements[p.Movements.Count - 1]);
                            }

                            // Se la distanza è inferiore alla distanza iniziale incrementa il contatore "near"
                            if (newDistance < initDistance )
                            {
                                // Aggiungi ulteriori verifiche sulle pareti o sulle stanze (commento non implementato)
                                near++;
                            }
                        }

                        // Se il numero di passi in cui i giocatori sono vicini è maggiore o uguale alla metà di "dim"
                        if (near >= dim / 2)
                        {
                            // Crea l'oggetto solo se necessario
                            var objHasValueGetClose = CreateObjHasValue(
                                "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/getCloseTo",
                                $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{p.Name}",
                                instancesToRelease
                            );

                            player.objHasValueRestrictionsPlayer.Add(objHasValueGetClose);
                        }
                    }
                }

                // check if player is InFOV other players
                foreach (var op in players) {
                    if (op != player) {
                        var dimOp = op.Movements.Count();
                        // euclidean distance
                        var dist = calcDistance(player.Movements[dim-1], op.Movements[dimOp-1]); 
                        // if distance < threshold then IsInFOV for both players 
                        if (dist <= 4.0) {
                            var objHasValueIsInFOV = CreateObjHasValue("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/IsInFOV", $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{op.Name}", instancesToRelease);                        
                            player.objHasValueRestrictionsPlayer.Add(objHasValueIsInFOV);
                        }
                    }
                }
                // check if player is nextTo a vent
                foreach (var v in MapData.Maps[game.Options.Map].Vents) {
                    var coordsVent = v.Value.Position;
                    var dist = calcDistance(player.Movements[dim-1], coordsVent);
                    if (dist <= 1.0) {
                        var vent = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{v.Value.Name}";  
                        var objQuantNextToVent = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/NextTo", new[] { vent },  instancesToRelease);

                        player.objQuantRestrictionsPlayer.Add(objQuantNextToVent);
                        break; //a player can't be next to more than 1 vent
                    }                        
                }
                // check if player is nextTo a task
                var nextTo = false;
                foreach (var t in MapData.Maps[game.Options.Map].Tasks) {
                    // there are task with more positions
                    for (var i=0; i < t.Value.Position.Count(); i++) {
                        var coordsTask = t.Value.Position[i];
                        var dist = calcDistance(player.Movements[dim-1], coordsTask);
                        // set a threshold
                        if (dist < 1.0) {
                            var task = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{t.Value.Type}";  
                            var objQuantNextToTask = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/NextTo", new[] { task },  instancesToRelease);

                            player.objQuantRestrictionsPlayer.Add(objQuantNextToTask);

                            var oldDist = calcDistance(player.Movements[0], coordsTask);
                            if (player.Movements[0] == player.Movements[dim-1]) {
                                if (player.Cls == impostorClass) {
                                    var objQuantFake = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Fake", new[] { task },  instancesToRelease);

                                    player.objQuantRestrictionsPlayer.Add(objQuantFake);
                                } else {
                                    var objQuantDoes = CreateObjValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Does", new[] { task },  instancesToRelease);

                                    player.objQuantRestrictionsPlayer.Add(objQuantDoes);
                                }
                            }
                            nextTo = true;
                            break;
                        } 
                    } if (nextTo == true) break;
                }
                 /*
                //counter of players in FOV
                int count = 0;
                
                foreach (var obj in player.objHasValueRestrictionsPlayer)
                {   
                    var pippo = cowl_obj_has_value.CowlObjHasValueGetProp(obj);
                    var temp = cowl_obj_prop_exp.CowlObjPropExpGetProp(pippo.__Instance);
                    instancesToRelease.Add(pippo.__Instance);
                    if (cowl_obj_prop.CowlObjPropGetIri(temp).ToString() == "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/IsInFOV")
                    {
                        count++;
                    }
                }
                var dataQuantNPlayersFOV =  CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/hasNPlayersInFOV", count.ToString(), "http://www.w3.org/2001/XMLSchema#integer", "", instancesToRelease);
                player.dataQuantRestrictionsPlayer.Add(dataQuantNPlayersFOV);
                */

                var resultCreatePlayer  = CreateIndividual(onto,$"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{player.Name}", new[] { player.Cls }, player.objHasValueRestrictionsPlayer.Distinct().ToArray(), player.objQuantRestrictionsPlayer.Distinct().ToArray(), player.dataQuantRestrictionsPlayer.Distinct().ToArray(), instancesToRelease);
            
            }
            
            var alive = 0;
            foreach (var p in players) {
                if (p.Cls == impostorClass || p.Cls == crewmateClass) {
                    //he's alive
                    alive++;
                }
            }

            var dataQuantRestrictionState = CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/CurrentState", new[] { gameState }, "http://www.w3.org/2001/XMLSchema#string", "", instancesToRelease);
            var dataQuantRestrictionNPlayers = CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasNAlivePlayers", new[] { alive.ToString() }, "http://www.w3.org/2001/XMLSchema#string", "", instancesToRelease);
            
            var resultCreateGame = CreateIndividual(onto,$"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{game.Code.Code}", new[] { gameClass }, null, null, new[] { dataQuantRestrictionState, dataQuantRestrictionNPlayers }, instancesToRelease, false);
            
            //write to file
            string folderPath = $"gameSession{game.Code}";
            if (!Directory.Exists(folderPath)) {
                Directory.CreateDirectory(folderPath); 
            } else if (numRestarts != 0) {
                folderPath = $"gameSession{game.Code}_{numRestarts}";
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
            Dictionary<byte, PlayerStruct> pStates = new Dictionary<byte, PlayerStruct>();
            foreach (var p in players) {
                var dim = p.Movements.Count();
                PlayerStruct ps = new PlayerStruct { State = p.State, Movements = new List<System.Numerics.Vector2> { p.Movements[dim - 1] }, VoteCount = p.VoteCount};

                pStates.Add(p.Id, ps);
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

        public static CowlDataQuant CreateDataValuesRestriction(string propertyIri, IEnumerable<string> fillerLiterals, string dt, string lang, List<nint> instancesToRelease)
        {
            var fillerVector = cowl_vector.UvecCowlObjectPtr();

            // Add the filler literals to the vector
            foreach (var fillerLiteralIri in fillerLiterals)
            {
                var fillerLiteral = cowl_literal.CowlLiteralFromString(UString.UstringCopyBuf(dt), UString.UstringCopyBuf(fillerLiteralIri), UString.UstringCopyBuf(lang));
                instancesToRelease.Add(fillerLiteral.__Instance);
                cowl_vector.UvecPushCowlObjectPtr(fillerVector, fillerLiteral.__Instance);
            }
            
            var operandsRole = cowl_vector.CowlVector(fillerVector);

            // Check the number of operands
            if (cowl_vector.CowlVectorCount(operandsRole) > 1)
            {
                // Create the closure
                var closure = cowl_nary_bool.CowlNaryBool(CowlNAryType.COWL_NT_INTERSECT, operandsRole);
                instancesToRelease.Add(closure.__Instance);

                // Create the task role
                var taskRole = cowl_data_prop.CowlDataPropFromString(UString.UstringCopyBuf(propertyIri));
                instancesToRelease.Add(taskRole.__Instance);

                // Create the object quantifier
                var data_quant = cowl_data_quant.CowlDataQuant(CowlQuantType.COWL_QT_ALL, taskRole.__Instance, operandsRole.__Instance);
                instancesToRelease.Add(data_quant.__Instance);
                instancesToRelease.Add(operandsRole.__Instance);

                return data_quant;
            }
            else
            {
                // Create the task role
                var taskRole = cowl_data_prop.CowlDataPropFromString(UString.UstringCopyBuf(propertyIri));
                instancesToRelease.Add(taskRole.__Instance);

                // Create the object quantifier
                var data_quant = cowl_data_quant.CowlDataQuant(CowlQuantType.COWL_QT_ALL, taskRole.__Instance, operandsRole.__Instance);
                instancesToRelease.Add(data_quant.__Instance);
                instancesToRelease.Add(operandsRole.__Instance);

                return data_quant;
            }
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
        public int VoteCount { get; set; }
        public List<CowlObjHasValue> objHasValueRestrictionsPlayer { get; set; }
        public List<CowlObjQuant> objQuantRestrictionsPlayer { get; set; }
        public List<CowlDataQuant> dataQuantRestrictionsPlayer { get; set; }

        public Player(byte id, string name, CowlClass cls, List<System.Numerics.Vector2> movements, System.Numerics.Vector2 initialPos, string state, int voteCount)
        {
            Id = id; 
            Name = name.Replace(" ", "");
            State = state;
            Cls = cls; //class of the player
            if (movements.Count() == 0) {
                Movements = new List<System.Numerics.Vector2>{initialPos};
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
            Movements.Clear(); // reset after meeting ended
            System.Numerics.Vector2 meetingSpawnCenter = new System.Numerics.Vector2(-0.72f, 0.62f); //meetingSpawnCenter for Skeld Map
            Movements.Add(meetingSpawnCenter);
        }
    }
}
