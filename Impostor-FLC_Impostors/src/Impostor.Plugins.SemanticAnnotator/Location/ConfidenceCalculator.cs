using System;
using System.Numerics;
using Data; 
using Geometry; 
using System.Linq; 

namespace Location
{
    
    public class ConfidenceCalculator
    {
        /// Calcola un punteggio di confidenza basato sulla prossimità al centroide di una stanza.
        
        public float CalculateConfidenceScore(Vector2 position, Room room)
        {

            // Controlliamo che la stanza e i suoi collider siano validi.
            // Usiamo il primo collider per definire la forma principale della stanza.
            if (room?.Colliders == null || !room.Colliders.Any() ||
                room.Colliders[0].Points == null || !room.Colliders[0].Points.Any())
            {
                return 0.0f; // Non possiamo calcolare la confidenza senza una geometria.
            }

            // Calcoliamo il centro geometrico (centroide) della stanza.
            // Questo sarà il nostro punto di "massima confidenza".
            var roomPolygon = room.Colliders[0].Points;
            Vector2 centroid = GeometryUtils.CalculateCentroid(roomPolygon);


            // Calcoliamo la distanza diretta tra la posizione data e il centroide.
            float distanceToCenter = Vector2.Distance(position, centroid);

            // Calcolo massima distanza possibile trovando il vertice del poligono più lontano dal centroide.
            // Questo rende il calcolo adattivo alla dimensione e forma di ogni stanza.
            float maxRadius = 0.0f;
            foreach (Vector2 vertex in roomPolygon)
            {
                float distanceToVertex = Vector2.Distance(centroid, vertex);
                if (distanceToVertex > maxRadius)
                {
                    maxRadius = distanceToVertex;
                }
            }

            // Se maxRadius è 0 (es. un poligono con un solo punto), la stanza non ha dimensione.
            if (maxRadius == 0)
            {
                // Se la posizione coincide con il centro, la confidenza è massima (1), altrimenti è nulla (0).
                return distanceToCenter == 0 ? 1.0f : 0.0f;
            }

            // Normalizziamo la distanza:
            // - Se position è sul centroide, normalizedDistance sarà 0.
            // - Se position è sul bordo più lontano, normalizedDistance sarà circa 1.
            float normalizedDistance = distanceToCenter / maxRadius;



            // Vogliamo che la confidenza sia ALTA quando la distanza dal centro è BASSA.
            // - Se normalizedDistance è 0 (al centro), il punteggio è 1 (massima confidenza).
            // - Se normalizedDistance è 1 (sul bordo), il punteggio è 0 (minima confidenza).
            float confidence = Math.Max(0.0f, 1.0f - normalizedDistance);

            return confidence;
        }

    }
}

