#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using cowl;
using CowlSharp.Wrapper;
using Location;
using Impostor.Plugins.SemanticAnnotator.Models;

namespace Impostor.Plugins.SemanticAnnotator.Annotator
{
    /// <summary>
    /// Step di annotazione per la localizzazione semantica dei player.
    /// Utilizza LocationFinding per calcolare la posizione e aggiunge restrizioni OWL ai player.
    /// </summary>
    public class LocationAnnotationStep
    {
        private readonly LocationFinding locationFinding;
        private const string NAMESPACE = "http://www.semanticweb.org/giova/ontologies/2024/5/AmongUs/";

        public LocationAnnotationStep(string mapJsonPath)
        {
            this.locationFinding = new LocationFinding(mapJsonPath);
        }

        /// <summary>
        /// Annota la localizzazione semantica per tutti i player nella lista.
        /// Aggiunge restrizioni OWL alle liste del player object.
        /// </summary>
        public void AnnotateLocations(List<Player> players, List<nint> instancesToRelease)
        {
            foreach (var player in players)
            {
                // Ottieni l'ultima posizione del player
                if (player.Movements == null || player.Movements.Count == 0)
                    continue;

                var lastMovement = player.Movements[player.Movements.Count - 1];
                Vector2 position = lastMovement.Position;

                // Usa LocationFinding per calcolare la localizzazione
                LocationResult result = locationFinding.FindLocation(player.Id.ToString(), position);

                // Aggiungi restrizioni alle liste del player
                AddLocationRestrictions(player, result, instancesToRelease);
            }
        }

        /// <summary>
        /// Crea e aggiunge le restrizioni OWL di localizzazione alle liste del player.
        /// </summary>
        private void AddLocationRestrictions(
            Player player,
            LocationResult locationResult,
            List<nint> instancesToRelease)
        {
            // IRI delle proprietà oggetto
            string isInRoomPropIri = NAMESPACE + "IsInRoom";
            string hasContextualPositionPropIri = NAMESPACE + "HasContextualPosition";
            string hasConfidenceLevelPropIri = NAMESPACE + "HasConfidenceLevel";

            // IRI delle classi target
            string roomClassIri = NAMESPACE + locationResult.RoomName;
            string contextualPosClassIri = NAMESPACE + locationResult.SemanticLabel;
            string confidenceClassIri = NAMESPACE + GetConfidenceLevelClass(locationResult.ConfidenceScore);

            // Crea restrizioni OWL (allValuesFrom)
            var isInRoomRestr = CowlWrapper.CreateAllValuesRestriction(
                isInRoomPropIri,
                new[] { roomClassIri },
                instancesToRelease
            );

            var hasContextualPositionRestr = CowlWrapper.CreateAllValuesRestriction(
                hasContextualPositionPropIri,
                new[] { contextualPosClassIri },
                instancesToRelease
            );

            var hasConfidenceLevelRestr = CowlWrapper.CreateAllValuesRestriction(
                hasConfidenceLevelPropIri,
                new[] { confidenceClassIri },
                instancesToRelease
            );

            // Aggiungi restrizioni alla lista del player
            player.objQuantRestrictionsPlayer.Add(isInRoomRestr);
            player.objQuantRestrictionsPlayer.Add(hasContextualPositionRestr);
            player.objQuantRestrictionsPlayer.Add(hasConfidenceLevelRestr);

            // Log per debug
            Console.WriteLine($"[LocationAnnotationStep] Player {player.Name}: {locationResult.RoomName} | {locationResult.SemanticLabel} | Confidence: {locationResult.ConfidenceScore:F2}");
        }

        /// <summary>
        /// Converte il punteggio di confidenza numerico in nome classe OWL.
        /// </summary>
        private string GetConfidenceLevelClass(float confidenceScore)
        {
            if (confidenceScore >= 0.90f)
                return "HighConfidence";
            if (confidenceScore >= 0.75f)
                return "MediumConfidence";
            return "LowConfidence";
        }
    }
}
