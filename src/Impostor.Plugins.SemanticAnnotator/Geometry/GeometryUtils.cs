using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using Data; 

namespace Geometry
{
    public static class GeometryUtils 
    {
        /// Calcola il centroide di un poligono (non auto-intersecante).
        public static Vector2 CalculateCentroid(List<Vector2> vertices)
        {
            if (vertices == null || !vertices.Any())
            {
                return Vector2.Zero;
            }
            if (vertices.Count == 1) return vertices[0];
            if (vertices.Count == 2) return new Vector2((vertices[0].X + vertices[1].X) / 2, (vertices[0].Y + vertices[1].Y) / 2);

            float accumulatedArea = 0.0f;
            float centerX = 0.0f;
            float centerY = 0.0f;

            for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
            {
                float temp = (vertices[i].X * vertices[j].Y) - (vertices[j].X * vertices[i].Y);
                accumulatedArea += temp;
                centerX += (vertices[i].X + vertices[j].X) * temp;
                centerY += (vertices[i].Y + vertices[j].Y) * temp;
            }

            if (Math.Abs(accumulatedArea) < 1E-7f)
            {
                float avgX = 0, avgY = 0;
                foreach (var v in vertices) { avgX += v.X; avgY += v.Y; }
                if (vertices.Count == 0) return Vector2.Zero; 
                return new Vector2(avgX / vertices.Count, avgY / vertices.Count);
            }

            accumulatedArea *= 3.0f;
            return new Vector2(centerX / accumulatedArea, centerY / accumulatedArea);
        }

        /// Determina se un punto è all'interno di un poligono usando l'algoritmo Ray Casting.
        public static bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3)
            {
                return false;
            }

            int polygonLength = polygon.Count;
            bool isInside = false;
            Vector2 lastVertex = polygon[polygonLength - 1];

            for (int i = 0; i < polygonLength; i++)
            {
                Vector2 currentVertex = polygon[i];
                if (point == currentVertex) return true; // Punto sul vertice

                // Ray-Casting test
                if ((currentVertex.Y < point.Y && lastVertex.Y >= point.Y) || (lastVertex.Y < point.Y && currentVertex.Y >= point.Y))
                {
                    // Calcola l'intersezione x del raggio orizzontale
                    // Evita la divisione per zero se il segmento è orizzontale e collineare con il punto Y
                    if (Math.Abs(lastVertex.Y - currentVertex.Y) > float.Epsilon)
                    {
                        if (currentVertex.X + (point.Y - currentVertex.Y) / (lastVertex.Y - currentVertex.Y) * (lastVertex.X - currentVertex.X) < point.X)
                        {
                            isInside = !isInside;
                        }
                    }
                    // Se il segmento è orizzontale e il punto Y è su di esso, controlla se X è compreso
                    else if (Math.Abs(currentVertex.Y - point.Y) < float.Epsilon)
                    {
                        if (Math.Min(currentVertex.X, lastVertex.X) <= point.X && point.X <= Math.Max(currentVertex.X, lastVertex.X))
                        {
                            return true; // Punto sul bordo orizzontale
                        }
                    }
                }
                lastVertex = currentVertex;
            }
            return isInside;
        }

    }
}
