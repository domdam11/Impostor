using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Strategic.WebApi.Models
{
    /// <summary>
    /// Dati utente per auth “leggera” (ruolo + sessionKey).
    /// Viene serializzato su Redis come JSON con campi id/role/sessionKey in camelCase.
    /// </summary>
    public sealed class UserAuth
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;          // es. "user42"

        [JsonPropertyName("role")]
        public string Role { get; set; } = "user";          // "user" | "admin"

        [JsonPropertyName("sessionKey")]
        public string SessionKey { get; set; } = default!;  // chiave segreta generata lato server

        [JsonPropertyName("createdAt")]
        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
    }
}
