using Strategic.WebApi.Models;

namespace Strategic.WebApi.Ports
{
    public interface IOntologyStore
    {
        Task<IReadOnlyList<OntologyVersion>> GetAllAsync();
        Task<OntologyVersion?> GetAsync(string id);
        Task<OntologyVersion> SaveAsync(string owlContent);
        Task<bool> DeleteAsync(string id);
    }
}
