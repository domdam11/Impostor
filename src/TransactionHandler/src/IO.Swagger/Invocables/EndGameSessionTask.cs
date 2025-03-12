using System;
using System.Threading.Tasks;
using Coravel.Invocable;
using IO.Swagger.Tasks;

namespace TransactionHandler.Tasks
{
    public class EndGameSessionTask : IInvocable, IInvocableWithPayload<string>
    {
        private readonly ITransactionManager _transactionManager;
        private readonly TaskControlService _taskControlService;

        // Inject the TransactionManager via constructor
        public EndGameSessionTask(ITransactionManager transactionManager, TaskControlService taskControlService)
        {
            _transactionManager = transactionManager;
            _taskControlService = taskControlService;
        }

        // Define a Payload to pass game parameters
        public string Payload { get; set; }

        // Define Invoke method required by IInvocable
        public async Task Invoke()
        {

            // Ensure parameters are set before invoking the task
            if (string.IsNullOrEmpty(Payload))
            {
                throw new InvalidOperationException("game id must be set before invoking the task.");
            }

            await _taskControlService.Semaphore.WaitAsync();
            try
            {
                await _transactionManager.EndGameSessionAsync(Payload);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in EndGameSessionTask: {ex.Message}");
            }
            finally
            {
                _taskControlService.Semaphore.Release();
            }
        }
    }
}
