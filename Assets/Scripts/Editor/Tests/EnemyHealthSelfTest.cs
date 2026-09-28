using System;
using UnityEditor;
using UnityEngine;

// Autotest de editor de EnemyHealth (HU-14, combate). Casos CP-SAL-01..03.
// No abre ni guarda escenas y no crea assets en disco: todo lo que crea vive en memoria con
// HideAndDontSave y se destruye al terminar cada caso (mismo patron que InventorySelfTest).
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod EnemyHealthSelfTest.RunAllAndExit -logFile <log>
public static class EnemyHealthSelfTest
{
    const string Tag = "[EnemyHealthSelfTest]";

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/EnemyHealth")]
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

        Run("CP-SAL-01", "TakeDamage reduce VidaActual y dispara AlRecibirDaño con (actual, maximo)", f =>
        {
            float actualRecibido = -1f, maximoRecibido = -1f;
            bool disparo = false;
            f.salud.AlRecibirDaño += (actual, maximo) =>
            {
                disparo = true;
                actualRecibido = actual;
                maximoRecibido = maximo;
            };

            f.salud.TakeDamage(20f);

            if (!Mathf.Approximately(f.salud.VidaActual, 30f)) return $"VidaActual = {f.salud.VidaActual} (se esperaba 30)";
            if (!disparo) return "AlRecibirDaño no se disparo";
            if (!Mathf.Approximately(actualRecibido, 30f) || !Mathf.Approximately(maximoRecibido, 50f))
                return $"AlRecibirDaño = ({actualRecibido}, {maximoRecibido}) (se esperaba (30, 50))";
            return null;
        });

        Run("CP-SAL-02", "TakeDamage hasta 0 o menos dispara AlMorir una sola vez y VidaActual no baja de 0", f =>
        {
            int muertes = 0;
            f.salud.AlMorir += () => muertes++;

            f.salud.TakeDamage(60f); // mas que maxHealth (50): deberia matar
            f.salud.TakeDamage(10f); // ya muerto: no debe volver a disparar ni bajar de 0

            if (!Mathf.Approximately(f.salud.VidaActual, 0f)) return $"VidaActual = {f.salud.VidaActual} (se esperaba 0)";
            if (muertes != 1) return $"AlMorir se disparo {muertes} veces (se esperaba 1)";
            return null;
        });

        Run("CP-SAL-03", "TakeDamage con cantidad <= 0 no cambia la vida ni dispara eventos", f =>
        {
            bool disparo = false;
            f.salud.AlRecibirDaño += (a, m) => disparo = true;

            f.salud.TakeDamage(0f);
            f.salud.TakeDamage(-5f);

            if (!Mathf.Approximately(f.salud.VidaActual, 50f)) return $"VidaActual = {f.salud.VidaActual} (se esperaba 50)";
            if (disparo) return "AlRecibirDaño se disparo con daño <= 0";
            return null;
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

    class Fixture
    {
        public readonly EnemyHealth salud;

        readonly GameObject go;

        public Fixture()
        {
            go = new GameObject("EnemyHealthSelfTest") { hideFlags = HideFlags.HideAndDontSave };
            salud = go.AddComponent<EnemyHealth>();
        }

        public void Destroy()
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
