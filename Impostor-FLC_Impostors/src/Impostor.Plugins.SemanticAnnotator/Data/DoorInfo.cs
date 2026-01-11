using System.Numerics;
namespace Data
{
    public class DoorInfo
    {
        public string DoorName { get; set; } = string.Empty; // Nome originale del collider della porta
        public Vector2 Position { get; set; }       // Centroide della porta
        public string? RoomA_Name { get; set; }      // Nome di una stanza adiacente
        public string? RoomB_Name { get; set; }      // Nome dell'altra stanza adiacente (può essere null se la porta dà sull'esterno o non si trova la seconda)
        public Room? RoomA_Ref { get; set; }         // Riferimento all'oggetto Room A
        public Room? RoomB_Ref { get; set; }         // Riferimento all'oggetto Room B
    }
}
