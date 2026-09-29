using System.Diagnostics;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

// Genera el NavMesh en tiempo de ejecucion para escenas con enemigos: el laberinto lo arma
// MapaBuilder (herramienta de editor) y puede regenerarse en cualquier momento, asi que no tiene
// sentido hornear y guardar un NavMesh en el archivo de escena, se quedaria desactualizado.
// HU-08 / RF10 (issue #58): esto solo arma el camino, EnemyAI todavia no lo usa (Tarea B).
public static class NavMeshRuntimeBuilder
{
    const string NombreObjeto = "_NavMeshRuntime";
    const string NombreObjetoLinks = "_NavMeshLinksRuntime";
    // Si el punto mas cercano alcanzable de un lado queda a mas de esto del punto mas cercano
    // alcanzable del otro lado, no se asume que sea el mismo hueco/puerta: se prefiere dejar el
    // camino incompleto antes que tender un link gigante entre dos zonas del mapa que en realidad
    // no deberian estar conectadas.
    const float DistanciaMaximaLink = 6f;
    const float RadioMuestreoConexion = 5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCrear()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
        EnsureBuilt();
    }

    static void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        EnsureBuilt();
    }

    // Sin campos estaticos de estado: cada llamada busca en la escena en vez de recordar si ya
    // construyo antes, asi que llamarla dos veces (o desde dos puntos de entrada, como arriba) es seguro.
    public static NavMeshSurface EnsureBuilt()
    {
        if (Object.FindAnyObjectByType<EnemyAI>() == null) return null;

        GameObject existente = GameObject.Find(NombreObjeto);
        if (existente != null) return existente.GetComponent<NavMeshSurface>();

        GameObject go = new GameObject(NombreObjeto);
        NavMeshSurface surface = go.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.agentTypeID = 0;

        ExcluirLoQueSeMueve();
        BuildNavMesh(surface);
        ConectarIslasDesconectadas();

        return surface;
    }

    // El "Glade" que arma SpawnZoneBuilder (o cualquier otra pieza generada por separado, como el
    // laberinto de MapaBuilder) puede quedar geometricamente pegada al resto del mapa sin que la
    // voxelizacion del NavMesh las una en una sola region (una costura de un par de cm alcanza para
    // que Recast las trate como islas separadas). En vez de asumir donde esta esa costura, se la
    // busca en tiempo de ejecucion: el ultimo corner de un camino PathPartial es el punto mas
    // cercano al destino dentro de la propia isla (el borde de la costura de este lado); desde ahi,
    // BuscarPuenteCercano barre un radio corto alrededor buscando el primer punto que SI este
    // conectado con el destino (el otro lado de la misma costura), y un NavMeshLink entre ambos
    // tiende el puente. Nunca toca la escena guardada: todo esto vive en memoria (HU-08).
    static void ConectarIslasDesconectadas()
    {
        PlayerStats jugador = Object.FindAnyObjectByType<PlayerStats>();
        if (jugador == null) return;
        if (!NavMesh.SamplePosition(jugador.transform.position, out NavMeshHit hitJugador, RadioMuestreoConexion, NavMesh.AllAreas)) return;

        if (GameObject.Find(NombreObjetoLinks) != null) return; // ya se corrio antes en esta escena

        GameObject contenedor = null;

        foreach (EnemyAI enemigo in Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude))
        {
            if (!NavMesh.SamplePosition(enemigo.transform.position, out NavMeshHit hitEnemigo, RadioMuestreoConexion, NavMesh.AllAreas))
                continue;

            NavMeshPath ida = new NavMeshPath();
            bool calculoIda = NavMesh.CalculatePath(hitEnemigo.position, hitJugador.position, NavMesh.AllAreas, ida);
            if (calculoIda && ida.status == NavMeshPathStatus.PathComplete) continue; // ya conectados
            if (ida.corners.Length == 0) continue;

            Vector3 borde = ida.corners[ida.corners.Length - 1];
            if (!BuscarPuenteCercano(borde, hitJugador.position, DistanciaMaximaLink, out Vector3 puente)) continue;

            if (contenedor == null) contenedor = new GameObject(NombreObjetoLinks);
            CrearLink(contenedor.transform, borde, puente);
        }
    }

    // Barre circulos concentricos (de 0.5 en 0.5 m, en 16 direcciones) alrededor de "borde" hasta
    // "radioMaximo", buscando el primer punto de NavMesh que tenga un camino completo hacia
    // "referenciaConectada". "borde" mismo nunca lo tiene (es, por definicion, el limite de una isla
    // que no llega a esa referencia): lo que se busca es el punto valido mas cercano del otro lado
    // de la costura.
    static bool BuscarPuenteCercano(Vector3 borde, Vector3 referenciaConectada, float radioMaximo, out Vector3 puente)
    {
        const int Direcciones = 16;
        const float Paso = 0.5f;

        for (float radio = Paso; radio <= radioMaximo; radio += Paso)
        {
            for (int i = 0; i < Direcciones; i++)
            {
                float angulo = i * (360f / Direcciones) * Mathf.Deg2Rad;
                Vector3 candidato = borde + new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * radio;

                if (!NavMesh.SamplePosition(candidato, out NavMeshHit hit, Paso * 0.75f, NavMesh.AllAreas)) continue;

                NavMeshPath prueba = new NavMeshPath();
                bool conectado = NavMesh.CalculatePath(referenciaConectada, hit.position, NavMesh.AllAreas, prueba)
                    && prueba.status == NavMeshPathStatus.PathComplete;

                if (conectado)
                {
                    puente = hit.position;
                    return true;
                }
            }
        }

        puente = borde;
        return false;
    }

    static void CrearLink(Transform padre, Vector3 a, Vector3 b)
    {
        GameObject go = new GameObject("NavMeshLink_Auto");
        go.transform.SetParent(padre, true);
        go.transform.position = a;

        NavMeshLink link = go.AddComponent<NavMeshLink>();
        link.startPoint = Vector3.zero;
        link.endPoint = go.transform.InverseTransformPoint(b);
        link.width = 1f;
        link.bidirectional = true;
        link.agentTypeID = 0;
        link.UpdateLink();

        Debug.Log($"[NavMeshRuntime] NavMeshLink automatico entre {a} y {b} (distancia {Vector3.Distance(a, b):0.00} m)");
    }

    // Todo lo que camina o se recoge queda afuera del NavMesh: si no, el propio collider del
    // jugador o de un enemigo agregaria geometria fantasma justo donde no hace falta caminar.
    static void ExcluirLoQueSeMueve()
    {
        PlayerStats jugador = Object.FindAnyObjectByType<PlayerStats>();
        if (jugador != null)
        {
            Excluir(jugador.gameObject);
            Excluir(jugador.transform.root.gameObject);
        }

        foreach (CharacterController cc in Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude))
        {
            Excluir(cc.gameObject);
        }

        foreach (EnemyAI enemigo in Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude))
        {
            Excluir(enemigo.gameObject);
        }

        // ItemPickup puede no existir todavia en esta rama (viene de feature/inventario), asi que
        // se busca el tipo por nombre en vez de referenciar la clase directo: compila en las dos.
        System.Type tipoItemPickup = BuscarTipoPorNombre("ItemPickup");
        if (tipoItemPickup != null)
        {
            foreach (Object item in Object.FindObjectsByType(tipoItemPickup, FindObjectsInactive.Exclude))
            {
                if (item is Component componente) Excluir(componente.gameObject);
            }
        }
    }

    static System.Type BuscarTipoPorNombre(string nombre)
    {
        foreach (System.Reflection.Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type tipo = asm.GetType(nombre);
            if (tipo != null) return tipo;
        }
        return null;
    }

    static void Excluir(GameObject objeto)
    {
        if (objeto == null) return;
        NavMeshModifier modifier = objeto.GetComponent<NavMeshModifier>();
        if (modifier == null) modifier = objeto.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true;
    }

    static void BuildNavMesh(NavMeshSurface surface)
    {
        Stopwatch cronometro = Stopwatch.StartNew();
        surface.BuildNavMesh();
        cronometro.Stop();

        NavMeshTriangulation triangulacion = NavMesh.CalculateTriangulation();
        int triangulos = triangulacion.indices.Length / 3;
        Debug.Log(string.Format("[NavMeshRuntime] built in {0} ms, triangles: {1}", cronometro.ElapsedMilliseconds, triangulos));
    }
}
