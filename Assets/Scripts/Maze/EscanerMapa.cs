using UnityEngine;

// Reconstruye la grilla de paredes a partir de los muros que genero MapaBuilder. Lee las posiciones y
// escalas de los cubos de 01_Muros_Perimetro y 02_Muros_Laberinto (no usa fisica): la escalera y la
// habitacion del comerciante, o cualquier otra pieza ajena al laberinto, no cuentan como pared.
public static class EscanerMapa
{
    public const string NombreRaiz = "Mapa";
    static readonly string[] Grupos = { "01_Muros_Perimetro", "02_Muros_Laberinto" };

    public static Transform BuscarRaiz()
    {
        GameObject go = GameObject.Find(NombreRaiz);
        return go != null ? go.transform : null;
    }

    // Devuelve null si no encuentra ningun muro. Asume muros alineados a los ejes y la raiz sin escala.
    public static bool[,] LeerParedes(Transform raiz)
    {
        var pared = new bool[MapaLayout.FilasGrilla, MapaLayout.ColumnasGrilla];
        bool alguno = false;

        foreach (string nombreGrupo in Grupos)
        {
            Transform grupo = raiz.Find(nombreGrupo);
            if (grupo == null) continue;

            foreach (Transform muro in grupo)
            {
                MapaLayout.LocalAGrilla(raiz.InverseTransformPoint(muro.position), out float fila, out float columna);
                Vector3 tamano = muro.lossyScale;
                float tilesZ = tamano.z / MapaLayout.AnchoCalle;
                float tilesX = tamano.x / MapaLayout.AnchoCalle;

                int r0 = Mathf.Max(0, Mathf.RoundToInt(fila - (tilesZ - 1f) / 2f));
                int r1 = Mathf.Min(MapaLayout.FilasGrilla - 1, Mathf.RoundToInt(fila + (tilesZ - 1f) / 2f));
                int c0 = Mathf.Max(0, Mathf.RoundToInt(columna - (tilesX - 1f) / 2f));
                int c1 = Mathf.Min(MapaLayout.ColumnasGrilla - 1, Mathf.RoundToInt(columna + (tilesX - 1f) / 2f));

                for (int r = r0; r <= r1; r++)
                {
                    for (int c = c0; c <= c1; c++)
                    {
                        pared[r, c] = true;
                        alguno = true;
                    }
                }
            }
        }

        return alguno ? pared : null;
    }

    // La salida oficial es la que marca Punto_Salida (la que usa ExitTrigger); si falta, se busca el
    // hueco del perimetro. Evita confundirla con una abertura accidental en otro lado del muro exterior.
    public static int CeldaDeSalida(GrafoLaberinto grafo, Transform raiz)
    {
        GameObject marca = GameObject.Find("Punto_Salida");
        if (marca == null) return grafo.BuscarCeldaSalida();

        MapaLayout.CeldaMasCercana(raiz.InverseTransformPoint(marca.transform.position), out int r, out int c);
        return grafo.IdCelda(r, c);
    }

    // Tile (fila, columna de la grilla de paredes) en el que cae un punto del mundo.
    public static void TileDe(Transform raiz, Vector3 posicionMundo, out int tileFila, out int tileColumna)
    {
        MapaLayout.LocalAGrilla(raiz.InverseTransformPoint(posicionMundo), out float fila, out float columna);
        tileFila = Mathf.RoundToInt(fila);
        tileColumna = Mathf.RoundToInt(columna);
    }
}
