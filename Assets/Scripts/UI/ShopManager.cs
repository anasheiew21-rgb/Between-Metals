using UnityEngine;

// Panel de la tienda del comerciante: lista de items con Comprar/Vender.
// Todavia no hay un sistema de oro/inventario del jugador en el proyecto, asi que Comprar/Vender
// por ahora solo mueven la cantidad del comerciante y lo dejan anotado en la consola; el enganche
// real (restar oro, agregar al inventario del jugador) queda listo para cuando eso exista.
public class ShopManager : MonoBehaviour
{
    [Header("Items en venta")]
    [SerializeField] private ItemComercio[] items;

    [Header("Jugador (se bloquean mientras la tienda esta abierta)")]
    [SerializeField] private PlayerController controlador;
    [SerializeField] private MouseLook camaraJugador;

    // Los demas scripts (Menu, PromptInteraccion) lo consultan para no superponerse con la tienda
    public static bool HayTiendaAbierta { get; private set; }

    private GUIStyle tituloStyle, filaStyle, precioStyle, botonStyle;

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

            GUILayout.Label(item.nombre, filaStyle, GUILayout.Width(220f));
            GUILayout.Label(item.precio + " oro", precioStyle, GUILayout.Width(80f));
            GUILayout.Label("x" + item.cantidad, precioStyle, GUILayout.Width(50f));

            GUI.enabled = item.cantidad > 0;
            if (GUILayout.Button("Comprar", botonStyle)) Comprar(item);
            GUI.enabled = true;

            if (GUILayout.Button("Vender", botonStyle)) Vender(item);

            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }
    }

    // TODO: cuando exista un inventario/oro del jugador, restar el precio y agregarle el item
    // en vez de solo descontarlo del stock del comerciante.
    void Comprar(ItemComercio item)
    {
        if (item.cantidad <= 0) return;

        item.cantidad--;
        Debug.Log("Comprado: " + item.nombre + " por " + item.precio + " oro. Quedan " + item.cantidad + " en la tienda.");
    }

    // TODO: cuando exista un inventario del jugador, sacarle el item y darle el oro en vez de
    // solo sumarlo al stock del comerciante.
    void Vender(ItemComercio item)
    {
        item.cantidad++;
        Debug.Log("Vendido: " + item.nombre + ". La tienda ahora tiene " + item.cantidad + ".");
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
    }
}
