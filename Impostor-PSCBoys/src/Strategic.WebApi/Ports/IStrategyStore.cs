namespace Strategic.WebApi.Ports
{
    using Strategic.WebApi.Models;

    public interface IStrategyStore
    {
        /// <summary>Restituisce tutte le strategie correnti.</summary>
        Task<IReadOnlyList<Strategy>> GetAllAsync();

        /// <summary>Sostituisce l’elenco di strategie correnti (operazione autoritativa).</summary>
        Task SaveAllAsync(IEnumerable<Strategy> strategies);

        /// <summary>Restituisce una singola strategia per id, se esiste.</summary>
        Task<Strategy?> GetByIdAsync(string id);

        /// <summary>Aggiunge o aggiorna una singola strategia (upsert).</summary>
        Task UpsertAsync(Strategy strategy);

        /// <summary>Elimina una strategia per id.</summary>
        Task<bool> DeleteAsync(string id);
    }
}
