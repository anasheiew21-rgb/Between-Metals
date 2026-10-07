using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Barras de vida y estamina y contador de oro en pantalla, conectados a PlayerStats por eventos
// (no sondea en Update, solo suaviza visualmente el valor que ya llego). Arma su propio Canvas por
// codigo al arrancar: es lo unico del proyecto que usa uGUI en vez de OnGUI, porque
// Image.FillMethod pide un Canvas real. No hace falta tocar la escena para usarlo.
//
// El oro no es una barra (no tiene maximo): es un cartelito con el numero, en el mismo amarillo
// que ya usa el panel de la tienda para que el jugador lea el mismo dato en los dos lados.
//
// Tambien dibuja la barra de acceso rapido (las casillas 1-4). La mecanica no vive aca: es de
// BarraRapida, y esta clase solo la dibuja y se refresca por eventos, igual que con PlayerStats.
//
// Reparto en pantalla: vida, estamina y oro en la esquina inferior IZQUIERDA (anclaje de punto en
// 0,0) y la barra rapida centrada abajo (anclaje 0.5,0). Las medidas estan todas en el bloque de
// constantes de mas abajo, en pixeles de la resolucion de referencia de 1920x1080 del CanvasScaler.
public class PlayerUI : MonoBehaviour
{
    [Header("Jugador")]
    [Tooltip("Si se deja vacio, se busca el primer PlayerStats de la escena al arrancar")]
    [SerializeField] private PlayerStats stats;

    [Tooltip("Si se deja vacio, se busca la BarraRapida de la escena al arrancar")]
    [SerializeField] private BarraRapida barraRapida;

    [Header("Colores")]
    [SerializeField] private Color colorVida = new Color(0.8f, 0.15f, 0.15f);
    [SerializeField] private Color colorVidaBaja = new Color(1f, 0.9f, 0.1f);
    [SerializeField] private Color colorEstamina = new Color(0.15f, 0.6f, 0.85f);
    [SerializeField] private Color colorEstaminaAgotada = new Color(0.5f, 0.5f, 0.5f);
    [Tooltip("Mismo amarillo que el 'Oro:' del panel de la tienda (ShopManager.oroStyle)")]
    [SerializeField] private Color colorOro = new Color(1f, 0.85f, 0.4f);
    [Range(0f, 1f)] [SerializeField] private float umbralVidaBaja = 0.3f;
    [SerializeField] private float velocidadSuavizado = 8f;

    [Header("Colores de la barra rapida")]
    [Tooltip("Borde de una casilla con un item que el jugador tiene")]
    [SerializeField] private Color colorCasillaLista = new Color(0.75f, 0.75f, 0.8f, 0.9f);
    [Tooltip("Borde de una casilla vacia, o con un item que el jugador no tiene")]
    [SerializeField] private Color colorCasillaVacia = new Color(0.35f, 0.35f, 0.4f, 0.6f);
    [Tooltip("Borde de la casilla del item que esta ahora mismo en la mano")]
    [SerializeField] private Color colorCasillaEquipada = new Color(1f, 0.85f, 0.4f, 1f);

    Image rellenoVida, rellenoEstamina;
    Text textoVida, textoEstamina, textoOro;

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
    }

    void ActualizarVida(float actual, float maximo)
    {
        rellenoObjetivoVida = maximo > 0f ? actual / maximo : 0f;
        if (rellenoVida != null) rellenoVida.color = rellenoObjetivoVida <= umbralVidaBaja ? colorVidaBaja : colorVida;
        if (textoVida != null) textoVida.text = Mathf.CeilToInt(actual) + " / " + Mathf.CeilToInt(maximo);
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
        if (textoOro != null) textoOro.text = "Oro: " + actual;
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
    const float MargenIzquierdo = 34f;       // del borde izquierdo de la pantalla a las barras
    const float MargenInferiorBarras = 28f;  // del borde de abajo a la barra de estamina
    const int FuenteBarra = 20;

    // Barra rapida: abajo del todo y al CENTRO, separada de las barras para que no se pisen ni en
    // pantallas angostas (las barras + el oro ocupan 544 px de los 1920 de referencia, y la barra
    // rapida 280 centrados: entre el borde derecho de una y el izquierdo de la otra sobran ~270).
    const float MargenInferior = 24f;     // del borde de abajo a la barra rapida
    const float LadoCasilla = 64f;
    const float SeparacionCasillas = 8f;  // entre casilla y casilla
    const float GrosorBorde = 2f;

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

        // Dos anclajes, los dos al borde de abajo, los dos de punto (anchorMin == anchorMax):
        //
        //   (0, 0)   esquina inferior izquierda -> vida, estamina y oro. Quedan a una distancia
        //            fija del borde izquierdo y del de abajo, en cualquier resolucion.
        //   (0.5, 0) centro del borde inferior  -> barra rapida. Queda siempre centrada.
        //
        // Son anclajes de punto y no estirados porque estos elementos tienen un tamano propio que
        // no debe deformarse: lo que los escala con la resolucion es el CanvasScaler de arriba
        // (ScaleWithScreenSize sobre 1920x1080), no el anclaje.
        Vector2 anclaAbajoIzquierda = new Vector2(0f, 0f);
        Vector2 anclaAbajoCentro = new Vector2(0.5f, 0f);

        CrearBarraRapida(canvas.transform, anclaAbajoCentro);

        CrearBarra(canvas.transform, "BarraVida", anclaAbajoIzquierda, new Vector2(0f, 0f),
            new Vector2(MargenIzquierdo, MargenInferiorBarras + AltoBarra + SeparacionBarras), colorVida, out rellenoVida, out textoVida);

        CrearBarra(canvas.transform, "BarraEstamina", anclaAbajoIzquierda, new Vector2(0f, 0f),
            new Vector2(MargenIzquierdo, MargenInferiorBarras), colorEstamina, out rellenoEstamina, out textoEstamina);

        // El oro va al costado derecho del par de barras, centrado verticalmente contra las dos.
        // Pivote en (0, 0): crece hacia la derecha, asi cambiarle el ancho no lo vuelve a mover.
        float centroDelPar = MargenInferiorBarras + (2f * AltoBarra + SeparacionBarras) * 0.5f;
        CrearEtiqueta(canvas.transform, "ContadorOro", anclaAbajoIzquierda, new Vector2(0f, 0f),
            new Vector2(MargenIzquierdo + AnchoBarra + SeparacionOro, centroDelPar - AltoBarra * 0.5f), colorOro, out textoOro);
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
        fondo.color = new Color(0f, 0f, 0f, 0.7f);
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
        resultado.nombre = NuevoTexto(nombreGO, 11, TextAnchor.MiddleCenter, Color.white);
        resultado.nombre.horizontalOverflow = HorizontalWrapMode.Wrap;

        // Numero de la tecla, arriba a la izquierda.
        GameObject teclaGO = NuevoRect("Tecla", fondoGO.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(2f, -1f), new Vector2(16f, 14f));
        RectTransform rtTecla = teclaGO.GetComponent<RectTransform>();
        rtTecla.pivot = new Vector2(0f, 1f);
        Text tecla = NuevoTexto(teclaGO, 12, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.75f));
        tecla.text = (indice + 1).ToString();

        // Cantidad, abajo a la derecha.
        GameObject cantidadGO = NuevoRect("Cantidad", fondoGO.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-3f, 1f), new Vector2(28f, 14f));
        RectTransform rtCantidad = cantidadGO.GetComponent<RectTransform>();
        rtCantidad.pivot = new Vector2(1f, 0f);
        resultado.cantidad = NuevoTexto(cantidadGO, 12, TextAnchor.LowerRight, colorOro);

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
                casilla.icono.color = disponible ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }

            if (casilla.nombre != null)
            {
                // El nombre es el respaldo del icono: mientras los ItemData no tengan sprite, es
                // lo unico que identifica la casilla.
                casilla.nombre.text = hayIcono || item == null ? string.Empty : item.itemName;
                casilla.nombre.color = disponible ? Color.white : new Color(1f, 1f, 1f, 0.4f);
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

    static Text NuevoTexto(GameObject go, int tamano, TextAnchor alineacion, Color color)
    {
        Text texto = go.AddComponent<Text>();
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = tamano;
        texto.alignment = alineacion;
        texto.color = color;
        texto.raycastTarget = false;
        texto.text = string.Empty;
        return texto;
    }

    // Mismo fondo oscuro y misma tipografia que CrearBarra, pero sin relleno: el oro no tiene
    // maximo, asi que una barra no significaria nada. Va al costado derecho de las dos barras y
    // mas angosto porque solo lleva un numero.
    static void CrearEtiqueta(Transform padre, string nombre, Vector2 ancla, Vector2 pivote, Vector2 posicion, Color colorTexto, out Text texto)
    {
        GameObject fondoGO = new GameObject(nombre + "_Fondo", typeof(RectTransform));
        fondoGO.transform.SetParent(padre, false);
        RectTransform rtFondo = fondoGO.GetComponent<RectTransform>();
        rtFondo.anchorMin = ancla;
        rtFondo.anchorMax = ancla;
        rtFondo.pivot = pivote;
        rtFondo.anchoredPosition = posicion;
        rtFondo.sizeDelta = new Vector2(AnchoOro, AltoBarra);
        Image imgFondo = fondoGO.AddComponent<Image>();
        imgFondo.color = new Color(0f, 0f, 0f, 0.6f);

        GameObject textoGO = new GameObject(nombre + "_Texto", typeof(RectTransform));
        textoGO.transform.SetParent(fondoGO.transform, false);
        RectTransform rtTexto = textoGO.GetComponent<RectTransform>();
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = Vector2.one;
        rtTexto.offsetMin = Vector2.zero;
        rtTexto.offsetMax = Vector2.zero;
        Text txt = textoGO.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = colorTexto;
        txt.fontSize = FuenteBarra;
        txt.fontStyle = FontStyle.Bold;
        txt.text = "";

        texto = txt;
    }

    // Fondo oscuro + relleno tipo Image.FillMethod.Horizontal + texto numerico encima
    static void CrearBarra(Transform padre, string nombre, Vector2 ancla, Vector2 pivote, Vector2 posicion, Color colorInicial, out Image relleno, out Text texto)
    {
        GameObject fondoGO = new GameObject(nombre + "_Fondo", typeof(RectTransform));
        fondoGO.transform.SetParent(padre, false);
        RectTransform rtFondo = fondoGO.GetComponent<RectTransform>();
        rtFondo.anchorMin = ancla;
        rtFondo.anchorMax = ancla;
        rtFondo.pivot = pivote;
        rtFondo.anchoredPosition = posicion;
        rtFondo.sizeDelta = new Vector2(AnchoBarra, AltoBarra);
        Image imgFondo = fondoGO.AddComponent<Image>();
        imgFondo.color = new Color(0f, 0f, 0f, 0.6f);

        GameObject rellenoGO = new GameObject(nombre + "_Relleno", typeof(RectTransform));
        rellenoGO.transform.SetParent(fondoGO.transform, false);
        RectTransform rtRelleno = rellenoGO.GetComponent<RectTransform>();
        rtRelleno.anchorMin = Vector2.zero;
        rtRelleno.anchorMax = Vector2.one;
        rtRelleno.offsetMin = new Vector2(2f, 2f);
        rtRelleno.offsetMax = new Vector2(-2f, -2f);
        Image imgRelleno = rellenoGO.AddComponent<Image>();
        imgRelleno.color = colorInicial;
        imgRelleno.type = Image.Type.Filled;
        imgRelleno.fillMethod = Image.FillMethod.Horizontal;
        imgRelleno.fillOrigin = (int)Image.OriginHorizontal.Left;
        imgRelleno.fillAmount = 1f;

        GameObject textoGO = new GameObject(nombre + "_Texto", typeof(RectTransform));
        textoGO.transform.SetParent(fondoGO.transform, false);
        RectTransform rtTexto = textoGO.GetComponent<RectTransform>();
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = Vector2.one;
        rtTexto.offsetMin = Vector2.zero;
        rtTexto.offsetMax = Vector2.zero;
        Text txt = textoGO.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.fontSize = FuenteBarra;
        txt.text = "";

        relleno = imgRelleno;
        texto = txt;
    }
}
