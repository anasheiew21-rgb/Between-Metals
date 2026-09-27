using System.Collections.Generic;
using UnityEngine;

// Preferencias de graficos (HU-02): calidad, pantalla completa y resolucion. Mismo patron
// estatico que AudioPreferences/KeyBindings: persiste en PlayerPrefs y se aplica solo con
// RuntimeInitializeOnLoadMethod, sin que la escena necesite ninguna referencia.
public static class GraphicsPreferences
{
    const string QualityKey = "Graphics_Quality";
    const string FullscreenKey = "Graphics_Fullscreen";
    const string ResolutionWidthKey = "Graphics_ResWidth";
    const string ResolutionHeightKey = "Graphics_ResHeight";

    static Resolution[] resoluciones;
    static int resolutionIndex;
    static bool inicializado;

    // Nombres definidos en ProjectSettings/QualitySettings.asset (hoy: "Mobile", "PC").
    public static string[] QualityNames => QualitySettings.names;

    public static int QualityLevel
    {
        get { return QualitySettings.GetQualityLevel(); }
        set
        {
            int nivel = Mathf.Clamp(value, 0, QualityNames.Length - 1);
            QualitySettings.SetQualityLevel(nivel, true);
            PlayerPrefs.SetInt(QualityKey, nivel);
            PlayerPrefs.Save();
        }
    }

    public static bool Fullscreen
    {
        get { return Screen.fullScreen; }
        set
        {
            Screen.fullScreen = value;
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    // Resoluciones distintas que ofrece el monitor actual (sin duplicar por refresh rate).
    public static Resolution[] Resoluciones => resoluciones ??= CalcularResolucionesUnicas();

    public static int ResolutionIndex => resolutionIndex;

    public static string ResolutionLabel(int index)
    {
        Resolution[] lista = Resoluciones;
        if (lista.Length == 0) return "-";
        Resolution r = lista[Mathf.Clamp(index, 0, lista.Length - 1)];
        return r.width + " x " + r.height;
    }

    // delta tipicamente +1/-1, para los botones "<"/">"; da la vuelta en los extremos.
    public static void CambiarResolucion(int delta)
    {
        Resolution[] lista = Resoluciones;
        if (lista.Length == 0) return;

        resolutionIndex = ((resolutionIndex + delta) % lista.Length + lista.Length) % lista.Length;
        AplicarResolucionActual();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (inicializado) return;
        inicializado = true;

        int nivelGuardado = PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel());
        QualitySettings.SetQualityLevel(Mathf.Clamp(nivelGuardado, 0, QualityNames.Length - 1), true);

        Screen.fullScreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;

        int anchoGuardado = PlayerPrefs.GetInt(ResolutionWidthKey, Screen.width);
        int altoGuardado = PlayerPrefs.GetInt(ResolutionHeightKey, Screen.height);
        resolutionIndex = EncontrarIndice(anchoGuardado, altoGuardado);
        // No se llama Screen.SetResolution aca: si ya coincide con la resolucion actual del
        // sistema no hace falta, y forzarla en BeforeSceneLoad puede generar un parpadeo de ventana.
    }

    static int EncontrarIndice(int ancho, int alto)
    {
        Resolution[] lista = Resoluciones;
        for (int i = 0; i < lista.Length; i++)
        {
            if (lista[i].width == ancho && lista[i].height == alto) return i;
        }
        return lista.Length > 0 ? lista.Length - 1 : 0;
    }

    static void AplicarResolucionActual()
    {
        Resolution r = Resoluciones[resolutionIndex];
        // Se preserva el FullScreenMode actual (no se fuerza a Fullscreen/Windowed desde aca):
        // esa decision es del toggle "Pantalla completa" (propiedad Fullscreen de arriba).
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode);

        PlayerPrefs.SetInt(ResolutionWidthKey, r.width);
        PlayerPrefs.SetInt(ResolutionHeightKey, r.height);
        PlayerPrefs.Save();
    }

    static Resolution[] CalcularResolucionesUnicas()
    {
        Resolution[] todas = Screen.resolutions;
        var unicas = new List<Resolution>();

        foreach (Resolution r in todas)
        {
            bool yaEsta = false;
            for (int i = 0; i < unicas.Count; i++)
            {
                if (unicas[i].width == r.width && unicas[i].height == r.height) { yaEsta = true; break; }
            }
            if (!yaEsta) unicas.Add(r);
        }

        if (unicas.Count == 0) unicas.Add(new Resolution { width = Screen.width, height = Screen.height });
        return unicas.ToArray();
    }
}
