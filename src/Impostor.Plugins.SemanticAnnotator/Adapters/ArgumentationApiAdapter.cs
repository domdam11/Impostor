using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Impostor.Plugins.SemanticAnnotator.Ports;

namespace Impostor.Plugins.SemanticAnnotator.Adapters
{
    public class ArgumentationApiAdapter : IArgumentationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ArgumentationApiAdapter> _logger;

        public ArgumentationApiAdapter(HttpClient httpClient, ILogger<ArgumentationApiAdapter> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> SendAnnotationsAsync(string annotations)
        {
            //string url = "http://127.0.0.1:18080/update";

            try
            {
                var content = new StringContent(annotations, Encoding.UTF8);
                var response = await _httpClient.PostAsync("update", content);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ArgumentationApiAdapter] Errore nella richiesta.");
                return null;
            }
        }
    }
}
