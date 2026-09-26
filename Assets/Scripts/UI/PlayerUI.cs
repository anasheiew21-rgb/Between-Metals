using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Barras de vida y estamina en pantalla, conectadas a PlayerStats por eventos (no sondea en
// Update, solo suaviza visualmente el valor que ya llego). Arma su propio Canvas por codigo al
// arrancar: es lo unico del proyecto que usa uGUI en vez de OnGUI, porque Image.FillMethod pide
// un Canvas real. No hace falta tocar la escena para usarlo.
public class PlayerUI : MonoBehaviour
{
    [Header("Jugador")]
    [Tooltip("Si se deja vacio, se busca el primer PlayerStats de la escena al arrancar")]
    [SerializeField] private PlayerStats stats;

    [Header("Colores")]
    [SerializeField] private Color colorVida = new Color(0.8f, 0.15f, 0.15f);
    [SerializeField] private Color colorVidaBaja = new Color(1f, 0.9f, 0.1f);
    [SerializeField] private Color colorEstamina = new Color(0.15f, 0.6f, 0.85f);
    [SerializeField] private Color colorEstaminaAgotada = new Color(0.5f, 0.5f, 0.5f);
    [Range(0f, 1f)] [SerializeField] private float umbralVidaBaja = 0.3f;
    [SerializeField] private float velocidadSuavizado = 8f;

    Image rellenoVida, rellenoEstamina;
    Text textoVida, textoEstamina;
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

        ConstruirCanvas();

        if (stats != null)
        {
            stats.AlCambiarVida += ActualizarVida;
            stats.AlCambiarEstamina += ActualizarEstamina;
            ActualizarVida(stats.CurrentHealth, stats.MaxHealth);
            ActualizarEstamina(stats.CurrentStamina, stats.MaxStamina);
        }
        else
        {
            Debug.LogWarning("PlayerUI: no se encontro ningun PlayerStats en la escena.");
        }
    }

    void OnDestroy()
    {
        if (stats == null) return;
        stats.AlCambiarVida -= ActualizarVida;
        stats.AlCambiarEstamina -= ActualizarEstamina;
    }

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

    // ---------------------------------------------------------------
    // Construccion del Canvas por codigo
    // ---------------------------------------------------------------
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

        CrearBarra(canvas.transform, "BarraVida", new Vector2(30f, -30f), colorVida, out rellenoVida, out textoVida);
        CrearBarra(canvas.transform, "BarraEstamina", new Vector2(30f, -70f), colorEstamina, out rellenoEstamina, out textoEstamina);
    }

    // Fondo oscuro + relleno tipo Image.FillMethod.Horizontal + texto numerico encima
    static void CrearBarra(Transform padre, string nombre, Vector2 posicionEsquina, Color colorInicial, out Image relleno, out Text texto)
    {
        const float ancho = 260f, alto = 28f;

        GameObject fondoGO = new GameObject(nombre + "_Fondo", typeof(RectTransform));
        fondoGO.transform.SetParent(padre, false);
        RectTransform rtFondo = fondoGO.GetComponent<RectTransform>();
        rtFondo.anchorMin = new Vector2(0f, 1f);
        rtFondo.anchorMax = new Vector2(0f, 1f);
        rtFondo.pivot = new Vector2(0f, 1f);
        rtFondo.anchoredPosition = posicionEsquina;
        rtFondo.sizeDelta = new Vector2(ancho, alto);
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
        txt.fontSize = 16;
        txt.text = "";

        relleno = imgRelleno;
        texto = txt;
    }
}
