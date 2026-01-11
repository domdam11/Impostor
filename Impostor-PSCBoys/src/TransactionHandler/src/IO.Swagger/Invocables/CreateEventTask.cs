using System;
using System.Threading.Tasks;
using Coravel.Invocable;
using IO.Swagger.Tasks;
using TransactionHandler.Tasks;

namespace TransactionHandler.Invocables
{
    public class CreateEventTask : IInvocable, IInvocableWithPayload<(string gameId, string eventId, string description, string metadata)>
    {
        private readonly ITransactionManager _transactionManager;
        private readonly TaskControlService _taskControlService;

        // Inject the TransactionManager via constructor
        public CreateEventTask(ITransactionManager transactionManager, TaskControlService taskControlService)
        {
            _transactionManager = transactionManager;
            _taskControlService = taskControlService;
        }

        // Define a Payload to pass game parameters
        public (string gameId, string eventId, string description, string metadata) Payload { get; set; }

        // Define Invoke method required by IInvocable
        public async Task Invoke()
        {
            // Ensure parameters are set before invoking the task
            if (string.IsNullOrEmpty(Payload.gameId) || string.IsNullOrEmpty(Payload.description))
            {
                throw new InvalidOperationException("game id and description must be set before invoking the task.");
            }

            await _taskControlService.Semaphore.WaitAsync();
            try
            {
                await _transactionManager.CreateEventAsync(Payload.gameId, Payload.eventId, Payload.description, Payload.metadata);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in CreateEventTask: {ex.Message}");
            }
            finally
            {
                _taskControlService.Semaphore.Release();
            }
        }
    }
}
