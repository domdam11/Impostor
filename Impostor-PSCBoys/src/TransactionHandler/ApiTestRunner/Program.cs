using Coravel;
using Coravel.Scheduling.Schedule.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Coravel.Queuing.Interfaces;
using TransactionHandler.Tasks;
using TransactionHandler.Invocables;
using IO.Swagger.Api;
using IO.Swagger.Client;
using IO.Swagger.Model;
using IO.Swagger.Tasks;

namespace ApiTestRunner
{
    class Program
    {
        static async Task Main(string[] args)
        {

            // Define game settings
            SemaphoreSlim _taskLock = new(1, 1);
            var gameId = Guid.NewGuid().ToString();
            int n_players = 4;
            string event1 = "Prefix(:=<http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/>) Prefix(xsd:=<http://www.w3.org/2001/XMLSchema#>) Prefix(owl:=<http://www.w3.org/2002/07/owl#>) Prefix(rdfs:=<http://www.w3.org/2000/01/rdf-schema#>) Ontology(ClassAssertion(ObjectIntersectionOf(:CrewMateAlive ObjectAllValuesFrom(:Calls :EmergencyCall) ObjectHasValue(:Reports :Stormynest) ObjectHasValue(:GetCloseTo :Retroindex) ObjectHasValue(:IsInFOV :Retroindex) ObjectHasValue(:IsInFOV :Fallpalmy) DataAllValuesFrom(:HasNPlayersInFOV DataOneOf(\"2\"^^xsd:integer)) DataAllValuesFrom(:HasCoordinates DataOneOf(\"<0,80169046. 1,4780272>\"))) :AldoMoro))";
            string event2 = "Prefix(:=<http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/>) Prefix(xsd:=<http://www.w3.org/2001/XMLSchema#>) Prefix(owl:=<http://www.w3.org/2002/07/owl#>) Prefix(rdfs:=<http://www.w3.org/2000/01/rdf-schema#>) Ontology(ClassAssertion(ObjectIntersectionOf(:CrewMateAlive :CrewMateDead) :AldoMoro))";

            /* -------- TESTS --------
             * There are currently 2 types of tests. Decomment the one you want to try
             * and comment the others, then run the code.


            /* -------- TEST 1 - Test with asynchronous tasks --------
             * Here I use the queue to queue all the tasks asynchronously.
             * To make sure that the tasks in the queue are not processed all at the same time,
             * I use a semaphore.
             * ---- DECOMMENT BELOW ---- */

            //// Create the host builder
            //var host = Host.CreateDefaultBuilder(args)
            //    .ConfigureServices((context, services) =>
            //    {
            //        // Configure the API client
            //        var config = new Configuration
            //        {
            //            BasePath = "http://localhost:8080"
            //        };
            //        var apiInstance = new BlockchainReSTAPIApi(config);

            //        // Register dependencies
            //        services.AddSingleton(apiInstance);
            //        services.AddSingleton<ITransactionManager, TransactionManager>();
            //        services.AddScheduler();
            //        services.AddQueue();
            //    })
            //    .Build();

            //// Get transaction manager and coravel queue from registered host services
            //var transactionManager = host.Services.GetRequiredService<ITransactionManager>();
            //var queue = host.Services.GetRequiredService<IQueue>();
            //var scheduler = host.Services.GetRequiredService<IScheduler>();

            //// Get the client id
            //queue.QueueAsyncTask(async () =>
            //{
            //    await _taskLock.WaitAsync();
            //    try
            //    {
            //        await transactionManager.GetClientIdAsync();
            //    }
            //    finally
            //    {
            //        _taskLock.Release();
            //    }
            //});

            //// Create a game session
            //queue.QueueAsyncTask(async () =>
            //{
            //    await _taskLock.WaitAsync();
            //    try
            //    {
            //        await transactionManager.CreateGameSessionAsync(gameId, "Test Game Session");
            //    }
            //    finally
            //    {
            //        _taskLock.Release();
            //    }
            //});

            //// Schedule the task to get game details every 30 seconds
            //scheduler.ScheduleAsync(async () =>
            //{
            //        // Get game details
            //        queue.QueueAsyncTask(async () =>
            //        {
            //            await _taskLock.WaitAsync();
            //            try
            //            {
            //                await transactionManager.GetGameDetailsAsync(gameId);
            //            }
            //            finally
            //            {
            //                _taskLock.Release();
            //            }
            //        });
            // })
            //.EveryThirtySeconds();

            //// Add players to the game session
            //for (int i = 0; i < n_players; i++)
            //{
            //    var j = i;
            //    queue.QueueAsyncTask(async () =>
            //    {
            //        await _taskLock.WaitAsync();
            //        try
            //        {
            //            await transactionManager.AddPlayerAsync(gameId, $"p{j + 1}", $"Player {j + 1}");
            //        }
            //        finally
            //        {
            //            _taskLock.Release();
            //        }
            //    });
            //}

            //// Create an event using the example annotation
            //queue.QueueAsyncTask(async () =>
            //{
            //    await _taskLock.WaitAsync();
            //    try
            //    {
            //        await transactionManager.CreateEventAsync(gameId, event1);
            //    }
            //    finally
            //    {
            //        _taskLock.Release();
            //    }
            //});

            //// Remove players
            //for (int i = 0; i < n_players; i++)
            //{
            //    var j = i;
            //    queue.QueueAsyncTask(async () =>
            //    {
            //        await _taskLock.WaitAsync();
            //        try
            //        {
            //            await transactionManager.RemovePlayerAsync(gameId, $"p{j + 1}", $"Player {j + 1}");
            //        }
            //        finally
            //        {
            //            _taskLock.Release();
            //        }
            //    });
            //}

            //// Close the game session
            //queue.QueueAsyncTask(async () =>
            //{
            //    await _taskLock.WaitAsync();
            //    try
            //    {
            //        await transactionManager.EndGameSessionAsync(gameId);
            //    }
            //    finally
            //    {
            //        _taskLock.Release();
            //    }
            //});

            //await host.RunAsync();


            /* -------- TEST 2 - Test with coravel invocable tasks --------
             * Here I queue and schedule the different tasks as coravel invocables, which
             * is the approach suggested by the coravel documentation.
             * Also here, I use the semaphore to make sure that all the tasks are not
             * executed at the same time.
             * ---- DECOMMENT BELOW ---- */

            //// Create the host builder
            //var host = Host.CreateDefaultBuilder(args)
            //    .ConfigureServices((context, services) =>
            //    {
            //        // Configure the API client
            //        var config = new Configuration
            //        {
            //            BasePath = "http://localhost:8080"
            //        };
            //        var apiInstance = new BlockchainReSTAPIApi(config);

            //        // Register dependencies and invocables
            //        services.AddSingleton(apiInstance);
            //        services.AddSingleton<ITransactionManager, TransactionManager>();
            //        services.AddSingleton<TaskControlService>();
            //        services.AddScheduler();
            //        services.AddQueue();
            //        services.AddTransient<GetClientIdTask>();
            //        services.AddTransient<CreateGameSessionTask>();
            //        services.AddTransient<GetGameDetailsTask>();
            //        services.AddTransient<EndGameSessionTask>();
            //        services.AddTransient<AddPlayerTask>();
            //        services.AddTransient<RemovePlayerTask>();
            //        services.AddTransient<CreateEventTask>();
            //    })
            //    .Build();

            //// Get transaction manager, coravel queue and scheduler from registered services
            //var transactionManager = host.Services.GetRequiredService<ITransactionManager>();
            //var queue = host.Services.GetRequiredService<IQueue>();
            //var scheduler = host.Services.GetRequiredService<IScheduler>();

            //// Get the client id
            //queue.QueueInvocable<GetClientIdTask>();

            //// Create a game session
            //queue.QueueInvocableWithPayload<CreateGameSessionTask, (string gameId, string description)>((gameId, "Example game session"));

            //// Schedule the task to get game details every 30 seconds
            //scheduler.ScheduleAsync(async () =>
            //{
            //    // Get game details
            //    queue.QueueInvocableWithPayload<GetGameDetailsTask, string>(gameId);
            //})
            //.EveryThirtySeconds();

            //// Add players to the game session
            //for (int i = 0; i < n_players; i++)
            //{
            //    var j = i;
            //    queue.QueueInvocableWithPayload<AddPlayerTask, (string gameId, string playerId, string description)>((gameId, $"p{j + 1}", $"Player {j + 1}"));
            //}

            //// Create an event using the example correct annotation
            //queue.QueueInvocableWithPayload<CreateEventTask, (string gameId, string description)>((gameId, event1));

            //// Try to create an event using the example wrong annotation
            //// -- DECOMMENT BELOW
            ////queue.QueueInvocableWithPayload<CreateEventTask, (string gameId, string description)>((gameId, event2));

            //// Remove players from the game session
            //for (int i = 0; i < n_players; i++)
            //{
            //    var j = i;
            //    queue.QueueInvocableWithPayload<RemovePlayerTask, (string gameId, string playerId, string description)>((gameId, $"p{j + 1}", $"Player {j + 1}"));
            //}

            //// Close the game session
            //queue.QueueInvocableWithPayload<EndGameSessionTask, string>(gameId);
            //await host.RunAsync();


        }
    }
}