using System.Collections.Generic;

namespace Data
{
    public class MapData
    {
        public List<Room> Rooms { get; set; } = new List<Room>();
        public List<DoorInfo> Doors { get; set; } = new List<DoorInfo>(); 
    }
}
