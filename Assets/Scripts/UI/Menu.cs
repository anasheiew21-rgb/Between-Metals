using UnityEngine;

// Teclas configurables. Se guardan en PlayerPrefs y sobreviven entre partidas.
public static class KeyBindings
{
    public enum Action { Forward, Back, Left, Right, Sprint, Flashlight }

    public static readonly string[] Names =
    {
        "Avanzar", "Retroceder", "Izquierda", "Derecha", "Correr", "Linterna"
    };

    static readonly KeyCode[] defaults =
    {
        KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.LeftShift, KeyCode.F
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

    enum Page { Main, Controls }

    bool open;
    bool started;
    Page page;
    KeyBindings.Action? waiting;

    readonly System.Array allKeys = System.Enum.GetValues(typeof(KeyCode));
    GUIStyle titleStyle, labelStyle, buttonStyle;

    // Crea el menu solo si la escena no tiene uno, para no depender de agregarlo a mano
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (FindFirstObjectByType<Menu>() != null) return;
        new GameObject("Menu").AddComponent<Menu>();
    }

    void Start()
    {
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

        if (waiting.HasValue)
        {
            ListenForKey();
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!open) Open();
            else if (page == Page.Controls) page = Page.Main;
            else if (started) Close();
        }

        // Se aplica cada frame para que MouseLook no vuelva a bloquear el cursor
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
    }

    void ListenForKey()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            waiting = null;
            return;
        }

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

    void Open()
    {
        open = true;
        IsOpen = true;
        page = Page.Main;
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

        // Se dibuja en una pantalla virtual de 720 de alto para que escale con la resolucion
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
        if (page == Page.Main) DrawMain();
        else DrawControls();
        GUILayout.EndArea();
    }

    void DrawMain()
    {
        if (GUILayout.Button(started ? "Continuar" : "Jugar", buttonStyle)) Close();
        if (GUILayout.Button("Controles", buttonStyle)) page = Page.Controls;
        if (GUILayout.Button("Salir", buttonStyle)) Quit();
    }

    void DrawControls()
    {
        GUILayout.Label("Controles", labelStyle);
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

        if (GUILayout.Button("Restablecer", buttonStyle))
        {
            KeyBindings.ResetDefaults();
            waiting = null;
        }
        if (GUILayout.Button("Volver", buttonStyle))
        {
            page = Page.Main;
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
