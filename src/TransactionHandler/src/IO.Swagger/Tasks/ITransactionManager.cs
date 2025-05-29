using System.Threading.Tasks;

namespace TransactionHandler.Tasks
{
    public interface ITransactionManager
    {
        /// <summary>
        /// Creates a new game session as a blockchain asset.
        /// </summary>
        Task CreateGameSessionAsync(string gameId, string description);

        /// <summary>
        /// Gets the details about a game session.
        /// </summary>
        Task<string> GetGameDetailsAsync(string gameId);

        /// <summary>
        /// Adds a player to a game session.
        /// </summary>
        Task AddPlayerAsync(string gameId, string playerId, string description);

        /// <summary>
        /// Removes a player from a game session.
        /// </summary>
        Task RemovePlayerAsync(string gameId, string playerId, string description);

        /// <summary>
        /// Creates an event inside a game session.
        /// </summary>
<<<<<<< Updated upstream
        Task CreateEventAsync(string gameId, string description);
=======
        Task CreateEventAsync(string gameId, string eventId, string description, string metadata);
>>>>>>> Stashed changes

        /// <summary>
        /// Updates the description of a game session.
        /// </summary>
        Task<string> UpdateDescriptionAsync(string gameId, string description);

        /// <summary>
        /// Changes the state of a game session.
        /// </summary>
        Task<string> ChangeStateAsync(string gameId, string state);

        /// <summary>
        /// Closes an active game session.
        /// </summary>
        Task EndGameSessionAsync(string gameId);

        /// <summary>
        /// Gets the details of the last event inside a game session.
        /// </summary>
        Task<string> GetEventDetailsAsync(string gameId);

        /// <summary>
        /// Gets the id of the client interacting with the API.
        /// </summary>
        Task<string> GetClientIdAsync();
    }
}
