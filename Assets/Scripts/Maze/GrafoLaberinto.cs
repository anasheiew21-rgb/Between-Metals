using System;
using System.Collections.Generic;

// Grafo de celdas del laberinto a partir de la grilla de paredes de MapaBuilder: (2*filas+1) x
// (2*columnas+1) tiles, las celdas en (impar, impar) y entre dos celdas vecinas un tile "hueco" que
// es pared o paso. Sin dependencias de Unity: se prueba sin escena.
public sealed class GrafoLaberinto
{
    static readonly int[] Dr = { -1, 1, 0, 0 };
    static readonly int[] Dc = { 0, 0, -1, 1 };

    readonly bool[,] pared;
    readonly int[] cola;

    public int Filas { get; }
    public int Columnas { get; }
    public int FilasGrilla { get; }
    public int ColumnasGrilla { get; }
    public int Celdas => Filas * Columnas;

    public GrafoLaberinto(bool[,] pared)
    {
        if (pared == null) throw new ArgumentNullException(nameof(pared));

        FilasGrilla = pared.GetLength(0);
        ColumnasGrilla = pared.GetLength(1);
        if (FilasGrilla < 3 || ColumnasGrilla < 3 || FilasGrilla % 2 == 0 || ColumnasGrilla % 2 == 0)
            throw new ArgumentException("La grilla debe medir (2*celdas+1) en cada eje, con al menos una celda.");

        this.pared = (bool[,])pared.Clone();
        Filas = (FilasGrilla - 1) / 2;
        Columnas = (ColumnasGrilla - 1) / 2;
        cola = new int[Celdas];
    }

    public int IdCelda(int r, int c) => r * Columnas + c;
    public int FilaDe(int idCelda) => idCelda / Columnas;
    public int ColumnaDe(int idCelda) => idCelda % Columnas;

    // Un hueco se identifica por el indice de su tile en la grilla; no cambia mientras no cambie la geometria.
    public int IdHueco(int tileFila, int tileColumna) => tileFila * ColumnasGrilla + tileColumna;

    public bool EsParedTile(int tileFila, int tileColumna) => pared[tileFila, tileColumna];

    // Tile entre dos celdas vecinas (no el perimetro ni los pilares), este abierto o cerrado.
    public bool EsTileDeHueco(int idHueco)
    {
        int tf = idHueco / ColumnasGrilla;
        int tc = idHueco % ColumnasGrilla;
        if (tf < 0 || tf >= FilasGrilla) return false;

        bool entreHorizontales = tf % 2 == 1 && tc % 2 == 0 && tc >= 2 && tc <= ColumnasGrilla - 3;
        bool entreVerticales = tf % 2 == 0 && tc % 2 == 1 && tf >= 2 && tf <= FilasGrilla - 3;
        return entreHorizontales || entreVerticales;
    }

    // Hueco por el que realmente se puede pasar entre sus dos celdas.
    public bool EsHuecoAbierto(int idHueco) => EsTileDeHueco(idHueco) && !pared[idHueco / ColumnasGrilla, idHueco % ColumnasGrilla];

    public bool Extremos(int idHueco, out int celdaA, out int celdaB)
    {
        celdaA = celdaB = -1;
        if (!EsTileDeHueco(idHueco)) return false;

        int tf = idHueco / ColumnasGrilla;
        int tc = idHueco % ColumnasGrilla;
        if (tf % 2 == 1)
        {
            celdaA = IdCelda((tf - 1) / 2, (tc - 2) / 2);
            celdaB = IdCelda((tf - 1) / 2, tc / 2);
        }
        else
        {
            celdaA = IdCelda((tf - 2) / 2, (tc - 1) / 2);
            celdaB = IdCelda(tf / 2, (tc - 1) / 2);
        }
        return true;
    }

    public IEnumerable<int> HuecosAbiertos()
    {
        for (int tf = 1; tf < FilasGrilla - 1; tf++)
        {
            for (int tc = 1; tc < ColumnasGrilla - 1; tc++)
            {
                int id = IdHueco(tf, tc);
                if (EsHuecoAbierto(id)) yield return id;
            }
        }
    }

    // Cantidad de pasos abiertos que salen de una celda (los de zonas amplias suelen dar 3 o 4).
    public int Grado(int idCelda)
    {
        int tf = 2 * FilaDe(idCelda) + 1;
        int tc = 2 * ColumnaDe(idCelda) + 1;
        int grado = 0;
        for (int d = 0; d < 4; d++)
        {
            int nf = tf + Dr[d];
            int nc = tc + Dc[d];
            if (nf < 1 || nf >= FilasGrilla - 1 || nc < 1 || nc >= ColumnasGrilla - 1) continue;
            if (!pared[nf, nc]) grado++;
        }
        return grado;
    }

    // Celda de borde cuyo muro exterior esta abierto (la salida del laberinto), o -1 si no hay.
    public int BuscarCeldaSalida()
    {
        for (int c = 0; c < Columnas; c++)
        {
            if (!pared[0, 2 * c + 1]) return IdCelda(0, c);
            if (!pared[FilasGrilla - 1, 2 * c + 1]) return IdCelda(Filas - 1, c);
        }
        for (int r = 0; r < Filas; r++)
        {
            if (!pared[2 * r + 1, 0]) return IdCelda(r, 0);
            if (!pared[2 * r + 1, ColumnasGrilla - 1]) return IdCelda(r, Columnas - 1);
        }
        return -1;
    }

    // Marca en "visitado" (de largo Celdas) todas las celdas a las que se llega desde "origen" sin
    // cruzar los huecos de "bloqueados". Reutiliza una cola interna: no es seguro entre hilos.
    public void CalcularAlcanzables(int origen, ISet<int> bloqueados, bool[] visitado)
    {
        if (visitado == null || visitado.Length != Celdas) throw new ArgumentException("visitado debe tener un elemento por celda.");
        if (origen < 0 || origen >= Celdas) throw new ArgumentOutOfRangeException(nameof(origen));

        Array.Clear(visitado, 0, visitado.Length);
        int ini = 0, fin = 0;
        cola[fin++] = origen;
        visitado[origen] = true;

        while (ini < fin)
        {
            int actual = cola[ini++];
            int tf = 2 * FilaDe(actual) + 1;
            int tc = 2 * ColumnaDe(actual) + 1;

            for (int d = 0; d < 4; d++)
            {
                int hf = tf + Dr[d];
                int hc = tc + Dc[d];
                if (hf < 1 || hf >= FilasGrilla - 1 || hc < 1 || hc >= ColumnasGrilla - 1) continue;
                if (pared[hf, hc]) continue;
                if (bloqueados != null && bloqueados.Contains(IdHueco(hf, hc))) continue;

                int vecino = IdCelda((tf + 2 * Dr[d] - 1) / 2, (tc + 2 * Dc[d] - 1) / 2);
                if (visitado[vecino]) continue;

                visitado[vecino] = true;
                cola[fin++] = vecino;
            }
        }
    }

    public bool TodosAlcanzables(int origen, IReadOnlyList<int> destinos, ISet<int> bloqueados)
    {
        var visitado = new bool[Celdas];
        CalcularAlcanzables(origen, bloqueados, visitado);

        for (int i = 0; i < destinos.Count; i++)
        {
            if (!visitado[destinos[i]]) return false;
        }
        return true;
    }
}
