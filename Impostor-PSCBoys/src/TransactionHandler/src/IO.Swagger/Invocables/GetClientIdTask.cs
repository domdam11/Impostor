using System;
using System.Threading.Tasks;
using Coravel.Invocable;
using IO.Swagger.Tasks;

namespace TransactionHandler.Tasks
{
    public class GetClientIdTask : IInvocable
    {
        private readonly ITransactionManager _transactionManager;
        private readonly TaskControlService _taskControlService;

        // Inject the TransactionManager via constructor
        public GetClientIdTask(ITransactionManager transactionManager, TaskControlService taskControlService)
        {
            _transactionManager = transactionManager;
            _taskControlService = taskControlService;
        }

        // Define Invoke method required by IInvocable
        public async Task Invoke()
        {
            await _taskControlService.Semaphore.WaitAsync();
            try
            {
                await _transactionManager.GetClientIdAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in GetClientIdTask: {ex.Message}");
            }
            finally
            {
                _taskControlService.Semaphore.Release();
            }
        }
    }
}
