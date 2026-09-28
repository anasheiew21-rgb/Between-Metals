using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Autotest de editor de GameManager (HU-12/13, victoria/derrota). Casos CP-GM-01..11.
// No abre ni guarda escenas y no crea assets en disco: todo lo que crea vive en memoria con
// HideAndDontSave y se destruye al terminar cada caso (mismo patron que EnemyHealthSelfTest).
// GameManager es una clase estatica: cada caso resetea su estado por reflexion antes y despues de
// correr, para no arrastrar estado entre casos ni hacia otros self-tests de la misma corrida batch.
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod GameManagerSelfTest.RunAllAndExit -logFile <log>
public static class GameManagerSelfTest
{
    const string Tag = "[GameManagerSelfTest]";

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/GameManager")]
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

        Run("CP-GM-01", "Ganar() cambia EstadoActual a Victoria y dispara AlGanar exactamente una vez", f =>
        {
            int veces = 0;
            GameManager.AlGanar += () => veces++;

            GameManager.Ganar();

            if (GameManager.EstadoActual != GameManager.Estado.Victoria) return $"EstadoActual = {GameManager.EstadoActual} (se esperaba Victoria)";
            return veces == 1 ? null : $"AlGanar se disparo {veces} veces (se esperaba 1)";
        });

        Run("CP-GM-02", "Perder() cambia EstadoActual a Derrota y dispara AlPerder exactamente una vez", f =>
        {
            int veces = 0;
            GameManager.AlPerder += () => veces++;

            GameManager.Perder();

            if (GameManager.EstadoActual != GameManager.Estado.Derrota) return $"EstadoActual = {GameManager.EstadoActual} (se esperaba Derrota)";
            return veces == 1 ? null : $"AlPerder se disparo {veces} veces (se esperaba 1)";
        });

        Run("CP-GM-03", "Ganar() pausa el juego y desbloquea el cursor", f =>
        {
            GameManager.Ganar();

            if (!Mathf.Approximately(Time.timeScale, 0f)) return $"Time.timeScale = {Time.timeScale} (se esperaba 0)";
            if (Cursor.lockState != CursorLockMode.None) return $"Cursor.lockState = {Cursor.lockState} (se esperaba None)";
            return Cursor.visible ? null : "Cursor.visible = false (se esperaba true)";
        });

        Run("CP-GM-04", "Perder() pausa el juego y desbloquea el cursor", f =>
        {
            GameManager.Perder();

            if (!Mathf.Approximately(Time.timeScale, 0f)) return $"Time.timeScale = {Time.timeScale} (se esperaba 0)";
            if (Cursor.lockState != CursorLockMode.None) return $"Cursor.lockState = {Cursor.lockState} (se esperaba None)";
            return Cursor.visible ? null : "Cursor.visible = false (se esperaba true)";
        });

        Run("CP-GM-05", "Llamar Ganar() dos veces no dispara AlGanar dos veces (idempotencia)", f =>
        {
            int veces = 0;
            GameManager.AlGanar += () => veces++;

            GameManager.Ganar();
            GameManager.Ganar();

            return veces == 1 ? null : $"AlGanar se disparo {veces} veces (se esperaba 1)";
        });

        Run("CP-GM-06", "Si ya se gano, Perder() no cambia el estado ni dispara AlPerder", f =>
        {
            int veces = 0;
            GameManager.AlPerder += () => veces++;

            GameManager.Ganar();
            GameManager.Perder();

            if (GameManager.EstadoActual != GameManager.Estado.Victoria) return $"EstadoActual = {GameManager.EstadoActual} (se esperaba que siguiera en Victoria)";
            return veces == 0 ? null : $"AlPerder se disparo {veces} veces (se esperaba 0)";
        });

        Run("CP-GM-07", "Si ya se perdio, Ganar() no cambia el estado ni dispara AlGanar", f =>
        {
            int veces = 0;
            GameManager.AlGanar += () => veces++;

            GameManager.Perder();
            GameManager.Ganar();

            if (GameManager.EstadoActual != GameManager.Estado.Derrota) return $"EstadoActual = {GameManager.EstadoActual} (se esperaba que siguiera en Derrota)";
            return veces == 0 ? null : $"AlGanar se disparo {veces} veces (se esperaba 0)";
        });

        Run("CP-GM-08", "ExitTrigger.Interactuar() dispara la Victoria", f =>
        {
            GameObject go = new GameObject("CP_GM_ExitTrigger", typeof(BoxCollider)) { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                ExitTrigger salida = go.AddComponent<ExitTrigger>();
                salida.Interactuar();

                return GameManager.EstadoActual == GameManager.Estado.Victoria ? null : $"EstadoActual = {GameManager.EstadoActual} (se esperaba Victoria)";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        });

        Run("CP-GM-09", "Wire() conecta PlayerStats.AlMorir con Perder(): matar al PlayerStats deja EstadoActual en Derrota", f =>
        {
            if (UnityEngine.Object.FindAnyObjectByType<PlayerStats>() != null)
                return "la escena de la corrida batch ya tiene un PlayerStats; este caso no se puede validar aqui";

            MethodInfo wire = typeof(GameManager).GetMethod("Wire", BindingFlags.NonPublic | BindingFlags.Static);
            if (wire == null) return "no se encontro GameManager.Wire por reflexion";

            // hideFlags = None (no HideAndDontSave): GameManager.Wire() lo busca con
            // FindAnyObjectByType, que no devuelve objetos HideAndDontSave (mismo motivo por el que
            // ReinicioUISelfTest.UiFixture.CreatePlayerStats usa hideFlags = None).
            GameObject go = new GameObject("CP_GM_PlayerStats") { hideFlags = HideFlags.None };
            try
            {
                PlayerStats stats = go.AddComponent<PlayerStats>();

                // AddComponent no dispara Awake() de forma sincronica en modo Editor/batch (mismo
                // problema que documenta ReinicioUISelfTest, CP-RUI-10): sin esto, currentHealth se
                // queda en 0 y TakeDamage no hace nada porque EstaViva ya seria false.
                MethodInfo awake = typeof(PlayerStats).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
                if (awake == null) return "no se encontro PlayerStats.Awake por reflexion";
                awake.Invoke(stats, null);

                wire.Invoke(null, null); // Wire() ya deja EstadoActual en Jugando antes de enganchar

                stats.TakeDamage(999999f);

                return GameManager.EstadoActual == GameManager.Estado.Derrota ? null : $"EstadoActual = {GameManager.EstadoActual} (se esperaba Derrota)";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        });

        Run("CP-GM-10", "ExitTrigger.Instalar() agrega el componente y un Collider trigger al marcador 'Punto_Salida'", f =>
        {
            if (GameObject.Find("Punto_Salida") != null)
                return "la escena de la corrida batch ya tiene un 'Punto_Salida'; este caso no se puede validar aqui";

            MethodInfo instalar = typeof(ExitTrigger).GetMethod("Instalar", BindingFlags.NonPublic | BindingFlags.Static);
            if (instalar == null) return "no se encontro ExitTrigger.Instalar por reflexion";

            GameObject marca = new GameObject("Punto_Salida") { hideFlags = HideFlags.None };
            try
            {
                instalar.Invoke(null, null);

                if (marca.GetComponent<ExitTrigger>() == null) return "no se agrego ningun ExitTrigger al marcador";

                Collider col = marca.GetComponent<Collider>();
                if (col == null) return "no se agrego ningun Collider al marcador";
                return col.isTrigger ? null : "el Collider agregado no es trigger";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(marca);
            }
        });

        Run("CP-GM-11", "ExitTrigger.Instalar() invocado dos veces no duplica el componente en 'Punto_Salida'", f =>
        {
            if (GameObject.Find("Punto_Salida") != null)
                return "la escena de la corrida batch ya tiene un 'Punto_Salida'; este caso no se puede validar aqui";

            MethodInfo instalar = typeof(ExitTrigger).GetMethod("Instalar", BindingFlags.NonPublic | BindingFlags.Static);
            if (instalar == null) return "no se encontro ExitTrigger.Instalar por reflexion";

            GameObject marca = new GameObject("Punto_Salida") { hideFlags = HideFlags.None };
            try
            {
                instalar.Invoke(null, null);
                instalar.Invoke(null, null);

                int cantidad = marca.GetComponents<ExitTrigger>().Length;
                return cantidad == 1 ? null : $"hay {cantidad} ExitTrigger en el marcador (se esperaba 1)";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(marca);
            }
        });

        Debug.Log($"{Tag} RESULT: {passed} passed, {failed} failed");
        return failed == 0;
    }

    static void Run(string id, string description, Func<Fixture, string> test)
    {
        Fixture fixture = null;
        string error;
        try
        {
            fixture = new Fixture();
            error = test(fixture);
        }
        catch (Exception e)
        {
            error = $"excepción {e.GetType().Name}: {e.Message}";
        }
        finally
        {
            fixture?.Destroy();
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

    // Resetea el estado estatico de GameManager antes y despues de cada caso, para que ninguno
    // arrastre el Victoria/Derrota (ni los suscriptores a AlGanar/AlPerder) del caso anterior, y
    // para no dejar Time.timeScale en 0 para el resto de la corrida batch.
    class Fixture
    {
        static readonly FieldInfo estadoField = typeof(GameManager).GetField("estadoActual", BindingFlags.NonPublic | BindingFlags.Static);
        static readonly FieldInfo alGanarField = typeof(GameManager).GetField("AlGanar", BindingFlags.NonPublic | BindingFlags.Static);
        static readonly FieldInfo alPerderField = typeof(GameManager).GetField("AlPerder", BindingFlags.NonPublic | BindingFlags.Static);

        public Fixture()
        {
            Reset();
        }

        public void Destroy()
        {
            Reset();
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
        }

        static void Reset()
        {
            if (estadoField != null) estadoField.SetValue(null, GameManager.Estado.Jugando);
            if (alGanarField != null) alGanarField.SetValue(null, null);
            if (alPerderField != null) alPerderField.SetValue(null, null);
        }
    }
}
