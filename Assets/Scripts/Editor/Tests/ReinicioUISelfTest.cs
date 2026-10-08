using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Autotest de editor de la recreacion de interfaz al reiniciar (issue #55).
// Casos CP-RUI-01..12 sobre PlayerUI.EnsureExists, GameOverUI.EnsureExists,
// PromptInteraccion.EnsureExists y EditorBuildSettings.
// No abre ni guarda escenas y no crea assets en disco.
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod ReinicioUISelfTest.RunAllAndExit -logFile <log>
public static class ReinicioUISelfTest
{
    const string Tag = "[ReinicioUISelfTest]";

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/Reinicio UI")]
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

        // CP-RUI-01..04: PlayerUI
        Run("CP-RUI-01", "PlayerUI: con un PlayerStats en la escena y sin PlayerUI, EnsureExists crea exactamente 1", () =>
        {
            UiFixture<PlayerUI> fx = null;
            try
            {
                fx = new UiFixture<PlayerUI>();
                if (fx.CountNew() != 0) return "ya habia un PlayerUI nuevo antes de llamar a EnsureExists";

                fx.CreatePlayerStats();
                PlayerUI.EnsureExists();

                int count = fx.CountNew();
                return count == 1 ? null : $"se crearon {count} PlayerUI (se esperaba 1)";
            }
            finally { fx?.Destroy(); }
        });

        Run("CP-RUI-02", "PlayerUI: llamar EnsureExists dos veces deja 1 solo PlayerUI", () =>
        {
            UiFixture<PlayerUI> fx = null;
            try
            {
                fx = new UiFixture<PlayerUI>();
                fx.CreatePlayerStats();
                PlayerUI.EnsureExists();
                PlayerUI.EnsureExists();

                int count = fx.CountNew();
                return count == 1 ? null : $"hay {count} PlayerUI despues de llamar dos veces (se esperaba 1)";
            }
            finally { fx?.Destroy(); }
        });

        Run("CP-RUI-03", "PlayerUI: despues de destruirlo, EnsureExists lo recrea", () =>
        {
            UiFixture<PlayerUI> fx = null;
            try
            {
                fx = new UiFixture<PlayerUI>();
                fx.CreatePlayerStats();
                PlayerUI.EnsureExists();

                PlayerUI[] first = fx.NewInstances();
                if (first.Length != 1) return $"setup invalido: {first.Length} PlayerUI antes de destruir (se esperaba 1)";
                UnityEngine.Object.DestroyImmediate(first[0].gameObject);

                PlayerUI.EnsureExists();
                int count = fx.CountNew();
                return count == 1 ? null : $"hay {count} PlayerUI despues de recrear (se esperaba 1)";
            }
            finally { fx?.Destroy(); }
        });

        Run("CP-RUI-04", "PlayerUI: sin PlayerStats en la escena, EnsureExists no crea nada", () =>
        {
            UiFixture<PlayerUI> fx = null;
            try
            {
                fx = new UiFixture<PlayerUI>();
                if (UnityEngine.Object.FindAnyObjectByType<PlayerStats>() != null)
                    return "la escena de la corrida batch ya tiene un PlayerStats; este caso no se puede validar aqui";

                PlayerUI.EnsureExists();
                int count = fx.CountNew();
                return count == 0 ? null : $"se crearon {count} PlayerUI sin PlayerStats en la escena";
            }
            finally { fx?.Destroy(); }
        });

        // CP-RUI-05..08: GameOverUI
        Run("CP-RUI-05", "GameOverUI: con un PlayerStats en la escena y sin GameOverUI, EnsureExists crea exactamente 1", () =>
        {
            UiFixture<GameOverUI> fx = null;
            try
            {
                fx = new UiFixture<GameOverUI>();
                if (fx.CountNew() != 0) return "ya habia un GameOverUI nuevo antes de llamar a EnsureExists";

                fx.CreatePlayerStats();
                GameOverUI.EnsureExists();

                int count = fx.CountNew();
                return count == 1 ? null : $"se crearon {count} GameOverUI (se esperaba 1)";
            }
            finally { fx?.Destroy(); }
        });

        Run("CP-RUI-06", "GameOverUI: llamar EnsureExists dos veces deja 1 solo GameOverUI", () =>
        {
            UiFixture<GameOverUI> fx = null;
            try
            {
                fx = new UiFixture<GameOverUI>();
                fx.CreatePlayerStats();
                GameOverUI.EnsureExists();
                GameOverUI.EnsureExists();

                int count = fx.CountNew();
                return count == 1 ? null : $"hay {count} GameOverUI despues de llamar dos veces (se esperaba 1)";
            }
            finally { fx?.Destroy(); }
        });

        Run("CP-RUI-07", "GameOverUI: despues de destruirlo, EnsureExists lo recrea", () =>
        {
            UiFixture<GameOverUI> fx = null;
            try
            {
                fx = new UiFixture<GameOverUI>();
                fx.CreatePlayerStats();
                GameOverUI.EnsureExists();

                GameOverUI[] first = fx.NewInstances();
                if (first.Length != 1) return $"setup invalido: {first.Length} GameOverUI antes de destruir (se esperaba 1)";
                UnityEngine.Object.DestroyImmediate(first[0].gameObject);

                GameOverUI.EnsureExists();
                int count = fx.CountNew();
                return count == 1 ? null : $"hay {count} GameOverUI despues de recrear (se esperaba 1)";
            }
            finally { fx?.Destroy(); }
        });

        Run("CP-RUI-08", "GameOverUI: sin PlayerStats en la escena, EnsureExists no crea nada", () =>
        {
            UiFixture<GameOverUI> fx = null;
            try
            {
                fx = new UiFixture<GameOverUI>();
                if (UnityEngine.Object.FindAnyObjectByType<PlayerStats>() != null)
                    return "la escena de la corrida batch ya tiene un PlayerStats; este caso no se puede validar aqui";

                GameOverUI.EnsureExists();
                int count = fx.CountNew();
                return count == 0 ? null : $"se crearon {count} GameOverUI sin PlayerStats en la escena";
            }
            finally { fx?.Destroy(); }
        });

        // CP-RUI-09..11: PromptInteraccion (no depende de PlayerStats)
        Run("CP-RUI-09", "PromptInteraccion: sin instancia previa, EnsureExists crea exactamente 1", () =>
        {
            UiFixture<PromptInteraccion> fx = null;
            try
            {
                fx = new UiFixture<PromptInteraccion>();
                if (fx.CountNew() != 0) return "ya habia una PromptInteraccion nueva antes de llamar a EnsureExists";

                PromptInteraccion.EnsureExists();
                int count = fx.CountNew();
                return count == 1 ? null : $"se crearon {count} PromptInteraccion (se esperaba 1)";
            }
            finally { fx?.Destroy(); }
        });

        Run("CP-RUI-10", "PromptInteraccion: llamar EnsureExists dos veces deja 1 sola instancia", () =>
        {
            // A diferencia de una carga de escena real en Play mode, en modo edicion/batch
            // AddComponent no dispara Awake() de forma sincronica: sin eso, Instancia seguiria en
            // null para el segundo EnsureExists y se crearia una segunda instancia por un artefacto
            // del entorno de prueba, no por un bug del arranque. Se fuerza Awake por reflexion
            // entre los dos llamados para reproducir el orden real (Awake corre antes de que algo
            // mas pueda volver a pedir la creacion).
            UiFixture<PromptInteraccion> fx = null;
            try
            {
                fx = new UiFixture<PromptInteraccion>();
                PromptInteraccion.EnsureExists();

                PromptInteraccion[] firstBatch = fx.NewInstances();
                if (firstBatch.Length != 1) return $"setup invalido: {firstBatch.Length} PromptInteraccion tras el primer EnsureExists (se esperaba 1)";

                MethodInfo awake = typeof(PromptInteraccion).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
                if (awake == null) return "no se encontro PromptInteraccion.Awake por reflexion";
                awake.Invoke(firstBatch[0], null);

                PromptInteraccion.EnsureExists();

                int count = fx.CountNew();
                return count == 1 ? null : $"hay {count} PromptInteraccion despues de llamar dos veces (se esperaba 1)";
            }
            finally { fx?.Destroy(); }
        });

        Run("CP-RUI-11", "PromptInteraccion: despues de destruirla, EnsureExists la recrea", () =>
        {
            UiFixture<PromptInteraccion> fx = null;
            try
            {
                fx = new UiFixture<PromptInteraccion>();
                PromptInteraccion.EnsureExists();

                PromptInteraccion[] first = fx.NewInstances();
                if (first.Length != 1) return $"setup invalido: {first.Length} PromptInteraccion antes de destruir (se esperaba 1)";
                UnityEngine.Object.DestroyImmediate(first[0].gameObject);

                PromptInteraccion.EnsureExists();
                int count = fx.CountNew();
                return count == 1 ? null : $"hay {count} PromptInteraccion despues de recrear (se esperaba 1)";
            }
            finally { fx?.Destroy(); }
        });

        // CP-RUI-12: Build Settings
        Run("CP-RUI-12", "EditorBuildSettings tiene exactamente 1 escena, Prototype.unity, habilitada y con el guid correcto", () =>
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length != 1) return $"hay {scenes.Length} escenas en Build Settings (se esperaba 1)";

            EditorBuildSettingsScene scene = scenes[0];
            if (!scene.enabled) return "la escena no esta habilitada";
            if (scene.path != "Assets/Scenes/Prototype.unity") return $"path = '{scene.path}'";

            string expectedGuid = AssetDatabase.AssetPathToGUID("Assets/Scenes/Prototype.unity");
            if (string.IsNullOrEmpty(expectedGuid)) return "no se pudo leer el guid de Assets/Scenes/Prototype.unity.meta";
            if (scene.guid.ToString() != expectedGuid) return $"guid = '{scene.guid}', se esperaba '{expectedGuid}'";

            return null;
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
            error = $"excepción {e.GetType().Name}: {e.Message}";
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

    // Mide y limpia solo las instancias de T que cada caso crea (T = PlayerUI, GameOverUI o
    // PromptInteraccion), sin asumir que la escena de la corrida batch estaba vacia. Los
    // PlayerStats creados con CreatePlayerStats tambien se destruyen en Destroy().
    class UiFixture<T> where T : MonoBehaviour
    {
        readonly List<GameObject> created = new List<GameObject>();
        readonly HashSet<T> baseline;

        public UiFixture()
        {
            baseline = new HashSet<T>(UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include));
        }

        public PlayerStats CreatePlayerStats()
        {
            GameObject go = new GameObject("CP_RUI_PlayerStats");
            go.hideFlags = HideFlags.None;
            PlayerStats stats = go.AddComponent<PlayerStats>();
            created.Add(go);
            return stats;
        }

        public T[] NewInstances()
        {
            List<T> list = new List<T>();
            foreach (T instance in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include))
            {
                if (!baseline.Contains(instance)) list.Add(instance);
            }
            return list.ToArray();
        }

        public int CountNew() => NewInstances().Length;

        public void Destroy()
        {
            foreach (T instance in NewInstances())
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
            foreach (GameObject go in created)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
