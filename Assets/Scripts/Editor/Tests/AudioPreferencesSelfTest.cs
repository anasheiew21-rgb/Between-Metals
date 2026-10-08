using System;
using UnityEditor;
using UnityEngine;

// Self-test manual para AudioPreferences. No hay asmdef de test en el proyecto
// todavia, asi que esto corre como item de menu del Editor en vez de NUnit.
public static class AudioPreferencesSelfTest
{
    const string Tag = "[AudioPreferencesSelfTest]";

    static int passed;
    static int failed;

    // Version batch (issue #8), mismo patron que los demas autotests del proyecto
    // (EnemyAISelfTest, EnvironmentSelfTest): un caso CP-AUD-XX por Run(), una linea
    // PASS/FAIL por caso, una linea RESULT y EditorApplication.Exit al final.
    // Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod AudioPreferencesSelfTest.RunAllAndExit -logFile <log>
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

        // No pisar un perfil real guardado por el usuario mientras se prueba.
        bool hadBackup = PlayerPrefs.HasKey(AudioPreferences.PrefsKey);
        string backup = PlayerPrefs.GetString(AudioPreferences.PrefsKey, "");

        try
        {
            Run("CP-AUD-01", "Sin perfil guardado, los valores son el default (1,1,1)", () =>
            {
                PlayerPrefs.DeleteKey(AudioPreferences.PrefsKey);
                AudioPreferences.Load();
                return CercaDeTodos(
                    ("master", AudioPreferences.Master, 1f),
                    ("music", AudioPreferences.Music, 1f),
                    ("sfx", AudioPreferences.Sfx, 1f));
            });

            Run("CP-AUD-02", "ParseProfile(\"\") devuelve los defaults", () =>
            {
                AudioProfile vacio = AudioPreferences.ParseProfile("");
                return CercaDeTodos(("master", vacio.master, 1f), ("music", vacio.music, 1f), ("sfx", vacio.sfx, 1f));
            });

            Run("CP-AUD-03", "JSON parcial conserva los defaults en los campos ausentes", () =>
            {
                AudioProfile parcial = AudioPreferences.ParseProfile("{\"master\":0.5}");
                return CercaDeTodos(("master", parcial.master, 0.5f), ("music", parcial.music, 1f), ("sfx", parcial.sfx, 1f));
            });

            Run("CP-AUD-04", "JSON corrupto cae a los defaults sin lanzar excepcion", () =>
            {
                AudioProfile corrupto = AudioPreferences.ParseProfile("{no es json valido");
                return CercaDeTodos(("master", corrupto.master, 1f), ("music", corrupto.music, 1f), ("sfx", corrupto.sfx, 1f));
            });

            Run("CP-AUD-05", "Valores fuera de [0,1] quedan clampeados", () =>
            {
                AudioProfile fueraDeRango = AudioPreferences.ParseProfile("{\"master\":5,\"music\":-3,\"sfx\":0.5}");
                return CercaDeTodos(("master", fueraDeRango.master, 1f), ("music", fueraDeRango.music, 0f), ("sfx", fueraDeRango.sfx, 0.5f));
            });

            Run("CP-AUD-06", "Guardar y volver a cargar desde PlayerPrefs (round trip) preserva los valores", () =>
            {
                AudioPreferences.Master = 0.5f;
                AudioPreferences.Music = 0.25f;
                AudioPreferences.Sfx = 0.75f;
                AudioPreferences.Load();
                return CercaDeTodos(
                    ("master", AudioPreferences.Master, 0.5f),
                    ("music", AudioPreferences.Music, 0.25f),
                    ("sfx", AudioPreferences.Sfx, 0.75f));
            });

            Run("CP-AUD-07", "ResetDefaults() vuelve a dejar master en 1", () =>
            {
                AudioPreferences.ResetDefaults();
                return CercaDeTodos(("master", AudioPreferences.Master, 1f));
            });

            Run("CP-AUD-08", "ToDecibels convierte 1 a 0dB, 0 al piso de -80dB, y 0.5 a ~-6.02dB", () =>
            {
                return CercaDeTodos(
                    ("ToDecibels(1)", AudioPreferences.ToDecibels(1f), 0f),
                    ("ToDecibels(0)", AudioPreferences.ToDecibels(0f), -80f),
                    ("ToDecibels(0.5)", AudioPreferences.ToDecibels(0.5f), -6.0206f));
            });

            Run("CP-AUD-09", "Apply() no lanza excepcion (haya o no un AudioMixer en Resources)", () =>
            {
                AudioPreferences.Apply();
                return null;
            });
        }
        finally
        {
            if (hadBackup) PlayerPrefs.SetString(AudioPreferences.PrefsKey, backup);
            else PlayerPrefs.DeleteKey(AudioPreferences.PrefsKey);
            PlayerPrefs.Save();
            AudioPreferences.Load();
        }

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

    // Devuelve null (OK) si todos los pares (nombre, valor, esperado[, tolerancia]) estan cerca;
    // si no, devuelve el motivo del primero que no coincide, para el mensaje de FAIL.
    static string CercaDeTodos(params (string nombre, float valor, float esperado)[] pares)
    {
        foreach (var p in pares)
        {
            if (Mathf.Abs(p.valor - p.esperado) > 0.001f)
                return $"{p.nombre} = {p.valor} (se esperaba {p.esperado})";
        }
        return null;
    }

    [MenuItem("Between Metals/Tests/Audio Preferences Self-Test")]
    public static void Run()
    {
        int passed = 0;
        int failed = 0;

        // No pisar un perfil real guardado por el usuario mientras se prueba.
        bool hadBackup = PlayerPrefs.HasKey(AudioPreferences.PrefsKey);
        string backup = PlayerPrefs.GetString(AudioPreferences.PrefsKey, "");

        void Check(string name, bool condition)
        {
            if (condition) { passed++; return; }
            failed++;
            Debug.LogError($"[AudioPreferencesSelfTest] FALLO: {name}");
        }

        void CheckApprox(string name, float actual, float expected, float tol = 0.001f)
        {
            Check($"{name} (esperado {expected}, obtenido {actual})", Mathf.Abs(actual - expected) <= tol);
        }

        try
        {
            // 1) Sin perfil guardado -> valores por defecto (1,1,1)
            PlayerPrefs.DeleteKey(AudioPreferences.PrefsKey);
            AudioPreferences.Load();
            CheckApprox("Default master", AudioPreferences.Master, 1f);
            CheckApprox("Default music", AudioPreferences.Music, 1f);
            CheckApprox("Default sfx", AudioPreferences.Sfx, 1f);

            // 2) ParseProfile con string vacio -> defaults
            AudioProfile empty = AudioPreferences.ParseProfile("");
            CheckApprox("ParseProfile('') master", empty.master, 1f);

            // 3) JSON parcial -> conserva defaults en los campos ausentes
            AudioProfile partial = AudioPreferences.ParseProfile("{\"master\":0.5}");
            CheckApprox("Partial JSON master", partial.master, 0.5f);
            CheckApprox("Partial JSON music (default preservado)", partial.music, 1f);
            CheckApprox("Partial JSON sfx (default preservado)", partial.sfx, 1f);

            // 4) JSON corrupto -> fallback a defaults, sin excepcion
            AudioProfile corrupt = AudioPreferences.ParseProfile("{no es json valido");
            CheckApprox("Corrupt JSON master", corrupt.master, 1f);
            CheckApprox("Corrupt JSON music", corrupt.music, 1f);
            CheckApprox("Corrupt JSON sfx", corrupt.sfx, 1f);

            // 5) Valores fuera de rango -> clamp [0,1]
            AudioProfile outOfRange = AudioPreferences.ParseProfile("{\"master\":5,\"music\":-3,\"sfx\":0.5}");
            CheckApprox("Clamp master alto", outOfRange.master, 1f);
            CheckApprox("Clamp music negativo", outOfRange.music, 0f);
            CheckApprox("Sin clamp necesario sfx", outOfRange.sfx, 0.5f);

            // 6) Guardado y carga (round trip) via PlayerPrefs real
            AudioPreferences.Master = 0.5f;
            AudioPreferences.Music = 0.25f;
            AudioPreferences.Sfx = 0.75f;
            AudioPreferences.Load(); // fuerza releer desde PlayerPrefs, no desde la instancia en memoria
            CheckApprox("Round trip master", AudioPreferences.Master, 0.5f);
            CheckApprox("Round trip music", AudioPreferences.Music, 0.25f);
            CheckApprox("Round trip sfx", AudioPreferences.Sfx, 0.75f);

            // 7) ResetDefaults
            AudioPreferences.ResetDefaults();
            CheckApprox("ResetDefaults master", AudioPreferences.Master, 1f);

            // 8) Conversion a decibeles
            CheckApprox("ToDecibels(1) = 0dB", AudioPreferences.ToDecibels(1f), 0f);
            CheckApprox("ToDecibels(0) = piso -80dB", AudioPreferences.ToDecibels(0f), -80f);
            CheckApprox("ToDecibels(0.5) ~ -6.02dB", AudioPreferences.ToDecibels(0.5f), -6.0206f, 0.01f);

            // 9) Apply() sin AudioMixer en Resources no debe tirar excepcion
            try
            {
                AudioPreferences.Apply();
                passed++;
            }
            catch (System.Exception e)
            {
                failed++;
                Debug.LogError($"[AudioPreferencesSelfTest] FALLO: Apply() sin mixer lanzo excepcion: {e}");
            }
        }
        finally
        {
            // Restaurar el estado previo de PlayerPrefs
            if (hadBackup) PlayerPrefs.SetString(AudioPreferences.PrefsKey, backup);
            else PlayerPrefs.DeleteKey(AudioPreferences.PrefsKey);
            PlayerPrefs.Save();
            AudioPreferences.Load();
        }

        if (failed == 0)
            Debug.Log($"[AudioPreferencesSelfTest] OK: {passed} checks pasaron.");
        else
            Debug.LogError($"[AudioPreferencesSelfTest] {failed} checks fallaron, {passed} pasaron.");
    }
}
