using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// HUD minimo del jugador (wireframe P-06): vida, estamina debajo, contador de oro, barra de acceso
// rapido y la "mano con linterna" a la derecha. Todo conectado a PlayerStats por eventos (no sondea
// en Update, solo suaviza visualmente el valor que ya llego).
//
// Arma su propio Canvas por codigo al arrancar: es lo unico del juego que usa uGUI en vez de OnGUI,
// porque Image.fillAmount -el relleno animado de las barras- pide un Canvas real. No hace falta
// tocar la escena para usarlo. Los colores y la tipografia salen igual de EstiloUI, asi que el HUD
// y los menus se ven del mismo juego aunque los dibujen dos sistemas distintos.
//
// El oro no es una barra (no tiene maximo): es un cartelito con el numero, con la misma tarjeta y
// el mismo blanco humo que el "N ORO" del panel de la tienda, para que el jugador lea el mismo dato
// igual en los dos lados.
//
// La barra de acceso rapido (las casillas 1-4) tambien se dibuja aca. La mecanica no vive aca: es
// de BarraRapida, y esta clase solo la dibuja y se refresca por eventos, igual que con PlayerStats.
//
// Reparto en pantalla, segun P-06: vida, estamina y oro en la esquina inferior IZQUIERDA (anclaje
// de punto en 0,0), la barra rapida centrada abajo (0.5,0), la linterna en la inferior DERECHA
// (1,0) y el centro VACIO (sin mira). Las medidas estan todas en el bloque de constantes de mas
// abajo, en pixeles de la resolucion de referencia de 1920x1080 del CanvasScaler.
public class PlayerUI : MonoBehaviour
{
    [Header("Jugador")]
    [Tooltip("Si se deja vacio, se busca el primer PlayerStats de la escena al arrancar")]
    [SerializeField] private PlayerStats stats;

    [Tooltip("Si se deja vacio, se busca la BarraRapida de la escena al arrancar")]
    [SerializeField] private BarraRapida barraRapida;

    // Los colores salen de la paleta de EstiloUI (Etapa 12), no de valores sueltos. Que significa
    // cada uno:
    //   - Vida: rojo sangre. Es el 10% de acento de la paleta, y le toca al dato mas critico.
    //   - Estamina: gris metal, que es el color de todo lo secundario.
    //   - Estamina en cero: rojo sangre, porque ahi si es una alerta (no se puede correr).
    // El numero de las dos barras va siempre en blanco humo, que contrasta contra el rojo y contra
    // el gris: por eso la estamina no se llena de blanco, que dejaria el texto ilegible encima.
    // No hay amarillos ni celestes: la paleta tiene cuatro colores.
    [Header("Colores")]
    [SerializeField] private Color colorVida = EstiloUI.RojoSangre;
    [SerializeField] private Color colorEstamina = EstiloUI.GrisMetal;
    [SerializeField] private Color colorEstaminaAgotada = EstiloUI.RojoSangre;
    [Tooltip("Por debajo de este porcentaje de vida, el numero se pone en rojo")]
    [Range(0f, 1f)] [SerializeField] private float umbralVidaBaja = 0.3f;
    [SerializeField] private float velocidadSuavizado = 8f;

    [Header("Colores de la barra rapida")]
    [Tooltip("Borde de una casilla con un item que el jugador tiene")]
    [SerializeField] private Color colorCasillaLista = EstiloUI.BlancoHumo;
    [Tooltip("Borde de una casilla vacia, o con un item que el jugador no tiene")]
    [SerializeField] private Color colorCasillaVacia = EstiloUI.GrisMetal;
    [Tooltip("Borde de la casilla del item que esta ahora mismo en la mano")]
    [SerializeField] private Color colorCasillaEquipada = EstiloUI.RojoSangre;

    Image rellenoVida, rellenoEstamina;
    Text textoVida, textoEstamina, textoOro;

    // "Mano con linterna" de P-06: el icono y la etiqueta de la esquina inferior derecha. No tiene
    // evento al que suscribirse (el estado vive en el Light de FlashlightController), asi que se
    // lee en Update comparando contra el ultimo valor dibujado.
    Image iconoLinterna;
    Text textoLinterna;
    FlashlightController linterna;
    bool linternaDibujadaEncendida;

    // Una entrada por casilla de la barra rapida. Se guardan las piezas que cambian de estado,
    // para refrescarlas sin volver a recorrer la jerarquia.
    struct Casilla
    {
        public Image borde;
        public Image icono;
        public Text nombre;
        public Text cantidad;
    }

    Casilla[] casillas = new Casilla[0];
    Inventory inventario;
    EquipoJugador equipo;
    float rellenoObjetivoVida = 1f, rellenoObjetivoEstamina = 1f;
    float vidaMostrada = 1f, estaminaMostrada = 1f;

    // Se crea sola si la escena no tiene una, para no depender de agregarla a mano. No se crea
    // en escenas sin jugador (por ejemplo un futuro menu de inicio sin PlayerStats).
    // Se recrea en cada carga de escena: Reiniciar recarga la escena y RuntimeInitializeOnLoadMethod corre una sola vez (#55).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCrear()
    {
        EnsureExists();
        SceneManager.sceneLoaded -= OnSceneLoadedRecrear;
        SceneManager.sceneLoaded += OnSceneLoadedRecrear;
    }

    static void OnSceneLoadedRecrear(Scene s, LoadSceneMode m) => EnsureExists();

    public static void EnsureExists()
    {
        if (FindAnyObjectByType<PlayerUI>() != null) return;
        if (FindAnyObjectByType<PlayerStats>() == null) return;
        new GameObject("PlayerUI").AddComponent<PlayerUI>();
    }

    void Start()
    {
        if (stats == null) stats = FindAnyObjectByType<PlayerStats>();

        // BarraRapida y EquipoJugador se instalan solos desde un RuntimeInitializeOnLoadMethod, que
        // no garantiza haber corrido antes que este Start: se les pide existir explicitamente para
        // que el orden no decida si la barra rapida queda cableada o no.
        BarraRapida.EnsureExists();
        EquipoJugador.EnsureExists();

        if (barraRapida == null) barraRapida = FindAnyObjectByType<BarraRapida>();
        inventario = FindAnyObjectByType<Inventory>();
        equipo = FindAnyObjectByType<EquipoJugador>();
        linterna = FindAnyObjectByType<FlashlightController>();

        // Deja lista la fuente y la paleta antes de armar el Canvas: los Text de abajo le piden
        // EstiloUI.Fuente, que recien existe despues de Construir().
        EstiloUI.Construir();

        ConstruirCanvas();

        if (stats != null)
        {
            stats.AlCambiarVida += ActualizarVida;
            stats.AlCambiarEstamina += ActualizarEstamina;
            stats.AlCambiarOro += ActualizarOro;
            ActualizarVida(stats.CurrentHealth, stats.MaxHealth);
            ActualizarEstamina(stats.CurrentStamina, stats.MaxStamina);
            ActualizarOro(stats.Oro);
        }
        else
        {
            Debug.LogWarning("PlayerUI: no se encontro ningun PlayerStats en la escena.");
        }

        // La barra rapida se refresca por cambios de estado, no en Update: lo que puede cambiar
        // una casilla es que entre o salga algo del inventario, o que el arma pase a la mano.
        if (inventario != null) inventario.OnInventoryChanged += ActualizarCasillas;
        if (equipo != null) equipo.AlCambiarArma += AlCambiarArma;
        ActualizarCasillas();
    }

    void OnDestroy()
    {
        if (inventario != null) inventario.OnInventoryChanged -= ActualizarCasillas;
        if (equipo != null) equipo.AlCambiarArma -= AlCambiarArma;

        if (stats == null) return;
        stats.AlCambiarVida -= ActualizarVida;
        stats.AlCambiarEstamina -= ActualizarEstamina;
        stats.AlCambiarOro -= ActualizarOro;
    }

    // AlCambiarArma manda un bool que aca no interesa: lo que cambia es que casilla se dibuja
    // como equipada, y eso se lee del estado.
    void AlCambiarArma(bool _) => ActualizarCasillas();

    void Update()
    {
        // El valor real ya llego por evento; aca solo se suaviza como se ve la barra
        vidaMostrada = Mathf.Lerp(vidaMostrada, rellenoObjetivoVida, velocidadSuavizado * Time.deltaTime);
        estaminaMostrada = Mathf.Lerp(estaminaMostrada, rellenoObjetivoEstamina, velocidadSuavizado * Time.deltaTime);

        if (rellenoVida != null) rellenoVida.fillAmount = vidaMostrada;
        if (rellenoEstamina != null) rellenoEstamina.fillAmount = estaminaMostrada;

        ActualizarLinterna();
    }

    void ActualizarVida(float actual, float maximo)
    {
        rellenoObjetivoVida = maximo > 0f ? actual / maximo : 0f;

        // La barra siempre es roja: el rojo es el color de la vida, no el de la alerta. Lo que
        // avisa que queda poca es el numero, que pasa de blanco humo a rojo.
        if (rellenoVida != null) rellenoVida.color = colorVida;
        if (textoVida != null)
        {
            textoVida.text = Mathf.CeilToInt(actual) + " / " + Mathf.CeilToInt(maximo);
            textoVida.color = rellenoObjetivoVida <= umbralVidaBaja ? EstiloUI.RojoSangre : EstiloUI.BlancoHumo;
        }
    }

    // Prende y apaga el icono de la linterna. Se compara con lo ultimo dibujado para no reescribir
    // el Text y el color en todos los frames.
    void ActualizarLinterna()
    {
        if (iconoLinterna == null) return;

        if (linterna == null)
        {
            linterna = FindAnyObjectByType<FlashlightController>();
            if (linterna == null) return;
        }

        bool encendida = linterna.Encendida;
        if (encendida == linternaDibujadaEncendida && textoLinterna != null && textoLinterna.text.Length > 0) return;

        linternaDibujadaEncendida = encendida;

        // Prendida: icono y etiqueta en rojo sangre, el acento del HUD. Apagada: gris metal, el
        // color de todo lo inactivo.
        Color color = encendida ? EstiloUI.RojoSangre : EstiloUI.GrisMetal;
        iconoLinterna.color = color;

        if (textoLinterna != null)
        {
            textoLinterna.text = encendida ? "ON" : "OFF";
            textoLinterna.color = color;
        }
    }

    void ActualizarEstamina(float actual, float maximo)
    {
        rellenoObjetivoEstamina = maximo > 0f ? actual / maximo : 0f;
        if (rellenoEstamina != null) rellenoEstamina.color = actual <= 0f ? colorEstaminaAgotada : colorEstamina;
        if (textoEstamina != null) textoEstamina.text = Mathf.CeilToInt(actual) + " / " + Mathf.CeilToInt(maximo);
    }

    // Lo dispara PlayerStats.AlCambiarOro, asi que se actualiza solo al recoger una moneda y al
    // comprar o vender en la tienda: no hay un segundo contador que pueda quedar desincronizado.
    void ActualizarOro(int actual)
    {
        if (textoOro != null) textoOro.text = actual + " ORO";
    }

    // ---------------------------------------------------------------
    // Construccion del Canvas por codigo
    // ---------------------------------------------------------------

    // Medidas del HUD, en pixeles de la resolucion de referencia (1920x1080) que usa el
    // CanvasScaler. Estan juntas aca y no repartidas en las llamadas para poder mover el bloque
    // entero cambiando un solo numero.
    // Vida, estamina y oro: esquina inferior IZQUIERDA. Mas grandes que antes para que se lean de
    // un vistazo sin sacar la vista del centro de la pantalla.
    const float AnchoBarra = 360f;
    const float AltoBarra = 40f;
    const float AnchoOro = 170f;
    const float SeparacionBarras = 8f;       // entre vida y estamina
    const float SeparacionOro = 14f;         // entre el borde derecho de las barras y el oro
    const float MargenIzquierdo = 34f;       // del borde izquierdo de la pantalla al icono de cada barra
    const float LadoIconoBarra = 28f;        // el corazon y el rayo, a la izquierda de su barra
    const float SeparacionIcono = 10f;       // entre el icono y su barra
    const float MargenInferiorBarras = 28f;  // del borde de abajo a la barra de estamina
    const int FuenteBarra = 20;

    // Barra rapida: abajo del todo y al CENTRO, separada de las barras para que no se pisen ni en
    // pantallas angostas (las barras + el oro ocupan 544 px de los 1920 de referencia, y la barra
    // rapida 280 centrados: entre el borde derecho de una y el izquierdo de la otra sobran ~270).
    const float MargenInferior = 24f;     // del borde de abajo a la barra rapida
    const float LadoCasilla = 64f;
    const float SeparacionCasillas = 8f;  // entre casilla y casilla
    const float GrosorBorde = 2f;

    // "Mano con linterna", en la esquina inferior DERECHA, que es donde la ubica P-06. Es la unica
    // pieza del HUD de ese lado, asi que no puede chocar con nada.
    const float AnchoLinterna = 150f;
    const float AltoLinterna = 52f;
    const float MargenDerecho = 34f;
    const float LadoIconoLinterna = 32f;

    void ConstruirCanvas()
    {
        GameObject canvasGO = new GameObject("Canvas_PlayerUI");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // Tres anclajes, los tres al borde de abajo, los tres de punto (anchorMin == anchorMax):
        //
        //   (0, 0)   esquina inferior izquierda -> vida, estamina y oro. Quedan a una distancia
        //            fija del borde izquierdo y del de abajo, en cualquier resolucion.
        //   (0.5, 0) centro del borde inferior  -> barra rapida. Queda siempre centrada.
        //   (1, 0)   esquina inferior derecha   -> la mano con linterna de P-06.
        //
        // Son anclajes de punto y no estirados porque estos elementos tienen un tamano propio que
        // no debe deformarse: lo que los escala con la resolucion es el CanvasScaler de arriba
        // (ScaleWithScreenSize sobre 1920x1080), no el anclaje.
        //
        // En el centro de la pantalla no va NADA, y es a proposito: P-06 excluye la mira central.
        // El juego no es de apuntar, y una mira en el medio del laberinto solo le quitaria vacio a
        // la imagen, que es de lo que vive el terror del juego.
        Vector2 anclaAbajoIzquierda = new Vector2(0f, 0f);
        Vector2 anclaAbajoCentro = new Vector2(0.5f, 0f);
        Vector2 anclaAbajoDerecha = new Vector2(1f, 0f);

        CrearBarraRapida(canvas.transform, anclaAbajoCentro);

        // Cada barra lleva su icono lineal a la izquierda (corazon y rayo): el numero dice cuanto
        // queda y el icono dice de que. Las barras arrancan despues del icono, no en el margen.
        float xBarra = MargenIzquierdo + LadoIconoBarra + SeparacionIcono;
        float yVida = MargenInferiorBarras + AltoBarra + SeparacionBarras;

        CrearIcono(canvas.transform, "IconoVida", anclaAbajoIzquierda, IconosUI.SpriteVida, colorVida,
            new Vector2(MargenIzquierdo, yVida + (AltoBarra - LadoIconoBarra) * 0.5f));

        CrearBarra(canvas.transform, "BarraVida", anclaAbajoIzquierda, new Vector2(0f, 0f),
            new Vector2(xBarra, yVida), colorVida, out rellenoVida, out textoVida);

        CrearIcono(canvas.transform, "IconoEstamina", anclaAbajoIzquierda, IconosUI.SpriteEstamina, EstiloUI.BlancoHumo,
            new Vector2(MargenIzquierdo, MargenInferiorBarras + (AltoBarra - LadoIconoBarra) * 0.5f));

        CrearBarra(canvas.transform, "BarraEstamina", anclaAbajoIzquierda, new Vector2(0f, 0f),
            new Vector2(xBarra, MargenInferiorBarras), colorEstamina, out rellenoEstamina, out textoEstamina);

        // El oro va al costado derecho del par de barras, centrado verticalmente contra las dos.
        // Pivote en (0, 0): crece hacia la derecha, asi cambiarle el ancho no lo vuelve a mover.
        float centroDelPar = MargenInferiorBarras + (2f * AltoBarra + SeparacionBarras) * 0.5f;
        CrearEtiqueta(canvas.transform, "ContadorOro", anclaAbajoIzquierda, new Vector2(0f, 0f),
            new Vector2(xBarra + AnchoBarra + SeparacionOro, centroDelPar - AltoBarra * 0.5f), EstiloUI.BlancoHumo, out textoOro);

        CrearManoLinterna(canvas.transform, anclaAbajoDerecha);
    }

    // ---------------------------------------------------------------
    // Mano con linterna (P-06)
    // ---------------------------------------------------------------

    // Tarjeta con el icono lineal de la linterna y su estado (ON/OFF), abajo a la derecha. Es un
    // indicador, no un boton: la linterna se prende con su tecla (F por defecto), asi que no
    // recibe clics -raycastTarget apagado en todas sus piezas, como el resto del HUD-.
    void CrearManoLinterna(Transform padre, Vector2 ancla)
    {
        // Pivote en (1, 0) y posicion negativa en x: crece hacia la izquierda desde la esquina, asi
        // cambiarle el ancho no lo despega del borde derecho.
        GameObject bordeGO = NuevoRect("ManoLinterna", padre, ancla, ancla,
            new Vector2(-MargenDerecho, MargenInferiorBarras), new Vector2(AnchoLinterna, AltoLinterna));
        bordeGO.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);

        Image borde = bordeGO.AddComponent<Image>();
        borde.color = EstiloUI.GrisMetal;
        borde.raycastTarget = false;

        GameObject fondoGO = NuevoRect("Fondo", bordeGO.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform rtFondo = fondoGO.GetComponent<RectTransform>();
        rtFondo.offsetMin = new Vector2(1f, 1f);
        rtFondo.offsetMax = new Vector2(-1f, -1f);
        Image fondo = fondoGO.AddComponent<Image>();
        fondo.color = EstiloUI.FondoTarjeta;
        fondo.raycastTarget = false;

        GameObject iconoGO = NuevoRect("Icono", fondoGO.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(12f, 0f), new Vector2(LadoIconoLinterna, LadoIconoLinterna));
        iconoGO.GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);
        iconoLinterna = iconoGO.AddComponent<Image>();
        iconoLinterna.sprite = IconosUI.SpriteLinterna;
        iconoLinterna.color = EstiloUI.GrisMetal;
        iconoLinterna.preserveAspect = true;
        iconoLinterna.raycastTarget = false;

        GameObject textoGO = NuevoRect("Estado", fondoGO.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-12f, 0f), new Vector2(AnchoLinterna - LadoIconoLinterna - 36f, AltoLinterna));
        textoGO.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
        textoLinterna = NuevoTexto(textoGO, FuenteBarra, TextAnchor.MiddleRight, EstiloUI.GrisMetal);
        textoLinterna.fontStyle = FontStyle.Bold;

        // Primer dibujado: deja el estado sincronizado sin esperar al Update.
        ActualizarLinterna();
    }

    // ---------------------------------------------------------------
    // Barra rapida
    // ---------------------------------------------------------------

    // Cuatro casillas centradas abajo. Cada una es un borde (el que cambia de color segun el
    // estado), un fondo oscuro adentro, el icono del item, su nombre como respaldo cuando el
    // ItemData todavia no tiene icono, el numero de la tecla arriba a la izquierda y la cantidad
    // abajo a la derecha.
    void CrearBarraRapida(Transform padre, Vector2 ancla)
    {
        casillas = new Casilla[BarraRapida.Casillas];

        var fila = new GameObject("BarraRapida", typeof(RectTransform));
        fila.transform.SetParent(padre, false);
        RectTransform rtFila = fila.GetComponent<RectTransform>();
        rtFila.anchorMin = ancla;
        rtFila.anchorMax = ancla;
        rtFila.pivot = new Vector2(0.5f, 0f);
        rtFila.anchoredPosition = new Vector2(0f, MargenInferior);
        rtFila.sizeDelta = new Vector2(
            BarraRapida.Casillas * LadoCasilla + (BarraRapida.Casillas - 1) * SeparacionCasillas,
            LadoCasilla);

        for (int i = 0; i < BarraRapida.Casillas; i++)
        {
            casillas[i] = CrearCasilla(fila.transform, i);
        }
    }

    Casilla CrearCasilla(Transform padre, int indice)
    {
        var resultado = new Casilla();

        // Desplazamiento desde el centro de la fila: con 4 casillas, los centros caen en
        // -1.5, -0.5, 0.5 y 1.5 pasos.
        float paso = LadoCasilla + SeparacionCasillas;
        float x = (indice - (BarraRapida.Casillas - 1) * 0.5f) * paso;

        GameObject bordeGO = NuevoRect("Casilla_" + (indice + 1) + "_Borde", padre,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(LadoCasilla, LadoCasilla));
        resultado.borde = bordeGO.AddComponent<Image>();
        resultado.borde.color = colorCasillaVacia;
        // raycastTarget apagado en toda la barra rapida: el panel de la tienda y el menu se dibujan
        // con IMGUI por encima, y una Image que capture clics aca abajo solo puede molestar.
        resultado.borde.raycastTarget = false;

        // El fondo va adentro del borde y un poco mas chico: la diferencia es lo que se ve como marco.
        GameObject fondoGO = NuevoRect("Fondo", bordeGO.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform rtFondo = fondoGO.GetComponent<RectTransform>();
        rtFondo.offsetMin = new Vector2(GrosorBorde, GrosorBorde);
        rtFondo.offsetMax = new Vector2(-GrosorBorde, -GrosorBorde);
        Image fondo = fondoGO.AddComponent<Image>();
        fondo.color = EstiloUI.FondoTarjeta;
        fondo.raycastTarget = false;

        // Icono: ocupa la casilla con un margen. Arranca apagado y se prende solo si el ItemData
        // tiene sprite (hoy ninguno lo tiene, de ahi el respaldo por nombre).
        GameObject iconoGO = NuevoRect("Icono", fondoGO.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform rtIcono = iconoGO.GetComponent<RectTransform>();
        rtIcono.offsetMin = new Vector2(6f, 6f);
        rtIcono.offsetMax = new Vector2(-6f, -6f);
        resultado.icono = iconoGO.AddComponent<Image>();
        resultado.icono.preserveAspect = true;
        resultado.icono.raycastTarget = false;
        resultado.icono.enabled = false;

        GameObject nombreGO = NuevoRect("Nombre", fondoGO.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform rtNombre = nombreGO.GetComponent<RectTransform>();
        rtNombre.offsetMin = new Vector2(3f, 3f);
        rtNombre.offsetMax = new Vector2(-3f, -12f); // deja libre la franja de abajo, donde va la cantidad
        resultado.nombre = NuevoTexto(nombreGO, 11, TextAnchor.MiddleCenter, EstiloUI.BlancoHumo);
        resultado.nombre.horizontalOverflow = HorizontalWrapMode.Wrap;

        // Numero de la tecla, arriba a la izquierda.
        GameObject teclaGO = NuevoRect("Tecla", fondoGO.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(2f, -1f), new Vector2(16f, 14f));
        RectTransform rtTecla = teclaGO.GetComponent<RectTransform>();
        rtTecla.pivot = new Vector2(0f, 1f);
        Text tecla = NuevoTexto(teclaGO, 12, TextAnchor.UpperLeft, EstiloUI.GrisMetal);
        tecla.text = (indice + 1).ToString();

        // Cantidad, abajo a la derecha.
        GameObject cantidadGO = NuevoRect("Cantidad", fondoGO.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-3f, 1f), new Vector2(28f, 14f));
        RectTransform rtCantidad = cantidadGO.GetComponent<RectTransform>();
        rtCantidad.pivot = new Vector2(1f, 0f);
        resultado.cantidad = NuevoTexto(cantidadGO, 12, TextAnchor.LowerRight, EstiloUI.BlancoHumo);

        return resultado;
    }

    // Repinta las cuatro casillas desde el estado actual. Es barato (4 casillas) y se llama solo
    // cuando algo cambio, no todos los frames.
    void ActualizarCasillas()
    {
        for (int i = 0; i < casillas.Length; i++)
        {
            ItemData item = barraRapida != null ? barraRapida.ItemDe(i) : null;
            bool disponible = barraRapida != null && barraRapida.Disponible(i);
            bool equipada = barraRapida != null && barraRapida.Equipado(i);
            int cantidad = barraRapida != null ? barraRapida.Cantidad(i) : 0;

            Casilla casilla = casillas[i];

            if (casilla.borde != null)
            {
                casilla.borde.color = equipada ? colorCasillaEquipada
                    : disponible ? colorCasillaLista
                    : colorCasillaVacia;
            }

            bool hayIcono = item != null && item.icon != null;
            if (casilla.icono != null)
            {
                casilla.icono.enabled = hayIcono;
                casilla.icono.sprite = hayIcono ? item.icon : null;
                // En gris si el jugador no tiene el item: se ve que la casilla existe pero no sirve.
                casilla.icono.color = disponible ? EstiloUI.BlancoHumo : EstiloUI.GrisMetal;
            }

            if (casilla.nombre != null)
            {
                // El nombre es el respaldo del icono: mientras los ItemData no tengan sprite, es
                // lo unico que identifica la casilla.
                casilla.nombre.text = hayIcono || item == null ? string.Empty : item.itemName;
                casilla.nombre.color = disponible ? EstiloUI.BlancoHumo : EstiloUI.GrisMetal;
            }

            if (casilla.cantidad != null)
            {
                casilla.cantidad.text = cantidad > 1 ? "x" + cantidad : string.Empty;
            }
        }
    }

    // ---------------------------------------------------------------
    // Helpers de uGUI
    // ---------------------------------------------------------------

    static GameObject NuevoRect(string nombre, Transform padre, Vector2 anclaMin, Vector2 anclaMax, Vector2 posicion, Vector2 tamano)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anclaMin;
        rt.anchorMax = anclaMax;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamano;
        return go;
    }

    // Icono lineal suelto del HUD, sin tarjeta detras: el corazon de la vida y el rayo de la
    // estamina. No recibe clics, como todo el HUD.
    static void CrearIcono(Transform padre, string nombre, Vector2 ancla, Sprite sprite, Color color, Vector2 posicion)
    {
        GameObject go = NuevoRect(nombre, padre, ancla, ancla, posicion, new Vector2(LadoIconoBarra, LadoIconoBarra));
        go.GetComponent<RectTransform>().pivot = new Vector2(0f, 0f);

        Image imagen = go.AddComponent<Image>();
        imagen.sprite = sprite;
        imagen.color = color;
        imagen.preserveAspect = true;
        imagen.raycastTarget = false;
    }

    // La fuente es la misma que usa el resto de la interfaz (Arial, la sustituta metrica de la
    // Helvetica del manual): la resuelve EstiloUI una sola vez, y Start la pide con Construir()
    // antes de armar el Canvas.
    static Text NuevoTexto(GameObject go, int tamano, TextAnchor alineacion, Color color)
    {
        Text texto = go.AddComponent<Text>();
        texto.font = EstiloUI.Fuente;
        texto.fontSize = tamano;
        texto.alignment = alineacion;
        texto.color = color;
        texto.raycastTarget = false;
        texto.text = string.Empty;
        return texto;
    }

    // Misma tarjeta que CrearBarra pero sin relleno: el oro no tiene maximo, asi que una barra no
    // significaria nada. Va al costado derecho de las dos barras y mas angosto porque solo lleva un
    // numero.
    static void CrearEtiqueta(Transform padre, string nombre, Vector2 ancla, Vector2 pivote, Vector2 posicion, Color colorTexto, out Text texto)
    {
        GameObject bordeGO = Tarjeta(nombre, padre, ancla, pivote, posicion, new Vector2(AnchoOro, AltoBarra), out Transform dentro);

        GameObject textoGO = NuevoRect(nombre + "_Texto", dentro, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform rtTexto = textoGO.GetComponent<RectTransform>();
        rtTexto.offsetMin = Vector2.zero;
        rtTexto.offsetMax = Vector2.zero;

        texto = NuevoTexto(textoGO, FuenteBarra, TextAnchor.MiddleCenter, colorTexto);
        texto.fontStyle = FontStyle.Bold;

        _ = bordeGO;
    }

    // Tarjeta del HUD: borde de 1 px gris metal con el fondo #161616 adentro, las mismas dos capas
    // que tienen las tarjetas de los menus. Devuelve el objeto del borde y, en 'dentro', el
    // transform del fondo, que es donde van los hijos.
    static GameObject Tarjeta(string nombre, Transform padre, Vector2 ancla, Vector2 pivote, Vector2 posicion, Vector2 tamano, out Transform dentro)
    {
        GameObject bordeGO = NuevoRect(nombre + "_Borde", padre, ancla, ancla, posicion, tamano);
        bordeGO.GetComponent<RectTransform>().pivot = pivote;
        Image borde = bordeGO.AddComponent<Image>();
        borde.color = EstiloUI.GrisMetal;
        borde.raycastTarget = false;

        GameObject fondoGO = NuevoRect(nombre + "_Fondo", bordeGO.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform rtFondo = fondoGO.GetComponent<RectTransform>();
        rtFondo.offsetMin = new Vector2(EstiloUI.BordeTarjeta, EstiloUI.BordeTarjeta);
        rtFondo.offsetMax = new Vector2(-EstiloUI.BordeTarjeta, -EstiloUI.BordeTarjeta);
        Image fondo = fondoGO.AddComponent<Image>();
        fondo.color = EstiloUI.FondoTarjeta;
        fondo.raycastTarget = false;

        dentro = fondoGO.transform;
        return bordeGO;
    }

    // Tarjeta (borde gris + canal #161616) + relleno tipo Image.FillMethod.Horizontal + texto
    // numerico encima. Es la misma barra que dibuja EstiloUI.Barra en las pantallas IMGUI, armada
    // con uGUI porque el relleno animado necesita Image.fillAmount.
    static void CrearBarra(Transform padre, string nombre, Vector2 ancla, Vector2 pivote, Vector2 posicion, Color colorInicial, out Image relleno, out Text texto)
    {
        Tarjeta(nombre, padre, ancla, pivote, posicion, new Vector2(AnchoBarra, AltoBarra), out Transform dentro);

        GameObject rellenoGO = NuevoRect(nombre + "_Relleno", dentro, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform rtRelleno = rellenoGO.GetComponent<RectTransform>();
        rtRelleno.offsetMin = new Vector2(EstiloUI.BordeTarjeta, EstiloUI.BordeTarjeta);
        rtRelleno.offsetMax = new Vector2(-EstiloUI.BordeTarjeta, -EstiloUI.BordeTarjeta);
        Image imgRelleno = rellenoGO.AddComponent<Image>();
        imgRelleno.color = colorInicial;
        imgRelleno.type = Image.Type.Filled;
        imgRelleno.fillMethod = Image.FillMethod.Horizontal;
        imgRelleno.fillOrigin = (int)Image.OriginHorizontal.Left;
        imgRelleno.fillAmount = 1f;
        imgRelleno.raycastTarget = false;

        GameObject textoGO = NuevoRect(nombre + "_Texto", dentro, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform rtTexto = textoGO.GetComponent<RectTransform>();
        rtTexto.offsetMin = Vector2.zero;
        rtTexto.offsetMax = Vector2.zero;

        relleno = imgRelleno;
        texto = NuevoTexto(textoGO, FuenteBarra, TextAnchor.MiddleCenter, EstiloUI.BlancoHumo);
    }
}
