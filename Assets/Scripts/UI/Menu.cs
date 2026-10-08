using UnityEngine;
using UnityEngine.SceneManagement;

// Teclas configurables. Se guardan en PlayerPrefs y sobreviven entre partidas.
public static class KeyBindings
{
    public enum Action { Forward, Back, Left, Right, Sprint, Flashlight, Jump, Attack }

    public static readonly string[] Names =
    {
        "Avanzar", "Retroceder", "Izquierda", "Derecha", "Correr", "Linterna", "Saltar", "Atacar"
    };

    static readonly KeyCode[] defaults =
    {
        KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.LeftShift, KeyCode.F, KeyCode.Space, KeyCode.Mouse0
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
    // El mismo componente sirve para las dos pantallas, en vez de duplicar las de Audio/Graficos/
    // Controles en un segundo script:
    //   Pausa     -> el de siempre, dentro de la escena de juego: Esc lo abre y lo cierra, y
    //                mientras esta abierto el juego queda congelado (Time.timeScale = 0).
    //   Principal -> el de la escena de inicio (Assets/Scenes/MenuPrincipal.unity): no se puede
    //                cerrar, "Jugar" carga la escena de juego y no congela nada (no hay partida
    //                todavia que congelar). El fondo con la foto lo dibuja FondoMenu, aparte.
    public enum Modo { Pausa, Principal }

    [Header("Menu")]
    public string gameTitle = "Between Metals";

    [Tooltip("Pausa = menu dentro de la partida. Principal = pantalla de inicio, no se puede cerrar")]
    public Modo modo = Modo.Pausa;

    [Tooltip("Solo en modo Pausa: si esta activo, el juego arranca en pausa con el menu abierto")]
    public bool openOnStart = false;

    [Tooltip("Solo en modo Principal: escena que carga el boton Jugar")]
    public string escenaDeJuego = "Prototype";

    [Tooltip("Solo en modo Pausa: escena que carga el boton 'Menu principal'")]
    public string escenaMenuPrincipal = "MenuPrincipal";

    [Tooltip("Cuanto se oscurece lo que hay detras del menu. En modo Principal conviene bajo, para que se vea la foto de fondo")]
    [Range(0f, 1f)] public float oscurecerFondo = 0.75f;

    // Los scripts del jugador lo consultan para ignorar la entrada mientras el menu esta abierto
    public static bool IsOpen { get; private set; }

    /// <summary>Verdadero si este menu es la pantalla de inicio y no el menu de pausa.</summary>
    public bool EsMenuPrincipal => modo == Modo.Principal;

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

        if (EsMenuPrincipal || openOnStart) Open();
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
        if (VictoryUI.EstaMostrando) return;      // idem con la pantalla de Victoria
        if (InventoryUI.IsOpen) return;           // el inventario maneja su propio cursor mientras esta abierto

        if (waiting.HasValue)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CancelarReasignacion();
            else ListenForKey();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Abrir y cerrar con Esc suena igual que apretar el boton: si no, el menu se oye a
            // medias (los botones suenan, la tecla no) y parece que el sonido falla.
            if (!open)
            {
                SonidosUI.SonarClick();
                Open();
            }
            else
            {
                SonidosUI.SonarAtras();
                VolverAtras();
            }
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

        // La escena de inicio no congela nada: no hay partida que congelar, y si quedara en
        // timeScale 0 el juego arrancaria pausado al cargar la escena de juego desde "Jugar".
        Time.timeScale = EsMenuPrincipal ? 1f : 0f;
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
                // El menu de inicio no se cierra con Esc: no hay nada atras todavia.
                if (started && !EsMenuPrincipal) Close();
                break;
        }
    }

    // Solo en modo Principal. Arranca la partida cargando la escena de juego; Time.timeScale ya
    // esta en 1 (ver Open), asi que la escena nueva no arranca congelada.
    void Jugar()
    {
        CargarEscena(escenaDeJuego, "escenaDeJuego");
    }

    // Solo en modo Pausa. Abandona la partida y vuelve a la pantalla de inicio. No pregunta nada:
    // el juego todavia no tiene partida guardada, asi que no hay nada que se pueda perder aparte
    // del progreso de la corrida, igual que ya pasa con "Reiniciar".
    void VolverAlMenuPrincipal()
    {
        CargarEscena(escenaMenuPrincipal, "escenaMenuPrincipal");
    }

    // Punto unico de carga de escena: se descongela el tiempo y se baja IsOpen ANTES de cargar,
    // porque los dos son estado global que sobrevive al cambio de escena (Time.timeScale se
    // quedaria en 0 y la escena nueva arrancaria congelada).
    void CargarEscena(string escena, string nombreDelCampo)
    {
        if (string.IsNullOrWhiteSpace(escena))
        {
            Debug.LogError($"Menu: '{nombreDelCampo}' esta vacio; no sabe que escena cargar.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(escena))
        {
            Debug.LogError($"Menu: la escena '{escena}' no esta en las Build Settings. Corré Between Metals > Menu > Crear escena de menu principal (o revisá Between Metals > Menu > Revisar Build Settings).", this);
            return;
        }

        IsOpen = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(escena);
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

        // El velo negro va encima de lo que haya atras: la partida congelada en modo Pausa, o la
        // foto que dibuja FondoMenu en modo Principal (ahi conviene bajarlo para que se vea).
        if (oscurecerFondo > 0f)
        {
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, oscurecerFondo);
            GUI.DrawTexture(new Rect(0, 0, w, 720f), Texture2D.whiteTexture);
            GUI.color = old;
        }

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
        if (EsMenuPrincipal)
        {
            // Sin "Reiniciar": antes de empezar no hay partida que reiniciar, haria lo mismo que Jugar.
            if (SonidosUI.Boton("Jugar", buttonStyle)) Jugar();
            if (SonidosUI.Boton("Opciones", buttonStyle)) AbrirOpciones();
            if (SonidosUI.Boton("Salir", buttonStyle)) Quit();
            return;
        }

        if (SonidosUI.Boton(started ? "Continuar" : "Jugar", buttonStyle)) Close();
        if (SonidosUI.Boton("Opciones", buttonStyle)) AbrirOpciones();
        if (SonidosUI.Boton("Reiniciar", buttonStyle)) Restart();

        // Gris si la escena del menu todavia no existe (nadie corrio MenuPrincipalBuilder): se ve
        // que el boton esta, pero no se puede apretar para que no tire un error al vacio.
        bool hayMenuPrincipal = !string.IsNullOrWhiteSpace(escenaMenuPrincipal)
            && Application.CanStreamedLevelBeLoaded(escenaMenuPrincipal);
        GUI.enabled = hayMenuPrincipal;
        if (SonidosUI.Boton("Menu principal", buttonStyle)) VolverAlMenuPrincipal();
        GUI.enabled = true;

        if (SonidosUI.Boton("Salir", buttonStyle)) Quit();
    }

    // Selector de categoria: Audio/Graficos/Controles tienen cada una su propia pantalla.
    void DrawOpciones()
    {
        GUILayout.Label("Opciones", labelStyle);
        GUILayout.Space(10f);

        if (SonidosUI.Boton("Audio", buttonStyle)) AbrirAudio();
        if (SonidosUI.Boton("Graficos", buttonStyle)) AbrirGraficos();
        if (SonidosUI.Boton("Controles", buttonStyle)) AbrirControles();

        GUILayout.Space(20f);
        if (SonidosUI.BotonAtras("Volver", buttonStyle)) estado = EstadoMenu.Principal;
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
        if (SonidosUI.BotonAtras("Volver", buttonStyle)) estado = EstadoMenu.Opciones;
    }

    // El slider sigue trabajando en 0..1 (mismo rango que espera AudioPreferences/AudioMixer);
    // solo el texto se muestra como porcentaje 0-100.
    void DrawVolumeSlider(string etiqueta, float valorActual, System.Action<float> aplicar)
    {
        GUILayout.Label(etiqueta + ": " + Mathf.RoundToInt(valorActual * 100f) + "%", labelStyle);
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
        if (SonidosUI.Boton("<", buttonStyle, GUILayout.Width(60f))) GraphicsPreferences.CambiarCalidad(-1);
        GUILayout.Label(GraphicsPreferences.QualityNames[GraphicsPreferences.QualityLevel], labelStyle, GUILayout.ExpandWidth(true));
        if (SonidosUI.Boton(">", buttonStyle, GUILayout.Width(60f))) GraphicsPreferences.CambiarCalidad(1);
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);

        GUILayout.Label("Resolucion", labelStyle);
        GUILayout.BeginHorizontal();
        if (SonidosUI.Boton("<", buttonStyle, GUILayout.Width(60f)))
            GraphicsPreferences.CambiarResolucion(-1);
        GUILayout.Label(GraphicsPreferences.ResolutionLabel(GraphicsPreferences.ResolutionIndex), labelStyle, GUILayout.ExpandWidth(true));
        if (SonidosUI.Boton(">", buttonStyle, GUILayout.Width(60f)))
            GraphicsPreferences.CambiarResolucion(1);
        GUILayout.EndHorizontal();

        GUILayout.Space(10f);

        string textoFullscreen = "Pantalla completa: " + (GraphicsPreferences.Fullscreen ? "Si" : "No");
        if (SonidosUI.Boton(textoFullscreen, buttonStyle)) GraphicsPreferences.Fullscreen = !GraphicsPreferences.Fullscreen;

        GUILayout.Space(20f);
        if (SonidosUI.BotonAtras("Volver", buttonStyle)) estado = EstadoMenu.Opciones;
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
            if (SonidosUI.Boton(text, buttonStyle)) waiting = a;
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10f);
        GUILayout.Label("Esc cancela el cambio de tecla", labelStyle);
        GUILayout.Space(10f);

        if (SonidosUI.Boton("Restablecer teclas", buttonStyle))
        {
            KeyBindings.ResetDefaults();
            waiting = null;
        }
        if (SonidosUI.BotonAtras("Volver", buttonStyle))
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
