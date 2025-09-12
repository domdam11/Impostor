using Strategic.WebApi.Models;

namespace Strategic.WebApi.Ports
{
    public interface ICorrectionStore
    {
        Task<Correction?> GetCorrectionAsync(string sessionId, string eventId, string userId);
        Task<bool> UpsertCorrectionAsync(Correction c);
        Task<bool> DeleteCorrectionAsync(string sessionId, string eventId, string userId);
    }
}
