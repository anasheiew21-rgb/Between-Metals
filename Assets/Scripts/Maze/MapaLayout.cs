using UnityEngine;

// Medidas del laberinto y conversion grilla <-> espacio local de la raiz "Mapa" (que no esta en el origen).
// Fuente unica: MapaBuilder (Editor) usa estas mismas constantes para generar la geometria.
public static class MapaLayout
{
    public const int Filas = 13;     // celdas de norte a sur
    public const int Columnas = 11;  // celdas de oeste a este
    public const float AnchoCalle = 6f;
    public const float AltoMuro = 8f;

    public const int FilasGrilla = 2 * Filas + 1;
    public const int ColumnasGrilla = 2 * Columnas + 1;

    // Fila 0 = norte (+Z), columna 0 = oeste (-X). La celda (r,c) esta en la posicion de grilla (2r+1, 2c+1).
    public static Vector3 GrillaALocal(float fila, float columna)
    {
        return new Vector3(
            (columna - (ColumnasGrilla - 1) / 2f) * AnchoCalle,
            0f,
            ((FilasGrilla - 1) / 2f - fila) * AnchoCalle);
    }

    public static void LocalAGrilla(Vector3 local, out float fila, out float columna)
    {
        columna = local.x / AnchoCalle + (ColumnasGrilla - 1) / 2f;
        fila = (FilasGrilla - 1) / 2f - local.z / AnchoCalle;
    }

    // Celda cuyo centro queda mas cerca de un punto local; fuera del mapa se recorta al borde.
    public static void CeldaMasCercana(Vector3 local, out int r, out int c)
    {
        LocalAGrilla(local, out float fila, out float columna);
        r = Mathf.Clamp(Mathf.RoundToInt((fila - 1f) / 2f), 0, Filas - 1);
        c = Mathf.Clamp(Mathf.RoundToInt((columna - 1f) / 2f), 0, Columnas - 1);
    }
}
