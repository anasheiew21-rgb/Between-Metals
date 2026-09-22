using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Genera el mapa (solo muros y planos) dentro de la escena abierta, con la jerarquia nombrada y ordenada.
// Menu: Between Metals > Mapa > Generar mapa
//
// Nombres de los muros:  Muro_<Zona>[_<Lado>]_<NN>_F<fila ini>-<fila fin>_C<col ini>-<col fin>
//   F = fila contada desde el NORTE (1 = la de arriba del plano)
//   C = columna contada desde el OESTE (1 = la de la izquierda del plano)
public static class MapaBuilder
{
    // ---- Medidas en metros. Cambia estos valores y vuelve a generar ----
    const float AnchoCalle = 4f;   // ancho de cada calle (y grosor minimo de un muro)
    const float AltoMuro = 8f;     // altura de los muros
    const bool CrearTecho = false; // plano extra a la altura de los muros

    const string NombreRaiz = "Mapa";

    // # = muro. Cualquier otro caracter es calle (los puntos solo sirven de referencia visual).
    // La fila 15 (indice 14) es el tunel lateral; la puerta '-' de la casa central se deja abierta.
    static readonly string[] Plano =
    {
        "############################",
        "#............##............#",
        "#.####.#####.##.#####.####.#",
        "#o####.#####.##.#####.####o#",
        "#.####.#####.##.#####.####.#",
        "#..........................#",
        "#.####.##.########.##.####.#",
        "#.####.##.########.##.####.#",
        "#......##....##....##......#",
        "######.##### ## #####.######",
        "     #.##### ## #####.#     ",
        "     #.##          ##.#     ",
        "     #.## ###--### ##.#     ",
        "######.## #      # ##.######",
        "      .   #      #   .      ",
        "######.## #      # ##.######",
        "     #.## ######## ##.#     ",
        "     #.##          ##.#     ",
        "     #.## ######## ##.#     ",
        "######.## ######## ##.######",
        "#............##............#",
        "#.####.#####.##.#####.####.#",
        "#.####.#####.##.#####.####.#",
        "#o..##.......  .......##..o#",
        "###.##.##.########.##.##.###",
        "###.##.##.########.##.##.###",
        "#......##....##....##......#",
        "#.##########.##.##########.#",
        "#.##########.##.##########.#",
        "#..........................#",
        "############################",
    };

    static readonly int Filas = Plano.Length;
    static readonly int Columnas = Plano[0].Length;
    const int FilaTunel = 14;

    // Zonas del mapa, en el orden en que aparecen en la Hierarchy
    static readonly string[] Zonas = { "Perimetro", "Bloques_Interiores", "Casa_Central", "Tuneles" };

    const StaticEditorFlags Estatico =
        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

    class Rectangulo
    {
        public int r0, r1, c0, c1;
        public string zona;
    }

    [MenuItem("Between Metals/Mapa/Generar mapa")]
    static void Generar()
    {
        for (int r = 0; r < Filas; r++)
        {
            if (Plano[r].Length != Columnas)
            {
                Debug.LogError("MapaBuilder: la fila " + (r + 1) + " mide " + Plano[r].Length + " y deberia medir " + Columnas + ".");
                return;
            }
        }

        GameObject anterior = GameObject.Find(NombreRaiz);
        if (anterior != null && !EditorUtility.DisplayDialog("Generar mapa",
                "Ya existe un objeto '" + NombreRaiz + "' en la escena. Se va a reemplazar.", "Reemplazar", "Cancelar"))
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        int grupoUndo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Generar mapa");

        if (anterior != null) Undo.DestroyObjectImmediate(anterior);

        Transform raiz = Vacio(NombreRaiz, null);

        // 00 - Suelo: un solo plano del tamano total del mapa
        Transform grupoSuelo = Vacio("00_Suelo", raiz);
        Pieza(PrimitiveType.Plane, "Suelo_Plano_Principal", grupoSuelo, Vector3.zero,
            new Vector3(Columnas * AnchoCalle / 10f, 1f, Filas * AnchoCalle / 10f));

        // 01..04 - Muros, agrupados por zona
        var grupos = new Dictionary<string, Transform>();
        for (int i = 0; i < Zonas.Length; i++)
        {
            grupos[Zonas[i]] = Vacio((i + 1).ToString("00") + "_Muros_" + Zonas[i], raiz);
        }

        var contadores = new Dictionary<string, int>();
        List<Rectangulo> muros = Rectangulos(CrearCuadricula());

        foreach (Rectangulo m in muros)
        {
            string lado = Lado(m);
            string clave = lado.Length > 0 ? m.zona + "_" + lado : m.zona;

            int n;
            contadores.TryGetValue(clave, out n);
            n++;
            contadores[clave] = n;

            string nombre = string.Format("Muro_{0}_{1:00}_F{2:00}-{3:00}_C{4:00}-{5:00}",
                clave, n, m.r0 + 1, m.r1 + 1, m.c0 + 1, m.c1 + 1);

            Vector3 pos = new Vector3(
                ((m.c0 + m.c1) / 2f - (Columnas - 1) / 2f) * AnchoCalle,
                AltoMuro / 2f,
                ((Filas - 1) / 2f - (m.r0 + m.r1) / 2f) * AnchoCalle);

            Vector3 escala = new Vector3(
                (m.c1 - m.c0 + 1) * AnchoCalle,
                AltoMuro,
                (m.r1 - m.r0 + 1) * AnchoCalle);

            Pieza(PrimitiveType.Cube, nombre, grupos[m.zona], pos, escala);
        }

        // 05 - Techo (opcional)
        if (CrearTecho)
        {
            Transform grupoTecho = Vacio("05_Techo", raiz);
            GameObject techo = Pieza(PrimitiveType.Plane, "Techo_Plano_Principal", grupoTecho,
                new Vector3(0f, AltoMuro, 0f),
                new Vector3(Columnas * AnchoCalle / 10f, 1f, Filas * AnchoCalle / 10f));
            techo.transform.localRotation = Quaternion.Euler(180f, 0f, 0f); // mira hacia abajo
        }

        Undo.CollapseUndoOperations(grupoUndo);
        Selection.activeGameObject = raiz.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Vector3 inicio = new Vector3(
            (13.5f - (Columnas - 1) / 2f) * AnchoCalle,
            0f,
            ((Filas - 1) / 2f - 23) * AnchoCalle);

        Debug.Log("Mapa generado: " + muros.Count + " muros. Tamano " + (Columnas * AnchoCalle) + " x " + (Filas * AnchoCalle)
                  + " m, centrado en el origen. Punto de salida sugerido para el jugador: " + inicio + " (sube Y a ~1).");
    }

    [MenuItem("Between Metals/Mapa/Borrar mapa generado")]
    static void Borrar()
    {
        GameObject raiz = GameObject.Find(NombreRaiz);
        if (raiz == null)
        {
            Debug.Log("MapaBuilder: no hay ningun objeto '" + NombreRaiz + "' en la escena.");
            return;
        }

        Undo.DestroyObjectImmediate(raiz);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    [MenuItem("Between Metals/Mapa/Borrar prototipo anterior")]
    static void BorrarPrototipo()
    {
        string[] nombres = { "Floor", "Wall_Left", "Wall_Right", "Ceiling" };
        var encontrados = new List<GameObject>();

        foreach (string nombre in nombres)
        {
            GameObject go = GameObject.Find(nombre);
            if (go != null) encontrados.Add(go);
        }

        if (encontrados.Count == 0)
        {
            EditorUtility.DisplayDialog("Borrar prototipo anterior",
                "No hay objetos Floor, Wall_Left, Wall_Right ni Ceiling en la escena abierta.", "OK");
            return;
        }

        string lista = string.Join(", ", encontrados.ConvertAll(g => g.name));
        if (!EditorUtility.DisplayDialog("Borrar prototipo anterior",
                "Se van a borrar: " + lista + ".\n\nSe puede deshacer con Ctrl+Z.", "Borrar", "Cancelar"))
        {
            return;
        }

        foreach (GameObject go in encontrados) Undo.DestroyObjectImmediate(go);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // Copia editable del plano: la puerta de la casa queda abierta y se tapan los extremos del tunel
    // para que el jugador no salga del suelo.
    static char[][] CrearCuadricula()
    {
        var g = new char[Filas][];
        for (int r = 0; r < Filas; r++) g[r] = Plano[r].ToCharArray();

        g[FilaTunel][0] = '#';
        g[FilaTunel][Columnas - 1] = '#';
        return g;
    }

    static string Zona(int r, int c)
    {
        if (r >= 12 && r <= 16 && c >= 10 && c <= 17) return "Casa_Central";
        if (r >= 9 && r <= 19 && (c <= 5 || c >= Columnas - 6)) return "Tuneles";
        if (r == 0 || r == Filas - 1 || c == 0 || c == Columnas - 1) return "Perimetro";
        return "Bloques_Interiores";
    }

    static string Lado(Rectangulo m)
    {
        if (m.zona == "Perimetro")
        {
            if (m.r0 == 0) return "Norte";
            if (m.r1 == Filas - 1) return "Sur";
            return m.c0 == 0 ? "Oeste" : "Este";
        }

        if (m.zona == "Tuneles") return m.c0 < Columnas / 2 ? "Oeste" : "Este";
        return "";
    }

    // Une las casillas de muro en el menor numero de bloques: primero por filas y luego hacia abajo
    static List<Rectangulo> Rectangulos(char[][] g)
    {
        var todos = new List<Rectangulo>();
        var abiertos = new List<Rectangulo>(); // los que terminan en la fila anterior

        for (int r = 0; r < Filas; r++)
        {
            var nuevosAbiertos = new List<Rectangulo>();
            int c = 0;

            while (c < Columnas)
            {
                if (g[r][c] != '#')
                {
                    c++;
                    continue;
                }

                string zona = Zona(r, c);
                int ini = c;
                while (c < Columnas && g[r][c] == '#' && Zona(r, c) == zona) c++;
                int fin = c - 1;

                Rectangulo rect = null;
                foreach (Rectangulo a in abiertos)
                {
                    if (a.c0 == ini && a.c1 == fin && a.zona == zona)
                    {
                        rect = a;
                        break;
                    }
                }

                if (rect != null)
                {
                    rect.r1 = r;
                }
                else
                {
                    rect = new Rectangulo { r0 = r, r1 = r, c0 = ini, c1 = fin, zona = zona };
                    todos.Add(rect);
                }

                nuevosAbiertos.Add(rect);
            }

            abiertos = nuevosAbiertos;
        }

        return todos;
    }

    static Transform Vacio(string nombre, Transform padre)
    {
        GameObject go = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
        go.transform.SetParent(padre, false);
        return go.transform;
    }

    static GameObject Pieza(PrimitiveType tipo, string nombre, Transform padre, Vector3 posLocal, Vector3 escala)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = posLocal;
        go.transform.localScale = escala;
        GameObjectUtility.SetStaticEditorFlags(go, Estatico);
        return go;
    }
}
