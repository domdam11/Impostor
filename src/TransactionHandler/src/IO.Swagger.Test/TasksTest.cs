using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TransactionHandler.Tasks;
using IO.Swagger.Api;
using IO.Swagger.Client;
using IO.Swagger.Model;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Coravel;
using Coravel.Queuing.Interfaces;
using Coravel.Scheduling.Schedule.Interfaces;
using System.Transactions;
using Coravel.Scheduling.Schedule;
using TransactionManager = TransactionHandler.Tasks.TransactionManager;
using System.Threading;
using Microsoft.Extensions.Hosting;

namespace IO.Swagger.Test
{
    [TestFixture]
    public class TasksTest
    {
        private TransactionManager _transactionManager;
        private IServiceProvider _serviceProvider;
        private IQueue _queue;
        private IScheduler _scheduler;
        private readonly SemaphoreSlim _taskLock = new(1, 1);
        private string _gameId;
        private string _event1 = "Prefix(:=<http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/>) Prefix(xsd:=<http://www.w3.org/2001/XMLSchema#>) Prefix(owl:=<http://www.w3.org/2002/07/owl#>) Prefix(rdfs:=<http://www.w3.org/2000/01/rdf-schema#>) Ontology(ClassAssertion(ObjectIntersectionOf(:CrewMateAlive ObjectAllValuesFrom(:Calls :EmergencyCall) ObjectHasValue(:Reports :Stormynest) ObjectHasValue(:GetCloseTo :Retroindex) ObjectHasValue(:IsInFOV :Retroindex) ObjectHasValue(:IsInFOV :Fallpalmy) DataAllValuesFrom(:HasNPlayersInFOV DataOneOf(\"2\"^^xsd:integer)) DataAllValuesFrom(:HasCoordinates DataOneOf(\"<0,80169046. 1,4780272>\"))) :AldoMoro))";
        private string _event2 = "Prefix(:=<http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/>) Prefix(xsd:=<http://www.w3.org/2001/XMLSchema#>) Prefix(owl:=<http://www.w3.org/2002/07/owl#>) Prefix(rdfs:=<http://www.w3.org/2000/01/rdf-schema#>) Ontology(ClassAssertion(ObjectIntersectionOf(:CrewMateAlive :CrewMateDead) :AldoMoro))";

        [SetUp]
        public async Task Setup()
        {

            // Configure the API client
            var config = new Configuration
            {
                BasePath = "http://localhost:8080"
            };
            var apiInstance = new BlockchainReSTAPIApi(config);

            // Configure services
            var services = new ServiceCollection();
            services.AddSingleton(apiInstance);
            services.AddSingleton<TransactionManager>();
            services.AddQueue();
            services.AddScheduler();

            _serviceProvider = services.BuildServiceProvider();
            _queue = _serviceProvider.GetService<IQueue>();
            _scheduler = _serviceProvider.GetService<IScheduler>();
            _transactionManager = _serviceProvider.GetService<TransactionManager>();
        }

        [Test]
        public async Task GameFlowTest()
        {
            // Generate a new game id
            _gameId = Guid.NewGuid().ToString();

            // Create a game session
            _queue.QueueAsyncTask(async () =>
            {
                await _taskLock.WaitAsync();
                try
                {
                    await _transactionManager.CreateGameSessionAsync(_gameId, "Test Game Session");
                }
                finally
                {
                    _taskLock.Release();
                }
            });

            // Get game session details every 30 seconds
            _scheduler.ScheduleAsync(async () =>
            {
                _queue.QueueAsyncTask(async () =>
                {
                    await _taskLock.WaitAsync();
                    try
                    {
                        await _transactionManager.GetGameDetailsAsync(_gameId);
                    }
                    finally
                    {
                        _taskLock.Release();
                    }
                });
            })
            .EveryThirtySeconds();

            // Add players
            for (int i = 0; i < 4; i++)
            {
                var j = i;
                _queue.QueueAsyncTask(async () =>
                {
                    await _taskLock.WaitAsync();
                    try
                    {
                        await _transactionManager.AddPlayerAsync(_gameId, $"p{i + 1}", $"Player {i + 1}");
                    }
                    finally
                    {
                        _taskLock.Release();
                    }
                });
            }

            // Queue an event using the annotation
            _queue.QueueAsyncTask(async () => await _transactionManager.CreateEventAsync(_gameId, _event1));

            // Remove players
            for (int i = 0; i < 4; i++)
            {
                var j = i;
                _queue.QueueAsyncTask(async () => {
                    await _taskLock.WaitAsync();
                    try
                    {
                        await _transactionManager.RemovePlayerAsync(_gameId, $"p{j + 1}", $"Player {j + 1}");
                    }
                    finally
                    {
                        _taskLock.Release();
                    }
                });
            }

            // Close game session
            _queue.QueueAsyncTask(async () => {
                await _taskLock.WaitAsync();
                try
                {
                    await _transactionManager.EndGameSessionAsync(_gameId);
                }
                finally
                {
                    _taskLock.Release();
                }
            });
        }

        [TearDown]
        public void Cleanup()
        {
            /* Not needed for now, since there is not a method to delete an asset.
             * Left here for future implementations. */
        }
    }

}
