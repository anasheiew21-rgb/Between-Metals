using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

// Autotest de editor del NavMesh en tiempo de ejecucion (HU-08, RF10, issue #58). Casos CP-NAV-01..08.
// Abre Prototype.unity para validar contra el laberinto real, pero NUNCA la guarda: el objeto
// "_NavMeshRuntime" y los NavMeshModifier que agrega NavMeshRuntimeBuilder quedan solo en memoria.
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod EnemyNavigationSelfTest.RunAllAndExit -logFile <log>
public static class EnemyNavigationSelfTest
{
    const string Tag = "[EnemyNavigationSelfTest]";
    const string EscenaPrototipo = "Assets/Scenes/Prototype.unity";
    const float DistanciaMaximaAlNavMesh = 1f;
    const long TiempoMaximoConstruccionMs = 2000;

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/Navegacion Enemigo")]
    static void RunFromMenu()
    {
        RunAll();
    }

    public static void RunAllAndExit()
    {
        bool ok = false;
        try
        {
            ok = RunAll();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool RunAll()
    {
        passed = 0;
        failed = 0;

        EditorSceneManager.OpenScene(EscenaPrototipo, OpenSceneMode.Single);

        Stopwatch cronometro = Stopwatch.StartNew();
        NavMeshRuntimeBuilder.EnsureBuilt();
        cronometro.Stop();
        long msConstruccion = cronometro.ElapsedMilliseconds;

        EnemyAI[] enemigos = UnityEngine.Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude);
        PlayerStats jugador = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();

        Run("CP-NAV-01", "El NavMesh tiene triangulos", () =>
        {
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            return tri.indices.Length > 0 ? null : "0 indices en la triangulacion";
        });

        Run("CP-NAV-02", "La posicion del enemigo esta a 1m o menos del NavMesh", () =>
        {
            if (enemigos.Length == 0) return "no hay ningun EnemyAI en la escena";
            Vector3 pos = enemigos[0].transform.position;
            if (!NavMesh.SamplePosition(pos, out NavMeshHit hit, DistanciaMaximaAlNavMesh, NavMesh.AllAreas))
                return $"sin punto de NavMesh a {DistanciaMaximaAlNavMesh}m de {pos}";
            return null;
        });

        Run("CP-NAV-03", "La posicion del jugador esta a 1m o menos del NavMesh", () =>
        {
            if (jugador == null) return "no se encontro ningun PlayerStats en la escena";
            Vector3 pos = jugador.transform.position;
            if (!NavMesh.SamplePosition(pos, out NavMeshHit hit, DistanciaMaximaAlNavMesh, NavMesh.AllAreas))
                return $"sin punto de NavMesh a {DistanciaMaximaAlNavMesh}m de {pos}";
            return null;
        });

        Run("CP-NAV-04", "Existe un camino completo del enemigo al jugador", () =>
        {
            if (enemigos.Length == 0) return "no hay ningun EnemyAI en la escena";
            if (jugador == null) return "no se encontro ningun PlayerStats en la escena";

            NavMeshPath path = new NavMeshPath();
            bool calculo = NavMesh.CalculatePath(enemigos[0].transform.position, jugador.transform.position, NavMesh.AllAreas, path);
            if (!calculo || path.status != NavMeshPathStatus.PathComplete)
                return $"status = {path.status}";

            Debug.Log($"{Tag} CP-NAV-04 longitud del camino: {LargoDelCamino(path):0.0} m");
            return null;
        });

        Run("CP-NAV-05", "Cada waypoint no nulo del enemigo es alcanzable", () =>
        {
            if (enemigos.Length == 0) return "no hay ningun EnemyAI en la escena";
            Transform[] waypoints = ObtenerWaypoints(enemigos[0]);
            if (waypoints == null) return "no se pudo leer el campo 'waypoints' por reflexion";

            var nulos = new List<int>();
            var inalcanzables = new List<int>();

            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null)
                {
                    nulos.Add(i);
                    continue;
                }
                if (!NavMesh.SamplePosition(waypoints[i].position, out NavMeshHit hit, DistanciaMaximaAlNavMesh, NavMesh.AllAreas))
                {
                    inalcanzables.Add(i);
                }
            }

            if (nulos.Count > 0) Debug.LogWarning($"{Tag} CP-NAV-05 waypoints null en los indices: {string.Join(", ", nulos)}");
            if (inalcanzables.Count > 0) return $"waypoints inalcanzables en los indices: {string.Join(", ", inalcanzables)}";
            return null;
        });

        Run("CP-NAV-06", "(informativo) Punto_Inicio y Punto_Salida son alcanzables desde el jugador", () =>
        {
            if (jugador == null) return null; // informativo: no hace fallar el resto de la suite

            foreach (string nombre in new[] { "Punto_Inicio", "Punto_Salida" })
            {
                GameObject punto = GameObject.Find(nombre);
                if (punto == null)
                {
                    Debug.LogWarning($"{Tag} CP-NAV-06 no existe '{nombre}' en la escena");
                    continue;
                }

                NavMeshPath path = new NavMeshPath();
                bool calculo = NavMesh.CalculatePath(jugador.transform.position, punto.transform.position, NavMesh.AllAreas, path);
                string estado = calculo ? path.status.ToString() : "CalculatePath devolvio false";
                Debug.Log($"{Tag} CP-NAV-06 '{nombre}': {estado}");
            }
            return null;
        });

        Run("CP-NAV-07", "El NavMesh se construyo en menos de 2000 ms", () =>
        {
            if (msConstruccion >= TiempoMaximoConstruccionMs)
            {
                Debug.LogWarning($"{Tag} CP-NAV-07 riesgo para RNF01: {msConstruccion} ms >= {TiempoMaximoConstruccionMs} ms");
                return $"{msConstruccion} ms >= {TiempoMaximoConstruccionMs} ms";
            }
            return null;
        });

        Run("CP-NAV-08", "EnsureBuilt() llamado dos veces no crea un segundo _NavMeshRuntime", () =>
        {
            NavMeshRuntimeBuilder.EnsureBuilt();
            int cantidad = ContarRaicesLlamadas("_NavMeshRuntime");
            return cantidad == 1 ? null : $"hay {cantidad} objetos '_NavMeshRuntime' en la escena (se esperaba 1)";
        });

        Debug.Log($"{Tag} RESULT: {passed} passed, {failed} failed");
        return failed == 0;
    }

    static void Run(string id, string description, Func<string> test)
    {
        string error;
        try
        {
            error = test();
        }
        catch (Exception e)
        {
            error = $"excepcion {e.GetType().Name}: {e.Message}";
        }

        if (error == null)
        {
            passed++;
            Debug.Log($"{Tag} {id} PASS - {description}");
        }
        else
        {
            failed++;
            Debug.LogError($"{Tag} {id} FAIL - {description} - {error}");
        }
    }

    static Transform[] ObtenerWaypoints(EnemyAI enemigo)
    {
        FieldInfo campo = typeof(EnemyAI).GetField("waypoints", BindingFlags.Instance | BindingFlags.NonPublic);
        return campo?.GetValue(enemigo) as Transform[];
    }

    static float LargoDelCamino(NavMeshPath path)
    {
        float largo = 0f;
        for (int i = 1; i < path.corners.Length; i++)
            largo += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return largo;
    }

    static int ContarRaicesLlamadas(string nombre)
    {
        int cantidad = 0;
        foreach (GameObject raiz in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (raiz.name == nombre) cantidad++;
        }
        return cantidad;
    }
}
