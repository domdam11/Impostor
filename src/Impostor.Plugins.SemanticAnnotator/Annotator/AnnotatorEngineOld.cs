//using cowl;
//using Impostor.Api.Events;
//using Impostor.Api.Events.Meeting;
//using Impostor.Api.Events.Player;
//using Impostor.Api.Events.Ship;
//using Impostor.Api.Games;
//using Impostor.Api.Innersloth;
//using Impostor.Api.Innersloth.Maps;
//using System.Text.Json;
//using Impostor.Api.Innersloth.GameOptions;
//using CowlSharp.Wrapper;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;
//using System.Net.Http;
//using System.IO;
//using Microsoft.Extensions.Options;
//using Microsoft.Extensions.Configuration;
//using Impostor.Plugins.SemanticAnnotator.Models;
//namespace Impostor.Plugins.SemanticAnnotator.Annotator
//{

//    public class AnnotatorEngineOld
//    {

//        private readonly Thresholds _thresholds;

//        public AnnotatorEngineOld(IOptions<AnnotatorServiceOptions> options)
//        {
//            _thresholds = options.Value.Thresholds;
//        }

//        /// <summary>
//        /// Retrieves the annotation.
//        /// </summary>
//        /// <returns>The annotation.</returns>
//        public (Dictionary<string, Player>, string) Annotate(string gameCode, GameEventCacheManager cacheManager, int numAnnot, int numRestarts, DateTimeOffset timestamp)
//        {
            
//            string nameSpace = "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/";

//            // You must always initialize the library before use.
//            try
//            {
//                // Initialize the Cowl library
//                cowl_config.CowlInit();
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine(e);
//                throw;
//            }


//            var instancesToRelease = new List<nint>();

//            // Instantiate a manager and deserialize an ontology from file.
//            CowlManager manager = cowl_manager.CowlManager();

//            var onto = cowl_manager.CowlManagerGetOntology(manager, cowl_ontology_id.CowlOntologyIdAnonymous());

//            // note: the game passed as argument represent the last status of the game to which events passed as argument are "applied"   
//            // Classes
//            var crewmateClass = CowlWrapper.CreateClassFromIri(CowlWrapper.GetFullIri(nameSpace, "CrewMateAlive"), instancesToRelease);
//            var crewmateDeadClass = CowlWrapper.CreateClassFromIri(CowlWrapper.GetFullIri(nameSpace, "CrewMateDead"), instancesToRelease);
//            var impostorClass = CowlWrapper.CreateClassFromIri(CowlWrapper.GetFullIri(nameSpace, "ImpostorAlive"), instancesToRelease);
//            var impostorDeadClass = CowlWrapper.CreateClassFromIri(CowlWrapper.GetFullIri(nameSpace, "ImpostorExiled"), instancesToRelease);
//            var gameClass = CowlWrapper.CreateClassFromIri(CowlWrapper.GetFullIri(nameSpace, "Game"), instancesToRelease);

//            // Retrive game status
//            var gameState = cacheManager.GetGameStateAsync(gameCode).Result;
//            int matchCounter = gameState.MatchCounter;
//            if (gameState == null)
//            {
//                throw new Exception($"Game state for game code {gameCode} not found in cache.");
//            }

//            // Create a list to hold the players
//            List<Player> players = new List<Player>();

//            foreach (var pState in gameState.Players)
//            {
//                var cls = crewmateClass;
//                switch (pState.Role.ToLower())
//                {
//                    case "crewmateghost":
//                        cls = crewmateDeadClass;
//                        break;
//                    case "impostorghost":
//                        cls = impostorDeadClass;
//                        break;
//                    case "impostor":
//                        cls = impostorClass;
//                        break;
//                    default:
//                        break;
//                }

//                var spawnMov = pState.Movements.FirstOrDefault();
               
//                var player = new Player(
//                    id: pState.id,
//                    name: pState.Name,
//                    cls: cls,
//                    sesCls: pState.Role,
//                    movements: pState.Movements,
//                    initialMov: spawnMov,
//                    state: pState.State,
//                    voteCount: pState.VoteCount
//                );
//                players.Add(player);
//                Console.WriteLine($"[AnnotatorEngine] Creato oggetto Player: {player.Name}, Ruolo: {player.SessionCls}");

//            }

//            // scan player events
//            foreach (var dictEvent in gameState.EventHistory)
//            {
//                if (!dictEvent.ContainsKey("EventType"))
//                    continue;

//                string eventType = dictEvent["EventType"].ToString();

//                switch (eventType)
//                {
//                    case "PlayerEnterVentEvent":
//                        HandlePlayerEnterVent(dictEvent, players, nameSpace, instancesToRelease);
//                        break;

//                    case "PlayerVentEvent":
//                        HandlePlayerVent(dictEvent, players, nameSpace, instancesToRelease);
//                        break;

//                    case "PlayerExitVentEvent":
//                        HandlePlayerExitVent(dictEvent, players, nameSpace, instancesToRelease);
//                        break;

//                    case "PlayerMurderEvent":
//                        HandlePlayerMurder(dictEvent, players, crewmateDeadClass, impostorDeadClass, nameSpace, instancesToRelease);
//                        break;

//                    case "PlayerCompletedTask":
//                        HandlePlayerCompletedTask(dictEvent, players, nameSpace, instancesToRelease);
//                        break;

//                    case "PlayerVotedEvent":
//                        HandlePlayerVotedEvent(dictEvent, players, nameSpace, instancesToRelease);
//                        break;

//                    case "PlayerStartMeetingEvent":
//                        HandlePlayerStartMeetingEvent(dictEvent, players, nameSpace, instancesToRelease);
//                        break;

//                    case "PlayerRepairSystem":
//                        HandlePlayerRepairSystem(dictEvent, players, nameSpace, instancesToRelease);
//                        break;

//                    case "PlayerMovement":
//                        HandlePlayerMovement(dictEvent, players, nameSpace, instancesToRelease);
//                        break;

//                    case "ShipSabotage":
//                        HandleShipSabotage(dictEvent, players, nameSpace, instancesToRelease);
//                        break;

//                    // Aggiungi altri case per altri tipi di eventi

//                    default:
//                        break;


//                }
//                //Console.WriteLine(ev);
//            }

//            // Creazione delle restrizioni di dati per le opzioni del gioco
//            var dataQuantRestrictionMap = CowlWrapper.CreateDataValuesRestriction(
//                CowlWrapper.GetFullIri(nameSpace, "UseMap"),
//                new[] { gameState.Map },
//                "http://www.w3.org/2001/XMLSchema#string",
//                "",
//                instancesToRelease
//            );

//            var dataQuantRestrictionState = CowlWrapper.CreateDataValuesRestriction(
//                CowlWrapper.GetFullIri(nameSpace, "CurrentState"),
//                new[] { gameState.GameStateName },
//                "http://www.w3.org/2001/XMLSchema#string",
//                "",
//                instancesToRelease
//            );

//            var dataQuantRestrictionNPlayers = CowlWrapper.CreateDataValuesRestriction(
//                CowlWrapper.GetFullIri(nameSpace, "HasNAlivePlayers"),
//                new[] { gameState.AlivePlayers.ToString() },
//                "http://www.w3.org/2001/XMLSchema#integer",
//                "",
//                instancesToRelease
//            );

//            var dataQuantRestrictionAnonymVotes = CowlWrapper.CreateDataValuesRestriction(
//                "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/AnonymousVotesEnabled",
//                new[] { gameState.AnonymousVotesEnabled.ToString().ToLower() },
//                "http://www.w3.org/2001/XMLSchema#boolean",
//                "",
//                instancesToRelease
//            );

//            var dataQuantRestrictionVisualTasks = CowlWrapper.CreateDataValuesRestriction(
//                "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/VisualTasksEnabled",
//                new[] { gameState.VisualTasksEnabled.ToString().ToLower() },
//                "http://www.w3.org/2001/XMLSchema#boolean",
//                "",
//                instancesToRelease
//            );

//            var dataQuantRestrictionConfirmEjects = CowlWrapper.CreateDataValuesRestriction(
//                "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/ConfirmEjects",
//                new[] { gameState.ConfirmEjects.ToString().ToLower() },
//                "http://www.w3.org/2001/XMLSchema#boolean",
//                "",
//                instancesToRelease
//            );

//            // Creazione dell’individuo Game
//            var resultCreateGame = CowlWrapper.CreateIndividual(
//                onto,
//                CowlWrapper.GetFullIri(nameSpace, gameCode),
//                new[] { gameClass },
//                null,
//                new[]
//                {
//                    dataQuantRestrictionMap,
//                    dataQuantRestrictionState,
//                    dataQuantRestrictionNPlayers,
//                    dataQuantRestrictionAnonymVotes,
//                    dataQuantRestrictionVisualTasks,
//                    dataQuantRestrictionConfirmEjects
//                },
//                instancesToRelease,
//                false
//            );

//            /*foreach (var player in players)
//            {
//                // IRI dell’individuo
//                string playerIndIri = CowlWrapper.GetFullIri(nameSpace, player.Name);
//                Console.WriteLine($"[AnnotatorEngine] Creazione individuo per Player: {player.Name}, classe: {player.Cls}, #objQuant: {player.objQuantRestrictionsPlayer.Count}, #hasValue: {player.objHasValueRestrictionsPlayer.Count}, #dataQuant: {player.dataQuantRestrictionsPlayer.Count}");

//                // Creo l’individuo del Player (con la classe e le objQuant + dataQuant)
//                var creationRet = CowlWrapper.CreateIndividual(
//                    onto,
//                    playerIndIri,
//                    new[] { player.Cls },
//                    player.objQuantRestrictionsPlayer,        
//                    player.dataQuantRestrictionsPlayer,       
//                    instancesToRelease,
//                    true
//                );
//                Console.WriteLine($"[AnnotatorEngine] -> CreateIndividual per {player.Name} restituisce: {creationRet}");
//                foreach (var hv in player.objHasValueRestrictionsPlayer)
//                {
//                    // Creiamo la class-assertion axiom
//                    var hvAxiom = cowl_cls_assert_axiom.CowlClsAssertAxiom(hv.__Instance,
//                        cowl_named_ind.CowlNamedIndFromString(UString.UstringCopyBuf(playerIndIri)).__Instance,
//                        null
//                    );

//                    instancesToRelease.Add(hvAxiom.__Instance);
//                    var retAxiom = cowl_ontology.CowlOntologyAddAxiom(onto, hvAxiom.__Instance);
//                    Console.WriteLine($"[AnnotatorEngine] ----> Aggiunta ObjHasValue Restriction per {player.Name} con esito: {retAxiom}");
//                }
//            }*/
//            // when all events have been analyzed, for each player create the individual with all collected properties
//            foreach (var player in players)
//            {

//                var dim = player.Movements.Count();
//                var nCrewmatesFOV = 0;
//                var nImpostorsFOV = 0;

//                // check if player fixed or moving towards someone

//                // if no movement, player is in a fixed position (maybe AFK?)
//                if(dim == 0)
//                {
//                    continue;
//                }
//                if (dim == 1)
//                {
//                    var dataQuantPos = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/IsFixed", new[] { "true" }, "http://www.w3.org/2001/XMLSchema#boolean", "", instancesToRelease);
//                    player.dataQuantRestrictionsPlayer.Add(dataQuantPos);
//                }
//                else
//                {
//                    // check movement trajectories to see if he's getting near someone. This method doesn't consider walls
//                    foreach (var p in players)
//                    {
//                        if(p.Movements.Count() == 0)
//                        {
//                            continue;
//                        }
//                        if (player.Cls == crewmateDeadClass || player.Cls == impostorDeadClass) continue;
//                        if (p == player) continue;
//                        if (p.Cls == crewmateDeadClass || p.Cls == impostorDeadClass) continue;

//                        var near = 0;
//                        var initDistance = CalcDistance(player.Movements[0].Position, p.Movements[0].Position);

//                        for (var i = 1; i < dim; i++)
//                        {
//                            var newDistance = 0.0;

//                            if (i < p.Movements.Count)
//                            {
//                                // player p is moving
//                                newDistance = CalcDistance(player.Movements[i].Position, p.Movements[i].Position);
//                            }
//                            else
//                            {
//                                // player p is fixed
//                                newDistance = CalcDistance(player.Movements[i].Position, p.Movements[p.Movements.Count - 1].Position);
//                            }

//                            if (newDistance < initDistance)
//                            {
//                                near++;
//                            }
//                        }

//                        if (near >= dim / 2)
//                        {
//                            var getClosePlayer = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{p.SessionCls}";
//                            var objQuantGetClose = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/GetCloseTo", new[] { getClosePlayer }, instancesToRelease);

//                            player.objQuantRestrictionsPlayer.Add(objQuantGetClose);
//                        }
//                    }
//                }

//                // check if player is InFOV other players
//                foreach (var op in players)
//                {
//                    //if dead player, skip
//                    if (op != player && op.Cls != crewmateDeadClass && op.Cls != impostorDeadClass)
//                    {
//                        var dimOp = op.Movements.Count();
//                        if(dimOp == 0)
//                        {
//                            continue;
//                        }
//                        // euclidean distance
//                        var dist = CalcDistance(player.Movements[dim - 1].Position, op.Movements[dimOp - 1].Position);
//                        // if distance < threshold then IsInFOV for both players 
//                        if (dist <= _thresholds.FOV)
//                        {
//                            var InFovIri = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{op.SessionCls}";
//                            var objQuantIsInFOV = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/IsInFOV", new[] { InFovIri }, instancesToRelease);
//                            player.objQuantRestrictionsPlayer.Add(objQuantIsInFOV);
//                            if (op.Cls == crewmateClass)
//                            {
//                                nCrewmatesFOV++;
//                            }
//                            else
//                            {
//                                nImpostorsFOV++;
//                            }
//                        }
//                    }
//                }

//                //counter of players in FOV
//                var dataQuantNCrewmatesFOV = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasNCrewmatesInFOV", new[] { nCrewmatesFOV.ToString() }, "http://www.w3.org/2001/XMLSchema#integer", "", instancesToRelease);
//                player.dataQuantRestrictionsPlayer.Add(dataQuantNCrewmatesFOV);
//                var dataQuantNImpostorsFOV = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasNImpostorsInFOV", new[] { nImpostorsFOV.ToString() }, "http://www.w3.org/2001/XMLSchema#integer", "", instancesToRelease);
//                player.dataQuantRestrictionsPlayer.Add(dataQuantNImpostorsFOV);
//                Enum.TryParse(gameState.Map, out MapTypes mapType);
//                // check if player is nextTo a vent
//                foreach (var v in MapData.Maps[mapType].Vents)
//                {
//                    var coordsVent = v.Value.Position;
//                    var dist = CalcDistance(player.Movements[dim - 1].Position, coordsVent);
//                    if (dist <= _thresholds.NextToVent)
//                    {
//                        var vent = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{v.Value.Name}";
//                        var objQuantNextToVent = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/NextTo", new[] { vent }, instancesToRelease);

//                        player.objQuantRestrictionsPlayer.Add(objQuantNextToVent);
//                        break; //a player can't be next to more than 1 vent
//                    }
//                }

//                // check if player is nextTo a task
//                foreach (var t in MapData.Maps[mapType].Tasks)
//                {
//                    var nextTo = false;
//                    for (var i = 0; i < t.Value.Position.Count(); i++)
//                    {
//                        var coordsTask = t.Value.Position[i];
//                        var dist = CalcDistance(player.Movements[dim - 1].Position, coordsTask);
//                        if (dist <= _thresholds.NextToVent)
//                        {
//                            var task = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{t.Value.Type}";
//                            var objQuantNextToTask = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/NextTo", new[] { task }, instancesToRelease);

//                            player.objQuantRestrictionsPlayer.Add(objQuantNextToTask);
//                            nextTo = true; //a player can't be next to more than 1 task
//                            break;
//                        }
//                    }
//                    if (nextTo) break;
//                }

//                // loop through player positions
//                for (var j = 0; j < dim; j++)
//                {
//                    var does = false;
//                    var playerMovement = player.Movements[j];
//                    // check if player is nextTo a task and does a task
//                    foreach (var t in MapData.Maps[mapType].Tasks)
//                    {
//                        var timeThreshold = _thresholds.TimeShort;
//                        switch (t.Value.Type)
//                        {
//                            case TaskTypes.InspectSample:
//                                timeThreshold = _thresholds.TimeInspectSample;
//                                break;
//                            case TaskTypes.UnlockManifolds:
//                                timeThreshold = _thresholds.TimeUnlockManifolds;
//                                break;
//                            case TaskTypes.CalibrateDistributor:
//                                timeThreshold = _thresholds.TimeCalibratedDistributor;
//                                break;
//                            case TaskTypes.ClearAsteroids:
//                                timeThreshold = _thresholds.TimeClearAsteroids;
//                                break;
//                            case TaskTypes.StartReactor:
//                                timeThreshold = _thresholds.TimeStartReactor;
//                                break;
//                            default:
//                                break;
//                        }

//                        var task = $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{t.Value.Type}";

//                        // there are tasks with more positions
//                        for (var i = 0; i < t.Value.Position.Count(); i++)
//                        {
//                            var coordsTask = t.Value.Position[i];

//                            // distance between player pos and task pos
//                            var dist = CalcDistance(playerMovement.Position, coordsTask);

//                            // if dist < threshold, check time of staying fixed
//                            if (dist <= _thresholds.NextToTask)
//                            {

//                                // check timespan between movements
//                                if (j < dim - 1)
//                                {
//                                    var nextMovement = player.Movements[j + 1];
//                                    var timeDiff = (nextMovement.Timestamp - playerMovement.Timestamp).TotalSeconds;

//                                    // if timespan compatible with time to perform task
//                                    if (timeDiff >= timeThreshold)
//                                    {
//                                        if (player.Cls == impostorClass)
//                                        {
//                                            var objQuantFake = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Fake", new[] { task }, instancesToRelease);

//                                            player.objQuantRestrictionsPlayer.Add(objQuantFake);
//                                        }
//                                        else
//                                        {
//                                            var objQuantDoes = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Does", new[] { task }, instancesToRelease);

//                                            player.objQuantRestrictionsPlayer.Add(objQuantDoes);
//                                        }
//                                        does = true;
//                                        break;
//                                    }
//                                }
//                                else
//                                {
//                                    // if last position, check time from last movement and now
//                                    var timeDiff = (timestamp - playerMovement.Timestamp).TotalSeconds;
//                                    if (timeDiff >= timeThreshold)
//                                    {
//                                        if (player.Cls == impostorClass)
//                                        {
//                                            var objQuantFake = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Fake", new[] { task }, instancesToRelease);

//                                            player.objQuantRestrictionsPlayer.Add(objQuantFake);
//                                        }
//                                        else
//                                        {
//                                            var objQuantDoes = CowlWrapper.CreateAllValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/Does", new[] { task }, instancesToRelease);

//                                            player.objQuantRestrictionsPlayer.Add(objQuantDoes);
//                                        }
//                                        does = true;
//                                        break;
//                                    }
//                                }
//                            }
//                            if (does) break;
//                        }
//                        if (does) break;
//                    }
//                }

//                var dataQuantHasCoordinates = CowlWrapper.CreateDataValuesRestriction("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/HasCoordinates", new[] { player.Movements[dim - 1].Position.ToString() }, "http://www.w3.org/2001/XMLSchema#string", "", instancesToRelease);
//                player.dataQuantRestrictionsPlayer.Add(dataQuantHasCoordinates);
//                var sessionClass = CowlWrapper.CreateClassFromIri($"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{player.SessionCls}", instancesToRelease);

//                var resultCreatePlayer = CowlWrapper.CreateIndividual(onto, $"http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/{player.Name}", new[] { player.Cls, sessionClass }, player.objQuantRestrictionsPlayer.ToArray(), player.dataQuantRestrictionsPlayer.ToArray(), instancesToRelease);

//            }



//            //write to file
//            string folderPath = $"gameSession{gameCode}_match{matchCounter}";
//            if (!Directory.Exists(folderPath))
//            {
//                Directory.CreateDirectory(folderPath);
//            }
//            else if (numRestarts != 0)
//            {
//                folderPath = $"gameSession{gameCode}_match{matchCounter}_{numRestarts}";
//                Directory.CreateDirectory(folderPath);
//            }
//            string absoluteHeaderDirectory = Path.Combine(folderPath, $"amongus_m{matchCounter}_{numAnnot}.owl");
//            var string3 = UString.UstringCopyBuf(absoluteHeaderDirectory);
//            cowl_sym_table.CowlSymTableRegisterPrefixRaw(cowl_ontology.CowlOntologyGetSymTable(onto), UString.UstringCopyBuf(""), UString.UstringCopyBuf("http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/"), false);
//            cowl_manager.CowlManagerWritePath(manager, onto, string3);


//            // Write the ontology to a string
//            UVec_char chars = uvec_builtin.UvecChar();
//            cowl_manager.CowlManagerWriteStrbuf(manager, onto, chars);
//            var sbyteArray = new sbyte[uvec_builtin.UvecCountChar(chars)];
//            uvec_builtin.UvecCopyToArrayChar(chars, sbyteArray);
//            byte[] byteArray = Array.ConvertAll(sbyteArray, b => (byte)b);
//            string result = System.Text.Encoding.UTF8.GetString(byteArray);

//            SaveLastOwl(result);
//            //Console.WriteLine(result);
//            //var result2 = Task.Run(async () => await CallArgumentationAsync(result));
//            //result2.Wait();
//            //Console.WriteLine(result2.Result);

//            foreach (var instance in instancesToRelease)
//            {
//                cowl_object.CowlRelease(instance);
//            }

//            //ConsoleDriver.Run(new CowlWrapper());
//            cowl_object.CowlRelease(onto.__Instance);
//            cowl_object.CowlRelease(manager.__Instance);

            
//            string newStateName = gameState.GameStateName;

//            return (players.ToDictionary(a => { return a.Id; }, a => { return a; }), newStateName);
//        }

//        private void HandlePlayerEnterVent(Dictionary<string, object> dictEvent, List<Player> players, string nameSpace, List<nint> instancesToRelease)
//        {
//            var ventName = dictEvent.ContainsKey("VentName") ? dictEvent["VentName"].ToString() : "UnknownVent";
//            var playerName = dictEvent.ContainsKey("PlayerName") ? dictEvent["PlayerName"].ToString() : "UnknownPlayer";

//            // Trova il giocatore nella lista
//            var player = players.FirstOrDefault(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
//            if (player != null && !string.IsNullOrEmpty(ventName))
//            {
//                var enterVentIri = $"{nameSpace}{ventName}";
//                var objQuantEnterVent = CowlWrapper.CreateAllValuesRestriction($"{nameSpace}EnterVent", new[] { enterVentIri }, instancesToRelease);
//                player.objQuantRestrictionsPlayer.Add(objQuantEnterVent);
//            }
//        }

//        private void HandlePlayerVent(Dictionary<string, object> dictEvent, List<Player> players, string nameSpace, List<nint> instancesToRelease)
//        {
//            var ventName = dictEvent.ContainsKey("VentName") ? dictEvent["VentName"].ToString() : "UnknownVent";
//            var playerName = dictEvent.ContainsKey("PlayerName") ? dictEvent["PlayerName"].ToString() : "UnknownPlayer";

//            // Trova il giocatore nella lista
//            var player = players.FirstOrDefault(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
//            if (player != null && !string.IsNullOrEmpty(ventName))
//            {
//                var ventIri = $"{nameSpace}{ventName}";
//                var objQuantVent = CowlWrapper.CreateAllValuesRestriction($"{nameSpace}VentTo", new[] { ventIri }, instancesToRelease);
//                var ventMov = new CustomMovement(new System.Numerics.Vector2(0, 0), DateTimeOffset.UtcNow);
//                player.Movements.Add(ventMov);
//                player.objQuantRestrictionsPlayer.Add(objQuantVent);
//            }
//        }

//        private void HandlePlayerExitVent(Dictionary<string, object> dictEvent, List<Player> players, string nameSpace, List<nint> instancesToRelease)
//        {
//            var ventName = dictEvent.ContainsKey("VentName") ? dictEvent["VentName"].ToString() : "UnknownVent";
//            var playerName = dictEvent.ContainsKey("PlayerName") ? dictEvent["PlayerName"].ToString() : "UnknownPlayer";

//            var player = players.FirstOrDefault(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
//            if (player != null && !string.IsNullOrEmpty(ventName))
//            {
//                var exitVentIri = $"{nameSpace}{ventName}";
//                var objQuantExitVent = CowlWrapper.CreateAllValuesRestriction($"{nameSpace}ExitVent", new[] { exitVentIri }, instancesToRelease);
//                player.objQuantRestrictionsPlayer.Add(objQuantExitVent);
//                if (player.Movements.Last() != null)
//                {
//                    // Aggiungi un movimento di uscita
//                    var exitMov = new CustomMovement(player.Movements.Last().Position, DateTimeOffset.UtcNow);
//                    player.Movements.Add(exitMov);
//                }
//            }
//        }

//        private void HandlePlayerMurder(Dictionary<string, object> dictEvent, List<Player> players, CowlClass crewmateDeadClass, CowlClass impostorDeadClass, string nameSpace, List<nint> instancesToRelease)
//        {
//            var killerName = dictEvent.ContainsKey("KillerName") ? dictEvent["KillerName"].ToString() : "UnknownKiller";
//            var victimName = dictEvent.ContainsKey("VictimName") ? dictEvent["VictimName"].ToString() : "UnknownVictim";

//            var killer = players.FirstOrDefault(p => p.Name.Equals(killerName, StringComparison.OrdinalIgnoreCase));
//            var victim = players.FirstOrDefault(p => p.Name.Equals(victimName, StringComparison.OrdinalIgnoreCase));

//            if (killer != null && victim != null)
//            {
//                // Evita di annotare se killer è già impostorDeadClass
//                if (killer.Cls != impostorDeadClass)
//                {
//                    var victimIri = $"{nameSpace}{victim.SessionCls}";
//                    var objQuantKill = CowlWrapper.CreateAllValuesRestriction($"{nameSpace}Kills", new[] { victimIri }, instancesToRelease);
//                    killer.objQuantRestrictionsPlayer.Add(objQuantKill);

//                    // Vittima diventa morta
//                    victim.Cls = crewmateDeadClass;
//                }
//            }
//        }

//        private void HandlePlayerCompletedTask(Dictionary<string, object> dictEvent, List<Player> players, string nameSpace, List<nint> instancesToRelease)
//        {
//            var playerName = dictEvent.ContainsKey("PlayerName") ? dictEvent["PlayerName"].ToString() : "UnknownPlayer";
//            var taskType = dictEvent.ContainsKey("TaskType") ? dictEvent["TaskType"].ToString() : "UnknownTask";
//            var isVisual = dictEvent.ContainsKey("IsVisual") ? Convert.ToBoolean(dictEvent["IsVisual"]) : false;

//            var player = players.FirstOrDefault(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
//            if (player != null && !string.IsNullOrEmpty(taskType))
//            {
//                var taskIri = $"{nameSpace}{taskType}";
//                var objQuantCompletedTask = CowlWrapper.CreateAllValuesRestriction($"{nameSpace}Completed", new[] { taskIri }, instancesToRelease);
//                player.objQuantRestrictionsPlayer.Add(objQuantCompletedTask);

//                // Se la task è visual, setta player.State = "trusted" se c’è un Crewmate in FOV
//                if (isVisual)
//                {
//                    foreach (var otherPlayer in players)
//                    {
//                        if (otherPlayer.Name.Equals(player.Name, StringComparison.OrdinalIgnoreCase) || otherPlayer.State.Equals("dead", StringComparison.OrdinalIgnoreCase))
//                            continue;
//                        if (player.Movements.Last() != null)
//                        {
//                            double distance = CalcDistance(player.Movements.Last().Position, otherPlayer.Movements.Last().Position);
//                            if (distance <= _thresholds.FOV)
//                            {
//                                player.State = "trusted";
//                                var dataQuantPlayerState = CowlWrapper.CreateDataValuesRestriction(
//                                    $"{nameSpace}HasStatus",
//                                    new[] { "trusted" },
//                                    "http://www.w3.org/2001/XMLSchema#string",
//                                    "",
//                                    instancesToRelease
//                                );
//                                player.dataQuantRestrictionsPlayer.Add(dataQuantPlayerState);
//                                break; // Almeno un Crewmate ha visto la task
//                            }
//                        }
//                    }
//                }
//            }
//        }

//        private void HandlePlayerVotedEvent(Dictionary<string, object> dictEvent, List<Player> players, string nameSpace, List<nint> instancesToRelease)
//        {
//            var voterName = dictEvent.ContainsKey("PlayerVoter") ? dictEvent["PlayerVoter"].ToString() : "UnknownVoter";
//            var voteType = dictEvent.ContainsKey("VoteType") ? dictEvent["VoteType"].ToString() : "UnknownVoteType";
//            var votedForName = dictEvent.ContainsKey("PlayerVoted") ? dictEvent["PlayerVoted"].ToString() : "UnknownVotedFor";

//            var voter = players.FirstOrDefault(p => p.Name.Equals(voterName, StringComparison.OrdinalIgnoreCase));
//            var votedFor = players.FirstOrDefault(p => p.Name.Equals(votedForName, StringComparison.OrdinalIgnoreCase));

//            if (voter != null && votedFor != null)
//            {
//                var votedIri = $"{nameSpace}{votedFor.SessionCls}";
//                var objQuantVote = CowlWrapper.CreateAllValuesRestriction($"{nameSpace}Votes", new[] { votedIri }, instancesToRelease);

//                voter.objQuantRestrictionsPlayer.Add(objQuantVote);

//                // Aggiorna il conteggio dei voti
//                votedFor.IncrementScore();
//            }
//        }

//        private void HandlePlayerStartMeetingEvent(Dictionary<string, object> dictEvent, List<Player> players, string nameSpace, List<nint> instancesToRelease)
//        {
//            var reason = dictEvent.ContainsKey("Reason") ? dictEvent["Reason"].ToString() : "EmergencyCall";

//            if (reason == "EmergencyCall")
//            {
//                var emergencyCallIri = $"{nameSpace}EmergencyCall";
//                var objQuantEmergencyMeeting = CowlWrapper.CreateAllValuesRestriction($"{nameSpace}Calls", new[] { emergencyCallIri }, instancesToRelease);
//                // Assumiamo che ci sia un giocatore specifico che ha chiamato la riunione
//                var playerName = dictEvent.ContainsKey("PlayerName") ? dictEvent["PlayerName"].ToString() : "UnknownPlayer";
//                var player = players.FirstOrDefault(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
//                if (player != null)
//                {
//                    player.objQuantRestrictionsPlayer.Add(objQuantEmergencyMeeting);
//                }
//            }
//            else
//            {
//                var reportedPlayerName = dictEvent.ContainsKey("ReportedPlayer") ? dictEvent["ReportedPlayer"].ToString() : "UnknownPlayer";
//                var reportedIri = $"{nameSpace}{reportedPlayerName}";
//                var objQuantReport = CowlWrapper.CreateAllValuesRestriction($"{nameSpace}Reports", new[] { reportedIri }, instancesToRelease);

//                var player = players.FirstOrDefault(p => p.Name.Equals(reportedPlayerName, StringComparison.OrdinalIgnoreCase));
//                if (player != null)
//                {
//                    player.objQuantRestrictionsPlayer.Add(objQuantReport);

//                    var emergencyCallIri = $"{nameSpace}EmergencyCall";
//                    var objQuantEmergencyMeeting = CowlWrapper.CreateAllValuesRestriction($"{nameSpace}Calls", new[] { emergencyCallIri }, instancesToRelease);
//                    player.objQuantRestrictionsPlayer.Add(objQuantEmergencyMeeting);
//                }
//            }
//        }

//        private void HandlePlayerRepairSystem(Dictionary<string, object> dictEvent, List<Player> players, string nameSpace, List<nint> instancesToRelease)
//        {
//            string playerName = dictEvent.ContainsKey("PlayerName") ? dictEvent["PlayerName"].ToString() : "UnknownPlayer";
//            string systemType = dictEvent.ContainsKey("SystemType") ? dictEvent["SystemType"].ToString() : "UnknownSystem";

//            // Mappatura del tipo di sistema alla terminologia dell'ontologia
//            string sabotageTask = systemType switch
//            {
//                "Electrical" => "FixLight",
//                "Comms" => "CommsSabotaged",
//                "Reactor" => "ReactorMeltdown",
//                "LifeSupp" => "O2OxygenDepleted",
//                "Admin" => "AdminOxygenDepleted",
//                _ => "AnotherTypeOfSabotageTask",
//            };

//            string repairIri = $"{nameSpace}{sabotageTask}";
//            var objQuantRepairSystem = CowlWrapper.CreateAllValuesRestriction($"{nameSpace}Repairs", new[] { repairIri }, instancesToRelease);

//            // Trova il giocatore nella lista
//            var player = players.FirstOrDefault(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
//            if (player != null)
//            {
//                player.objQuantRestrictionsPlayer.Add(objQuantRepairSystem);
//                // Potrebbe essere necessario aggiornare lo stato del gioco se applicabile
//                // Ad esempio: gameState = "none";
//            }
//        }

//        private void HandlePlayerMovement(Dictionary<string, object> dictEvent, List<Player> players, string nameSpace, List<nint> instancesToRelease)
//        {
//            string playerName = dictEvent.ContainsKey("PlayerName") ? dictEvent["PlayerName"].ToString() : "UnknownPlayer";
//            float posX = dictEvent.ContainsKey("CurrentPositionX") ? Convert.ToSingle(dictEvent["CurrentPositionX"]) : 0f;
//            float posY = dictEvent.ContainsKey("CurrentPositionY") ? Convert.ToSingle(dictEvent["CurrentPositionY"]) : 0f;
//            DateTimeOffset timestamp = dictEvent.ContainsKey("Timestamp") ? DateTimeOffset.Parse(dictEvent["Timestamp"].ToString()) : DateTimeOffset.UtcNow;

//            var player = players.FirstOrDefault(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
//            if (player != null)
//            {
//                var mov = new CustomMovement(new System.Numerics.Vector2(posX, posY), timestamp);
//                player.Movements.Add(mov);
//                // Puoi aggiungere ulteriori restrizioni o logica se necessario
//            }
//        }

//        private void HandleShipSabotage(Dictionary<string, object> dictEvent, List<Player> players, string nameSpace, List<nint> instancesToRelease)
//        {
//            string playerName = dictEvent.ContainsKey("PlayerName") ? dictEvent["PlayerName"].ToString() : "UnknownPlayer";
//            string systemType = dictEvent.ContainsKey("SystemType") ? dictEvent["SystemType"].ToString() : "UnknownSystem";

//            // Mappatura del tipo di sabotaggio alla terminologia dell'ontologia
//            string sabotage = systemType switch
//            {
//                "Electrical" => "SabotageFixLights",
//                "MedBay" => "SabotageMedScan",
//                "Doors" => "SabotageDoorSabotage",
//                "Comms" => "CommsSabotaged",
//                "Security" => "SabotageSecurity",
//                "Reactor" => "SabotageReactorMeltdown",
//                "LifeSupp" => "SabotageOxygenDepleted",
//                "Ventilation" => "SabotageVentilation",
//                _ => "AnotherTypeOfSabotage",
//            };

//            string sabotageIri = $"{nameSpace}{sabotage}";
//            var objQuantSabotage = CowlWrapper.CreateAllValuesRestriction($"{nameSpace}Sabotages", new[] { sabotageIri }, instancesToRelease);

//            var player = players.FirstOrDefault(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
//            if (player != null)
//            {
//                player.objQuantRestrictionsPlayer.Add(objQuantSabotage);
//                // Aggiorna lo stato del gioco se necessario, ad esempio:
//                // gameState = "sabotage";
//            }
//        }



//        /*public static async Task<string> CallArgumentationAsync(string annotations)
//        {
//            string url_owl = "http://127.0.0.1:18080/update";
//            using (HttpClient client = new HttpClient())
//            {
//                try
//                {
//                    var StringContent = new StringContent(annotations, System.Text.Encoding.UTF8);
//                    HttpResponseMessage response_1 = await client.PostAsync(url_owl, StringContent);
//                    string result = await response_1.Content.ReadAsStringAsync();
//                    return result;
//                }
//                catch (HttpRequestException e)
//                {
//                    Console.WriteLine($"Errore nella richiesta: {e.Message}");
//                    return "Errore nella richiesta";
//                }
//            }
//        }*/


//        private static double CalcDistance(System.Numerics.Vector2 posPlayer1, System.Numerics.Vector2 posPlayer2)
//        {
//            return Math.Sqrt(Math.Pow(posPlayer1.X - posPlayer2.X, 2) + Math.Pow(posPlayer1.Y - posPlayer2.Y, 2));
//        }

//        /*public static Thresholds LoadThresholds(string filePath)
//        {
//            if (File.Exists(filePath))
//            {
//                var json = File.ReadAllText(filePath);
//                return JsonSerializer.Deserialize<Thresholds>(json);
//            }
//            else
//            {
//                Console.WriteLine("JSON file not found");
//                return new Thresholds() { FOV = 3.0, NextToTask = 1.0, NextToVent = 1.0, TimeShort = 2.0, TimeInspectSample = 3.0, TimeUnlockManifolds = 5.0, TimeCalibratedDistributor = 9.0, TimeClearAsteroids = 11.0, TimeStartReactor = 28.0 };  // default threhsolds
//            }
//        }*/

//        private string _lastOwl;

//        public string GetLastOwl() => _lastOwl;

//        private void SaveLastOwl(string owlContent)
//        {
//            _lastOwl = owlContent;
//        }


//    }




    
//}
