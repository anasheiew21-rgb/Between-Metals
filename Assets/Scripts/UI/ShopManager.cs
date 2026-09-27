using UnityEngine;

// Panel de la tienda del comerciante: lista de items con Comprar/Vender, conectada al oro de
// PlayerStats y al Inventory real del jugador (RF06). Comprar agrega el ItemData de ItemComercio
// al inventario (falla sin cobrar si esta lleno); vender lo quita de ahi. El stock que le queda a
// la tienda (ItemComercio.cantidad) es lo unico que sigue llevando esta clase.
public class ShopManager : MonoBehaviour
{
    [Header("Items en venta")]
    [SerializeField] private ItemComercio[] items;

    [Header("Jugador (se bloquean mientras la tienda esta abierta)")]
    [SerializeField] private PlayerController controlador;
    [SerializeField] private MouseLook camaraJugador;
    [Tooltip("Si se deja vacio, busca un PlayerStats en el jugador al arrancar")]
    [SerializeField] private PlayerStats statsJugador;
    [Tooltip("Si se deja vacio, busca un Inventory en la escena al arrancar")]
    [SerializeField] private Inventory inventarioJugador;

    // Los demas scripts (Menu, PromptInteraccion) lo consultan para no superponerse con la tienda
    public static bool HayTiendaAbierta { get; private set; }

    private GUIStyle tituloStyle, filaStyle, precioStyle, botonStyle, oroStyle;
    private int oroMostrado;

    void Awake()
    {
        if (statsJugador == null) statsJugador = FindAnyObjectByType<PlayerStats>();
        if (inventarioJugador == null) inventarioJugador = FindAnyObjectByType<Inventory>();
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

        // Congela el juego (BUG-03): OnGUI sigue corriendo con Time.timeScale = 0, asi que los
        // clics del panel funcionan igual.
        Time.timeScale = 0f;

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

        Time.timeScale = 1f;

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
            if (item.item == null) continue; // entrada sin ItemData asignado: no hay nada que comerciar

            GUILayout.BeginHorizontal();

            Texture icono = item.item.icon != null ? item.item.icon.texture : null;
            GUILayout.Label(new GUIContent(item.item.itemName, icono), filaStyle, GUILayout.Width(180f));
            GUILayout.Label(item.precio + " oro", precioStyle, GUILayout.Width(70f));
            GUILayout.Label("x" + item.cantidad, precioStyle, GUILayout.Width(40f));

            bool puedeComprar = item.cantidad > 0
                && statsJugador != null && statsJugador.PuedePagar(item.precio)
                && inventarioJugador != null && !inventarioJugador.IsFull;
            GUI.enabled = puedeComprar;
            if (GUILayout.Button("Comprar", botonStyle)) Comprar(item);

            GUI.enabled = inventarioJugador != null && inventarioJugador.HasItem(item.item);
            if (GUILayout.Button("Vender", botonStyle)) Vender(item);
            GUI.enabled = true;

            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }
    }

    void Comprar(ItemComercio item)
    {
        if (item.item == null || item.cantidad <= 0) return;
        if (statsJugador == null || inventarioJugador == null || inventarioJugador.IsFull) return;
        if (!statsJugador.GastarOro(item.precio)) return;

        inventarioJugador.AddItem(item.item);
        item.cantidad--;
        Debug.Log("Comprado: " + item.item.itemName + " por " + item.precio + " oro. Quedan " + item.cantidad + " en la tienda.");

        ActualizarUI();
    }

    void Vender(ItemComercio item)
    {
        if (item.item == null || inventarioJugador == null) return;
        if (!inventarioJugador.RemoveItem(item.item)) return;

        item.cantidad++;
        statsJugador.AgregarOro(item.precio);
        Debug.Log("Vendido: " + item.item.itemName + ". La tienda ahora tiene " + item.cantidad + ".");

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
            alignment = TextAnchor.MiddleLeft,
            imagePosition = ImagePosition.ImageLeft
        };
        filaStyle.normal.textColor = Color.white;

        precioStyle = new GUIStyle(filaStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            imagePosition = ImagePosition.TextOnly
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
