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

        return surface;
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
