using UnityEngine;

// Panel de la tienda del comerciante: lista de items con Comprar/Vender, conectada al oro de
// PlayerStats. La cantidad que el jugador posee de cada item se lleva en el propio
// ItemComercio.cantidadJugador (todavia no hay un inventario separado del jugador).
public class ShopManager : MonoBehaviour
{
    [Header("Items en venta")]
    [SerializeField] private ItemComercio[] items;

    [Header("Jugador (se bloquean mientras la tienda esta abierta)")]
    [SerializeField] private PlayerController controlador;
    [SerializeField] private MouseLook camaraJugador;
    [Tooltip("Si se deja vacio, busca un PlayerStats en el jugador al arrancar")]
    [SerializeField] private PlayerStats statsJugador;

    // Los demas scripts (Menu, PromptInteraccion) lo consultan para no superponerse con la tienda
    public static bool HayTiendaAbierta { get; private set; }

    private GUIStyle tituloStyle, filaStyle, precioStyle, botonStyle, oroStyle;
    private int oroMostrado;

    void Awake()
    {
        if (statsJugador == null) statsJugador = FindAnyObjectByType<PlayerStats>();
    }

    void Update()
    {
        // Esc tambien cierra la tienda, igual que hace con el menu de pausa
        if (HayTiendaAbierta && Input.GetKeyDown(KeyCode.Escape))
        {
            CerrarTienda();
        }
    }

    void OnDisable()
    {
        if (HayTiendaAbierta) CerrarTienda();
    }

    public void AlternarTienda()
    {
        if (HayTiendaAbierta) CerrarTienda();
        else AbrirTienda();
    }

    public void AbrirTienda()
    {
        HayTiendaAbierta = true;

        if (controlador != null) controlador.enabled = false;
        if (camaraJugador != null) camaraJugador.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        PromptInteraccion.Instancia?.Ocultar();

        ActualizarUI();
    }

    // Refresca el texto de oro mostrado en el panel. En OnGUI cada fila ya vuelve a leer el
    // estado actual todos los frames, asi que lo unico que hace falta cachear es el oro; se llama
    // al abrir la tienda y despues de cada Comprar/Vender para dejar la intencion explicita.
    void ActualizarUI()
    {
        oroMostrado = statsJugador != null ? statsJugador.Oro : 0;
    }

    public void CerrarTienda()
    {
        HayTiendaAbierta = false;

        if (controlador != null) controlador.enabled = true;
        if (camaraJugador != null) camaraJugador.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnGUI()
    {
        if (!HayTiendaAbierta) return;

        ConstruirEstilos();

        // Misma pantalla virtual de 720 de alto que usa Menu, para que se vea consistente
        float s = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;

        Color colorPrevio = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(0, 0, w, 720f), Texture2D.whiteTexture);
        GUI.color = colorPrevio;

        GUILayout.BeginArea(new Rect((w - 520f) / 2f, 70f, 520f, 580f));
        GUILayout.Label("Comerciante", tituloStyle);
        GUILayout.Label("Oro: " + oroMostrado, oroStyle);
        GUILayout.Space(20f);

        DibujarItems();

        GUILayout.Space(20f);
        if (GUILayout.Button("Cerrar", botonStyle, GUILayout.Height(44f))) CerrarTienda();
        GUILayout.EndArea();
    }

    void DibujarItems()
    {
        if (items == null || items.Length == 0)
        {
            GUILayout.Label("No tiene nada para vender por ahora.", filaStyle);
            return;
        }

        foreach (ItemComercio item in items)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label(item.nombre, filaStyle, GUILayout.Width(180f));
            GUILayout.Label(item.precio + " oro", precioStyle, GUILayout.Width(70f));
            GUILayout.Label("x" + item.cantidad, precioStyle, GUILayout.Width(40f));

            bool puedeComprar = item.cantidad > 0 && statsJugador != null && statsJugador.PuedePagar(item.precio);
            GUI.enabled = puedeComprar;
            if (GUILayout.Button("Comprar", botonStyle)) Comprar(item);

            GUI.enabled = item.cantidadJugador > 0;
            if (GUILayout.Button("Vender", botonStyle)) Vender(item);
            GUI.enabled = true;

            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }
    }

    void Comprar(ItemComercio item)
    {
        if (item.cantidad <= 0 || statsJugador == null) return;
        if (!statsJugador.GastarOro(item.precio)) return;

        item.cantidad--;
        item.cantidadJugador++;
        Debug.Log("Comprado: " + item.nombre + " por " + item.precio + " oro. Quedan " + item.cantidad + " en la tienda.");

        ActualizarUI();
    }

    void Vender(ItemComercio item)
    {
        if (item.cantidadJugador <= 0 || statsJugador == null) return;

        item.cantidadJugador--;
        item.cantidad++;
        statsJugador.AgregarOro(item.precio);
        Debug.Log("Vendido: " + item.nombre + ". La tienda ahora tiene " + item.cantidad + ".");

        ActualizarUI();
    }

    void ConstruirEstilos()
    {
        if (tituloStyle != null) return;

        tituloStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        tituloStyle.normal.textColor = Color.white;

        filaStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleLeft
        };
        filaStyle.normal.textColor = Color.white;

        precioStyle = new GUIStyle(filaStyle)
        {
            alignment = TextAnchor.MiddleCenter
        };
        precioStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);

        botonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            fixedHeight = 32f,
            fixedWidth = 90f
        };

        oroStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        oroStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);
    }
}
