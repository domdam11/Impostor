using Data;
using Geometry;
using Semantic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.IO;
using System.Reflection;
using cowl;
using Impostor.Plugins.SemanticAnnotator.Utils;

namespace Location
{
    public class LocationFinding
    {
        private readonly MapData mapData;
        private readonly ConfidenceCalculator confidenceCalculator;
        private readonly string outputFilePath;
        private readonly SemanticLabeler semanticLabelerInternal;
        private readonly Dictionary<string, string> confidenceLevelOwlDescriptors;
        private readonly Dictionary<Data.Room, Vector2> roomCentroids;
        private readonly Dictionary<Data.Room, Vector2> hallwayCentroids;

        private const float DOOR_PROXIMITY_THRESHOLD = 3.0f;
        private static readonly StringComparison SC = StringComparison.OrdinalIgnoreCase;
        private readonly List<DoorInfo> _allDoors;


        public LocationFinding(string mapJsonPath)
        {
            var mapDataLoader = new Data.MapDataLoader();
            this.mapData = mapDataLoader.LoadMapData(mapJsonPath)
                           ?? throw new ArgumentNullException(nameof(mapData), "LoadMapData ha restituito null. Controllare il percorso del file JSON e la sua validità.");

            this.roomCentroids = new Dictionary<Data.Room, Vector2>();
            this.hallwayCentroids = new Dictionary<Data.Room, Vector2>();
            
            _allDoors = mapData.Doors;

            foreach (var room in this.mapData.Rooms)
            {
                var mainCollider = room.Colliders.FirstOrDefault();
                if (room.Name != null && mainCollider != null && mainCollider.Points.Any())
                {
                    Vector2 centroid = GeometryUtils.CalculateCentroid(mainCollider.Points);
                    if (room.Name.Contains("Hallway", SC))
                    {
                        this.hallwayCentroids.Add(room, centroid);
                    }
                    else
                    {
                        this.roomCentroids.Add(room, centroid);
                    }
                }
            }
            Console.WriteLine($"[LocationFinding] Calcolati {roomCentroids.Count} centroidi di stanze e {hallwayCentroids.Count} di corridoi.");

            this.confidenceCalculator = new ConfidenceCalculator();
            this.semanticLabelerInternal = new SemanticLabeler();
            CowlWrapper.InitializeStaticOntology(CowlWrapper.BaseOntologyIRI);

            this.confidenceLevelOwlDescriptors = new Dictionary<string, string>
            {
                { "High", CowlWrapper.SanitizeForIri("HighConfidence") },
                { "Medium", CowlWrapper.SanitizeForIri("MediumConfidence") },
                { "Low", CowlWrapper.SanitizeForIri("LowConfidence") }
            };

            string? pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();
            this.outputFilePath = Path.Combine(pluginDirectory, "Data", "amongus_semantic_output.owl");
            string? outputDir = Path.GetDirectoryName(this.outputFilePath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            Console.WriteLine($"[LocationFinding] Inizializzato. OWL in: {this.outputFilePath}");
            Console.WriteLine($"[LocationFinding] Ontologia utilizzerà Base IRI: {CowlWrapper.BaseOntologyIRI}");
        }

        /// Metodo principale per localizzare un giocatore. Determina la stanza, calcola la confidenza,
        /// genera etichette semantiche e aggiorna l'ontologia OWL con queste informazioni.
        public LocationResult FindLocation(string playerID, Vector2 position)
        {
            if (this.mapData.Rooms == null || !this.mapData.Rooms.Any())
                throw new InvalidOperationException("Dati mappa non inizializzati o vuoti.");

            //Distinzione se il player è strettamente dentro il poligono di una stanza o meno.
            Data.Room? strictlyFoundRoom = FindStrictlyContainingRoom(position);

            string roomNameForOntology;
            string contextualLabel;
            Vector2 relativePos;
            float confidence;

            if (strictlyFoundRoom != null)
            {
                // --- CASO 1: Il giocatore è DENTRO un'area poligonale definita (stanza o corridoio) ---
                roomNameForOntology = strictlyFoundRoom.Name ?? "UnknownRoom";
                confidence = this.confidenceCalculator.CalculateConfidenceScore(position, strictlyFoundRoom);
                relativePos = CalculateRelativePositionInternal(position, strictlyFoundRoom);
                contextualLabel = GenerateRoomContextLabel(position, strictlyFoundRoom);
            }
            else
            {
                // --- CASO 2: Il giocatore è FUORI da aree definite (spazio aperto, corridoio non poligonale, ecc.) ---

                // a) Trova la STANZA PRINCIPALE più vicina per l'assegnazione della posizione.
                Data.Room? closestMainRoom = FindClosestMainRoom(position);
                roomNameForOntology = closestMainRoom?.Name ?? "outsideMap";

                // b) Genera l'ETICHETTA SEMANTICA basandosi sul CORRIDOIO più vicino.
                contextualLabel = this.semanticLabelerInternal.GenerateHallwayProximityLabel(position, this.hallwayCentroids, _allDoors);

                // c) Calcola la confidenza basandosi sulla distanza dalla stanza principale più vicina.
                // Se non c'è una stanza principale (mappa vuota?), la confidenza è bassa.
                confidence = (closestMainRoom != null)
                    ? this.confidenceCalculator.CalculateConfidenceScore(position, closestMainRoom)
                    : 0.1f;

                // La posizione relativa viene calcolata rispetto alla stanza più vicina.
                relativePos = (closestMainRoom != null)
                    ? CalculateRelativePositionInternal(position, closestMainRoom)
                    : Vector2.Zero;
            }

            string sanitizedContextualLabel = CowlWrapper.SanitizeForIri(contextualLabel);
            string confidenceOwlLabel = GetOwlConfidenceLevelDescriptorInternal(confidence);
            string playerStateIriSuffix = CowlWrapper.SanitizeForIri(playerID);

            WriteSemanticData(playerStateIriSuffix, roomNameForOntology, sanitizedContextualLabel, confidenceOwlLabel);

            return new LocationResult
            {
                RoomName = roomNameForOntology,
                RelativePosition = relativePos,
                ConfidenceScore = confidence,
                SemanticLabel = sanitizedContextualLabel
            };
        }

        ///Trova la stanza che contiene STRETTAMENTE la posizione data. Non esegue fallback.
        private Data.Room? FindStrictlyContainingRoom(Vector2 position)
        {
            if (this.mapData.Rooms == null) return null;

            foreach (var room in this.mapData.Rooms)
            {
                var collider = room.Colliders.FirstOrDefault();
                if (collider != null && collider.Points.Count > 2)
                {
                    if (GeometryUtils.IsPointInPolygon(position, collider.Points))
                    {
                        return room; // Corrispondenza perfetta trovata.
                    }
                }
            }
            return null; // Nessuna stanza contiene strettamente il punto.
        }

        ///Metodo di fallback: trova la stanza principale (non-corridoio) più vicina a una data posizione.
        private Data.Room? FindClosestMainRoom(Vector2 position)
        {
            if (this.roomCentroids == null || !this.roomCentroids.Any())
            {
                return null; // Nessuna stanza principale disponibile
            }

            Data.Room? closestRoom = null;
            float minDistanceSquared = float.MaxValue;

            // Itera solo sui centroidi delle stanze principali (già filtrati nel costruttore)
            foreach (var entry in this.roomCentroids)
            {
                float distSq = Vector2.DistanceSquared(position, entry.Value);
                if (distSq < minDistanceSquared)
                {
                    minDistanceSquared = distSq;
                    closestRoom = entry.Key;
                }
            }
            return closestRoom;
        }

        

        private string GenerateRoomContextLabel(Vector2 playerPosition, Data.Room currentRoom)
        {
            Data.DoorInfo? closestDoor = FindClosestDoorInRoom(playerPosition, currentRoom);

            if (closestDoor != null)
            {
                string currentRoomNameSanitized = CowlWrapper.SanitizeForIri(currentRoom.Name!);
                string adjacentRoomNameSanitized = string.Equals(CowlWrapper.SanitizeForIri(closestDoor.RoomA_Name), currentRoomNameSanitized, SC) ?
                                          CowlWrapper.SanitizeForIri(closestDoor.RoomB_Name!) :
                                          CowlWrapper.SanitizeForIri(closestDoor.RoomA_Name!);
                return $"NearDoorTo{adjacentRoomNameSanitized}";
            }
            return this.semanticLabelerInternal.GenerateSemanticLabel(playerPosition, currentRoom);
        }

        private Data.DoorInfo? FindClosestDoorInRoom(Vector2 playerPosition, Data.Room currentRoom)
        {
            if (this.mapData.Doors == null) return null;

            Data.DoorInfo? closestDoor = null;
            float minDistanceToDoor = float.MaxValue;
            string currentRoomNameSanitized = CowlWrapper.SanitizeForIri(currentRoom.Name ?? "");
            if (string.IsNullOrEmpty(currentRoomNameSanitized)) return null;

            foreach (var door in this.mapData.Doors)
            {
                if (string.IsNullOrEmpty(door.DoorName)) continue;

                string doorRoomA_Sanitized = CowlWrapper.SanitizeForIri(door.RoomA_Name);
                string doorRoomB_Sanitized = CowlWrapper.SanitizeForIri(door.RoomB_Name);

                bool isDoorOfCurrentRoom =
                    (string.Equals(doorRoomA_Sanitized, currentRoomNameSanitized, SC) && !string.IsNullOrEmpty(door.RoomB_Name)) ||
                    (string.Equals(doorRoomB_Sanitized, currentRoomNameSanitized, SC) && !string.IsNullOrEmpty(door.RoomA_Name));

                if (isDoorOfCurrentRoom)
                {
                    float distance = Vector2.Distance(playerPosition, door.Position);
                    if (distance < DOOR_PROXIMITY_THRESHOLD && distance < minDistanceToDoor)
                    {
                        minDistanceToDoor = distance;
                        closestDoor = door;
                    }
                }
            }
            return closestDoor;
        }

        private Vector2 CalculateRelativePositionInternal(Vector2 position, Data.Room room)
        {
            var roomCollider = room.Colliders.FirstOrDefault();
            if (roomCollider?.Bounds.HasValue != true) return Vector2.Zero;

            Data.Rect bounds = roomCollider.Bounds.Value;
            if (bounds.Width < float.Epsilon || bounds.Height < float.Epsilon) return new Vector2(0.5f, 0.5f);

            float relativeX = (position.X - bounds.X) / bounds.Width;
            float relativeY = (position.Y - bounds.Y) / bounds.Height;

            return new Vector2(Math.Clamp(relativeX, 0f, 1f), Math.Clamp(relativeY, 0f, 1f));
        }

        private string GetOwlConfidenceLevelDescriptorInternal(float confidenceScore)
        {
            if (confidenceLevelOwlDescriptors == null)
            {
                Console.Error.WriteLine("[LocationFinding] ERRORE: confidenceLevelOwlDescriptors è NULL!");
                return CowlWrapper.SanitizeForIri("ErrorConfidenceMappingNull");
            }
            if (confidenceScore >= 0.90f) return confidenceLevelOwlDescriptors["High"];
            if (confidenceScore >= 0.75f) return confidenceLevelOwlDescriptors["Medium"];
            return confidenceLevelOwlDescriptors["Low"];
        }

       
        private void WriteSemanticData(string playerStateIriSuffix, string roomNameInput, string contextualLabelInput, string confidenceOwlLabelInput)
        {
            if (CowlWrapper.Ontology?.__Instance == IntPtr.Zero || CowlWrapper.Manager?.__Instance == IntPtr.Zero)
            {
                Console.Error.WriteLine("[LocationFinding] ERRORE CRITICO: Ontology o Manager non inizializzati.");
                return;
            }

            string finalSanitizedRoomName = CowlWrapper.SanitizeForIri(roomNameInput);

            try
            {
                string playerIndividualIri = CowlWrapper.GetPlayerIndividualIri(playerStateIriSuffix);
                string playerClassIri = CowlWrapper.PlayerClassIRI;
                string roomClassIri = CowlWrapper.GetRoomClassIri(finalSanitizedRoomName);
                string contextualPosClassIri = CowlWrapper.GetContextualPositionClassIri(contextualLabelInput);
                string confidenceClassIri = CowlWrapper.GetConfidenceLevelClassIri(confidenceOwlLabelInput);

                string isInRoomPropIri = CowlWrapper.IsInRoomPropertyIRI;
                string hasCtxPosPropIri = CowlWrapper.HasContextualPositionPropertyIRI;
                string hasConfPropIri = CowlWrapper.HasConfidenceLevelPropertyIRI;

                CowlClass playerOwlClass = CowlWrapper.CreateClassFromIri(playerClassIri);
                CowlObjQuant isInRoomRestr = CowlWrapper.CreateAllValuesRestriction(isInRoomPropIri, new[] { roomClassIri });
                CowlObjQuant hasCtxPosRestr = CowlWrapper.CreateAllValuesRestriction(hasCtxPosPropIri, new[] { contextualPosClassIri });
                CowlObjQuant hasConfRestr = CowlWrapper.CreateAllValuesRestriction(hasConfPropIri, new[] { confidenceClassIri });

                var classesForInd = new List<CowlClass> { playerOwlClass };
                var restrictionsForInd = new List<CowlObjQuant> { isInRoomRestr, hasCtxPosRestr, hasConfRestr };

                CowlRet result = CowlWrapper.CreateIndividual(CowlWrapper.Ontology, playerIndividualIri, classesForInd, restrictionsForInd);

                if (result != CowlRet.COWL_OK)
                    Console.Error.WriteLine($"[LocationFinding] Errore Cowl ({result}) durante la creazione dell'individuo '{playerIndividualIri}'.");

                CowlWrapper.SaveStaticOntology(this.outputFilePath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[LocationFinding] Eccezione in WriteSemanticData: {ex.Message}\n{ex.StackTrace}");
            }
        }
       
    }
}
