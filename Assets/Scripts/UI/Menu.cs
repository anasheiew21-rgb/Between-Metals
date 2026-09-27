using UnityEngine;
using UnityEngine.SceneManagement;

// Teclas configurables. Se guardan en PlayerPrefs y sobreviven entre partidas.
public static class KeyBindings
{
    public enum Action { Forward, Back, Left, Right, Sprint, Flashlight, Jump }

    public static readonly string[] Names =
    {
        "Avanzar", "Retroceder", "Izquierda", "Derecha", "Correr", "Linterna", "Saltar"
    };

    static readonly KeyCode[] defaults =
    {
        KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.LeftShift, KeyCode.F, KeyCode.Space
    };

    const string Prefix = "Key_";
    static KeyCode[] keys;

    static KeyCode[] Keys
    {
        get
        {
            if (keys == null) Load();
            return keys;
        }
    }

    public static KeyCode Get(Action action) { return Keys[(int)action]; }
    public static bool Held(Action action) { return Input.GetKey(Get(action)); }
    public static bool Down(Action action) { return Input.GetKeyDown(Get(action)); }

    // -1, 0 o 1 segun las teclas de cada lado
    public static float Axis(Action negative, Action positive)
    {
        return (Held(positive) ? 1f : 0f) - (Held(negative) ? 1f : 0f);
    }

    // Si la tecla ya la usa otra accion, se intercambian para no dejar dos acciones en la misma tecla
    public static void Set(Action action, KeyCode key)
    {
        int i = (int)action;
        for (int j = 0; j < Keys.Length; j++)
        {
            if (j != i && Keys[j] == key)
            {
                Store(j, Keys[i]);
                break;
            }
        }
        Store(i, key);
        PlayerPrefs.Save();
    }

    public static void ResetDefaults()
    {
        for (int i = 0; i < defaults.Length; i++) Store(i, defaults[i]);
        PlayerPrefs.Save();
    }

    static void Load()
    {
        keys = new KeyCode[defaults.Length];
        for (int i = 0; i < defaults.Length; i++)
            keys[i] = (KeyCode)PlayerPrefs.GetInt(Prefix + i, (int)defaults[i]);
    }

    static void Store(int i, KeyCode key)
    {
        Keys[i] = key;
        PlayerPrefs.SetInt(Prefix + i, (int)key);
    }
}

public class Menu : MonoBehaviour
{
    [Header("Menu")]
    public string gameTitle = "Between Metals";
    [Tooltip("Si esta activo, el juego arranca en pausa con el menu abierto")]
    public bool openOnStart = true;

    // Los scripts del jugador lo consultan para ignorar la entrada mientras el menu esta abierto
    public static bool IsOpen { get; private set; }

    // Opciones ahora es un selector de categoria (Audio/Graficos/Controles), cada una su propia
    // pantalla; VolverAtras() sabe que las 3 categorias vuelven a Opciones y Opciones vuelve a Principal.
    enum EstadoMenu { Principal, Opciones, Audio, Graficos, Controles }

    const string SensitivityKey = "Sensitivity";

    bool open;
    bool started;
    EstadoMenu estado;
    KeyBindings.Action? waiting;
    float sensitivity;

    readonly System.Array allKeys = System.Enum.GetValues(typeof(KeyCode));
    GUIStyle titleStyle, labelStyle, buttonStyle;

    // Crea el menu solo si la escena no tiene uno, para no depender de agregarlo a mano
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindAnyObjectByType<Menu>() != null) return;
        new GameObject("Menu").AddComponent<Menu>();
    }

    void Start()
    {
        sensitivity = PlayerPrefs.GetFloat(SensitivityKey, 1.0f);

        if (openOnStart) Open();
        else started = true;
    }

    void OnDisable()
    {
        IsOpen = false;
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (ShopManager.HayTiendaAbierta) return; // la tienda maneja su propio Esc para cerrarse
        if (GameOverUI.EstaMostrando) return;     // no pisar el cursor libre de la pantalla de Game Over
        if (InventoryUI.IsOpen) return;           // el inventario maneja su propio cursor mientras esta abierto

        if (waiting.HasValue)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CancelarReasignacion();
            else ListenForKey();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!open) Open();
            else VolverAtras();
        }

        // Se aplica cada frame para que MouseLook no vuelva a bloquear el cursor
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
    }

    void ListenForKey()
    {
        foreach (KeyCode k in allKeys)
        {
            if (k == KeyCode.None || k >= KeyCode.Mouse0) continue; // solo teclado
            if (Input.GetKeyDown(k))
            {
                KeyBindings.Set(waiting.Value, k);
                waiting = null;
                return;
            }
        }
    }

    void CancelarReasignacion()
    {
        waiting = null;
    }

    void Open()
    {
        open = true;
        IsOpen = true;
        estado = EstadoMenu.Principal;
        waiting = null;
        Time.timeScale = 0f;
    }

    void Close()
    {
        open = false;
        IsOpen = false;
        started = true;
        waiting = null;
        Time.timeScale = 1f;
    }

    void AbrirOpciones()
    {
        estado = EstadoMenu.Opciones;
        waiting = null;
    }

    void AbrirAudio()
    {
        estado = EstadoMenu.Audio;
        waiting = null;
    }

    void AbrirGraficos()
    {
        estado = EstadoMenu.Graficos;
        waiting = null;
    }

    void AbrirControles()
    {
        estado = EstadoMenu.Controles;
        waiting = null;
    }

    // Punto unico de "atras": si esta reasignando una tecla, cancela; si esta en Audio, Graficos
    // o Controles, vuelve a Opciones (su categoria); si esta en Opciones, vuelve al Principal;
    // si ya esta en el Principal, cierra el menu.
    void VolverAtras()
    {
        if (waiting.HasValue)
        {
            CancelarReasignacion();
            return;
        }

        switch (estado)
        {
            case EstadoMenu.Audio:
            case EstadoMenu.Graficos:
            case EstadoMenu.Controles:
                estado = EstadoMenu.Opciones;
                break;
            case EstadoMenu.Opciones:
                estado = EstadoMenu.Principal;
                break;
            default:
                if (started) Close();
                break;
        }
    }

    void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnGUI()
    {
        if (!open) return;

        BuildStyles();

        // Se dibuja en una pantalla virtual de 720 de alto para que escale con la resolucion.
        // s/w se recalculan de Screen.height/width en cada OnGUI (no se cachean en un campo), asi
        // que un cambio de resolucion o de pantalla completa hecho desde DrawGraficos() se refleja
        // solo en el frame siguiente, sin ningun ajuste extra.
        float s = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;

        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, w, 720f), Texture2D.whiteTexture);
        GUI.color = old;

        GUILayout.BeginArea(new Rect((w - 460f) / 2f, 90f, 460f, 540f));
        GUILayout.Label(gameTitle, titleStyle);
        GUILayout.Space(20f);
        switch (estado)
        {
            case EstadoMenu.Principal: DrawMain(); break;
            case EstadoMenu.Opciones: DrawOpciones(); break;
            case EstadoMenu.Audio: DrawAudio(); break;
            case EstadoMenu.Graficos: DrawGraficos(); break;
            default: DrawControls(); break;
        }
        GUILayout.EndArea();
    }

    void DrawMain()
    {
        if (GUILayout.Button(started ? "Continuar" : "Jugar", buttonStyle)) Close();
        if (GUILayout.Button("Opciones", buttonStyle)) AbrirOpciones();
        if (GUILayout.Button("Reiniciar", buttonStyle)) Restart();
        if (GUILayout.Button("Salir", buttonStyle)) Quit();
    }

    // Selector de categoria: Audio/Graficos/Controles tienen cada una su propia pantalla.
    void DrawOpciones()
    {
        GUILayout.Label("Opciones", labelStyle);
        GUILayout.Space(10f);

        if (GUILayout.Button("Audio", buttonStyle)) AbrirAudio();
        if (GUILayout.Button("Graficos", buttonStyle)) AbrirGraficos();
        if (GUILayout.Button("Controles", buttonStyle)) AbrirControles();

        GUILayout.Space(20f);
        if (GUILayout.Button("Volver", buttonStyle)) estado = EstadoMenu.Principal;
    }

    // 3 sliders sobre AudioPreferences (Master/Music/Sfx), que ya persiste y aplica al AudioMixer
    // por su cuenta: esta pantalla solo lee/escribe sus propiedades, no toca AudioListener.
    void DrawAudio()
    {
        GUILayout.Label("Audio", labelStyle);
        GUILayout.Space(10f);

        DrawVolumeSlider("General", AudioPreferences.Master, v => AudioPreferences.Master = v);
        GUILayout.Space(10f);
        DrawVolumeSlider("Musica", AudioPreferences.Music, v => AudioPreferences.Music = v);
        GUILayout.Space(10f);
        DrawVolumeSlider("Efectos", AudioPreferences.Sfx, v => AudioPreferences.Sfx = v);

        GUILayout.Space(20f);
        if (GUILayout.Button("Volver", buttonStyle)) estado = EstadoMenu.Opciones;
    }

    void DrawVolumeSlider(string etiqueta, float valorActual, System.Action<float> aplicar)
    {
        GUILayout.Label(etiqueta + ": " + valorActual.ToString("0.00"), labelStyle);
        float nuevoValor = GUILayout.HorizontalSlider(valorActual, 0f, 1f);
        if (!Mathf.Approximately(nuevoValor, valorActual)) aplicar(nuevoValor);
    }

    // Calidad y resolucion se ciclan con "<"/">" (IMGUI no trae un dropdown nativo); pantalla
    // completa es un simple toggle. Todo persiste y se aplica solo, via GraphicsPreferences.
    void DrawGraficos()
    {
        GUILayout.Label("Graficos", labelStyle);
        GUILayout.Space(10f);

        GUILayout.Label("Calidad", labelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", buttonStyle, GUILayout.Width(60f)))
            GraphicsPreferences.QualityLevel = GraphicsPreferences.QualityLevel - 1;
        GUILayout.Label(GraphicsPreferences.QualityNames[GraphicsPreferences.QualityLevel], labelStyle, GUILayout.ExpandWidth(true));
        if (GUILayout.Button(">", buttonStyle, GUILayout.Width(60f)))
            GraphicsPreferences.QualityLevel = GraphicsPreferences.QualityLevel + 1;
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);

        GUILayout.Label("Resolucion", labelStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("<", buttonStyle, GUILayout.Width(60f)))
            GraphicsPreferences.CambiarResolucion(-1);
        GUILayout.Label(GraphicsPreferences.ResolutionLabel(GraphicsPreferences.ResolutionIndex), labelStyle, GUILayout.ExpandWidth(true));
        if (GUILayout.Button(">", buttonStyle, GUILayout.Width(60f)))
            GraphicsPreferences.CambiarResolucion(1);
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);

        string textoFullscreen = "Pantalla completa: " + (GraphicsPreferences.Fullscreen ? "Si" : "No");
        if (GUILayout.Button(textoFullscreen, buttonStyle)) GraphicsPreferences.Fullscreen = !GraphicsPreferences.Fullscreen;

        GUILayout.Space(20f);
        if (GUILayout.Button("Volver", buttonStyle)) estado = EstadoMenu.Opciones;
    }

    void DrawControls()
    {
        GUILayout.Label("Controles", labelStyle);
        GUILayout.Space(10f);

        GUILayout.Label("Sensibilidad del mouse: " + sensitivity.ToString("0.00"), labelStyle);
        float newSensitivity = GUILayout.HorizontalSlider(sensitivity, 0.1f, 5f);
        if (!Mathf.Approximately(newSensitivity, sensitivity))
        {
            sensitivity = newSensitivity;
            PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
            PlayerPrefs.Save();
        }

        GUILayout.Space(10f);

        foreach (KeyBindings.Action a in System.Enum.GetValues(typeof(KeyBindings.Action)))
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(KeyBindings.Names[(int)a], labelStyle, GUILayout.Width(200f));
            string text = waiting == a ? "Pulsa una tecla..." : KeyBindings.Get(a).ToString();
            if (GUILayout.Button(text, buttonStyle)) waiting = a;
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10f);
        GUILayout.Label("Esc cancela el cambio de tecla", labelStyle);
        GUILayout.Space(10f);

        if (GUILayout.Button("Restablecer teclas", buttonStyle))
        {
            KeyBindings.ResetDefaults();
            waiting = null;
        }
        if (GUILayout.Button("Volver", buttonStyle))
        {
            estado = EstadoMenu.Opciones;
            waiting = null;
        }
    }

    void BuildStyles()
    {
        if (titleStyle != null) return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 42,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = Color.white;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleLeft
        };
        labelStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 22,
            fixedHeight = 44f
        };
    }
}
