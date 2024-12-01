using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator
{
    public class GameEventCacheManager
    {
        // Dizionario con gameCode come chiave e lista di dizionari (eventi) come valore
        private readonly Dictionary<string, List<Dictionary<string, object>>> _eventCache;

        public GameEventCacheManager()
        {
            // Inizializzazione del dizionario per la cache degli eventi
            _eventCache = new Dictionary<string, List<Dictionary<string, object>>>();
        }

        // Recupera tutte le sessioni attive basate sui GameCode
        public List<string> GetActiveSessions()
        {
            // Restituisce solo le chiavi (gameCode) delle sessioni attive
            return new List<string>(_eventCache.Keys);
        }

        // Metodo per aggiungere un evento per un determinato gioco
        public async Task AddEventAsync(string gameCode, Dictionary<string, object> eventData)
        {
            // Se la chiave gameCode non esiste, la crea
            if (!_eventCache.ContainsKey(gameCode))
            {
                _eventCache[gameCode] = new List<Dictionary<string, object>>();
            }

            // Aggiunge l'evento alla lista corrispondente al gameCode
            _eventCache[gameCode].Add(eventData);
            await Task.CompletedTask;
        }

        // Metodo per ottenere tutti gli eventi per un determinato gioco in modo asincrono
        public async Task<List<Dictionary<string, object>>> GetEventsByGameCodeAsync(string gameCode)
        {
            // Controlla se esistono eventi per il gameCode specificato
            if (_eventCache.ContainsKey(gameCode))
            {
                // Restituisce la lista degli eventi per quel gameCode
                return await Task.FromResult(_eventCache[gameCode]);
            }

            // Se non ci sono eventi, restituisce una lista vuota
            return await Task.FromResult(new List<Dictionary<string, object>>());
        }

        // Metodo per ottenere tutti gli eventi per tutti i giochi in modo asincrono
        public async Task<Dictionary<string, List<Dictionary<string, object>>>> GetAllEventsAsync()
        {
            // Restituisce l'intero dizionario della cache
            return await Task.FromResult(_eventCache);
        }

        // Metodo per salvare la cache di tutti i giochi in un file JSON
        public async Task SaveAllEventsCacheAsync(string filePath)
        {
            // Serializza il dizionario in formato JSON con indentazione
            var json = JsonSerializer.Serialize(_eventCache, new JsonSerializerOptions { WriteIndented = true });
            // Scrive il JSON nel file specificato
            await File.WriteAllTextAsync(filePath, json);
        }

        // Metodo per caricare la cache di tutti i giochi da un file JSON
        public async Task LoadAllEventsCacheAsync(string filePath)
        {
            // Verifica se il file esiste
            if (File.Exists(filePath))
            {
                // Legge il contenuto del file
                var json = await File.ReadAllTextAsync(filePath);
                // Deserializza il contenuto in un dizionario
                var loadedCache = JsonSerializer.Deserialize<Dictionary<string, List<Dictionary<string, object>>>>(json);

                // Aggiunge i dati deserializzati alla cache esistente
                if (loadedCache != null)
                {
                    foreach (var entry in loadedCache)
                    {
                        if (!_eventCache.ContainsKey(entry.Key))
                        {
                            _eventCache[entry.Key] = entry.Value;
                        }
                        else
                        {
                            _eventCache[entry.Key].AddRange(entry.Value);
                        }
                    }
                }
            }
        }

        // Metodo per cancellare gli eventi di un determinato gioco in modo asincrono
        public async Task ClearGameEventsAsync(string gameCode)
        {
            // Verifica se esistono eventi per il gameCode e li rimuove
            if (_eventCache.ContainsKey(gameCode))
            {
                _eventCache[gameCode].Clear();
            }
            await Task.CompletedTask;
        }
    }
}
