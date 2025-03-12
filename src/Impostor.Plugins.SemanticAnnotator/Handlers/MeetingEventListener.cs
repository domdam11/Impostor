using Impostor.Api.Games;
using Impostor.Api.Events;
using Impostor.Api.Events.Meeting;
using Impostor.Plugins.SemanticAnnotator;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Microsoft.Extensions.Logging;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Impostor.Plugins.SemanticAnnotator.Handlers
{
    /// <summary>
    /// Event listener for handling meeting-related events during the game.
    /// It tracks the start and end of meetings, votes, and player eliminations.
    /// </summary>
    public class MeetingEventListener : IEventListener
    {
        private readonly ILogger<MeetingEventListener> _logger;
        private readonly GameEventCacheManager _eventCacheManager;

        public MeetingEventListener(ILogger<MeetingEventListener> logger, GameEventCacheManager eventCacheManager)
        {
            _logger = logger;
            _eventCacheManager = eventCacheManager;
        }

        /// <summary>
        /// Handles the event when a meeting starts.
        /// This function logs the meeting start and updates the game state.
        /// </summary>
        [EventListener]
        public async ValueTask OnMeetingStarted(IMeetingStartedEvent e)
        {
            _logger.LogInformation("Meeting > started");

            // Create a dictionary to store event details
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Use a GUID as a unique EventId
                { "GameCode", e.Game.Code },                // Store the game code
                { "EventType", "MeetingStarted" },          // Specify the type of event
                { "Timestamp", DateTime.UtcNow }           // Capture the event timestamp in UTC
            };

            // Store the event data in the cache
            await _eventCacheManager.AddEventAsync(e.Game.Code, eventData);

            // Update the game state to reflect that a meeting is in progress
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code);
            if (gameState != null)
            {
                gameState.GameStateName = "meeting";
                await _eventCacheManager.UpdateGameStateAsync(e.Game.Code, gameState);
            }
        }

        /// <summary>
        /// Handles the event when a meeting ends.
        /// It logs the meeting outcome, tracks votes, and updates player states accordingly.
        /// </summary>
        [EventListener]
        public async ValueTask OnMeetingEnded(IMeetingEndedEvent e)
        {
            _logger.LogInformation("Meeting > ended, exiled: {exiled}, tie: {tie}",
                                    e.Exiled?.PlayerInfo.PlayerName, e.IsTie);

            // Create a dictionary to store event details
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Use a GUID as a unique EventId
                { "GameCode", e.Game.Code },                // Store the game code
                { "EventType", "MeetingEnded" },            // Specify the type of event
                { "Timestamp", DateTime.UtcNow },           // Capture the event timestamp in UTC
                { "Exiled", e.Exiled?.PlayerInfo.PlayerName}, // Store the name of the exiled player, if any
                { "IsTie", e.IsTie}                         // Indicate whether the vote resulted in a tie
            };

            // Store the event data in the cache
            await _eventCacheManager.AddEventAsync(e.Game.Code, eventData);

            // Log the state of all players involved in the meeting
            foreach (var playerState in e.MeetingHud.PlayerStates)
            {
                if (playerState.IsDead)
                {
                    _logger.LogInformation("- {player} is dead", playerState.TargetPlayer.PlayerName);

                    // Store event details for the dead player
                    var deadPlayerEventData = new Dictionary<string, object>
                    {
                        { "EventId", Guid.NewGuid().ToString() },  // Use a GUID as a unique EventId
                        { "GameCode", e.Game.Code },                // Store the game code
                        { "EventType", "PlayerDead" },              // Specify the type of event
                        { "Timestamp", DateTime.UtcNow },           // Capture the event timestamp in UTC
                        { "Playerdead", playerState.TargetPlayer.PlayerName }
                    };
                    await _eventCacheManager.AddEventAsync(e.Game.Code, deadPlayerEventData);

                    // Update the player's state in the game
                    var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code);
                    if (gameState != null)
                    {
                        var player = gameState.Players.FirstOrDefault(p => p.Name.Equals(playerState.TargetPlayer.PlayerName, StringComparison.OrdinalIgnoreCase));
                        if (player != null)
                        {
                            player.State = "dead";  // Mark the player as dead in the game state
                            await _eventCacheManager.UpdateGameStateAsync(e.Game.Code, gameState);
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("- {player} voted for {voteType} {votedFor}",
                                            playerState.TargetPlayer.PlayerName,
                                            playerState.VoteType,
                                            playerState.VotedFor?.PlayerInfo.PlayerName);

                    // Store voting event details in the cache
                    var voteEventData = new Dictionary<string, object>
                    {
                        { "EventId", Guid.NewGuid().ToString() },  // Use a GUID as a unique EventId
                        { "GameCode", e.Game.Code },                // Store the game code
                        { "EventType", "PlayerVote" },              // Specify the type of event
                        { "Timestamp", DateTime.UtcNow },           // Capture the event timestamp in UTC
                        { "PlayerVoter", playerState.TargetPlayer.PlayerName },  // Name of the player who voted
                        { "VoteType", playerState.VoteType },       // Type of vote cast
                        { "PlayerVoted", playerState.VotedFor?.PlayerInfo.PlayerName } // Name of the player voted for, if any
                    };
                    await _eventCacheManager.AddEventAsync(e.Game.Code, voteEventData);

                    // Update the vote count for the player who was voted for
                    if (playerState.VotedFor != null)
                    {
                        var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code);
                        if (gameState != null)
                        {
                            var votedPlayer = gameState.Players.FirstOrDefault(p => p.Name.Equals(playerState.VotedFor.PlayerInfo.PlayerName, StringComparison.OrdinalIgnoreCase));
                            if (votedPlayer != null)
                            {
                                votedPlayer.VoteCount++; // Increment the vote count
                                await _eventCacheManager.UpdateGameStateAsync(e.Game.Code, gameState);
                            }
                        }
                    }
                }
            }
        }
    }
}
