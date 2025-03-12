using System.Threading.Tasks;
using Coravel.Invocable;
using IO.Swagger.Tasks;

namespace TransactionHandler.Tasks
{

    /* N.B. This task will be ipotetically used inside the scheduler to schedule periodically
     * the annotation. For now, it is just a template */

    public class GetAnnotationTask : IInvocable
    {
        private readonly ITransactionManager _transactionManager;
        private readonly TaskControlService _taskControlService;

        // Inject the TransactionManager via constructor
        public GetAnnotationTask(ITransactionManager transactionManager, TaskControlService taskControlService)
        {
            _transactionManager = transactionManager;
            _taskControlService = taskControlService;
        }

        // Execute method required by IInvocable
        public async Task Invoke()
        {
            //return "Prefix(:=<http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/>) Prefix(xsd:=<http://www.w3.org/2001/XMLSchema#>) Prefix(owl:=<http://www.w3.org/2002/07/owl#>) Prefix(rdfs:=<http://www.w3.org/2000/01/rdf-schema#>) Ontology(ClassAssertion(ObjectIntersectionOf(:CrewMateAlive ObjectAllValuesFrom(:Calls :EmergencyCall) ObjectHasValue(:Reports :Stormynest) ObjectHasValue(:GetCloseTo :Retroindex) ObjectHasValue(:IsInFOV :Retroindex) ObjectHasValue(:IsInFOV :Fallpalmy) DataAllValuesFrom(:HasNPlayersInFOV DataOneOf(\"2\"^^xsd:integer)) DataAllValuesFrom(:HasCoordinates DataOneOf(\"<0,80169046. 1,4780272>\"))) :AldoMoro))";
        }
    }
}
