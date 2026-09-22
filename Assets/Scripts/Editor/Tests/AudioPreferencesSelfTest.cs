using UnityEditor;
using UnityEngine;

// Self-test manual para AudioPreferences. No hay asmdef de test en el proyecto
// todavia, asi que esto corre como item de menu del Editor en vez de NUnit.
public static class AudioPreferencesSelfTest
{
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
