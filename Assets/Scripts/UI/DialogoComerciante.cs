using UnityEngine;

// Frases del comerciante: una tarjeta de dialogo abajo al centro con lo que el enano dice al
// saludarte, al venderte algo, al comprarte algo y al cerrar la tienda.
//
// No toca ShopManager. Las compras y las ventas se detectan por los eventos que Inventory ya
// expone (OnItemAdded/OnItemRemoved), filtrados por ShopManager.HayTiendaAbierta: si el item entra
// al inventario con la tienda abierta, fue una compra, y si sale, fue una venta. Un item levantado
// del piso del laberinto no dispara nada porque ahi la tienda esta cerrada.
//
// Vive en UI/ y no en Systems/ porque dibuja: en este proyecto todas las clases con OnGUI estan en
// UI/ (o Inventory/), igual que ShopManager, que tambien es un componente del comerciante.
//
// El reloj es Time.unscaledTime a proposito, como en AvisosUI: la tienda congela el juego con
// Time.timeScale = 0 y con el reloj escalado la frase no se iria nunca de la pantalla.
//
// Lo que NO cubre: "no te alcanza el oro" y "tenes el inventario lleno". ShopManager desactiva esos
// botones antes de que se puedan apretar (GUI.enabled = false), asi que no hay ninguna accion que
// escuchar; avisarlo desde aca pediria tocar ShopManager, y de eso ya se encarga su propio aviso.
public class DialogoComerciante : MonoBehaviour
{
    [Header("Quien habla")]
    [SerializeField] private string nombre = "Comerciante";

    [Header("Frases")]
    [Tooltip("Al abrirse la tienda")]
    [SerializeField] private string[] saludos =
    {
        "Otro que baja. Pasá, que acá abajo no muerde nada... todavía.",
        "No preguntes cómo llegué. Yo tampoco me acuerdo.",
        "Mirá tranquilo. El metal no se mueve si no lo tocás.",
        "Hace rato que no sube nadie. ¿Seguís entero?",
    };

    [Tooltip("Cuando el jugador compra algo")]
    [SerializeField] private string[] alComprar =
    {
        "Buena elección. A mí ya no me sirve.",
        "Llevátelo. Y no vuelvas a buscarlo si lo perdés.",
        "Que te dure más que al dueño anterior.",
    };

    [Tooltip("Cuando el jugador vende algo")]
    [SerializeField] private string[] alVender =
    {
        "Lo guardo. Alguien va a necesitarlo.",
        "Me sirve. No preguntes para qué.",
        "Oro por algo que ya no usás. Es un buen trato.",
    };

    [Tooltip("Al cerrarse la tienda")]
    [SerializeField] private string[] despedidas =
    {
        "Cerrá la puerta al salir. Por las dudas.",
        "Si los pasillos cambian, no fue culpa mía.",
        "Volvé cuando tengas oro. O cuando necesites compañía.",
        "Andá. Y no mires atrás mucho tiempo.",
    };

    [Header("Tiempos")]
    [Tooltip("Segundos que la frase queda en pantalla, fundido incluido")]
    [Min(0.5f)] [SerializeField] private float duracion = 4.5f;

    [Tooltip("Segundos de fundido al final")]
    [Min(0f)] [SerializeField] private float fundido = 0.5f;

    [Header("Gestos")]
    [Tooltip("Opcional. Si esta, el enano acompana la frase con un gesto. El saludo lo maneja " +
             "ComercianteGestos por su cuenta, asi que aca no se repite.")]
    [SerializeField] private ComercianteGestos gestos;

    [SerializeField] private string gestoCompra = "happy_hand_gesture";
    [SerializeField] private string gestoVenta = "acknowledging";
    [SerializeField] private string gestoDespedida = "dismissing_gesture";

    // --- Maquetado, en px del lienzo virtual de 1280x720 de EstiloUI ---
    // Abajo al centro, pegado al borde: es el unico hueco libre con la tienda abierta. El panel de
    // ShopManager ocupa del y=70 al y=650, el cartel de PromptInteraccion vive en Alto-140 (y se
    // oculta solo con la tienda abierta), AvisosUI esta arriba a la derecha y el HUD abajo a la
    // izquierda.
    const float AnchoCartel = 620f;
    const float MargenInferior = 8f;
    const float AltoNombre = 18f;

    string linea;
    float momentoFin;
    bool tiendaAbiertaElFrameAnterior;

    Inventory inventario;
    // Indice de la ultima frase usada de cada grupo, para no repetir dos veces seguida la misma.
    int ultimoSaludo = -1, ultimaCompra = -1, ultimaVenta = -1, ultimaDespedida = -1;

    void Start()
    {
        if (gestos == null) gestos = GetComponent<ComercianteGestos>();

        // Mismo criterio que ShopManager.Awake: se busca una sola vez en la escena.
        inventario = FindAnyObjectByType<Inventory>();
        if (inventario == null)
        {
            Debug.LogWarning("DialogoComerciante: no se encontro ningun Inventory en la escena; " +
                             "las frases de compra y venta no van a salir.", this);
            return;
        }

        inventario.OnItemAdded += AlAgregarItem;
        inventario.OnItemRemoved += AlQuitarItem;
    }

    void OnDestroy()
    {
        if (inventario == null) return;

        inventario.OnItemAdded -= AlAgregarItem;
        inventario.OnItemRemoved -= AlQuitarItem;
    }

    void Update()
    {
        bool abierta = ShopManager.HayTiendaAbierta;

        // Solo en el frame del cambio: si no, el saludo se repetiria todos los frames que el panel
        // siga abierto.
        if (abierta && !tiendaAbiertaElFrameAnterior)
        {
            // El gesto del saludo ya lo dispara ComercianteGestos al abrirse la tienda.
            Decir(saludos, ref ultimoSaludo, null);
        }
        else if (!abierta && tiendaAbiertaElFrameAnterior)
        {
            Decir(despedidas, ref ultimaDespedida, gestoDespedida);
        }

        tiendaAbiertaElFrameAnterior = abierta;

        if (linea != null && Time.unscaledTime >= momentoFin) linea = null;
    }

    // Un item que entra al inventario con la tienda abierta es una compra; con la tienda cerrada es
    // algo que el jugador levanto del piso y no tiene nada que ver con el comerciante.
    void AlAgregarItem(ItemData item)
    {
        if (!ShopManager.HayTiendaAbierta) return;
        Decir(alComprar, ref ultimaCompra, gestoCompra);
    }

    void AlQuitarItem(ItemData item)
    {
        if (!ShopManager.HayTiendaAbierta) return;
        Decir(alVender, ref ultimaVenta, gestoVenta);
    }

    /// <summary>
    /// Pone una frase del grupo en pantalla y, si hay gesto, se lo pide a ComercianteGestos. Elige
    /// al azar sin repetir la ultima del mismo grupo.
    /// </summary>
    void Decir(string[] frases, ref int ultima, string gesto)
    {
        if (frases == null || frases.Length == 0) return;

        int elegida;
        if (frases.Length == 1 || ultima < 0)
        {
            // Primera vez de este grupo: entran todas. Sortear sobre Length-1 aca dejaria la ultima
            // frase del array sin poder salir nunca como primera.
            elegida = Random.Range(0, frases.Length);
        }
        else
        {
            // Se sortea entre las OTRAS: asi no hace falta reintentar y nunca sale la misma dos
            // veces seguidas.
            elegida = Random.Range(0, frases.Length - 1);
            if (elegida >= ultima) elegida++;
        }

        ultima = elegida;
        linea = frases[elegida];
        momentoFin = Time.unscaledTime + duracion;

        if (gestos != null && !string.IsNullOrEmpty(gesto)) gestos.Reproducir(gesto);
    }

    void OnGUI()
    {
        if (string.IsNullOrEmpty(linea)) return;

        // Las pantallas modales que tapan todo: ahi la frase no tiene por que seguir visible. La
        // tienda NO esta en la lista a proposito, porque es justo cuando el enano habla.
        if (Menu.IsOpen || GameOverUI.EstaMostrando || VictoryUI.EstaMostrando) return;

        float w = EstiloUI.AbrirLienzo();

        float restante = momentoFin - Time.unscaledTime;
        float alfa = fundido > 0f ? Mathf.Clamp01(restante / fundido) : 1f;

        float anchoTexto = AnchoCartel - EstiloUI.Padding * 2f;
        float altoTexto = EstiloUI.Cuerpo.CalcHeight(new GUIContent(linea), anchoTexto);
        float altoCartel = EstiloUI.Padding * 2f + AltoNombre + 4f + altoTexto;

        var cartel = new Rect((w - AnchoCartel) * 0.5f,
                              EstiloUI.Alto - MargenInferior - altoCartel,
                              AnchoCartel, altoCartel);

        Color colorPrevio = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alfa);

        EstiloUI.Rellenar(cartel, EstiloUI.FondoTarjeta);
        EstiloUI.Marco(cartel, EstiloUI.GrisMetal, EstiloUI.BordeTarjeta);

        // El nombre en rojo sangre, el mismo acento que usa el resto del HUD para la tecla y las
        // alertas; asi se lee quien habla sin agregar un retrato ni otra fuente.
        Color contenidoPrevio = GUI.contentColor;
        GUI.contentColor = EstiloUI.RojoSangre;
        GUI.Label(new Rect(cartel.x + EstiloUI.Padding, cartel.y + EstiloUI.Padding * 0.5f,
                           anchoTexto, AltoNombre), nombre, EstiloUI.Nota);
        GUI.contentColor = contenidoPrevio;

        GUI.Label(new Rect(cartel.x + EstiloUI.Padding,
                           cartel.y + EstiloUI.Padding * 0.5f + AltoNombre + 4f,
                           anchoTexto, altoTexto), linea, EstiloUI.Cuerpo);

        GUI.color = colorPrevio;
    }
}
