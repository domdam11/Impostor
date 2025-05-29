using Impostor.Api.Events;
using Impostor.Api.Events.Meeting;
using Microsoft.Extensions.Logging;
using Impostor.Plugins.SemanticAnnotator.Annotator;

namespace Impostor.Plugins.Example.Handlers
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
        public void OnMeetingStarted(IMeetingStartedEvent e)
        {
            _logger.LogInformation("Meeting > started");
            // add event in order to annotate
            _eventCacheManager.SaveEvent(e.Game.Code, e);
        }

        [EventListener]
        public void OnMeetingEnded(IMeetingEndedEvent e)
        {
            _logger.LogInformation("Meeting > ended, exiled: {exiled}, tie: {tie}", e.Exiled?.PlayerInfo.PlayerName, e.IsTie);
            // add event in order to annotate
            _eventCacheManager.SaveEvent(e.Game.Code, e);

            foreach (var playerState in e.MeetingHud.PlayerStates)
            {
                if (playerState.IsDead)
                {
                    _logger.LogInformation("- {player} is dead", playerState.TargetPlayer.PlayerName);
                }
                else
                {
                    _logger.LogInformation("- {player} voted for {voteType} {votedFor}", playerState.TargetPlayer.PlayerName, playerState.VoteType, playerState.VotedFor?.PlayerInfo.PlayerName);
                }
            }
        }
    }
}
