using UnityEngine;
using UnityEngine.Audio;

// Perfil de volumen. Los campos arrancan en 1 (100%); JsonUtility.FromJsonOverwrite
// preserva ese default en los campos que no vengan en el JSON guardado.
[System.Serializable]
public class AudioProfile
{
    public float master = 1f;
    public float music = 1f;
    public float sfx = 1f;
}

// Persiste el volumen en PlayerPrefs (perfil serializado como JSON) y lo aplica
// a un AudioMixer, siguiendo el mismo patron estatico que KeyBindings.
public static class AudioPreferences
{
    public const string PrefsKey = "AudioProfile";

    // El AudioMixer debe existir en Assets/Audio/Resources/MainMixer.mixer,
    // con los parametros expuestos MasterVolume/MusicVolume/SFXVolume (en dB).
    const string MixerResourcePath = "MainMixer";
    const string MasterParam = "MasterVolume";
    const string MusicParam = "MusicVolume";
    const string SfxParam = "SFXVolume";

    const float MinDecibels = -80f;

    static AudioProfile profile;
    static AudioMixer mixer;
    static bool mixerLookupDone;

    public static float Master
    {
        get { return Profile.master; }
        set { Profile.master = Clamp(value); Apply(); Save(); }
    }

    public static float Music
    {
        get { return Profile.music; }
        set { Profile.music = Clamp(value); Apply(); Save(); }
    }

    public static float Sfx
    {
        get { return Profile.sfx; }
        set { Profile.sfx = Clamp(value); Apply(); Save(); }
    }

    static AudioProfile Profile
    {
        get
        {
            if (profile == null) Load();
            return profile;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Load();
        Apply();
    }

    public static void Load()
    {
        profile = ParseProfile(PlayerPrefs.GetString(PrefsKey, ""));
    }

    // Publico para poder probarlo sin pasar por PlayerPrefs.
    public static AudioProfile ParseProfile(string json)
    {
        AudioProfile result = new AudioProfile();
        if (string.IsNullOrEmpty(json)) return result;

        try
        {
            JsonUtility.FromJsonOverwrite(json, result);
        }
        catch (System.Exception)
        {
            return new AudioProfile();
        }

        result.master = Clamp(result.master);
        result.music = Clamp(result.music);
        result.sfx = Clamp(result.sfx);
        return result;
    }

    public static void Save()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Profile));
        PlayerPrefs.Save();
    }

    public static void ResetDefaults()
    {
        profile = new AudioProfile();
        Apply();
        Save();
    }

    public static void Apply()
    {
        AudioMixer m = Mixer;
        if (m == null) return; // todavia no existe el asset del mixer

        m.SetFloat(MasterParam, ToDecibels(Profile.master));
        m.SetFloat(MusicParam, ToDecibels(Profile.music));
        m.SetFloat(SfxParam, ToDecibels(Profile.sfx));
    }

    static AudioMixer Mixer
    {
        get
        {
            if (!mixerLookupDone)
            {
                mixer = Resources.Load<AudioMixer>(MixerResourcePath);
                mixerLookupDone = true;
            }
            return mixer;
        }
    }

    static float Clamp(float v)
    {
        if (float.IsNaN(v)) return 1f;
        return Mathf.Clamp01(v);
    }

    // Piso en -80dB: por debajo, Unity lo trata como silencio (rango estandar de un grupo expuesto).
    public static float ToDecibels(float linear)
    {
        return linear <= 0.0001f ? MinDecibels : Mathf.Log10(linear) * 20f;
    }
}
