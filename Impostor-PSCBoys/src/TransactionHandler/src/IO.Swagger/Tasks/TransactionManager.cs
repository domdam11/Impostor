using System;
using System.Threading.Tasks;
using IO.Swagger.Api;
using IO.Swagger.Model;

namespace TransactionHandler.Tasks
{
    public class TransactionManager : ITransactionManager
    {
        private readonly IBlockchainReSTAPIApi _client;

        public TransactionManager(IBlockchainReSTAPIApi client)
        {
            _client = client;
        }

        // Create a new game session
        public async Task CreateGameSessionAsync(string gameId, string description)
        {
            try
            {
                Console.WriteLine($"Creating game session {gameId}...");

                // Build the AssetDTO object
                var assetDto = new AssetDTO
                {
                    Descrizione = description,
                    Stato = AssetDTO.StatoEnum.Incorso,
                    Id = gameId
                };

                // Create the Asset passing the AssetDTO, change the description and the state
                var response1 = await _client.CreateAssetAsync(assetDto);
                Console.WriteLine($"Game session created:\n {response1}");
                var response2 = await ChangeStateAsync(gameId, AssetDTO.StatoEnum.Incorso);
                Console.WriteLine($"Game started:\n {response2}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error creating game session: {ex.Message}");
            }
        }

        // Close an active game session
        public async Task EndGameSessionAsync(string gameId)
        {
            try
            {
                Console.WriteLine($"Closing game session {gameId}... ");

                // Update description and state through the corresponding methods
                var response = await ChangeStateAsync(gameId, AssetDTO.StatoEnum.Chiuso);
                Console.WriteLine($"Game session terminated. \n{response}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error closing game session: {ex.Message}");
            }
        }

        // Get the details of a game session
        public async Task<string> GetGameDetailsAsync(string gameId)
        {
            try
            {
                Console.WriteLine($"Retrieving game details for game {gameId}...");

                // Call the API
                var response = await _client.ReadAssetAsync(gameId);
                Console.WriteLine($"Details retrieved succesfully. Response: {response}");
                return response;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error retrieving game details: {ex.Message}");
                return string.Empty;
            }
        }

        // Create a generic game event
        public async Task CreateEventAsync(string gameId, string eventId, string description, string metadata)
        {
            try
            {
                Console.WriteLine($"Creating a new event with id {eventId} in game {gameId}...");

                // Build the EventDTO object
                var eventDto = new EventDTO
                {
                    Descrizione = description,
                    Metadata = metadata
                };

                // Call the API
                await _client.CreateEventAsync(eventDto, gameId, eventId);
                Console.WriteLine($"Event created successfully.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error creating the event: {ex.Message}");
            }
        }

        // Get the details of the last event in a game session
        public async Task<string> GetEventDetailsAsync(string gameId) {
            try
            {
                Console.WriteLine($"Retrieving event details from game {gameId}...");
                // Call the API
                var response = await _client.ReadEventAsync(gameId);
                return response;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error retrieving event details: {ex.Message}");
                return string.Empty;
            }
        }

        // Add a player to a game
        public async Task AddPlayerAsync(string gameId, string playerId, string description)
        {
            try
            {
                Console.WriteLine($"Adding player {playerId} to game {gameId}...");

                // Build the PlayerDTO object
                var playerDto = new PlayerDTO
                {
                    PlayerId = playerId,
                    Descrizione = description
                };

                // Call the API
                await _client.AddPlayerAsync(playerDto, gameId);
                Console.WriteLine($"Player added successfully.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error adding the player: {ex.Message}");
            }
        }

        // Remove a player from a game
        public async Task RemovePlayerAsync(string gameId, string playerId, string description)
        {
            try
            {
                Console.WriteLine($"Removing player {playerId} from game {gameId}...");

                // Build the PlayerDTO object
                var playerDto = new PlayerDTO
                {
                    PlayerId = playerId,
                    Descrizione = description
                };

                // Call the API
                await _client.RemovePlayerAsync(playerDto, gameId);
                Console.WriteLine($"Player removed successfully.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error removing the player: {ex.Message}");
            }
        }

        // Update the description of a game
        public async Task<string> UpdateDescriptionAsync(string gameId, string description)
        {
            try
            {
                Console.WriteLine($"Updating description for game {gameId}...");

                // Build the AssetDTO object
                var assetDto = new AssetDTO
                {
                    Descrizione = description,
                    Id = gameId
                };

                // Call the API
                var response = await _client.UpdateDescriptionAsync(assetDto, gameId);
                return response;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error updating the description: {ex.Message}");
                return string.Empty;
            }
        }

        // Change the state of a game
        public async Task<string> ChangeStateAsync(string gameId, AssetDTO.StatoEnum state)
        {
            try
            {
                Console.WriteLine($"Changing state for game {gameId}...");

                // Build the AssetDTO object
                var assetDto = new AssetDTO
                {
                    Id = gameId,
                    Stato = state,
                    Descrizione = ""
                };

                // Call the API
                var response = await _client.ChangeStateAsync(assetDto, gameId);
                return response;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error changing the state: {ex.Message}");
                return string.Empty;
            }
        }

        // Get the id of the client interacting with the API
        public async Task<string> GetClientIdAsync()
        {
            try
            {
                Console.WriteLine($"Retrieving client information...");
                // Call the API
                var response = await _client.GetClientIDAsync();
                Console.WriteLine($"Information retrieved succesfully. Response: {response}");
                return response;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error retrieving the client id: {ex.Message}");
                return string.Empty;
            }
        }
    }
}

