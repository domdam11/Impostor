using System;
using System.Threading.Tasks;
using Coravel.Invocable;
using IO.Swagger.Tasks;
using TransactionHandler.Tasks;

namespace TransactionHandler.Invocables
{
    public class AddPlayerTask : IInvocable, IInvocableWithPayload<(string gameId, string playerId, string description)>
    {
        private readonly ITransactionManager _transactionManager;
        private readonly TaskControlService _taskControlService;

        // Inject the TransactionManager via constructor
        public AddPlayerTask(ITransactionManager transactionManager, TaskControlService taskControlService)
        {
            _transactionManager = transactionManager;
            _taskControlService = taskControlService;
        }

        // Define a Payload to pass game parameters
        public (string gameId, string playerId, string description) Payload { get; set; }

        // Define Invoke method required by IInvocable
        public async Task Invoke()
        {
            // Ensure parameters are set before invoking the task
            if (string.IsNullOrEmpty(Payload.gameId) || string.IsNullOrEmpty(Payload.playerId) || string.IsNullOrEmpty(Payload.description))
            {
                throw new InvalidOperationException("game id, player id and description must be set before invoking the task.");
            }

            await _taskControlService.Semaphore.WaitAsync();
            try
            {
                await _transactionManager.AddPlayerAsync(Payload.gameId, Payload.playerId, Payload.description);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in AddPlayerTask: {ex.Message}");
            }
            finally
            {
                _taskControlService.Semaphore.Release();
            }
        }
    }
}
