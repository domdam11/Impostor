using Coravel.Invocable;
using System.Threading.Tasks;
using Impostor.Plugins.SemanticAnnotator.Application;

namespace Impostor.Plugins.SemanticAnnotator.Annotator
{
    public class AnnotateTask : IInvocable
    {
        private readonly AnnotationService _annotationService;

        public AnnotateTask(AnnotationService annotationService)
        {
            _annotationService = annotationService;
        }

        public Task Invoke()
        {
            return _annotationService.AnnotateAllSessionsAsync();
        }
    }
}
