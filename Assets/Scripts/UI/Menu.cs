using UnityEngine;

// Teclas configurables. Se guardan en PlayerPrefs y sobreviven entre partidas.
//
// Escape NO esta en esta lista a proposito: el manual de la Etapa 11 la define como tecla fija de
// pausa (P-08), asi que no se puede reasignar ni se puede pisar con otra accion. La pantalla de
// Controles la muestra como una fila mas, pero sin boton.
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

// Menu de inicio (P-01), menu de pausa (P-08) y las pantallas de Configuracion y Controles
// (P-02, P-03). Dibuja con los estilos de EstiloUI, asi que la identidad visual de la Etapa 12
// (paleta 60/30/10, Helvetica, botones de 44 px con esquinas de 4 px) sale de ahi y no de aca.
//
// El mismo componente sirve para las dos pantallas principales, en vez de duplicar las de Audio/
// Graficos/Controles en un segundo script:
//   Pausa     -> el de siempre, dentro de la escena de juego: Esc lo abre y lo cierra, y mientras
//                esta abierto el juego queda congelado (Time.timeScale = 0).
//   Principal -> el de la escena de inicio (Assets/Scenes/MenuPrincipal.unity): no se puede
//                cerrar, "Jugar" carga la escena de juego y no congela nada (no hay partida
//                todavia que congelar). El fondo con la foto lo dibuja FondoMenu, aparte.
//
// Diferencia de botones entre las dos, como pide la Etapa 11:
//   P-01 (inicio): JUGAR / CONFIGURACION / SALIR         -- sin Reiniciar: no hay partida que reiniciar.
//   P-08 (pausa):  CONTINUAR / CONFIGURACION / REINICIAR / SALIR
// En pausa, REINICIAR devuelve al menu de inicio (es lo que especifica P-08), no recarga la
// partida: volver a empezar es apretar JUGAR desde ahi.
public class Menu : MonoBehaviour
{
    public enum Modo { Pausa, Principal }

    [Header("Menu")]
    public string gameTitle = "Between Metals";

    [Tooltip("Pausa = menu dentro de la partida. Principal = pantalla de inicio, no se puede cerrar")]
    public Modo modo = Modo.Pausa;

    [Tooltip("Solo en modo Pausa: si esta activo, el juego arranca en pausa con el menu abierto")]
    public bool openOnStart = false;

    [Tooltip("Solo en modo Principal: escena que carga el boton Jugar")]
    public string escenaDeJuego = NavegacionUI.EscenaDeJuego;

    [Tooltip("Solo en modo Pausa: escena que carga el boton Reiniciar")]
    public string escenaMenuPrincipal = NavegacionUI.EscenaMenuPrincipal;

    [Tooltip("Cuanto se oscurece lo que hay detras del menu. En modo Principal conviene bajo, para que se vea la foto de fondo")]
    [Range(0f, 1f)] public float oscurecerFondo = 0.75f;

    [Tooltip("Solo en modo Principal: baja la columna de botones, en px del lienzo de 1280x720. Sirve para que no tape el titulo de la foto de fondo")]
    [Range(0f, 300f)] public float desplazamientoBotones = 0f;

    // Los scripts del jugador lo consultan para ignorar la entrada mientras el menu esta abierto
    public static bool IsOpen { get; private set; }

    /// <summary>Verdadero si este menu es la pantalla de inicio y no el menu de pausa.</summary>
    public bool EsMenuPrincipal => modo == Modo.Principal;

    // Configuracion es un selector de categoria (Audio/Graficos/Controles), cada una su propia
    // pantalla; VolverAtras() sabe que las 3 categorias vuelven a Configuracion y Configuracion
    // vuelve a Principal.
    enum EstadoMenu { Principal, Configuracion, Audio, Graficos, Controles }

    const string SensitivityKey = "Sensitivity";

    // --- Maquetado, en px del lienzo virtual de 1280x720 de EstiloUI ---
    const float TituloY = 70f;             // del borde de arriba del area segura al titulo
    const float AltoTitulo = 62f;
    const float AnchoRegla = 96f;          // la rayita roja debajo del titulo
    const float BotonesY = 232f;           // donde arranca la columna de botones del menu principal
    const float AnchoPanel = 620f;         // ancho de las tarjetas de Audio y Graficos
    const float AnchoPanelControles = 760f; // Controles necesita mas: lleva una tabla de 2 columnas
    const float PanelY = 190f;
    const float PanelYControles = 152f;    // arranca mas arriba: su panel es el mas alto de todos
    const float AltoFilaControles = 32f;   // filas mas bajas que EstiloUI.AltoFila, para que entren las 10
    const float AltoEtiqueta = 26f;
    const float AltoBarra = 18f;
    const float AnchoValor = 90f;          // la columna del "70%" a la derecha de cada barra

    bool open;
    bool started;
    EstadoMenu estado;
    KeyBindings.Action? waiting;
    float sensitivity;

    readonly System.Array allKeys = System.Enum.GetValues(typeof(KeyCode));

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
            if (k == KeyCode.Escape) continue;                      // Esc es la tecla fija de pausa (P-08)
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

    void Ir(EstadoMenu destino)
    {
        estado = destino;
        waiting = null;
    }

    // Punto unico de "atras": si esta reasignando una tecla, cancela; si esta en Audio, Graficos
    // o Controles, vuelve a Configuracion; si esta en Configuracion, vuelve al Principal; si ya
    // esta en el Principal, cierra el menu.
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
                estado = EstadoMenu.Configuracion;
                break;
            case EstadoMenu.Configuracion:
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
        IsOpen = false;
        NavegacionUI.Cargar(escenaDeJuego, "escenaDeJuego", this);
    }

    // Solo en modo Pausa: el REINICIAR de P-08. Abandona la partida y vuelve a la pantalla de
    // inicio. No pregunta nada: el juego todavia no tiene partida guardada, asi que no hay nada
    // que se pueda perder aparte del progreso de la corrida.
    void Reiniciar()
    {
        IsOpen = false;
        NavegacionUI.Cargar(escenaMenuPrincipal, "escenaMenuPrincipal", this);
    }

    // ---------------------------------------------------------------
    // Dibujo
    // ---------------------------------------------------------------

    void OnGUI()
    {
        if (!open) return;

        float w = EstiloUI.AbrirLienzo();

        // El velo negro va encima de lo que haya atras: la partida congelada en modo Pausa, o la
        // foto que dibuja FondoMenu en modo Principal (ahi conviene bajarlo para que se vea).
        EstiloUI.Velo(oscurecerFondo);

        Rect area = EstiloUI.AreaSegura;

        // En el inicio manda el nombre del juego (P-01); en pausa, saber donde esta parado. El
        // !started cubre el caso de openOnStart en la escena de juego: ahi el menu se abre antes de
        // que la partida empiece, asi que todavia no es una pausa, es una pantalla de arranque.
        DibujarTitulo(area, EsMenuPrincipal || !started ? gameTitle : "Pausa");

        switch (estado)
        {
            case EstadoMenu.Principal: DibujarPrincipal(area); break;
            case EstadoMenu.Configuracion: DibujarConfiguracion(area); break;
            case EstadoMenu.Audio: DibujarAudio(area); break;
            case EstadoMenu.Graficos: DibujarGraficos(area); break;
            default: DibujarControles(area); break;
        }
    }

    // Titular en Helvetica Bold y MAYUSCULAS, con una rayita roja debajo: es el unico acento de
    // color de la pantalla cuando no hay ningun boton primario a la vista.
    // Un titulo vacio no dibuja nada, ni la rayita roja: es como se apaga el titulo del menu de
    // inicio cuando la foto de fondo ya trae el nombre del juego (si no, salen los dos).
    void DibujarTitulo(Rect area, string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return;

        var r = new Rect(area.x, area.y + TituloY, area.width, AltoTitulo);
        GUI.Label(r, texto.ToUpperInvariant(), EstiloUI.Titulo);
        EstiloUI.Rellenar(new Rect(area.center.x - AnchoRegla * 0.5f, r.yMax + 12f, AnchoRegla, 2f), EstiloUI.RojoSangre);
    }

    // P-01 y P-08. El primer boton es el primario (rojo): es la accion que el jugador viene a
    // hacer. Los demas van secundarios, para que el rojo siga siendo el 10% de la pantalla.
    void DibujarPrincipal(Rect area)
    {
        // El desplazamiento solo corre en el menu de inicio: ahi la foto de fondo puede tener su
        // propio titulo y hace falta bajar la columna para no taparlo. En pausa el fondo es la
        // partida congelada y la columna va donde siempre.
        float y = BotonesY + (EsMenuPrincipal ? desplazamientoBotones : 0f);
        GUILayout.BeginArea(Columna(area, EstiloUI.AnchoColumna, y, 340f));

        if (EsMenuPrincipal)
        {
            if (SonidosUI.Boton("JUGAR", EstiloUI.BotonPrimario)) Jugar();
            if (SonidosUI.Boton("CONFIGURACIÓN", EstiloUI.BotonSecundario)) Ir(EstadoMenu.Configuracion);
            if (SonidosUI.Boton("SALIR", EstiloUI.BotonSecundario)) NavegacionUI.Salir();
        }
        else
        {
            if (SonidosUI.Boton(started ? "CONTINUAR" : "JUGAR", EstiloUI.BotonPrimario)) Close();
            if (SonidosUI.Boton("CONFIGURACIÓN", EstiloUI.BotonSecundario)) Ir(EstadoMenu.Configuracion);

            // Gris si la escena del menu todavia no existe (nadie corrio MenuPrincipalBuilder): se
            // ve que el boton esta, pero no se puede apretar para que no tire un error al vacio.
            GUI.enabled = NavegacionUI.SePuedeCargar(escenaMenuPrincipal);
            if (SonidosUI.Boton("REINICIAR", EstiloUI.BotonSecundario)) Reiniciar();
            GUI.enabled = true;

            if (SonidosUI.Boton("SALIR", EstiloUI.BotonSecundario)) NavegacionUI.Salir();
        }

        GUILayout.EndArea();

        if (!EsMenuPrincipal)
        {
            GUI.Label(Centrada(area, 520f, BotonesY + 300f, AltoEtiqueta), "Esc cierra el menú y vuelve a la partida", EstiloUI.NotaCentrada);
        }
    }

    // P-02: selector de categoria. Ninguna de las tres es "la" accion esperada, asi que las tres
    // van secundarias y el unico primario es VOLVER... que tampoco lo es: queda secundario tambien.
    void DibujarConfiguracion(Rect area)
    {
        GUILayout.BeginArea(Columna(area, EstiloUI.AnchoColumna, BotonesY, 340f));

        if (SonidosUI.Boton("AUDIO", EstiloUI.BotonSecundario)) Ir(EstadoMenu.Audio);
        if (SonidosUI.Boton("GRÁFICOS", EstiloUI.BotonSecundario)) Ir(EstadoMenu.Graficos);
        if (SonidosUI.Boton("CONTROLES", EstiloUI.BotonSecundario)) Ir(EstadoMenu.Controles);
        GUILayout.Space(EstiloUI.Separacion * 2f);
        if (SonidosUI.BotonAtras("VOLVER", EstiloUI.BotonPrimario)) Ir(EstadoMenu.Principal);

        GUILayout.EndArea();
    }

    // P-02: las tres barras de sonido separadas que pide la Etapa 11. Se apoyan en
    // AudioPreferences, que ya persiste y aplica al AudioMixer por su cuenta: esta pantalla solo
    // lee y escribe sus propiedades, no toca AudioListener.
    void DibujarAudio(Rect area)
    {
        const float alto = 290f;
        Rect panel = Centrada(area, AnchoPanel, PanelY, alto);
        Rect dentro = Tarjeta(panel, "AUDIO");

        float y = dentro.y;
        y = Deslizador(dentro, y, "General", AudioPreferences.Master, Porcentaje, v => AudioPreferences.Master = v);
        y = Deslizador(dentro, y, "Música", AudioPreferences.Music, Porcentaje, v => AudioPreferences.Music = v);
        Deslizador(dentro, y, "Efectos", AudioPreferences.Sfx, Porcentaje, v => AudioPreferences.Sfx = v);

        BotonVolver(panel, EstadoMenu.Configuracion);
    }

    // P-02: calidad grafica, resolucion y pantalla completa. Todo persiste y se aplica solo, via
    // GraphicsPreferences. Calidad y resolucion se ciclan con "<" / ">": no hay dropdown nativo.
    void DibujarGraficos(Rect area)
    {
        const float alto = 290f;
        Rect panel = Centrada(area, AnchoPanel, PanelY, alto);
        Rect dentro = Tarjeta(panel, "GRÁFICOS");

        float y = dentro.y;
        y = Selector(dentro, y, "Calidad",
            GraphicsPreferences.QualityNames[GraphicsPreferences.QualityLevel],
            GraphicsPreferences.CambiarCalidad);

        y = Selector(dentro, y, "Resolución",
            GraphicsPreferences.ResolutionLabel(GraphicsPreferences.ResolutionIndex),
            GraphicsPreferences.CambiarResolucion);

        // Un toggle de dos estados: se dibuja como un selector pero el paso no importa, los dos
        // lados hacen lo mismo (dar la vuelta).
        Selector(dentro, y, "Pantalla completa",
            GraphicsPreferences.Fullscreen ? "Sí" : "No",
            _ => GraphicsPreferences.Fullscreen = !GraphicsPreferences.Fullscreen);

        BotonVolver(panel, EstadoMenu.Configuracion);
    }

    // P-03: sensibilidad del mouse y remapeo de teclas, en una tabla con header rojo y filas
    // alternadas. La primera fila es Pausa = Esc, sin boton: es la tecla fija del wireframe.
    void DibujarControles(Rect area)
    {
        var acciones = (KeyBindings.Action[])System.Enum.GetValues(typeof(KeyBindings.Action));

        // La tabla de Controles es la pantalla mas cargada del juego: header + Esc + una fila por
        // accion son 10 filas que, a los 34 px del resto de las tablas, no entran abajo del titulo
        // en los 720 px del lienzo. Por eso usa filas de 32 px, y la altura del panel se calcula
        // sumando sus partes en vez de ponerse a ojo: asi agregar una accion a KeyBindings no
        // desborda el panel en silencio.
        float altoTabla = AltoFilaControles * (acciones.Length + 2);
        float alto = EstiloUI.Padding * 2f + 34f + 6f        // relleno + subtitulo de la tarjeta
            + AltoEtiqueta + AltoBarra + EstiloUI.Separacion * 2f // sensibilidad
            + 6f + altoTabla                                  // tabla de teclas
            + 8f + AltoEtiqueta                               // nota del pie
            + EstiloUI.Separacion + EstiloUI.AltoBoton;       // botones

        Rect panel = Centrada(area, AnchoPanelControles, PanelYControles, alto);
        Rect dentro = Tarjeta(panel, "CONTROLES");

        float y = Deslizador(dentro, dentro.y, "Sensibilidad del mouse", (sensitivity - SensMin) / (SensMax - SensMin),
            v => (SensMin + v * (SensMax - SensMin)).ToString("0.00"),
            v =>
            {
                float nueva = SensMin + v * (SensMax - SensMin);
                if (Mathf.Approximately(nueva, sensitivity)) return;
                sensitivity = nueva;
                PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
                PlayerPrefs.Save();
            });

        y += 6f;

        // Header rojo de la tabla.
        float anchoTecla = 200f;
        var header = new Rect(dentro.x, y, dentro.width, AltoFilaControles);
        GUI.Label(header, "ACCIÓN", EstiloUI.HeaderTabla);
        GUI.Label(new Rect(header.xMax - anchoTecla, header.y, anchoTecla, header.height), "TECLA", EstiloUI.HeaderTabla);
        y += AltoFilaControles;

        // Fila 0: la tecla fija. Va primera para que se lea como parte del esquema de control y no
        // como una nota al pie.
        Rect filaEsc = Fila(dentro, y, 0, AltoFilaControles);
        GUI.Label(Celda(filaEsc), "Pausa", EstiloUI.Cuerpo);
        GUI.Label(new Rect(filaEsc.xMax - anchoTecla + 10f, filaEsc.y, anchoTecla - 10f, filaEsc.height), "ESC — fija", EstiloUI.Nota);
        y += AltoFilaControles;

        for (int i = 0; i < acciones.Length; i++)
        {
            KeyBindings.Action a = acciones[i];
            Rect fila = Fila(dentro, y, i + 1, AltoFilaControles);

            GUI.Label(Celda(fila), KeyBindings.Names[(int)a], EstiloUI.Cuerpo);

            var botonTecla = new Rect(fila.xMax - anchoTecla + 10f, fila.y + 2f, anchoTecla - 20f, AltoFilaControles - 4f);
            string texto = waiting == a ? "PULSÁ UNA TECLA…" : KeyBindings.Get(a).ToString().ToUpperInvariant();
            if (SonidosUI.Boton(botonTecla, texto, EstiloUI.BotonFila)) waiting = a;

            y += AltoFilaControles;
        }

        y += 8f;
        GUI.Label(new Rect(dentro.x, y, dentro.width, AltoEtiqueta), "Esc cancela el cambio de tecla. La pausa siempre es Esc.", EstiloUI.Nota);

        // Dos botones al pie: restablecer es destructivo pero reversible, asi que va secundario.
        float anchoBoton = (dentro.width - EstiloUI.Separacion) * 0.5f;
        var pie = new Rect(dentro.x, panel.yMax - EstiloUI.Padding - EstiloUI.AltoBoton, anchoBoton, EstiloUI.AltoBoton);
        if (SonidosUI.Boton(pie, "RESTABLECER TECLAS", EstiloUI.BotonSecundario))
        {
            KeyBindings.ResetDefaults();
            waiting = null;
        }

        pie.x += anchoBoton + EstiloUI.Separacion;
        if (SonidosUI.BotonAtras(pie, "VOLVER", EstiloUI.BotonPrimario)) Ir(EstadoMenu.Configuracion);
    }

    // ---------------------------------------------------------------
    // Piezas de maquetado
    // ---------------------------------------------------------------

    const float SensMin = 0.1f, SensMax = 5f;

    static string Porcentaje(float v) => Mathf.RoundToInt(v * 100f) + "%";

    // Rectangulo de ancho fijo centrado horizontalmente en el area, con 'y' relativa al area.
    static Rect Centrada(Rect area, float ancho, float y, float alto)
    {
        return new Rect(area.x + (area.width - ancho) * 0.5f, area.y + y, ancho, alto);
    }

    static Rect Columna(Rect area, float ancho, float y, float alto) => Centrada(area, ancho, y, alto);

    // Dibuja la tarjeta con su subtitulo y devuelve el rectangulo util de adentro (ya sin el
    // relleno interno ni el espacio del subtitulo).
    static Rect Tarjeta(Rect panel, string subtitulo)
    {
        GUI.Box(panel, GUIContent.none, EstiloUI.Tarjeta);

        var cabecera = new Rect(panel.x + EstiloUI.Padding, panel.y + EstiloUI.Padding, panel.width - EstiloUI.Padding * 2f, 34f);
        GUI.Label(cabecera, subtitulo, EstiloUI.Subtitulo);

        return new Rect(cabecera.x, cabecera.yMax + 6f, cabecera.width, panel.yMax - cabecera.yMax - EstiloUI.Padding - 6f);
    }

    void BotonVolver(Rect panel, EstadoMenu destino)
    {
        var r = new Rect(panel.x + EstiloUI.Padding, panel.yMax - EstiloUI.Padding - EstiloUI.AltoBoton,
            panel.width - EstiloUI.Padding * 2f, EstiloUI.AltoBoton);
        if (SonidosUI.BotonAtras(r, "VOLVER", EstiloUI.BotonPrimario)) Ir(destino);
    }

    // Etiqueta + barra arrastrable + valor a la derecha. Devuelve la 'y' de la fila siguiente.
    // 'valor' y lo que recibe 'aplicar' van siempre en 0..1: la conversion a porcentaje o a
    // sensibilidad la hace quien llama, con 'formato' para el texto.
    static float Deslizador(Rect dentro, float y, string etiqueta, float valor, System.Func<float, string> formato, System.Action<float> aplicar)
    {
        GUI.Label(new Rect(dentro.x, y, dentro.width - AnchoValor, AltoEtiqueta), etiqueta, EstiloUI.Cuerpo);
        GUI.Label(new Rect(dentro.xMax - AnchoValor, y, AnchoValor, AltoEtiqueta), formato(valor), EstiloUI.DatoDerecha);

        float yBarra = y + AltoEtiqueta;
        var r = new Rect(dentro.x, yBarra, dentro.width, AltoBarra);
        float nuevo = EstiloUI.BarraArrastrable(r, valor);
        if (!Mathf.Approximately(nuevo, valor)) aplicar(nuevo);

        return yBarra + AltoBarra + EstiloUI.Separacion * 2f;
    }

    // Etiqueta + "<" valor ">" en una fila. Devuelve la 'y' de la fila siguiente.
    static float Selector(Rect dentro, float y, string etiqueta, string valor, System.Action<int> cambiar)
    {
        const float lado = 44f;

        GUI.Label(new Rect(dentro.x, y, dentro.width * 0.5f, EstiloUI.AltoBoton), etiqueta, EstiloUI.Cuerpo);

        float xDerecha = dentro.xMax;
        if (SonidosUI.Boton(new Rect(xDerecha - lado, y, lado, EstiloUI.AltoBoton), ">", EstiloUI.BotonSecundario)) cambiar(1);
        if (SonidosUI.Boton(new Rect(dentro.x + dentro.width * 0.5f, y, lado, EstiloUI.AltoBoton), "<", EstiloUI.BotonSecundario)) cambiar(-1);

        var rValor = new Rect(dentro.x + dentro.width * 0.5f + lado, y, xDerecha - lado - (dentro.x + dentro.width * 0.5f + lado), EstiloUI.AltoBoton);
        GUI.Label(rValor, valor, EstiloUI.DatoCentrado);

        return y + EstiloUI.AltoBoton + EstiloUI.Separacion;
    }

    // Fila de tabla con el fondo alternado del manual.
    static Rect Fila(Rect dentro, float y, int indice, float alto)
    {
        var r = new Rect(dentro.x, y, dentro.width, alto);
        EstiloUI.FilaTabla(r, indice);
        return r;
    }

    static Rect Celda(Rect fila) => new Rect(fila.x + 10f, fila.y, fila.width - 20f, fila.height);
}
