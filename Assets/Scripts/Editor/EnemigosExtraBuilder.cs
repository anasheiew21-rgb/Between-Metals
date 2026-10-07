using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Agrega 2 enemigos mas al laberinto reusando TAL CUAL la logica del enemigo que ya esta en la
// escena: no se escribe ningun comportamiento nuevo, se copian los valores de sus componentes
// (EnemyAI, EnemyHealth, EnemyAnimator, Rigidbody) con el mismo "Paste Component Values" del
// inspector, asi cualquier ajuste de tuning del original viaja a las copias.
//
// Lo visual es a proposito una esfera primitiva de Unity, la misma convencion de placeholder que
// tenia el enemigo original antes del modelo rigueado: por eso la esfera queda con su MeshFilter/
// MeshRenderer/SphereCollider en el MISMO objeto que EnemyAI. Cuando el modelo y el esqueleto esten
// listos alcanza con correr "Between Metals/Enemigos/Build + Setup completo", que justamente busca
// esa esfera (EnemyModelSetup.ReemplazarEsferaPorModelo quita el mesh y EnemySetupFixer cambia el
// SphereCollider por la capsula medida sobre el modelo).
//
// Las posiciones no estan hardcodeadas: se eligen por distancia en el grafo del laberinto (el mismo
// EscanerMapa/GrafoLaberinto que usan las barreras dinamicas), tomando las celdas mas lejanas al
// spawn del jugador, a la salida y al enemigo original; asi siguen siendo validas si se regenera el
// mapa. Menu: Between Metals > Enemigos
public static class EnemigosExtraBuilder
{
    const string NombreRaiz = "Enemigos_Extra";
    const int Cantidad = 2;
    const float DiametroEsfera = 1f;   // placeholder de 1 m en calles de 6 m: se ve sin tapar el pasillo
    const float AlturaDeDiseno = 1f;   // misma Y que el enemigo original y los Punto_*; EnemyAI despues lo baja al NavMesh
    const int DistanciaMinimaWaypoint = 2; // celdas entre el enemigo y cada waypoint de su patrulla
    const int DistanciaMaximaWaypoint = 6; // mas lejos que esto la patrulla se le va del sector
    const int WaypointsPorEnemigo = 3;     // igual que el enemigo original (con 2+ validos no cae en Deambular)

    [MenuItem("Between Metals/Enemigos/Colocar 2 enemigos extra (esferas)", priority = 300)]
    static void Colocar()
    {
        EnemyAI plantilla = BuscarPlantilla();
        if (plantilla == null)
        {
            EditorUtility.DisplayDialog("Colocar enemigos extra",
                "No hay ningun EnemyAI fuera de " + NombreRaiz + " en la escena abierta: " +
                "hace falta el enemigo original como plantilla.", "OK");
            return;
        }

        Transform raizMapa = EscanerMapa.BuscarRaiz();
        bool[,] paredes = raizMapa != null ? EscanerMapa.LeerParedes(raizMapa) : null;
        if (paredes == null)
        {
            EditorUtility.DisplayDialog("Colocar enemigos extra",
                "No se encontro el laberinto (" + EscanerMapa.NombreRaiz + " con sus muros) en la escena abierta.", "OK");
            return;
        }

        var grafo = new GrafoLaberinto(paredes);
        List<int> aEvitar = CeldasAEvitar(grafo, raizMapa, plantilla);
        List<int> elegidas = ElegirCeldas(grafo, aEvitar, Cantidad);
        if (elegidas.Count < Cantidad)
        {
            EditorUtility.DisplayDialog("Colocar enemigos extra",
                "Solo se encontraron " + elegidas.Count + " celdas validas y lejanas para poner enemigos.", "OK");
            return;
        }

        GameObject anterior = GameObject.Find(NombreRaiz);
        if (anterior != null && !EditorUtility.DisplayDialog("Colocar enemigos extra",
                "Ya existe " + NombreRaiz + " en la escena. Se va a reemplazar.", "Reemplazar", "Cancelar"))
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        int grupoUndo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Colocar enemigos extra");

        if (anterior != null) Undo.DestroyObjectImmediate(anterior);

        var contenedor = new GameObject(NombreRaiz);
        Undo.RegisterCreatedObjectUndo(contenedor, "Crear " + NombreRaiz);

        for (int i = 0; i < elegidas.Count; i++)
        {
            // El original se llama "Enemigo", asi que estos siguen la numeracion desde el 02.
            string nombre = string.Format("Enemigo_{0:00}", i + 2);
            Construir(grafo, raizMapa, contenedor.transform, plantilla, elegidas[i], nombre);
        }

        Undo.CollapseUndoOperations(grupoUndo);
        Selection.activeGameObject = contenedor;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("EnemigosExtraBuilder: " + elegidas.Count + " enemigos esfera colocados bajo " + NombreRaiz +
            ", copiando los componentes de " + plantilla.name + ". Revisa la escena y guardala (Ctrl+S).");
    }

    [MenuItem("Between Metals/Enemigos/Quitar enemigos extra", priority = 301)]
    static void Quitar()
    {
        GameObject raiz = GameObject.Find(NombreRaiz);
        if (raiz == null)
        {
            Debug.Log("EnemigosExtraBuilder: no hay ningun objeto " + NombreRaiz + " en la escena.");
            return;
        }

        Undo.DestroyObjectImmediate(raiz);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // Variante para -executeMethod EnemigosExtraBuilder.Run (batch/CI), al estilo de
    // BuildEnemySetup: abre Prototype.unity, coloca los enemigos y guarda la escena.
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
        Colocar();
        EditorSceneManager.SaveOpenScenes();
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // El enemigo original: cualquier EnemyAI que no sea de los que coloca este builder.
    static EnemyAI BuscarPlantilla()
    {
        GameObject raiz = GameObject.Find(NombreRaiz);
        foreach (EnemyAI ia in Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include))
        {
            if (raiz != null && ia.transform.IsChildOf(raiz.transform)) continue;
            return ia;
        }
        return null;
    }

    // ---------------------------------------------------------------- construccion

    static void Construir(GrafoLaberinto grafo, Transform raizMapa, Transform contenedor,
        EnemyAI plantilla, int celda, string nombre)
    {
        // Esfera primitiva como placeholder, con el mesh y el collider en el mismo objeto que la IA:
        // ver el comentario de arriba sobre por que esta convencion y no un hijo "Modelo".
        GameObject enemigo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Undo.RegisterCreatedObjectUndo(enemigo, "Crear " + nombre);
        enemigo.name = nombre;
        enemigo.transform.SetParent(contenedor, false);
        enemigo.transform.position = PosicionDeCelda(grafo, raizMapa, celda);
        enemigo.transform.localScale = Vector3.one * DiametroEsfera;
        enemigo.layer = plantilla.gameObject.layer;
        enemigo.tag = plantilla.tag;

        // Fisico, no trigger: PlayerCombat golpea con un SphereCast que usa
        // QueryTriggerInteraction.Ignore, un collider en trigger seria invulnerable.
        SphereCollider collider = enemigo.GetComponent<SphereCollider>();
        collider.isTrigger = false;
        collider.radius = 0.5f; // radio local de la esfera primitiva: la hitbox mide lo que se ve

        // Mismo orden que el original: Rigidbody antes de EnemyAI (que lo exige con RequireComponent).
        CopiarValores(plantilla.GetComponent<Rigidbody>(), enemigo);
        CopiarValores(plantilla.GetComponent<EnemyHealth>(), enemigo);
        EnemyAI ia = CopiarValores(plantilla, enemigo);
        CopiarValores(plantilla.GetComponent<EnemyAnimator>(), enemigo);

        AjustarAgente(enemigo);
        AgregarAudioSource(enemigo);

        Transform[] waypoints = CrearWaypoints(grafo, raizMapa, contenedor, celda, nombre);
        AsignarWaypoints(ia, waypoints);
    }

    // "Paste Component Values" del inspector: el enemigo nuevo arranca con exactamente el mismo
    // tuning que el original (rangos de vision, dano, cooldowns, vida, suavizado del animator...).
    static T CopiarValores<T>(T origen, GameObject destino) where T : Component
    {
        if (origen == null) return null;

        T copia = destino.GetComponent<T>();
        if (copia == null) copia = Undo.AddComponent<T>(destino);
        else Undo.RecordObject(copia, "Copiar valores de " + typeof(T).Name);

        ComponentUtility.CopyComponent(origen);
        ComponentUtility.PasteComponentValues(copia);
        return copia;
    }

    // EnemyAI crea y configura el agente solo en Play (AsegurarAgente), pero dejarlo en la escena
    // hace visible en el inspector que la navegacion esta medida sobre la esfera y no con los
    // 0.5 / 2 m por defecto. El alto se baja al de la esfera; el radio se topea con el del tipo de
    // agente con el que NavMeshRuntimeBuilder hornea el NavMesh (pedir mas no abre mas espacio).
    static void AjustarAgente(GameObject enemigo)
    {
        NavMeshAgent agente = enemigo.GetComponent<NavMeshAgent>();
        if (agente == null) agente = Undo.AddComponent<NavMeshAgent>(enemigo);
        else Undo.RecordObject(agente, "Ajustar agente");

        float radio = DiametroEsfera * 0.5f;
        float radioHorneado = NavMesh.GetSettingsByID(agente.agentTypeID).agentRadius;
        if (radioHorneado > 0f) radio = Mathf.Min(radio, radioHorneado);

        agente.radius = radio;
        agente.height = DiametroEsfera;
        agente.updateRotation = false; // la rotacion la maneja EnemyAI.MirarHacia()
    }

    // Mismos valores que EnemySetupFixer le deja al enemigo original, para que los rugidos y el
    // golpe se oigan igual de lejos y pasen por el grupo sfx del mixer.
    static void AgregarAudioSource(GameObject enemigo)
    {
        AudioSource fuente = enemigo.GetComponent<AudioSource>();
        if (fuente == null) fuente = Undo.AddComponent<AudioSource>(enemigo);
        else Undo.RecordObject(fuente, "Configurar AudioSource");

        fuente.playOnAwake = false;
        fuente.loop = false;
        fuente.spatialBlend = 1f;
        fuente.rolloffMode = AudioRolloffMode.Logarithmic;
        fuente.minDistance = 2f;
        fuente.maxDistance = 25f;
        AudioPreferences.RutearASfx(fuente);
    }

    // Los waypoints son hermanos del enemigo, NO sus hijos: colgados del enemigo se moverian con
    // el y la patrulla nunca llegaria a destino.
    static Transform[] CrearWaypoints(GrafoLaberinto grafo, Transform raizMapa, Transform contenedor,
        int celdaEnemigo, string nombreEnemigo)
    {
        List<int> celdas = CeldasDePatrulla(grafo, celdaEnemigo, WaypointsPorEnemigo);

        var grupo = new GameObject("Waypoints_" + nombreEnemigo);
        Undo.RegisterCreatedObjectUndo(grupo, "Crear " + grupo.name);
        grupo.transform.SetParent(contenedor, false);

        var puntos = new Transform[celdas.Count];
        for (int i = 0; i < celdas.Count; i++)
        {
            var punto = new GameObject(string.Format("{0}_Waypoint_{1}", nombreEnemigo, i + 1));
            Undo.RegisterCreatedObjectUndo(punto, "Crear " + punto.name);
            punto.transform.SetParent(grupo.transform, false);
            punto.transform.position = PosicionDeCelda(grafo, raizMapa, celdas[i]);
            puntos[i] = punto.transform;
        }

        if (celdas.Count < 2)
        {
            Debug.LogWarning("EnemigosExtraBuilder: " + nombreEnemigo + " quedo con " + celdas.Count +
                " waypoint(s) validos; va a patrullar en modo Deambular.", grupo);
        }

        return puntos;
    }

    // waypoints y audioSource son privados en EnemyAI, asi que se escriben por SerializedObject.
    // audioSource se limpia a proposito: si en la plantilla estuviera asignado, la copia apuntaria
    // al AudioSource del enemigo original y los dos sonarian desde el mismo lugar.
    static void AsignarWaypoints(EnemyAI ia, Transform[] waypoints)
    {
        var so = new SerializedObject(ia);

        SerializedProperty lista = so.FindProperty("waypoints");
        lista.arraySize = waypoints.Length;
        for (int i = 0; i < waypoints.Length; i++)
        {
            lista.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
        }

        SerializedProperty fuente = so.FindProperty("audioSource");
        if (fuente != null) fuente.objectReferenceValue = null;

        so.ApplyModifiedProperties();
    }

    // ---------------------------------------------------------------- eleccion de celdas

    // Spawn del jugador, salida y enemigo original: de estos tres puntos se quieren alejar los
    // enemigos nuevos (el jugador no se los tiene que encontrar encima al aparecer, y no deben
    // apilarse con el que ya patrulla).
    static List<int> CeldasAEvitar(GrafoLaberinto grafo, Transform raizMapa, EnemyAI plantilla)
    {
        var celdas = new List<int>();

        PlayerStats jugador = Object.FindAnyObjectByType<PlayerStats>();
        if (jugador != null) celdas.Add(CeldaDe(grafo, raizMapa, jugador.transform.position));

        GameObject inicio = GameObject.Find("Punto_Inicio");
        if (inicio != null) celdas.Add(CeldaDe(grafo, raizMapa, inicio.transform.position));

        int salida = EscanerMapa.CeldaDeSalida(grafo, raizMapa);
        if (salida >= 0) celdas.Add(salida);

        celdas.Add(CeldaDe(grafo, raizMapa, plantilla.transform.position));
        return celdas;
    }

    // Greedy: la celda mas lejana (en pasos por el laberinto, no en linea recta) del conjunto a
    // evitar; una vez elegida se suma al conjunto, asi la siguiente tambien se separa de ella.
    static List<int> ElegirCeldas(GrafoLaberinto grafo, List<int> aEvitar, int cantidad)
    {
        var elegidas = new List<int>();
        var origenes = new List<int>(aEvitar);

        for (int n = 0; n < cantidad; n++)
        {
            int[] distancia = Distancias(grafo, origenes);
            int mejor = -1;
            int mejorDistancia = 0;

            for (int celda = 0; celda < grafo.Celdas; celda++)
            {
                if (distancia[celda] <= mejorDistancia) continue; // > 0 y el primer maximo gana: deterministico
                if (EsCeldaCerrada(grafo, celda)) continue;

                mejor = celda;
                mejorDistancia = distancia[celda];
            }

            if (mejor < 0) break;
            elegidas.Add(mejor);
            origenes.Add(mejor);
        }

        return elegidas;
    }

    // Celdas para la ruta de patrulla: a unos pocos pasos del enemigo y lo mas separadas entre si
    // que se pueda, para que la ronda recorra su zona en vez de ir y venir por el mismo pasillo.
    static List<int> CeldasDePatrulla(GrafoLaberinto grafo, int celdaEnemigo, int cantidad)
    {
        int[] distancia = Distancias(grafo, new List<int> { celdaEnemigo });

        var candidatas = new List<int>();
        for (int celda = 0; celda < grafo.Celdas; celda++)
        {
            if (distancia[celda] < DistanciaMinimaWaypoint || distancia[celda] > DistanciaMaximaWaypoint) continue;
            if (EsCeldaCerrada(grafo, celda)) continue;
            candidatas.Add(celda);
        }

        var elegidas = new List<int>();
        while (elegidas.Count < cantidad && candidatas.Count > 0)
        {
            int mejor = -1;
            int mejorPuntaje = -1;

            foreach (int celda in candidatas)
            {
                // Primera vuelta: la mas lejana del enemigo. Despues: la que mejor se separa de las
                // que ya estan elegidas.
                int puntaje = elegidas.Count == 0 ? distancia[celda] : int.MaxValue;
                foreach (int ya in elegidas) puntaje = Mathf.Min(puntaje, Manhattan(grafo, celda, ya));

                if (puntaje <= mejorPuntaje) continue;
                mejor = celda;
                mejorPuntaje = puntaje;
            }

            if (mejor < 0) break;
            elegidas.Add(mejor);
            candidatas.Remove(mejor);
        }

        return elegidas;
    }

    // BFS multiorigen por pasos del laberinto. -1 = no se llega (celda cerrada o en otra isla).
    static int[] Distancias(GrafoLaberinto grafo, List<int> origenes)
    {
        var distancia = new int[grafo.Celdas];
        for (int i = 0; i < distancia.Length; i++) distancia[i] = -1;

        var cola = new Queue<int>();
        foreach (int origen in origenes)
        {
            if (origen < 0 || origen >= grafo.Celdas || distancia[origen] == 0) continue;
            distancia[origen] = 0;
            cola.Enqueue(origen);
        }

        while (cola.Count > 0)
        {
            int actual = cola.Dequeue();
            foreach (int vecino in Vecinos(grafo, actual))
            {
                if (distancia[vecino] >= 0) continue;
                distancia[vecino] = distancia[actual] + 1;
                cola.Enqueue(vecino);
            }
        }

        return distancia;
    }

    // Celdas contiguas sin pared en el hueco del medio. La celda (r,c) vive en el tile (2r+1, 2c+1)
    // de la grilla de paredes, y entre dos celdas vecinas hay siempre exactamente un tile de hueco.
    static IEnumerable<int> Vecinos(GrafoLaberinto grafo, int celda)
    {
        int r = grafo.FilaDe(celda);
        int c = grafo.ColumnaDe(celda);

        if (r > 0 && !grafo.EsParedTile(2 * r, 2 * c + 1)) yield return grafo.IdCelda(r - 1, c);
        if (r < grafo.Filas - 1 && !grafo.EsParedTile(2 * r + 2, 2 * c + 1)) yield return grafo.IdCelda(r + 1, c);
        if (c > 0 && !grafo.EsParedTile(2 * r + 1, 2 * c)) yield return grafo.IdCelda(r, c - 1);
        if (c < grafo.Columnas - 1 && !grafo.EsParedTile(2 * r + 1, 2 * c + 2)) yield return grafo.IdCelda(r, c + 1);
    }

    // Una celda tapada por geometria (la escalera, el cuarto del comerciante): no es piso caminable.
    static bool EsCeldaCerrada(GrafoLaberinto grafo, int celda)
    {
        return grafo.EsParedTile(2 * grafo.FilaDe(celda) + 1, 2 * grafo.ColumnaDe(celda) + 1);
    }

    static int Manhattan(GrafoLaberinto grafo, int a, int b)
    {
        return Mathf.Abs(grafo.FilaDe(a) - grafo.FilaDe(b)) + Mathf.Abs(grafo.ColumnaDe(a) - grafo.ColumnaDe(b));
    }

    static Vector3 PosicionDeCelda(GrafoLaberinto grafo, Transform raizMapa, int celda)
    {
        Vector3 local = MapaLayout.GrillaALocal(2 * grafo.FilaDe(celda) + 1, 2 * grafo.ColumnaDe(celda) + 1);
        Vector3 mundo = raizMapa.TransformPoint(local);
        mundo.y = raizMapa.position.y + AlturaDeDiseno;
        return mundo;
    }

    static int CeldaDe(GrafoLaberinto grafo, Transform raizMapa, Vector3 posicionMundo)
    {
        MapaLayout.CeldaMasCercana(raizMapa.InverseTransformPoint(posicionMundo), out int r, out int c);
        return grafo.IdCelda(r, c);
    }
}
