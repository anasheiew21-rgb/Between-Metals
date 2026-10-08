using UnityEngine;

// Panel del comerciante (RF06, wireframe P-04): tabla de trueques fijos con las columnas DAS y
// RECIBÍS, un boton INTERCAMBIAR por fila y SALIR al pie.
//
// Como se mapean los trueques sobre la economia que ya existe: cada ItemComercio sigue siendo un
// articulo con precio y stock, y se muestra como DOS trueques, uno por sentido.
//
//     DAS 30 oro          RECIBÍS Poción de vida     [INTERCAMBIAR]   <- comprar
//     DAS Poción de vida  RECIBÍS 30 oro             [INTERCAMBIAR]   <- vender
//
// Asi la pantalla habla el idioma del wireframe ("das / recibís") sin perder ninguna de las dos
// operaciones que el juego ya tenia, y sin tocar la mecanica: comprar agrega el ItemData al
// inventario y cobra el oro, vender hace lo inverso. El stock que le queda a la tienda
// (ItemComercio.cantidad) es lo unico que sigue llevando esta clase.
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

    // --- Maquetado, en px del lienzo virtual de 1280x720 de EstiloUI ---
    const float AnchoPanel = 760f;
    const float AltoCabecera = 34f;
    const float LadoIconoTitulo = 24f;
    const float AnchoBoton = 180f;
    const float SeparacionBoton = 10f;

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
    // al abrir la tienda y despues de cada intercambio para dejar la intencion explicita.
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

    // ---------------------------------------------------------------
    // Dibujo (P-04)
    // ---------------------------------------------------------------

    void OnGUI()
    {
        if (!HayTiendaAbierta) return;

        float w = EstiloUI.AbrirLienzo();
        EstiloUI.Velo(0.85f);

        Rect area = EstiloUI.AreaSegura;

        int totalFilas = ContarTrueques();

        // Cuanto se puede dibujar sin salirse del area segura: con la cabecera, la tabla, el pie y
        // los dos rellenos, el panel no puede pasar de 'altoDisponible'. Si la tienda tuviera mas
        // articulos de los que entran, se muestran los que caben y una nota con los que faltan, en
        // vez de dibujar un panel mas alto que la pantalla.
        float altoFijo = EstiloUI.Padding * 2f + AltoCabecera + 10f + EstiloUI.AltoFila + 10f + EstiloUI.AltoBoton;
        float altoDisponible = area.height - 80f;
        int maxFilas = Mathf.Max(1, Mathf.FloorToInt((altoDisponible - altoFijo) / EstiloUI.AltoFila));
        int filas = Mathf.Min(totalFilas, maxFilas);
        bool hayOcultas = filas < totalFilas;

        float altoPanel = altoFijo + filas * EstiloUI.AltoFila + (hayOcultas ? EstiloUI.AltoFila : 0f);
        var panel = new Rect(
            area.x + (area.width - AnchoPanel) * 0.5f,
            area.y + (area.height - altoPanel) * 0.5f,
            AnchoPanel, altoPanel);

        GUI.Box(panel, GUIContent.none, EstiloUI.Tarjeta);

        // Cabecera: titulo a la izquierda y el oro del jugador a la derecha. El oro va en blanco
        // humo y no en el amarillo de antes: la paleta de la Etapa 12 tiene cuatro colores y el
        // amarillo no es uno.
        var cabecera = new Rect(panel.x + EstiloUI.Padding, panel.y + EstiloUI.Padding,
            panel.width - EstiloUI.Padding * 2f, AltoCabecera);

        var icono = new Rect(cabecera.x, cabecera.y + (AltoCabecera - LadoIconoTitulo) * 0.5f, LadoIconoTitulo, LadoIconoTitulo);
        Color colorPrevio = GUI.color;
        GUI.color = EstiloUI.BlancoHumo;
        GUI.DrawTexture(icono, IconosUI.Trueque);
        GUI.color = colorPrevio;

        GUI.Label(new Rect(icono.xMax + 10f, cabecera.y, cabecera.width, AltoCabecera), "COMERCIANTE", EstiloUI.Subtitulo);
        GUI.Label(cabecera, oroMostrado + " ORO", EstiloUI.DatoDerecha);

        float y = cabecera.yMax + 10f;
        float anchoColumna = (cabecera.width - AnchoBoton - SeparacionBoton) * 0.5f;

        // Header rojo de la tabla.
        var header = new Rect(cabecera.x, y, cabecera.width, EstiloUI.AltoFila);
        GUI.Label(header, "DAS", EstiloUI.HeaderTabla);
        GUI.Label(new Rect(header.x + anchoColumna, header.y, anchoColumna, header.height), "RECIBÍS", EstiloUI.HeaderTabla);
        y += EstiloUI.AltoFila;

        if (totalFilas == 0)
        {
            var vacia = new Rect(cabecera.x, y, cabecera.width, EstiloUI.AltoFila);
            EstiloUI.FilaTabla(vacia, 0);
            GUI.Label(Celda(vacia), "No tiene nada para intercambiar por ahora.", EstiloUI.Nota);
        }
        else
        {
            y = DibujarTrueques(cabecera, y, anchoColumna, filas);

            if (hayOcultas)
            {
                var nota = new Rect(cabecera.x, y, cabecera.width, EstiloUI.AltoFila);
                EstiloUI.FilaTabla(nota, filas);
                GUI.Label(Celda(nota), "y " + (totalFilas - filas) + " trueque(s) más…", EstiloUI.Nota);
            }
        }

        var salir = new Rect(cabecera.x, panel.yMax - EstiloUI.Padding - EstiloUI.AltoBoton,
            cabecera.width, EstiloUI.AltoBoton);
        if (GUI.Button(salir, "SALIR", EstiloUI.BotonPrimario)) CerrarTienda();
    }

    // Dos filas por articulo: comprar y vender. Devuelve la 'y' siguiente a la ultima fila dibujada.
    float DibujarTrueques(Rect cabecera, float y, float anchoColumna, int maxFilas)
    {
        int indice = 0;

        foreach (ItemComercio item in items)
        {
            if (item.item == null) continue; // entrada sin ItemData asignado: no hay nada que comerciar

            string nombre = item.item.itemName;
            Texture icono = item.item.icon != null ? item.item.icon.texture : null;

            // Comprar: da oro y recibe el item. Necesita stock, oro y lugar en el inventario.
            if (indice < maxFilas)
            {
                bool puede = item.cantidad > 0
                    && statsJugador != null && statsJugador.PuedePagar(item.precio)
                    && inventarioJugador != null && !inventarioJugador.IsFull;

                DibujarFila(cabecera, y, indice, anchoColumna,
                    new GUIContent(item.precio + " oro"),
                    new GUIContent(nombre + (item.cantidad > 0 ? "  (x" + item.cantidad + ")" : "  (sin stock)"), icono),
                    puede, () => Comprar(item));

                y += EstiloUI.AltoFila;
                indice++;
            }

            // Vender: da el item y recibe oro. Necesita tenerlo en el inventario.
            if (indice < maxFilas)
            {
                bool puede = inventarioJugador != null && inventarioJugador.HasItem(item.item);

                DibujarFila(cabecera, y, indice, anchoColumna,
                    new GUIContent(nombre, icono),
                    new GUIContent(item.precio + " oro"),
                    puede, () => Vender(item));

                y += EstiloUI.AltoFila;
                indice++;
            }

            if (indice >= maxFilas) break;
        }

        return y;
    }

    void DibujarFila(Rect cabecera, float y, int indice, float anchoColumna, GUIContent das, GUIContent recibis, bool habilitado, System.Action intercambiar)
    {
        var fila = new Rect(cabecera.x, y, cabecera.width, EstiloUI.AltoFila);
        EstiloUI.FilaTabla(fila, indice);

        GUI.Label(new Rect(fila.x + 10f, fila.y, anchoColumna - 10f, fila.height), das, EstiloUI.Cuerpo);
        GUI.Label(new Rect(fila.x + anchoColumna + 10f, fila.y, anchoColumna - 10f, fila.height), recibis, EstiloUI.Cuerpo);

        var boton = new Rect(fila.xMax - AnchoBoton, fila.y + 3f, AnchoBoton, EstiloUI.AltoFila - 6f);
        GUI.enabled = habilitado;
        if (GUI.Button(boton, "INTERCAMBIAR", EstiloUI.BotonFila)) intercambiar();
        GUI.enabled = true;
    }

    static Rect Celda(Rect fila) => new Rect(fila.x + 10f, fila.y, fila.width - 20f, fila.height);

    int ContarTrueques()
    {
        if (items == null) return 0;

        int total = 0;
        foreach (ItemComercio item in items)
        {
            if (item.item != null) total += 2; // comprar y vender
        }
        return total;
    }

    // ---------------------------------------------------------------
    // Operaciones
    // ---------------------------------------------------------------

    void Comprar(ItemComercio item)
    {
        if (item.item == null || item.cantidad <= 0) return;
        if (statsJugador == null || inventarioJugador == null) return;

        if (inventarioJugador.IsFull)
        {
            AvisosUI.Alertar("Inventario lleno");
            return;
        }

        if (!statsJugador.GastarOro(item.precio))
        {
            AvisosUI.Alertar("Oro insuficiente");
            return;
        }

        inventarioJugador.AddItem(item.item);
        item.cantidad--;
        Debug.Log("Comprado: " + item.item.itemName + " por " + item.precio + " oro. Quedan " + item.cantidad + " en la tienda.");

        AvisosUI.Mostrar("Intercambio realizado");
        ActualizarUI();
    }

    void Vender(ItemComercio item)
    {
        if (item.item == null || inventarioJugador == null) return;
        if (!inventarioJugador.RemoveItem(item.item)) return;

        item.cantidad++;
        statsJugador.AgregarOro(item.precio);
        Debug.Log("Vendido: " + item.item.itemName + ". La tienda ahora tiene " + item.cantidad + ".");

        AvisosUI.Mostrar("Intercambio realizado");
        ActualizarUI();
    }
}
