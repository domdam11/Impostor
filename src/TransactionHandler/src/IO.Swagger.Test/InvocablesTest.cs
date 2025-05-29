using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TransactionHandler.Invocables;
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
using TransactionHandler.Tasks;
using System.Threading;
using IO.Swagger.Tasks;

namespace IO.Swagger.Test
{
    [TestFixture]
    public class InvocablesTest
    {
        private TransactionManager _transactionManager;
        private IServiceProvider _serviceProvider;
        private IQueue _queue;
        private IScheduler _scheduler;
        private string _gameId;
<<<<<<< Updated upstream
        private string _event1 = "Prefix(:=<http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/>) Prefix(xsd:=<http://www.w3.org/2001/XMLSchema#>) Prefix(owl:=<http://www.w3.org/2002/07/owl#>) Prefix(rdfs:=<http://www.w3.org/2000/01/rdf-schema#>) Ontology(ClassAssertion(ObjectIntersectionOf(:CrewMateAlive ObjectAllValuesFrom(:Calls :EmergencyCall) ObjectHasValue(:Reports :Stormynest) ObjectHasValue(:GetCloseTo :Retroindex) ObjectHasValue(:IsInFOV :Retroindex) ObjectHasValue(:IsInFOV :Fallpalmy) DataAllValuesFrom(:HasNPlayersInFOV DataOneOf(\"2\"^^xsd:integer)) DataAllValuesFrom(:HasCoordinates DataOneOf(\"<0,80169046. 1,4780272>\"))) :AldoMoro))";
        private string _event2 = "Prefix(:=<http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/>) Prefix(xsd:=<http://www.w3.org/2001/XMLSchema#>) Prefix(owl:=<http://www.w3.org/2002/07/owl#>) Prefix(rdfs:=<http://www.w3.org/2000/01/rdf-schema#>) Ontology(ClassAssertion(ObjectIntersectionOf(:CrewMateAlive :CrewMateDead) :AldoMoro))";
=======
        private string _eventId;
        private string _event1 = "Prefix(:=<http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/>) Prefix(xsd:=<http://www.w3.org/2001/XMLSchema#>) Prefix(owl:=<http://www.w3.org/2002/07/owl#>) Prefix(rdfs:=<http://www.w3.org/2000/01/rdf-schema#>) Ontology(ClassAssertion(ObjectIntersectionOf(:CrewMateAlive ObjectAllValuesFrom(:Calls :EmergencyCall) ObjectHasValue(:Reports :Stormynest) ObjectHasValue(:GetCloseTo :Retroindex) ObjectHasValue(:IsInFOV :Retroindex) ObjectHasValue(:IsInFOV :Fallpalmy) DataAllValuesFrom(:HasNPlayersInFOV DataOneOf(\"2\"^^xsd:integer)) DataAllValuesFrom(:HasCoordinates DataOneOf(\"<0,80169046. 1,4780272>\"))) :AldoMoro))";
        private string _event2 = "Prefix(:=<http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/>) Prefix(xsd:=<http://www.w3.org/2001/XMLSchema#>) Prefix(owl:=<http://www.w3.org/2002/07/owl#>) Prefix(rdfs:=<http://www.w3.org/2000/01/rdf-schema#>) Ontology(ClassAssertion(ObjectIntersectionOf(:CrewMateAlive :CrewMateDead) :AldoMoro))";
        private string _metadata = "";
>>>>>>> Stashed changes

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
            services.AddSingleton<TaskControlService>();
            services.AddQueue();
            services.AddScheduler();
            services.AddTransient<CreateGameSessionTask>();
            services.AddTransient<GetGameDetailsTask>();
            services.AddTransient<EndGameSessionTask>();
            services.AddTransient<AddPlayerTask>();
            services.AddTransient<RemovePlayerTask>();
            services.AddTransient<CreateEventTask>();

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
            _queue.QueueInvocableWithPayload<CreateGameSessionTask, (string gameId, string description)>((_gameId, "Test game session"));

            // Get game session details every 30 seconds
            _scheduler.ScheduleAsync(async () =>
            {
                // Get game details
                _queue.QueueInvocableWithPayload<GetGameDetailsTask, string>(_gameId);
            })
            .EveryThirtySeconds();

            // Add players
            for (int i = 0; i < 4; i++)
            {
                var j = i;
                _queue.QueueInvocableWithPayload<AddPlayerTask, (string gameId, string playerId, string description)>((_gameId, $"p{j + 1}", $"Player {j + 1}"));
            }

            // Queue an event using the annotation
<<<<<<< Updated upstream
            _queue.QueueInvocableWithPayload<CreateEventTask, (string gameId, string description)>((_gameId, _event1));
=======
            _queue.QueueInvocableWithPayload<CreateEventTask, (string gameId, string eventId, string description, string metadata)>((_gameId, _eventId, _event1, _metadata));
>>>>>>> Stashed changes

            // Remove players
            for (int i = 0; i < 4; i++)
            {
                var j = i;
                _queue.QueueInvocableWithPayload<RemovePlayerTask, (string gameId, string playerId, string description)>((_gameId, $"p{j + 1}", $"Player {j + 1}"));
            }

            // Close the game session
            _queue.QueueInvocableWithPayload<EndGameSessionTask, string>(_gameId);
        }

        [TearDown]
        public void Cleanup()
        {
            /* Not needed for now, since there is not a method to delete an asset.
             * Left here for future implementations. */
        }
    }

}
