using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Genera un laberinto de verdad (no una cuadricula con loops) dentro de la escena abierta,
// con la jerarquia nombrada y ordenada. Solo paredes y planos: nada de logica de juego.
// Menu: Between Metals > Mapa > Generar mapa
//
// Nombres de los muros:  Muro_<Zona>[_<Lado>]_<NN>_F<fila ini>-<fila fin>_C<col ini>-<col fin>
//   F = fila contada desde el NORTE (1 = la de arriba del plano)
//   C = columna contada desde el OESTE (1 = la de la izquierda del plano)
public static class MapaBuilder
{
    // ---- Tamano del laberinto, en celdas. Cambialo y vuelve a generar ----
    const int Filas = 13;    // celdas de norte a sur
    const int Columnas = 11; // celdas de oeste a este
    const int Semilla = 424242; // mismo numero = mismo laberinto. Cambialo para obtener otro distinto.

    // ---- Medidas en metros ----
    const float AnchoCalle = 6f;   // ancho de cada pasillo (y grosor de un muro)
    const float AltoMuro = 8f;     // altura de los muros del laberinto
    const bool CrearTecho = false; // plano extra a la altura de los muros

    // ---- Escalera y habitacion del comerciante ----
    // Peldanos y fondo chicos: la escalera queda corta, pegada al laberinto en vez de lejos en el aire.
    const int NumeroPeldanos = 6;
    const float FondoPeldano = 1f;        // profundidad de cada escalon, en metros
    const float AlturaHabitacion = 4f;    // cuanto sube la escalera = altura del piso de la habitacion
    const float AltoParedHabitacion = 4f;
    const float GrosorParedHabitacion = 0.4f;
    const float TamanoHabitacionCeldas = 3f; // lado de la habitacion, en unidades de AnchoCalle
    const float AnchoPuertaHabitacion = 3f;  // ancho del hueco de entrada, en metros

    // ---- Zonas amplias: bloques de celdas (coordenadas de celda) a los que se les sacan las
    // paredes internas para que queden como una sola sala abierta. La de spawn incluye la celda
    // de inicio (esquina 0,0) y es la base del jugador; las otras son plazas sueltas y una laguna.
    // No se pueden pisar entre si, ni salirse de la grilla Filas x Columnas, ni tocar el perimetro
    // salvo la de spawn (que arranca justo en la esquina de inicio).
    struct BloqueAmplio
    {
        public string nombre;
        public int r0, r1, c0, c1;
        public bool esLaguna;
    }

    static readonly BloqueAmplio[] ZonasAmplias =
    {
        new BloqueAmplio { nombre = "Zona_Spawn",     r0 = 0, r1 = 2, c0 = 0, c1 = 2, esLaguna = false },
        new BloqueAmplio { nombre = "Plaza_Laguna",   r0 = 5, r1 = 6, c0 = 4, c1 = 5, esLaguna = true },
        new BloqueAmplio { nombre = "Plaza_Amplia_A", r0 = 9, r1 = 10, c0 = 1, c1 = 2, esLaguna = false },
        new BloqueAmplio { nombre = "Plaza_Amplia_B", r0 = 2, r1 = 3, c0 = 8, c1 = 9, esLaguna = false },
    };

    // Bloque de la zona del comerciante: totalmente interior (no toca ningun borde de la grilla),
    // asi la escalera y la habitacion quedan adentro del laberinto en vez de salir al exterior.
    // Es un poco mas grande que las plazas a proposito: dentro entran la escalera y la habitacion
    // con margen de sobra, sin llegar a tocar los muros del laberinto que rodean el bloque.
    static readonly BloqueAmplio ComercianteBloque =
        new BloqueAmplio { nombre = "Zona_Comerciante_Base", r0 = 6, r1 = 8, c0 = 6, c1 = 9, esLaguna = false };

    const string NombreRaiz = "Mapa";

    // La grilla real tiene el doble de filas/columnas que celdas, mas 1: entre cada dos celdas
    // hay una fila o columna que es la pared que las separa (o el hueco, si el algoritmo la abrio).
    static int FilasGrilla => 2 * Filas + 1;
    static int ColumnasGrilla => 2 * Columnas + 1;

    static readonly int[] Dr = { -1, 1, 0, 0 };
    static readonly int[] Dc = { 0, 0, -1, 1 };

    const StaticEditorFlags Estatico =
        StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

    class Rectangulo
    {
        public int r0, r1, c0, c1;
        public bool esPerimetro;
    }

    struct Celda
    {
        public int r, c;
    }

    // Mismo comando en dos lugares del menu: uno arriba de todo (acceso rapido) y el otro
    // agrupado con el resto de los comandos de Mapa.
    [MenuItem("Between Metals/Generar Mapa", priority = 1)]
    [MenuItem("Between Metals/Mapa/Generar mapa", priority = 100)]
    static void Generar()
    {
        if (!ValidarZonasAmplias()) return;

        Undo.IncrementCurrentGroup();
        int grupoUndo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Generar mapa");

        GameObject anterior = GameObject.Find(NombreRaiz);
        if (anterior != null && !EditorUtility.DisplayDialog("Generar mapa",
                "Ya existe un objeto '" + NombreRaiz + "' en la escena. Se va a reemplazar.", "Reemplazar", "Cancelar"))
        {
            return;
        }
        if (anterior != null) Undo.DestroyObjectImmediate(anterior);

        // 1) Laberinto: grilla de paredes + celda de inicio
        bool[,] muro = GenerarLaberinto(out Celda inicio, out int[,] distancia);

        // 2) Elegir la salida: la celda de borde mas lejana del inicio, la mas "profunda" del recorrido.
        Celda salida = CeldaDeSalida(distancia, out string ladoSalida);
        AbrirPerimetro(muro, salida.r, salida.c);

        // 3) Zonas amplias y bloque del comerciante: se abren despues del laberinto para no afectar
        //    el calculo de distancias de arriba.
        foreach (BloqueAmplio z in ZonasAmplias) AbrirBloque(muro, z);
        AbrirBloque(muro, ComercianteBloque);

        // La escalera arranca en el centro de la celda mas al oeste del bloque y sube hacia el este,
        // adentro del mismo bloque: la zona del comerciante queda dentro del laberinto, no afuera.
        float filaCentroComerciante = (ComercianteBloque.r0 + ComercianteBloque.r1) / 2f;
        Vector3 baseComerciante = GridToWorld(2 * filaCentroComerciante + 1, 2 * ComercianteBloque.c0 + 1);
        Vector3 haciaComerciante = Vector3.right;

        // 4) Construir todo
        Transform raiz = Vacio(NombreRaiz, null);

        Transform grupoSuelo = Vacio("00_Suelo", raiz);
        Pieza(PrimitiveType.Plane, "Suelo_Plano_Principal", grupoSuelo, Vector3.zero,
            new Vector3(ColumnasGrilla * AnchoCalle / 10f, 1f, FilasGrilla * AnchoCalle / 10f));

        Transform grupoPerimetro = Vacio("01_Muros_Perimetro", raiz);
        Transform grupoLaberinto = Vacio("02_Muros_Laberinto", raiz);
        ConstruirMuros(muro, grupoPerimetro, grupoLaberinto);

        Transform grupoPuntos = Vacio("03_Puntos_Referencia", raiz);
        Vector3 posInicio = GridToWorld(2 * inicio.r + 1, 2 * inicio.c + 1);
        Punto("Punto_Inicio", grupoPuntos, posInicio + Vector3.up * 0.2f);

        Vector3 posSalida = PosicionApertura(salida.r, salida.c, ladoSalida);
        Vector3 salidaExterior = posSalida + Normal(ladoSalida) * (AnchoCalle * 0.5f);
        Punto("Punto_Salida", grupoPuntos, salidaExterior + Vector3.up * 0.2f);

        if (CrearTecho)
        {
            Transform grupoTecho = Vacio("04_Techo", raiz);
            GameObject techo = Pieza(PrimitiveType.Plane, "Techo_Plano_Principal", grupoTecho,
                new Vector3(0f, AltoMuro, 0f),
                new Vector3(ColumnasGrilla * AnchoCalle / 10f, 1f, FilasGrilla * AnchoCalle / 10f));
            techo.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
        }

        Transform grupoZonasAmplias = Vacio("05_Zonas_Amplias", raiz);
        ConstruirZonasAmplias(grupoZonasAmplias);

        Transform grupoComerciante = Vacio("06_Zona_Comerciante", raiz);
        ConstruirEscaleraYHabitacion(baseComerciante, haciaComerciante, grupoComerciante);

        Undo.CollapseUndoOperations(grupoUndo);
        Selection.activeGameObject = raiz.gameObject;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(string.Format(
            "Mapa generado: laberinto de {0}x{1} celdas ({2}x{3} m). Inicio (base del jugador) en {4}. " +
            "Salida al {5} en {6}. Comerciante: escalera en {7} (dentro del laberinto), habitacion {8} m mas arriba.",
            Filas, Columnas, ColumnasGrilla * AnchoCalle, FilasGrilla * AnchoCalle,
            posInicio, ladoSalida, salidaExterior, baseComerciante, AlturaHabitacion));
    }

    [MenuItem("Between Metals/Mapa/Borrar mapa generado", priority = 101)]
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

    [MenuItem("Between Metals/Mapa/Borrar prototipo anterior", priority = 102)]
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

    // Chequeo de cordura sobre todos los bloques reservados (zonas amplias + comerciante): que no
    // se salgan de la grilla y que no se pisen entre si. Con los valores de arriba esto siempre
    // pasa; queda como red de seguridad si alguien los cambia.
    static bool ValidarZonasAmplias()
    {
        var todos = new List<BloqueAmplio>(ZonasAmplias) { ComercianteBloque };

        for (int i = 0; i < todos.Count; i++)
        {
            BloqueAmplio a = todos[i];
            if (a.r0 < 0 || a.r1 >= Filas || a.c0 < 0 || a.c1 >= Columnas)
            {
                Debug.LogError("MapaBuilder: la zona '" + a.nombre + "' se sale de la grilla (" + Filas + "x" + Columnas + " celdas).");
                return false;
            }

            for (int j = i + 1; j < todos.Count; j++)
            {
                BloqueAmplio b = todos[j];
                bool solapaFilas = a.r0 <= b.r1 && b.r0 <= a.r1;
                bool solapaColumnas = a.c0 <= b.c1 && b.c0 <= a.c1;
                if (solapaFilas && solapaColumnas)
                {
                    Debug.LogError("MapaBuilder: las zonas '" + a.nombre + "' y '" + b.nombre + "' se superponen.");
                    return false;
                }
            }
        }

        if (ComercianteBloque.r0 == 0 || ComercianteBloque.r1 == Filas - 1 ||
            ComercianteBloque.c0 == 0 || ComercianteBloque.c1 == Columnas - 1)
        {
            Debug.LogError("MapaBuilder: la zona del comerciante debe quedar totalmente interior (no puede tocar el borde de la grilla).");
            return false;
        }

        return true;
    }

    // ---------------------------------------------------------------
    // Generacion del laberinto (recursive backtracker): arranca en la celda (0,0)
    // y va tumbando paredes hacia celdas sin visitar, elegidas al azar con una semilla fija.
    // El resultado es un laberinto "perfecto": un unico camino posible entre dos celdas cualquiera
    // (las zonas amplias, que se abren despues, le agregan loops locales a proposito).
    // ---------------------------------------------------------------
    static bool[,] GenerarLaberinto(out Celda inicio, out int[,] distancia)
    {
        bool[,] muro = new bool[FilasGrilla, ColumnasGrilla];
        for (int r = 0; r < FilasGrilla; r++)
            for (int c = 0; c < ColumnasGrilla; c++)
                muro[r, c] = true;

        var rng = new System.Random(Semilla);
        bool[,] visitado = new bool[Filas, Columnas];
        var pila = new Stack<Celda>();

        inicio = new Celda { r = 0, c = 0 };
        pila.Push(inicio);
        visitado[0, 0] = true;
        muro[1, 1] = false;

        while (pila.Count > 0)
        {
            Celda actual = pila.Peek();
            var vecinos = new List<int>();

            for (int d = 0; d < 4; d++)
            {
                int nr = actual.r + Dr[d];
                int nc = actual.c + Dc[d];
                if (nr >= 0 && nr < Filas && nc >= 0 && nc < Columnas && !visitado[nr, nc])
                {
                    vecinos.Add(d);
                }
            }

            if (vecinos.Count == 0)
            {
                pila.Pop();
                continue;
            }

            int elegido = vecinos[rng.Next(vecinos.Count)];
            int fr = actual.r + Dr[elegido];
            int fc = actual.c + Dc[elegido];

            visitado[fr, fc] = true;
            muro[2 * fr + 1, 2 * fc + 1] = false;                 // centro de la nueva celda
            muro[actual.r + fr + 1, actual.c + fc + 1] = false;   // pared que la separaba de la actual

            pila.Push(new Celda { r = fr, c = fc });
        }

        distancia = DistanciasDesde(muro, inicio);
        return muro;
    }

    // Distancia (en celdas) desde el inicio a cada celda del laberinto, recorriendo solo pasos abiertos.
    static int[,] DistanciasDesde(bool[,] muro, Celda inicio)
    {
        int[,] dist = new int[Filas, Columnas];
        for (int r = 0; r < Filas; r++)
            for (int c = 0; c < Columnas; c++)
                dist[r, c] = -1;

        dist[inicio.r, inicio.c] = 0;
        var cola = new Queue<Celda>();
        cola.Enqueue(inicio);

        while (cola.Count > 0)
        {
            Celda actual = cola.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int nr = actual.r + Dr[d];
                int nc = actual.c + Dc[d];
                if (nr < 0 || nr >= Filas || nc < 0 || nc >= Columnas) continue;
                if (dist[nr, nc] != -1) continue;
                if (muro[actual.r + nr + 1, actual.c + nc + 1]) continue; // hay pared entre medio

                dist[nr, nc] = dist[actual.r, actual.c] + 1;
                cola.Enqueue(new Celda { r = nr, c = nc });
            }
        }

        return dist;
    }

    static bool DentroDeAlgunaZonaAmplia(int r, int c)
    {
        foreach (BloqueAmplio z in ZonasAmplias)
        {
            if (r >= z.r0 && r <= z.r1 && c >= z.c0 && c <= z.c1) return true;
        }
        return false;
    }

    // Celdas de borde que no caen dentro de una zona amplia (la de spawn toca la esquina 0,0).
    static IEnumerable<Celda> CeldasDeBorde()
    {
        for (int r = 0; r < Filas; r++)
            for (int c = 0; c < Columnas; c++)
                if ((r == 0 || r == Filas - 1 || c == 0 || c == Columnas - 1) && !DentroDeAlgunaZonaAmplia(r, c))
                    yield return new Celda { r = r, c = c };
    }

    // La salida es la celda de borde mas lejana del inicio: la mas "profunda" del recorrido.
    static Celda CeldaDeSalida(int[,] distancia, out string lado)
    {
        Celda mejor = CeldasDeBorde().OrderByDescending(x => distancia[x.r, x.c]).First();
        lado = LadoDe(mejor);
        return mejor;
    }

    static string LadoDe(Celda celda)
    {
        if (celda.r == 0) return "Norte";
        if (celda.r == Filas - 1) return "Sur";
        if (celda.c == 0) return "Oeste";
        return "Este";
    }

    // Abre un hueco en el muro exterior, en el lado que le corresponde a esa celda de borde.
    static void AbrirPerimetro(bool[,] muro, int r, int c)
    {
        if (r == 0) muro[0, 2 * c + 1] = false;
        else if (r == Filas - 1) muro[FilasGrilla - 1, 2 * c + 1] = false;
        else if (c == 0) muro[2 * r + 1, 0] = false;
        else muro[2 * r + 1, ColumnasGrilla - 1] = false;
    }

    // Saca todas las paredes internas de un bloque de celdas: quedan unidas en una sola sala.
    // Las paredes del borde del bloque (hacia celdas de afuera) no se tocan: por ahi entra y
    // sale el laberinto, con la cantidad de puertas que le haya tocado en la generacion.
    static void AbrirBloque(bool[,] muro, BloqueAmplio z)
    {
        for (int r = z.r0; r <= z.r1; r++)
        {
            for (int c = z.c0; c <= z.c1; c++)
            {
                muro[2 * r + 1, 2 * c + 1] = false;
                if (c < z.c1) muro[2 * r + 1, 2 * c + 2] = false;
                if (r < z.r1) muro[2 * r + 2, 2 * c + 1] = false;
            }
        }
    }

    static Vector3 PosicionApertura(int r, int c, string lado)
    {
        if (lado == "Norte") return GridToWorld(0, 2 * c + 1);
        if (lado == "Sur") return GridToWorld(FilasGrilla - 1, 2 * c + 1);
        if (lado == "Oeste") return GridToWorld(2 * r + 1, 0);
        return GridToWorld(2 * r + 1, ColumnasGrilla - 1);
    }

    // Direccion hacia afuera del mapa para cada lado (mismos ejes que GridToWorld).
    static Vector3 Normal(string lado)
    {
        switch (lado)
        {
            case "Norte": return Vector3.forward;
            case "Sur": return Vector3.back;
            case "Oeste": return Vector3.left;
            default: return Vector3.right;
        }
    }

    // ---------------------------------------------------------------
    // Construccion de geometria
    // ---------------------------------------------------------------

    static void ConstruirMuros(bool[,] muro, Transform grupoPerimetro, Transform grupoLaberinto)
    {
        var contadores = new Dictionary<string, int>();
        List<Rectangulo> muros = Rectangulos(muro);

        foreach (Rectangulo m in muros)
        {
            string lado = m.esPerimetro ? LadoDeGrilla(m) : "";
            string zona = m.esPerimetro ? "Perimetro" : "Laberinto";
            string clave = lado.Length > 0 ? zona + "_" + lado : zona;

            int n;
            contadores.TryGetValue(clave, out n);
            n++;
            contadores[clave] = n;

            string nombre = string.Format("Muro_{0}_{1:00}_F{2:00}-{3:00}_C{4:00}-{5:00}",
                clave, n, m.r0 + 1, m.r1 + 1, m.c0 + 1, m.c1 + 1);

            Vector3 pos = GridToWorld((m.r0 + m.r1) / 2f, (m.c0 + m.c1) / 2f);
            pos.y = AltoMuro / 2f;

            Vector3 escala = new Vector3(
                (m.c1 - m.c0 + 1) * AnchoCalle,
                AltoMuro,
                (m.r1 - m.r0 + 1) * AnchoCalle);

            Pieza(PrimitiveType.Cube, nombre, m.esPerimetro ? grupoPerimetro : grupoLaberinto, pos, escala);
        }
    }

    static string LadoDeGrilla(Rectangulo m)
    {
        if (m.r0 == 0) return "Norte";
        if (m.r1 == FilasGrilla - 1) return "Sur";
        if (m.c0 == 0) return "Oeste";
        if (m.c1 == ColumnasGrilla - 1) return "Este";
        return ""; // no deberia pasar: un rectangulo de perimetro siempre toca un borde
    }

    // Une las casillas de muro contiguas en el menor numero de bloques rectangulares.
    static List<Rectangulo> Rectangulos(bool[,] muro)
    {
        var todos = new List<Rectangulo>();
        var abiertos = new List<Rectangulo>();

        for (int r = 0; r < FilasGrilla; r++)
        {
            var nuevosAbiertos = new List<Rectangulo>();
            int c = 0;

            while (c < ColumnasGrilla)
            {
                if (!muro[r, c]) { c++; continue; }

                int ini = c;
                while (c < ColumnasGrilla && muro[r, c]) c++;
                int fin = c - 1;

                Rectangulo rect = abiertos.Find(a => a.c0 == ini && a.c1 == fin);
                if (rect != null)
                {
                    rect.r1 = r;
                }
                else
                {
                    bool esPerimetro = r == 0 || r == FilasGrilla - 1 || ini == 0 || fin == ColumnasGrilla - 1;
                    rect = new Rectangulo { r0 = r, r1 = r, c0 = ini, c1 = fin, esPerimetro = esPerimetro };
                    todos.Add(rect);
                }

                nuevosAbiertos.Add(rect);
            }

            abiertos = nuevosAbiertos;
        }

        return todos;
    }

    // Marca cada zona amplia en la Hierarchy con un punto de referencia en su centro.
    // La sala en si ya existe por la falta de muros ahi (ConstruirMuros no genera nada donde no hay pared);
    // aca solo se agrega el punto y, en la laguna, un plano de agua.
    static void ConstruirZonasAmplias(Transform grupoRaiz)
    {
        foreach (BloqueAmplio z in ZonasAmplias)
        {
            Transform grupoZona = Vacio(z.nombre, grupoRaiz);

            Vector3 centro = GridToWorld(z.r0 + z.r1 + 1, z.c0 + z.c1 + 1);
            float ancho = (z.c1 - z.c0 + 1) * AnchoCalle;
            float profundidad = (z.r1 - z.r0 + 1) * AnchoCalle;

            Punto("Punto_" + z.nombre, grupoZona, centro + Vector3.up);

            if (z.esLaguna)
            {
                GameObject agua = Pieza(PrimitiveType.Plane, "Laguna_Plano_Agua", grupoZona,
                    centro + Vector3.up * 0.02f, new Vector3(ancho / 10f * 0.9f, 1f, profundidad / 10f * 0.9f));
                agua.GetComponent<Renderer>().sharedMaterial = MaterialTintado(new Color(0.15f, 0.35f, 0.55f));
                Object.DestroyImmediate(agua.GetComponent<Collider>()); // no bloquea: solo es visual
            }
        }
    }

    // Escalera de bloques (no una escalera de mano) que sube desde "basePos", en la direccion
    // "hacia", hasta una habitacion elevada cerrada por sus cuatro lados salvo la puerta que da a
    // la propia escalera. Todo queda adentro del bloque reservado para el comerciante, dentro del
    // laberinto, sin salir al exterior del mapa.
    static void ConstruirEscaleraYHabitacion(Vector3 basePos, Vector3 hacia, Transform grupoRaiz)
    {
        basePos.y = 0f;
        bool ejeZ = Mathf.Abs(hacia.z) > 0.5f;
        Vector3 ejeLateral = ejeZ ? Vector3.right : Vector3.forward;

        Transform grupoEscalera = Vacio("Escalera", grupoRaiz);
        float alturaPeldano = AlturaHabitacion / NumeroPeldanos;

        for (int i = 0; i < NumeroPeldanos; i++)
        {
            float distanciaCentro = FondoPeldano * (i + 0.5f);
            float altura = alturaPeldano * (i + 1);

            Vector3 pos = basePos + hacia * distanciaCentro;
            pos.y = altura / 2f;

            Vector3 escala = new Vector3(
                ejeZ ? AnchoCalle : FondoPeldano,
                altura,
                ejeZ ? FondoPeldano : AnchoCalle);

            Pieza(PrimitiveType.Cube, "Peldano_" + (i + 1).ToString("00"), grupoEscalera, pos, escala);
        }

        // La habitacion empieza justo despues del ultimo peldano: sin hueco entre la escalera y la puerta.
        float distanciaAlCentro = FondoPeldano * NumeroPeldanos + (TamanoHabitacionCeldas * AnchoCalle) / 2f;
        Vector3 centroHabitacion = basePos + hacia * distanciaAlCentro;
        centroHabitacion.y = AlturaHabitacion;

        Transform grupoHabitacion = Vacio("Habitacion_Comerciante", grupoRaiz);
        float lados = TamanoHabitacionCeldas * AnchoCalle;
        float mitad = lados / 2f;
        float alturaPared = AltoParedHabitacion;

        Pieza(PrimitiveType.Plane, "Habitacion_Suelo", grupoHabitacion, centroHabitacion,
            new Vector3(lados / 10f, 1f, lados / 10f));

        // Pared del fondo, opuesta a la escalera
        Vector3 posFondo = centroHabitacion + hacia * mitad;
        posFondo.y = centroHabitacion.y + alturaPared / 2f;
        Pieza(PrimitiveType.Cube, "Muro_Habitacion_Fondo", grupoHabitacion, posFondo,
            ejeZ ? new Vector3(lados, alturaPared, GrosorParedHabitacion) : new Vector3(GrosorParedHabitacion, alturaPared, lados));

        // Paredes laterales, de punta a punta
        Vector3 posLateralA = centroHabitacion + ejeLateral * mitad;
        posLateralA.y = centroHabitacion.y + alturaPared / 2f;
        Vector3 posLateralB = centroHabitacion - ejeLateral * mitad;
        posLateralB.y = centroHabitacion.y + alturaPared / 2f;

        Pieza(PrimitiveType.Cube, "Muro_Habitacion_LadoA", grupoHabitacion, posLateralA,
            ejeZ ? new Vector3(GrosorParedHabitacion, alturaPared, lados) : new Vector3(lados, alturaPared, GrosorParedHabitacion));
        Pieza(PrimitiveType.Cube, "Muro_Habitacion_LadoB", grupoHabitacion, posLateralB,
            ejeZ ? new Vector3(GrosorParedHabitacion, alturaPared, lados) : new Vector3(lados, alturaPared, GrosorParedHabitacion));

        // Pared de entrada (lado de la escalera), partida en dos para dejar una puerta en el medio:
        // la habitacion queda cerrada por los cuatro lados en vez de con un frente abierto.
        float mitadPuerta = AnchoPuertaHabitacion / 2f;
        float largoTramo = mitad - mitadPuerta;
        if (largoTramo > 0.01f)
        {
            float centroTramo = mitadPuerta + largoTramo / 2f;

            Vector3 posEntradaA = centroHabitacion - hacia * mitad + ejeLateral * centroTramo;
            posEntradaA.y = centroHabitacion.y + alturaPared / 2f;
            Vector3 posEntradaB = centroHabitacion - hacia * mitad - ejeLateral * centroTramo;
            posEntradaB.y = centroHabitacion.y + alturaPared / 2f;

            Pieza(PrimitiveType.Cube, "Muro_Habitacion_Entrada_A", grupoHabitacion, posEntradaA,
                ejeZ ? new Vector3(largoTramo, alturaPared, GrosorParedHabitacion) : new Vector3(GrosorParedHabitacion, alturaPared, largoTramo));
            Pieza(PrimitiveType.Cube, "Muro_Habitacion_Entrada_B", grupoHabitacion, posEntradaB,
                ejeZ ? new Vector3(largoTramo, alturaPared, GrosorParedHabitacion) : new Vector3(GrosorParedHabitacion, alturaPared, largoTramo));
        }

        Punto("Punto_Comerciante", grupoHabitacion, centroHabitacion + Vector3.up);
    }

    // Convierte una posicion de la grilla (puede ser fraccionaria, para centros de bloques) a metros.
    // Fila 0 = norte (+Z). Columna 0 = oeste (-X). Coincide con como se miden los muros del laberinto.
    static Vector3 GridToWorld(float row, float col)
    {
        return new Vector3(
            (col - (ColumnasGrilla - 1) / 2f) * AnchoCalle,
            0f,
            ((FilasGrilla - 1) / 2f - row) * AnchoCalle);
    }

    static Transform Vacio(string nombre, Transform padre)
    {
        GameObject go = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
        go.transform.SetParent(padre, false);
        return go.transform;
    }

    static void Punto(string nombre, Transform padre, Vector3 posLocal)
    {
        GameObject go = Vacio(nombre, padre).gameObject;
        go.transform.localPosition = posLocal;
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

    static Material MaterialTintado(Color color)
    {
        GameObject sonda = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Material baseMat = sonda.GetComponent<Renderer>().sharedMaterial;
        Object.DestroyImmediate(sonda);

        Material mat = new Material(baseMat);
        mat.color = color;
        return mat;
    }
}
