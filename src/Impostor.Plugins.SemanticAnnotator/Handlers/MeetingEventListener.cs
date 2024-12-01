using Impostor.Api.Games;
using Impostor.Api.Events;
using Impostor.Api.Events.Meeting;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Handlers
{
    public class MeetingEventListener : IEventListener
    {
        private readonly ILogger<MeetingEventListener> _logger;
        private readonly GameEventCacheManager _eventCacheManager;

        public MeetingEventListener(ILogger<MeetingEventListener> logger, GameEventCacheManager eventCacheManager)
        {
            _logger = logger;
            _eventCacheManager = eventCacheManager;
        }

        [EventListener]
        public async Task OnMeetingStarted(IMeetingStartedEvent e)
        {
            _logger.LogInformation("Meeting > started");

            // Crea un dizionario per rappresentare le informazioni sull'evento
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Usa un GUID come EventId
                { "GameCode", e.Game.Code },                // Codice del gioco
                { "EventType", "MeetingStarted" },          // Tipo di evento
                { "Timestamp", DateTime.UtcNow }           // Timestamp dell'evento
            };

            // Salva l'evento nella cache
            await _eventCacheManager.AddEventAsync(e.Game.Code, eventData);
        }

        [EventListener]
        public async Task OnMeetingEnded(IMeetingEndedEvent e)
        {
            _logger.LogInformation("Meeting > ended, exiled: {exiled}, tie: {tie}",
                                    e.Exiled?.PlayerInfo.PlayerName, e.IsTie);

            // Crea un dizionario per rappresentare le informazioni sull'evento
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Usa un GUID come EventId
                { "GameCode", e.Game.Code },                // Codice del gioco
                { "EventType", "MeetingEnded" },            // Tipo di evento
                { "Timestamp", DateTime.UtcNow },           // Timestamp dell'evento
                { "Exiled", e.Exiled?.PlayerInfo.PlayerName}, 
                { "IsTie", e.IsTie}
            };

            // Salva l'evento nella cache
            await _eventCacheManager.AddEventAsync(e.Game.Code, eventData);

            // Log dei giocatori coinvolti nel meeting
            foreach (var playerState in e.MeetingHud.PlayerStates)
            {
                if (playerState.IsDead)
                {
                    _logger.LogInformation("- {player} is dead", playerState.TargetPlayer.PlayerName);

                    // Aggiungi il registro nella cache per il giocatore morto
                    var deadPlayerEventData = new Dictionary<string, object>
                    {
                        { "EventId", Guid.NewGuid().ToString() },  // Usa un GUID come EventId
                        { "GameCode", e.Game.Code },                // Codice del gioco
                        { "EventType", "PlayerDead" },              // Tipo di evento
                        { "Timestamp", DateTime.UtcNow },           // Timestamp dell'evento
                        { "Playerdead", playerState.TargetPlayer.PlayerName } 
                    };
                    await _eventCacheManager.AddEventAsync(e.Game.Code, deadPlayerEventData);
                }
                else
                {
                    _logger.LogInformation("- {player} voted for {voteType} {votedFor}",
                                            playerState.TargetPlayer.PlayerName,
                                            playerState.VoteType,
                                            playerState.VotedFor?.PlayerInfo.PlayerName);

                    // Aggiungi il registro nella cache per il voto
                    var voteEventData = new Dictionary<string, object>
                    {
                        { "EventId", Guid.NewGuid().ToString() },  // Usa un GUID come EventId
                        { "GameCode", e.Game.Code },                // Codice del gioco
                        { "EventType", "PlayerVote" },              // Tipo di evento
                        { "Timestamp", DateTime.UtcNow },           // Timestamp dell'evento
                        { "PlayerVoter", playerState.TargetPlayer.PlayerName },  
                        { "VoteType", playerState.VoteType },
                        { "PlayerVoted", playerState.VotedFor?.PlayerInfo.PlayerName }
                    };
                    await _eventCacheManager.AddEventAsync(e.Game.Code, voteEventData);
                }
            }
        }
    }
}
