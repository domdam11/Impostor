#nullable enable
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Data
{


    public class MapDataLoader
    {
        // Classi interne per deserializzare la struttura JSON grezza della mappa
        private class RawColliderLayer { public string? Name { get; set; } public List<RawColliderData?>? Colliders { get; set; } }
        private class RawColliderData { public string? Name { get; set; } public string? Path { get; set; } }
        private class RawLayers { public Dictionary<string, RawColliderLayer?>? Layers { get; set; } }
        private class RawMapRoot { public RawLayers? Colliders { get; set; } }

        // Tolleranza usata per determinare se i bounding box di una porta e di una stanza sono adiacenti
        private const float BOUNDING_BOX_ADJACENCY_TOLERANCE = 0.5f;

        public MapData LoadMapData(string mapJsonPath)
        {
            MapData mapData = new MapData();
            List<DoorInfo> tempDoors = new List<DoorInfo>();
            HashSet<string> processedDoorColliderNames = new HashSet<string>();

            try
            {
                string mapJsonString = File.ReadAllText(mapJsonPath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                RawMapRoot? rawMap = JsonSerializer.Deserialize<RawMapRoot>(mapJsonString, options);

                if (rawMap?.Colliders?.Layers != null)
                {
                    // Usiamo il Layer 2 che contiene le aree logiche delle stanze.
                    const string roomLayerKey = "2";

                    foreach (var layerPair in rawMap.Colliders.Layers)
                    {
                        RawColliderLayer? layer = layerPair.Value;
                        if (layer == null || layer.Colliders == null) continue;

                        
                        if (layerPair.Key != roomLayerKey) // Processa le porte dagli altri layer
                        {
                            foreach (RawColliderData? colliderData in layer.Colliders)
                            {
                                if (colliderData?.Name != null && colliderData.Name.Contains("Door", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (processedDoorColliderNames.Add(colliderData.Name))
                                    {
                                        List<Vector2> points = ParsePath(colliderData.Path);
                                        if (!points.Any()) continue;
                                        Rect bounds = CalculateBoundingBox(points);
                                        Vector2 doorCenter = new Vector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                                        tempDoors.Add(new DoorInfo { DoorName = colliderData.Name, Position = doorCenter });
                                    }
                                }
                            }
                        }
                        // Identifica le stanze SOLO dal Layer 2
                        else if (layerPair.Key == roomLayerKey)
                        {
                            foreach (RawColliderData? colliderData in layer.Colliders)
                            {
                                if (colliderData?.Name == null || colliderData.Path == null) continue;

                                string roomName = ExtractRoomName(colliderData.Name);

                                if (string.IsNullOrWhiteSpace(roomName) || roomName.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                // Dato che il Layer 2 ha un solo poligono per stanza, possiamo creare direttamente i collider delle stanze
                                if (!mapData.Rooms.Any(r => r.Name.Equals(roomName, StringComparison.OrdinalIgnoreCase)))
                                {
                                    var room = new Room { Name = roomName };
                                    List<Vector2> points = ParsePath(colliderData.Path);
                                    if (points.Any())
                                    {
                                        var roomCollider = new Collider { Name = colliderData.Name, Points = points, Bounds = CalculateBoundingBox(points) };
                                        room.Colliders.Add(roomCollider);
                                    }
                                    mapData.Rooms.Add(room);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERRORE durante il parsing di '{mapJsonPath}': {ex.Message}");
            }

            AssociateDoorsToRooms(tempDoors, mapData.Rooms, mapData.Doors);

            Console.WriteLine($"--- Stanze Caricate (solo da Layer 2): {mapData.Rooms.Count} ---");
            Console.WriteLine($"--- Porte Associate Correttamente a Stanze: {mapData.Doors.Count} ---");

            return mapData;
        }
        //Associazione delle porte alle stanze, utilizzata per la creazione degli IRI
        private void AssociateDoorsToRooms(List<DoorInfo> foundDoors, List<Room> allRooms, List<DoorInfo> finalDoorList)
        {
            foreach (var door in foundDoors)
            {
                string? inferredRoomFromDoorName = InferRoomFromDoorName(door.DoorName);
                List<Tuple<Room, float>> candidateRooms = new List<Tuple<Room, float>>(); // Lista di stanze candidate e la loro distanza dalla porta

                foreach (var room in allRooms)
                {
                    if (room.Colliders == null || !room.Colliders.Any() || string.IsNullOrEmpty(room.Name)) continue;

                    foreach (var roomCollider in room.Colliders)
                    {
                        if (roomCollider.Points.Any() && roomCollider.Bounds.HasValue)
                        {
                            Rect roomColliderBounds = roomCollider.Bounds.Value;
                            Vector2 roomColliderCenter = new Vector2(roomColliderBounds.X + roomColliderBounds.Width / 2, roomColliderBounds.Y + roomColliderBounds.Height / 2);
                            float distanceToColliderCenter = Vector2.Distance(door.Position, roomColliderCenter);

                            // Crea un piccolo bounding box fittizio per la porta per il controllo di adiacenza/sovrapposizione
                            Rect doorPseudoBounds = new Rect(door.Position.X - 0.1f, door.Position.Y - 0.1f, 0.2f, 0.2f);
                            if (RectsOverlapOrAreAdjacent(doorPseudoBounds, roomColliderBounds, BOUNDING_BOX_ADJACENCY_TOLERANCE))
                            {
                                // Se la stanza non è già tra le candidate viene aggiunta
                                if (!candidateRooms.Any(cr => cr.Item1.Name != null && cr.Item1.Name.Equals(room.Name, StringComparison.OrdinalIgnoreCase)))
                                {
                                    candidateRooms.Add(Tuple.Create(room, distanceToColliderCenter));
                                }
                            }
                        }
                    }
                }

                // Ordina le stanze candidate per distanza crescente dal centro della porta
                candidateRooms.Sort((a, b) => a.Item2.CompareTo(b.Item2));

                if (candidateRooms.Any())
                {
                    int inferredRoomIdx = -1;
                    // Se è stato possibile inferire una stanza dal nome della porta, cerca quella stanza tra le candidate
                    if (inferredRoomFromDoorName != null)
                    {
                        inferredRoomIdx = candidateRooms.FindIndex(cr => string.Equals(cr.Item1.Name, inferredRoomFromDoorName, StringComparison.OrdinalIgnoreCase));
                    }

                    // Assegna le stanze A e B alla porta
                    if (inferredRoomIdx != -1) // Se la stanza inferita è tra le candidate
                    {
                        door.RoomA_Ref = candidateRooms[inferredRoomIdx].Item1;
                        door.RoomA_Name = door.RoomA_Ref.Name;
                        // Prende la successiva candidata (se esiste) come stanza B
                        var tempSecondCandidate = candidateRooms.Where((cr, idx) => idx != inferredRoomIdx).FirstOrDefault();
                        if (tempSecondCandidate != null)
                        {
                            door.RoomB_Ref = tempSecondCandidate.Item1;
                            door.RoomB_Name = door.RoomB_Ref.Name;
                        }
                    }
                    else // Altrimenti, prende le prime due candidate per distanza
                    {
                        door.RoomA_Ref = candidateRooms[0].Item1;
                        door.RoomA_Name = door.RoomA_Ref.Name;
                        if (candidateRooms.Count > 1)
                        {
                            door.RoomB_Ref = candidateRooms[1].Item1;
                            door.RoomB_Name = door.RoomB_Ref.Name;
                        }
                    }

                    // Aggiunge la porta alla lista finale solo se almeno una stanza è stata associata
                    if (door.RoomA_Ref != null)
                    {
                        finalDoorList.Add(door);
                    }
                }
            }
        }

        private string? InferRoomFromDoorName(string doorName)
        {
            // Logica per tentare di estrarre un nome di stanza dal nome del collider della porta (es. "MedBay/MedBay_ScanDoor")
            var parts = doorName.Split('/');
            if (parts.Length > 1)
            {
                // Gestisce nomi tipo "SkeldShip(Clone)/MedBay/MedBay_Door"
                if (parts[0].Equals("SkeldShip(Clone)", StringComparison.OrdinalIgnoreCase) && parts.Length > 2)
                {
                    if (!parts[1].Contains("Door", StringComparison.OrdinalIgnoreCase)) return parts[1];
                }
                // Gestisce nomi tipo "MedBay/MedBay_Door"
                else if (!parts[0].Equals("SkeldShip(Clone)", StringComparison.OrdinalIgnoreCase) &&
                         !parts[0].Contains("Door", StringComparison.OrdinalIgnoreCase))
                {
                    return parts[0];
                }
            }
            return null; // Non è stato possibile inferire un nome di stanza
        }

        private bool RectsOverlapOrAreAdjacent(Rect r1, Rect r2, float tolerance)
        {
            // Controlla se due rettangoli si sovrappongono o sono adiacenti entro una data tolleranza
            return (r1.X < r2.X + r2.Width + tolerance &&
                    r1.X + r1.Width > r2.X - tolerance &&
                    r1.Y < r2.Y + r2.Height + tolerance &&
                    r1.Y + r1.Height > r2.Y - tolerance);
        }

        private string ExtractRoomName(string rawColliderName)
        {
            // Estrae il nome della stanza dal nome completo del collider (es. "UpperEngine/Ground" -> "UpperEngine")
            if (string.IsNullOrEmpty(rawColliderName)) return "Unknown";
            var parts = rawColliderName.Split('/');

            if (parts.Length >= 2)
            {
                // Gestisce il formato "SkeldShip(Clone)/NomeStanza/..."
                if (parts[0].Equals("SkeldShip(Clone)", StringComparison.OrdinalIgnoreCase))
                {
                    // Se la seconda parte è "Ground", "Room", inizia con minuscola o contiene "_",
                    // allora parts[1] è probabilmente il nome della stanza.
                    // Altrimenti, parts[1] potrebbe essere un oggetto dentro SkeldShip(Clone) ma non una stanza nominata.
                    if (parts.Length > 1) // parts[1] è il nome della stanza in questo caso
                    {
                        
                        return parts[1];
                    }
                }
                // Gestisce il formato "NomeStanza/Qualcosa"
                return parts[0];
            }
            // Se non c'è '/', il nome grezzo potrebbe essere direttamente il nome della stanza (o un oggetto)
            if (!rawColliderName.Contains("/")) return rawColliderName;
            return "Unknown"; // Fallback se il formato non è riconosciuto
        }


        private List<Vector2> ParsePath(string pathString)
        {
            // Parsa una stringa di path SVG (comandi M, L, H, V, Z, ecc.) e la converte in una lista di Vector2.
            // Supporta comandi assoluti (maiuscoli) e relativi (minuscoli).
            // Per comandi curvi (C, S, Q, T, A), approssima prendendo solo il punto finale del comando
            // Questo è sufficiente per definire i poligoni dei collider se i path SVG originali usano principalmente linee.
            List<Vector2> points = new List<Vector2>();
            if (string.IsNullOrWhiteSpace(pathString)) return points;

            float currentX = 0, currentY = 0; // Tiene traccia della posizione corrente per i comandi relativi

            // Divide la stringa del path in segmenti di comando (es. "M10,20", "L30,40")
            string[] commandSegments = Regex.Split(pathString, @"(?=[MLHVCSQTAZmlhvcsqtaz])");

            foreach (string segment in commandSegments)
            {
                string trimmedSegment = segment.Trim();
                if (string.IsNullOrEmpty(trimmedSegment)) continue;

                char commandChar = trimmedSegment[0];
                string argsString = trimmedSegment.Length > 1 ? trimmedSegment.Substring(1).Trim() : "";

                // Estrae i valori numerici dagli argomenti del comando
                var argMatches = Regex.Matches(argsString, @"-?\d*\.?\d+([eE][-+]?\d+)?");
                List<float> argValues = new List<float>();
                foreach (Match m in argMatches.Cast<Match>())
                {
                    if (float.TryParse(m.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out float val))
                    {
                        argValues.Add(val);
                    }
                }

                int argIndex = 0;
                // Funzione helper per processare gruppi di argomenti per comandi che possono averne multipli (es. L x1,y1 x2,y2 ...)
                Action<int, Action<float[]>> processArgs = (count, action) =>
                {
                    while (argIndex + count <= argValues.Count)
                    {
                        action(argValues.GetRange(argIndex, count).ToArray());
                        argIndex += count;
                    }
                };

                switch (commandChar)
                {
                    case 'M': // MoveTo assoluto
                        processArgs(2, p => { currentX = p[0]; currentY = p[1]; points.Add(new Vector2(currentX, currentY)); });
                        break;
                    case 'm': // MoveTo relativo
                        processArgs(2, p => { currentX += p[0]; currentY += p[1]; points.Add(new Vector2(currentX, currentY)); });
                        break;
                    case 'L': // LineTo assoluto
                        processArgs(2, p => { currentX = p[0]; currentY = p[1]; points.Add(new Vector2(currentX, currentY)); });
                        break;
                    case 'l': // LineTo relativo
                        processArgs(2, p => { currentX += p[0]; currentY += p[1]; points.Add(new Vector2(currentX, currentY)); });
                        break;
                    case 'H': // Horizontal LineTo assoluto
                        processArgs(1, p => { currentX = p[0]; points.Add(new Vector2(currentX, currentY)); });
                        break;
                    case 'h': // Horizontal LineTo relativo
                        processArgs(1, p => { currentX += p[0]; points.Add(new Vector2(currentX, currentY)); });
                        break;
                    case 'V': // Vertical LineTo assoluto
                        processArgs(1, p => { currentY = p[0]; points.Add(new Vector2(currentX, currentY)); });
                        break;
                    case 'v': // Vertical LineTo relativo
                        processArgs(1, p => { currentY += p[0]; points.Add(new Vector2(currentX, currentY)); });
                        break;
                    case 'Z':
                    case 'z': // ClosePath
                        // Se il path non è vuoto e l'ultimo punto non è uguale al primo, aggiunge il primo punto per chiudere il poligono.
                        if (points.Count > 0 && points.Last() != points.First())
                        {
                            points.Add(points[0]);
                        }
                        break;
                    // Per le curve (C, S, Q, T, A), aggiungiamo solo il punto finale per semplificare il poligono.
                    case 'C': processArgs(6, p => { currentX = p[4]; currentY = p[5]; points.Add(new Vector2(currentX, currentY)); }); break; // Cubic Bezier assoluto
                    case 'c': processArgs(6, p => { currentX += p[4]; currentY += p[5]; points.Add(new Vector2(currentX, currentY)); }); break; // Cubic Bezier relativo
                    case 'S': processArgs(4, p => { currentX = p[2]; currentY = p[3]; points.Add(new Vector2(currentX, currentY)); }); break; // Smooth Cubic Bezier assoluto
                    case 's': processArgs(4, p => { currentX += p[2]; currentY += p[3]; points.Add(new Vector2(currentX, currentY)); }); break; // Smooth Cubic Bezier relativo
                    case 'Q': processArgs(4, p => { currentX = p[2]; currentY = p[3]; points.Add(new Vector2(currentX, currentY)); }); break; // Quadratic Bezier assoluto
                    case 'q': processArgs(4, p => { currentX += p[2]; currentY += p[3]; points.Add(new Vector2(currentX, currentY)); }); break; // Quadratic Bezier relativo
                    case 'T': processArgs(2, p => { currentX = p[0]; currentY = p[1]; points.Add(new Vector2(currentX, currentY)); }); break; // Smooth Quadratic Bezier assoluto
                    case 't': processArgs(2, p => { currentX += p[0]; currentY += p[1]; points.Add(new Vector2(currentX, currentY)); }); break; // Smooth Quadratic Bezier relativo
                    case 'A': processArgs(7, p => { currentX = p[5]; currentY = p[6]; points.Add(new Vector2(currentX, currentY)); }); break; // Elliptical Arc assoluto
                    case 'a': processArgs(7, p => { currentX += p[5]; currentY += p[6]; points.Add(new Vector2(currentX, currentY)); }); break; // Elliptical Arc relativo
                    default:
                        break;
                }
            }
            return points;
        }

        private Rect CalculateBoundingBox(List<Vector2> points)
        {
            // Calcola il rettangolo di delimitazione (bounding box) per una lista di punti.
            if (points == null || !points.Any())
            {
                // Se non ci sono punti, restituisce un rettangolo vuoto.
                return new Rect(0, 0, 0, 0);
            }
            float minX = points.Min(p => p.X);
            float minY = points.Min(p => p.Y);
            float maxX = points.Max(p => p.X);
            float maxY = points.Max(p => p.Y);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
