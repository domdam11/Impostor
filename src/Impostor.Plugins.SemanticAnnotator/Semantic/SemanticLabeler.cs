#nullable enable
using Data;
using Geometry;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;

namespace Semantic
{
    public class SemanticLabeler
    {
        // Definisce la distanza massima (in unità di gioco) entro cui una porta è considerata "vicina".

        private const float DoorProximityThreshold = 4.5f;


        public SemanticLabeler()
        {

        }


        // Genera un'etichetta di zona (es. "isInUpperLeft") per un giocatore che si trova all'interno di una stanza definita.
        public string GenerateSemanticLabel(Vector2 position, Room room)
        {
            Vector2 relativeQuadrant = CalculateRelativeQuadrant(position, room);

            if (relativeQuadrant.Equals(Vector2.Zero))
                return "isAtUndeterminedPosition";
            else if (relativeQuadrant.X < 0 && relativeQuadrant.Y > 0)
                return "isInUpperLeft";
            else if (relativeQuadrant.X > 0 && relativeQuadrant.Y > 0)
                return "isInUpperRight";
            else if (relativeQuadrant.X < 0 && relativeQuadrant.Y < 0)
                return "isInLowerLeft";
            else if (relativeQuadrant.X > 0 && relativeQuadrant.Y < 0)
                return "isInLowerRight";
            else
                return "isNearCenter";
        }


        /// Genera un'etichetta semantica basata sulla prossimità. Priorità viene data alla porta più vicina, altrimenti al corridoio più vicino. 
        /// Viene chiamato quando un giocatore non si trova all'interno di nessuna area definita.

        public string GenerateHallwayProximityLabel(
            Vector2 playerPosition,
            Dictionary<Data.Room, Vector2> hallwayCentroids,
            IEnumerable<Data.DoorInfo> allDoors)
        {
            if (hallwayCentroids == null || !hallwayCentroids.Any())
            {
                return "outsideMap";
            }

            // 1. Trova il corridoio più vicino al giocatore
            Room? closestHallway = null;
            float minHallwayDistSq = float.MaxValue;
            foreach (var entry in hallwayCentroids)
            {
                float distSq = Vector2.DistanceSquared(playerPosition, entry.Value);
                if (distSq < minHallwayDistSq)
                {
                    minHallwayDistSq = distSq;
                    closestHallway = entry.Key;
                }
            }

            if (closestHallway == null)
            {
                return "outsideMap";
            }

            // 2. Trova la porta fisicamente più vicina al giocatore.
            DoorInfo? closestDoor = null;
            float minDoorDistSq = float.MaxValue;
            if (allDoors != null)
            {
                foreach (var door in allDoors)
                {
                    float doorDistSq = Vector2.DistanceSquared(playerPosition, door.Position);
                    if (doorDistSq < minDoorDistSq)
                    {
                        minDoorDistSq = doorDistSq;
                        closestDoor = door;
                    }
                }
            }

            // 3. Se una porta è abbastanza vicina, genera la sua etichetta specifica.
            if (closestDoor != null && minDoorDistSq < (DoorProximityThreshold * DoorProximityThreshold))
            {
                // Verifica che la porta più vicina sia effettivamente collegata al corridoio in cui ci troviamo.
                // Usiamo ReferenceEquals per un confronto sicuro e veloce tra oggetti.
                if (ReferenceEquals(closestDoor.RoomA_Ref, closestHallway) || ReferenceEquals(closestDoor.RoomB_Ref, closestHallway))
                {
                    // La porta è rilevante. Identifica la stanza di destinazione.
                    Room? destinationRoom = ReferenceEquals(closestDoor.RoomA_Ref, closestHallway)
                                            ? closestDoor.RoomB_Ref
                                            : closestDoor.RoomA_Ref;

                    if (destinationRoom != null && !string.IsNullOrEmpty(destinationRoom.Name))
                    {
                        return $"NearDoorTo{destinationRoom.Name}";
                    }
                }
            }

            // 4. Se nessuna porta rilevante è vicina, usa l'etichetta generica del corridoio.
            if (!string.IsNullOrEmpty(closestHallway.Name))
            {
                return $"Near{closestHallway.Name}";
            }

            // Fallback finale.
            return "outsideMap";
        }

        private Vector2 CalculateRelativeQuadrant(Vector2 position, Room room)
        {
            if (room?.Colliders == null || !room.Colliders.Any() ||
                room.Colliders[0].Points == null || !room.Colliders[0].Points.Any())
            {
                return Vector2.Zero;
            }

            Vector2 centroid = GeometryUtils.CalculateCentroid(room.Colliders[0].Points);

            if (centroid.Equals(Vector2.Zero))
            {
                return Vector2.Zero;
            }

            if (position.X < centroid.X)
            {
                if (position.Y < centroid.Y) return new Vector2(-1, -1);
                else return new Vector2(-1, 1);
            }
            else
            {
                if (position.Y < centroid.Y) return new Vector2(1, -1);
                else return new Vector2(1, 1);
            }
        }
    }
}
