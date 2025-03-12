using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using cowl;
using Impostor.Plugins.SemanticAnnotator.Annotator;

namespace Impostor.Plugins.SemanticAnnotator.Models
{
    public class Player
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string State { get; set; }
        public CowlClass Cls { get; set; }
        public string SessionCls { get; set; }
        public List<CustomMovement> Movements { get; set; }
        public int VoteCount { get; set; }
        public List<CowlObjHasValue> objHasValueRestrictionsPlayer { get; set; }
        public List<CowlObjQuant> objQuantRestrictionsPlayer { get; set; }
        public List<CowlDataQuant> dataQuantRestrictionsPlayer { get; set; }

        public Player(string id, string name, CowlClass cls, string sesCls, List<CustomMovement> movements, CustomMovement initialMov, string state, int voteCount)
        {
            Id = id;
            Name = name.Replace(" ", "");
            State = state;
            Cls = cls; //class of the player
            SessionCls = sesCls; //class of the player for the session
            if (movements.Count() == 0)
            {
                Movements = new List<CustomMovement> { initialMov };
            }
            else
            {
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
            VoteCount++; // Increment vote count
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
}
