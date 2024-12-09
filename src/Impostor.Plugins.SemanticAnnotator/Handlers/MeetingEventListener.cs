using Impostor.Api.Events;
using Impostor.Api.Events.Meeting;
using Microsoft.Extensions.Logging;
<<<<<<< HEAD
=======
using Impostor.Plugins.SemanticAnnotator.Annotator;
>>>>>>> 78f1e2eb8a16ecbc059c7d2e709b50a9de97723d

namespace Impostor.Plugins.Example.Handlers
{
    public class MeetingEventListener : IEventListener
    {
        private readonly ILogger<MeetingEventListener> _logger;

        public MeetingEventListener(ILogger<MeetingEventListener> logger)
        {
            _logger = logger;
        }

        [EventListener]
        public void OnMeetingStarted(IMeetingStartedEvent e)
        {
            _logger.LogInformation("Meeting > started");
<<<<<<< HEAD
=======
            // add event in order to annotate
            EventUtility.SaveEvent(e);
>>>>>>> 78f1e2eb8a16ecbc059c7d2e709b50a9de97723d
        }

        [EventListener]
        public void OnMeetingEnded(IMeetingEndedEvent e)
        {
            _logger.LogInformation("Meeting > ended, exiled: {exiled}, tie: {tie}", e.Exiled?.PlayerInfo.PlayerName, e.IsTie);
<<<<<<< HEAD
=======
            // add event in order to annotate
            EventUtility.SaveEvent(e);
>>>>>>> 78f1e2eb8a16ecbc059c7d2e709b50a9de97723d

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
