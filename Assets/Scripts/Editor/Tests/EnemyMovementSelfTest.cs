using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Prueba en Play Mode real que el EnemyAI con el modelo+Animator nuevos se mueve de verdad sobre
// el mapa (NavMeshAgent solo simula movimiento en Play, ver el comentario de EnemyAISelfTest sobre
// isOnNavMesh). Usa SessionState en vez de campos estaticos porque entrar/salir de Play Mode
// dispara un domain reload que los borraria a mitad de la espera.
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod EnemyMovementSelfTest.RunAndExit -logFile <log>
[InitializeOnLoad]
public static class EnemyMovementSelfTest
{
    const string Tag = "[EnemyMovementSelfTest]";
    const string EscenaPrototipo = "Assets/Scenes/Prototype.unity";
    const float SegundosAEsperar = 4f;
    const float DistanciaMinimaEsperada = 0.2f;

    const string ClaveEnCurso = "EnemyMovementSelfTest.EnCurso";
    const string ClaveInicioTime = "EnemyMovementSelfTest.InicioTime";
    const string ClaveInicioX = "EnemyMovementSelfTest.InicioX";
    const string ClaveInicioZ = "EnemyMovementSelfTest.InicioZ";

    static EnemyMovementSelfTest()
    {
        EditorApplication.update += Tick;
    }

    [MenuItem("Between Metals/Tests/Enemy Movement (Play Mode)")]
    public static void RunAndExit()
    {
        SessionState.EraseBool(ClaveEnCurso);
        EditorSceneManager.OpenScene(EscenaPrototipo, OpenSceneMode.Single);

        var enemigo = Object.FindAnyObjectByType<EnemyAI>();
        if (enemigo == null)
        {
            Debug.LogError($"{Tag} no hay ningun EnemyAI en la escena, no se puede probar nada");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        SessionState.SetBool(ClaveEnCurso, true);
        EditorApplication.isPlaying = true;
    }

    static void Tick()
    {
        if (!SessionState.GetBool(ClaveEnCurso, false)) return;
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;

        var enemigo = Object.FindAnyObjectByType<EnemyAI>();
        if (enemigo == null) return; // la escena todavia esta cargando este frame

        if (!SessionState.GetBool(ClaveInicioTime + ".set", false))
        {
            SessionState.SetFloat(ClaveInicioTime, Time.time);
            SessionState.SetFloat(ClaveInicioX, enemigo.transform.position.x);
            SessionState.SetFloat(ClaveInicioZ, enemigo.transform.position.z);
            SessionState.SetBool(ClaveInicioTime + ".set", true);
            return;
        }

        float inicioTime = SessionState.GetFloat(ClaveInicioTime, Time.time);
        if (Time.time - inicioTime < SegundosAEsperar) return;

        Vector3 posInicial = new Vector3(SessionState.GetFloat(ClaveInicioX, 0), 0, SessionState.GetFloat(ClaveInicioZ, 0));
        Vector3 posFinal = enemigo.transform.position;
        float distancia = Vector2.Distance(new Vector2(posInicial.x, posInicial.z), new Vector2(posFinal.x, posFinal.z));

        Animator animator = enemigo.GetComponentInChildren<Animator>();
        NavMeshAgent agente = enemigo.GetComponent<NavMeshAgent>();

        Debug.Log($"{Tag} posicion inicial={posInicial}, final={posFinal}, distancia recorrida={distancia:F3} m " +
            $"en {SegundosAEsperar} s de juego (estado={enemigo.EstadoActual}, " +
            $"agent.velocity={(agente != null ? agente.velocity.ToString("F2") : "sin NavMeshAgent")}, " +
            $"Animator={(animator != null ? "si" : "no")}, " +
            $"isHuman={(animator != null && animator.isHuman)}, " +
            $"Speed={(animator != null ? animator.GetFloat("Speed").ToString("F2") : "n/a")}, " +
            $"estadoAnim={(animator != null ? animator.GetCurrentAnimatorStateInfo(0).ToString() : "n/a")})");

        bool ok = distancia >= DistanciaMinimaEsperada;
        Debug.Log($"{Tag} RESULT: {(ok ? "PASS" : "FAIL")} (se esperaban al menos {DistanciaMinimaEsperada} m)");

        SessionState.SetBool(ClaveEnCurso, false);
        SessionState.EraseBool(ClaveInicioTime + ".set");
        EditorApplication.isPlaying = false;

        EditorApplication.delayCall += () =>
        {
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        };
    }
}
