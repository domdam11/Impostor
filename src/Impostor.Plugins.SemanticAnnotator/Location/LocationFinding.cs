#nullable enable
using Data;
using Geometry;
using Semantic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Location
{
    public class LocationFinding
    {
        private readonly MapData mapData;
        private readonly ConfidenceCalculator confidenceCalculator;
        private readonly SemanticLabeler semanticLabelerInternal;
        private readonly Dictionary<Data.Room, Vector2> roomCentroids;
        private readonly Dictionary<Data.Room, Vector2> hallwayCentroids;
        private const float DOOR_PROXIMITY_THRESHOLD = 3.0f;
        private static readonly StringComparison SC = StringComparison.OrdinalIgnoreCase;
        private readonly List<DoorInfo> allDoors;

        public LocationFinding(string mapJsonPath)
        {
            var mapDataLoader = new Data.MapDataLoader();
            this.mapData = mapDataLoader.LoadMapData(mapJsonPath) 
                ?? throw new ArgumentNullException(nameof(mapData), "LoadMapData ha restituito null.");

            this.roomCentroids = new Dictionary<Data.Room, Vector2>();
            this.hallwayCentroids = new Dictionary<Data.Room, Vector2>();
            allDoors = mapData.Doors;

            foreach (var room in this.mapData.Rooms)
            {
                var mainCollider = room.Colliders.FirstOrDefault();
                if (room.Name != null && mainCollider != null && mainCollider.Points.Any())
                {
                    Vector2 centroid = GeometryUtils.CalculateCentroid(mainCollider.Points);
                    if (room.Name.Contains("Hallway", SC))
                        this.hallwayCentroids.Add(room, centroid);
                    else
                        this.roomCentroids.Add(room, centroid);
                }
            }

            Console.WriteLine($"LocationFinding: Calcolati {roomCentroids.Count} centroidi di stanze e {hallwayCentroids.Count} di corridoi.");
            this.confidenceCalculator = new ConfidenceCalculator();
            this.semanticLabelerInternal = new SemanticLabeler();
        }

        public LocationResult FindLocation(string playerID, Vector2 position)
        {
            if (this.mapData.Rooms == null || !this.mapData.Rooms.Any())
                throw new InvalidOperationException("Dati mappa non inizializzati o vuoti.");

            Data.Room? strictlyFoundRoom = FindStrictlyContainingRoom(position);
            string roomNameForOntology;
            string contextualLabel;
            Vector2 relativePos;
            float confidence;

            if (strictlyFoundRoom != null)
            {
                roomNameForOntology = strictlyFoundRoom.Name ?? "UnknownRoom";
                confidence = this.confidenceCalculator.CalculateConfidenceScore(position, strictlyFoundRoom);
                relativePos = CalculateRelativePositionInternal(position, strictlyFoundRoom);
                contextualLabel = GenerateRoomContextLabel(position, strictlyFoundRoom);
            }
            else
            {
                Data.Room? closestMainRoom = FindClosestMainRoom(position);
                roomNameForOntology = closestMainRoom?.Name ?? "outsideMap";
                contextualLabel = this.semanticLabelerInternal.GenerateHallwayProximityLabel(
                    position, this.hallwayCentroids, allDoors);
                confidence = closestMainRoom != null 
                    ? this.confidenceCalculator.CalculateConfidenceScore(position, closestMainRoom) 
                    : 0.1f;
                relativePos = closestMainRoom != null 
                    ? CalculateRelativePositionInternal(position, closestMainRoom) 
                    : Vector2.Zero;
            }

            return new LocationResult
            {
                RoomName = roomNameForOntology,
                RelativePosition = relativePos,
                ConfidenceScore = confidence,
                SemanticLabel = contextualLabel
            };
        }

        private Data.Room? FindStrictlyContainingRoom(Vector2 position)
        {
            if (this.mapData.Rooms == null) return null;

            foreach (var room in this.mapData.Rooms)
            {
                var collider = room.Colliders.FirstOrDefault();
                if (collider != null && collider.Points.Count > 2)
                {
                    if (GeometryUtils.IsPointInPolygon(position, collider.Points))
                        return room;
                }
            }
            return null;
        }

        private Data.Room? FindClosestMainRoom(Vector2 position)
        {
            if (this.roomCentroids == null || !this.roomCentroids.Any())
                return null;

            Data.Room? closestRoom = null;
            float minDistanceSquared = float.MaxValue;

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
                string currentRoomNameSanitized = currentRoom.Name!;
                string adjacentRoomNameSanitized = string.Equals(closestDoor.RoomA_Name, currentRoomNameSanitized, SC)
                    ? closestDoor.RoomB_Name!
                    : closestDoor.RoomA_Name!;
                return $"NearDoorTo{adjacentRoomNameSanitized}";
            }
            return this.semanticLabelerInternal.GenerateSemanticLabel(playerPosition, currentRoom);
        }

        private Data.DoorInfo? FindClosestDoorInRoom(Vector2 playerPosition, Data.Room currentRoom)
        {
            if (this.mapData.Doors == null) return null;

            Data.DoorInfo? closestDoor = null;
            float minDistanceToDoor = float.MaxValue;
            string currentRoomNameSanitized = currentRoom.Name ?? "";

            if (string.IsNullOrEmpty(currentRoomNameSanitized))
                return null;

            foreach (var door in this.mapData.Doors)
            {
                if (string.IsNullOrEmpty(door.DoorName)) continue;

                string doorRoomASanitized = door.RoomA_Name;
                string doorRoomBSanitized = door.RoomB_Name;

                bool isDoorOfCurrentRoom = 
                    string.Equals(doorRoomASanitized, currentRoomNameSanitized, SC) ||
                    (!string.IsNullOrEmpty(door.RoomB_Name) && 
                     string.Equals(doorRoomBSanitized, currentRoomNameSanitized, SC));

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
            if (roomCollider?.Bounds.HasValue != true)
                return Vector2.Zero;

            if (!roomCollider.Bounds.HasValue)
                return Vector2.Zero;

            Data.Rect bounds = roomCollider.Bounds.Value;

            if (bounds.Width < float.Epsilon || bounds.Height < float.Epsilon)
                return new Vector2(0.5f, 0.5f);

            float relativeX = (position.X - bounds.X) / bounds.Width;
            float relativeY = (position.Y - bounds.Y) / bounds.Height;

            return new Vector2(Math.Clamp(relativeX, 0f, 1f), Math.Clamp(relativeY, 0f, 1f));
        }
    }
}
