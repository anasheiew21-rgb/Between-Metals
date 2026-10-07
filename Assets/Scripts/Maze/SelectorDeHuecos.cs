using System;
using System.Collections.Generic;

// Elige en que huecos del laberinto conviene poner barreras: pasos abiertos de pasillo (no el interior
// de una sala) que alguna vez se pueden cerrar sin dejar al jugador sin ruta a los puntos obligatorios,
// lejos del spawn y de la salida, y repartidos con una separacion minima. Sin dependencias de Unity.
public static class SelectorDeHuecos
{
    public struct Opciones
    {
        public int cantidad;
        public int semilla;
        public int separacionMinima;     // celdas (distancia Manhattan) entre dos barreras
        public int distanciaMinimaSpawn; // celdas entre un hueco con barrera y el spawn
        public int distanciaMinimaSalida;
        public int gradoMaximoDePasillo; // al menos una de las dos celdas del hueco tiene que tener a lo sumo este grado
    }

    // "candidatos" es cuantos huecos cumplian las condiciones antes de repartirlos.
    public static List<int> Elegir(GrafoLaberinto grafo, int spawn, int salida, IReadOnlyList<int> obligatorios, Opciones opciones, out int candidatos)
    {
        var todos = new List<int>(grafo.HuecosAbiertos());
        var mapa = new MapaSectores(grafo, todos, salida);

        var validos = new List<int>();
        foreach (int hueco in todos)
        {
            grafo.Extremos(hueco, out int a, out int b);
            if (Math.Min(grafo.Grado(a), grafo.Grado(b)) > opciones.gradoMaximoDePasillo) continue;
            if (Distancia(grafo, a, spawn) < opciones.distanciaMinimaSpawn || Distancia(grafo, b, spawn) < opciones.distanciaMinimaSpawn) continue;
            if (Distancia(grafo, a, salida) < opciones.distanciaMinimaSalida || Distancia(grafo, b, salida) < opciones.distanciaMinimaSalida) continue;
            if (!mapa.PuedeCerrar(hueco, spawn, obligatorios)) continue;

            validos.Add(hueco);
        }
        candidatos = validos.Count;

        var azar = new Random(opciones.semilla);
        for (int i = validos.Count - 1; i > 0; i--)
        {
            int j = azar.Next(i + 1);
            int tmp = validos[i];
            validos[i] = validos[j];
            validos[j] = tmp;
        }

        var elegidos = new List<int>();
        foreach (int hueco in validos)
        {
            if (elegidos.Count >= opciones.cantidad) break;

            grafo.Extremos(hueco, out int a, out _);
            bool lejos = true;
            foreach (int otro in elegidos)
            {
                grafo.Extremos(otro, out int otraCelda, out _);
                if (Distancia(grafo, a, otraCelda) < opciones.separacionMinima)
                {
                    lejos = false;
                    break;
                }
            }
            if (lejos) elegidos.Add(hueco);
        }

        return elegidos;
    }

    static int Distancia(GrafoLaberinto grafo, int celdaA, int celdaB)
    {
        return Math.Abs(grafo.FilaDe(celdaA) - grafo.FilaDe(celdaB)) + Math.Abs(grafo.ColumnaDe(celdaA) - grafo.ColumnaDe(celdaB));
    }
}
