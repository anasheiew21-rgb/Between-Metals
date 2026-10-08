using UnityEngine;
using UnityEngine.SceneManagement;

// Interfaz del inventario (RF06, HU-05, #16, wireframe P-05): grilla de lugares a la izquierda,
// panel de detalle del item seleccionado a la derecha (nombre, imagen y descripcion corta) y los
// botones USAR y CERRAR al pie, como pide la Etapa 11.
//
// La logica de que mostrar vive en InventoryPanelState; esta clase traduce teclas y clics a
// acciones, dibuja con los estilos de EstiloUI y, al abrir/cerrar el panel, congela el juego igual
// que ShopManager.AbrirTienda/CerrarTienda (Time.timeScale, cursor y controles de movimiento/mirada
// del jugador).
//
// Los mensajes breves ("Recogiste X", "Usaste X") ya no se dibujan aca: se derivan a AvisosUI, que
// es la pantalla de avisos de P-09, para que todo lo que es un aviso pasajero se vea igual venga
// del inventario, del comerciante o de donde sea.
public class InventoryUI : MonoBehaviour
{
    // Teclas del inventario, configurables con HU-02. Ninguna la usa otro script del equipo.
    const KeyCode ToggleKey = KeyCode.Tab;
    const KeyCode UseKey = KeyCode.R;
    static readonly KeyCode[] SlotKeys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
        KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0
    };

    const float SearchInterval = 1f;

    // Menu lo consulta para no pelearse por el cursor mientras el inventario esta abierto
    // (mismo patron que ShopManager.HayTiendaAbierta / GameOverUI.EstaMostrando).
    public static bool IsOpen { get; private set; }

    // --- Maquetado, en px del lienzo virtual de 1280x720 de EstiloUI ---
    const int Columnas = 5;
    const float AnchoPanel = 880f;
    const float AnchoDetalle = 300f;
    const float SeparacionColumnas = 20f;
    const float AltoCasilla = 72f;
    const float AltoCabecera = 34f;
    const float LadoImagen = 96f;
    const float LadoIconoTitulo = 24f;
    const float AltoDescripcion = 48f;   // dos lineas de cuerpo: la "descripcion corta" de P-05
    const float AltoNota = 24f;

    InventoryPanelState state;
    Inventory inventory;
    float nextSearchTime;

    // Encontrados bajo demanda al abrir/cerrar el panel (esta clase se autocrea sin referencias
    // de Inspector, a diferencia de ShopManager). Se resuelven cada vez por si la escena se
    // recargo y las instancias anteriores ya no existen.
    PlayerController controlador;
    MouseLook camaraJugador;

    // Textos cacheados: se rearman solo cuando cambia state.Version o la capacidad.
    GUIContent[] slotContents = new GUIContent[0];
    string countLabel = string.Empty;
    string nombreSeleccionado = string.Empty;
    string descripcionSeleccionada = string.Empty;
    Texture imagenSeleccionada;
    string footerLabel;
    int builtVersion = -1;
    int builtCapacity = -1;

    // Ultimo mensaje derivado a AvisosUI, para no reenviarlo en todos los frames que sigue vigente.
    string ultimoAviso = string.Empty;

    // Si la pausa, el cursor y el bloqueo de controles estan puestos ahora mismo. Es lo que
    // SincronizarEfectos compara contra el estado del panel; ver el comentario de ese metodo.
    bool efectosAplicados;

    // RuntimeInitializeOnLoadMethod corre una sola vez; Reiniciar recarga la escena sin volver a
    // dispararlo, asi que hace falta reaccionar a sceneLoaded para recrear el InventoryUI que se
    // destruyo junto con la escena anterior.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCrear()
    {
        EnsureExists();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureExists();
    }

    /// <summary>Crea un InventoryUI si la escena tiene un PlayerStats y todavía no tiene uno. No hace nada en otro caso.</summary>
    public static void EnsureExists()
    {
        if (FindAnyObjectByType<InventoryUI>() != null) return;
        if (FindAnyObjectByType<PlayerStats>() == null) return; // sin jugador (ej. el menu de inicio), no hace falta
        new GameObject("InventoryUI").AddComponent<InventoryUI>();
    }

    void Awake()
    {
        state = new InventoryPanelState(null, ToggleKey.ToString(), () => Time.time);
        footerLabel = "1-0 o clic: seleccionar    " + UseKey + ": usar    " + ToggleKey + ": cerrar";
    }

    void OnDestroy()
    {
        // Por si el objeto se destruye (ej. recarga de escena) con el panel todavia abierto: sin
        // esto, Time.timeScale y los controles del jugador quedarian pisados para siempre (Time.timeScale
        // no se resetea solo entre escenas, igual que ya maneja GameOverUI/Menu).
        if (efectosAplicados) CerrarInventario();
        state?.Dispose();
    }

    void Update()
    {
        ResolveInventory();

        state.SetBlocked(Menu.IsOpen || ShopManager.HayTiendaAbierta || GameOverUI.EstaMostrando);

        if (state.IsBlocked || inventory == null)
        {
            if (efectosAplicados) CerrarInventario(); // el bloqueo forzo el cierre: hay que restaurar
            return;
        }

        DerivarAvisos();

        if (Input.GetKeyDown(ToggleKey)) state.Toggle();

        SincronizarEfectos();

        if (!state.IsOpen) return;

        if (Input.GetKeyDown(UseKey)) state.UseSelected(Time.time);

        for (int i = 0; i < SlotKeys.Length; i++)
        {
            if (Input.GetKeyDown(SlotKeys[i])) state.SelectSlot(i);
        }

        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0f) state.Cycle(-1);      // rueda hacia arriba: anterior
        else if (scroll < 0f) state.Cycle(1);  // rueda hacia abajo: siguiente
    }

    // Lo que InventoryPanelState tiene para decir ("Recogiste X", "Usaste X") se publica en
    // AvisosUI una sola vez por mensaje. El estado no avisa cuando cambia, asi que se compara con
    // el ultimo publicado: mientras el texto siga siendo el mismo no se reenvia nada.
    void DerivarAvisos()
    {
        string mensaje = state.GetMessage(Time.time);

        if (mensaje.Length == 0)
        {
            ultimoAviso = string.Empty;
            return;
        }

        if (mensaje == ultimoAviso) return;

        ultimoAviso = mensaje;
        AvisosUI.Mostrar(mensaje);
    }

    /// <summary>
    /// Aplica o deshace los efectos de abrir el panel (pausa, cursor, controles) si todavia no
    /// coinciden con el estado del panel.
    ///
    /// Esto se compara contra 'efectosAplicados' y NO contra lo que valia IsOpen al principio del
    /// Update, que es lo que hacia antes: el boton CERRAR cambia el estado desde OnGUI, que corre
    /// DESPUES de Update, asi que al frame siguiente el panel ya figuraba cerrado desde el primer
    /// renglon y la comparacion no detectaba ningun cambio. Resultado: el juego se quedaba en pausa
    /// con Time.timeScale en 0 y sin ningun panel abierto.
    ///
    /// Llevando aparte que efectos estan aplicados, da igual desde donde se abra o se cierre el
    /// panel -la tecla en Update, un boton en OnGUI, o el bloqueo de otra pantalla-: la proxima
    /// pasada por aca lo acomoda.
    /// </summary>
    void SincronizarEfectos()
    {
        if (state.IsOpen == efectosAplicados) return;

        if (state.IsOpen) AbrirInventario();
        else CerrarInventario();
    }

    // Misma logica que ShopManager.AbrirTienda/CerrarTienda: congela el juego, libera el cursor
    // (para que la flecha del raton aparezca sobre el panel) y bloquea el movimiento/mirada
    // mientras el inventario esta abierto.
    void AbrirInventario()
    {
        efectosAplicados = true;
        IsOpen = true;
        Time.timeScale = 0f;

        ResolvePlayerControls();
        if (controlador != null) controlador.enabled = false;
        if (camaraJugador != null) camaraJugador.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void CerrarInventario()
    {
        efectosAplicados = false;
        IsOpen = false;
        Time.timeScale = 1f;

        ResolvePlayerControls();
        if (controlador != null) controlador.enabled = true;
        if (camaraJugador != null) camaraJugador.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void ResolvePlayerControls()
    {
        if (controlador == null) controlador = FindAnyObjectByType<PlayerController>();
        if (camaraJugador == null) camaraJugador = FindAnyObjectByType<MouseLook>();
    }

    // Busca el inventario, lo cachea y, si desaparece, lo vuelve a buscar como mucho una vez por segundo.
    void ResolveInventory()
    {
        if (inventory != null) return;

        if (state.Inventory is object) state.Bind(null); // el anterior fue destruido
        if (Time.unscaledTime < nextSearchTime) return;

        nextSearchTime = Time.unscaledTime + SearchInterval;
        inventory = FindAnyObjectByType<Inventory>();
        if (inventory != null) state.Bind(inventory);
    }

    // ---------------------------------------------------------------
    // Dibujo (P-05)
    // ---------------------------------------------------------------

    void OnGUI()
    {
        if (inventory == null || state.IsBlocked || !state.IsOpen) return;

        float w = EstiloUI.AbrirLienzo();
        EstiloUI.Velo(0.85f);

        RebuildLabelsIfNeeded();

        int capacity = builtCapacity;
        int columnas = Mathf.Min(Columnas, capacity);
        int filas = (capacity + columnas - 1) / columnas;

        float anchoGrilla = AnchoPanel - EstiloUI.Padding * 2f - AnchoDetalle - SeparacionColumnas;
        float ladoCasilla = (anchoGrilla - (columnas - 1) * EstiloUI.Separacion) / columnas;
        float altoGrilla = filas * AltoCasilla + (filas - 1) * EstiloUI.Separacion;

        // El detalle manda sobre la altura del panel: lleva imagen, descripcion y dos botones, y
        // con pocos items es mas alto que la grilla.
        float altoDetalle = AltoCabecera + LadoImagen + AltoDescripcion + EstiloUI.AltoBoton * 2f + EstiloUI.Separacion * 4f;
        float altoCuerpo = Mathf.Max(altoGrilla, altoDetalle);
        float altoPanel = EstiloUI.Padding * 2f + AltoCabecera + 10f + altoCuerpo + 10f + AltoNota;

        Rect area = EstiloUI.AreaSegura;
        var panel = new Rect(
            area.x + (area.width - AnchoPanel) * 0.5f,
            area.y + (area.height - altoPanel) * 0.5f,
            AnchoPanel, altoPanel);

        GUI.Box(panel, GUIContent.none, EstiloUI.Tarjeta);

        // Cabecera: titulo a la izquierda, contador a la derecha. El contador se pone rojo cuando
        // el inventario esta lleno: es el estado que al jugador le importa ver de lejos.
        var cabecera = new Rect(panel.x + EstiloUI.Padding, panel.y + EstiloUI.Padding, panel.width - EstiloUI.Padding * 2f, AltoCabecera);

        var icono = new Rect(cabecera.x, cabecera.y + (AltoCabecera - LadoIconoTitulo) * 0.5f, LadoIconoTitulo, LadoIconoTitulo);
        Color colorPrevio = GUI.color;
        GUI.color = EstiloUI.BlancoHumo;
        GUI.DrawTexture(icono, IconosUI.Inventario);
        GUI.color = colorPrevio;

        GUI.Label(new Rect(icono.xMax + 10f, cabecera.y, cabecera.width, AltoCabecera), "INVENTARIO", EstiloUI.Subtitulo);

        GUI.Label(cabecera, countLabel, inventory.IsFull ? EstiloUI.DatoDerechaAlerta : EstiloUI.DatoDerecha);

        float yCuerpo = cabecera.yMax + 10f;

        DibujarGrilla(new Rect(cabecera.x, yCuerpo, anchoGrilla, altoGrilla), capacity, columnas, ladoCasilla);
        DibujarDetalle(new Rect(cabecera.xMax - AnchoDetalle, yCuerpo, AnchoDetalle, altoDetalle));

        GUI.Label(new Rect(cabecera.x, yCuerpo + altoCuerpo + 10f, cabecera.width, AltoNota), footerLabel, EstiloUI.Nota);
    }

    // Grilla de lugares. Cada casilla es una tarjeta clickeable; la seleccionada lleva el borde
    // rojo de 2 px que el manual reserva para la tarjeta activa.
    void DibujarGrilla(Rect zona, int capacity, int columnas, float ladoCasilla)
    {
        for (int i = 0; i < capacity; i++)
        {
            int fila = i / columnas, columna = i % columnas;
            var casilla = new Rect(
                zona.x + columna * (ladoCasilla + EstiloUI.Separacion),
                zona.y + fila * (AltoCasilla + EstiloUI.Separacion),
                ladoCasilla, AltoCasilla);

            GUIStyle estilo = i == state.SelectedIndex ? EstiloUI.CasillaActiva : EstiloUI.Casilla;
            if (GUI.Button(casilla, slotContents[i], estilo)) state.SelectSlot(i);
        }
    }

    // Panel de detalle de P-05: nombre, imagen, descripcion corta y los botones USAR y CERRAR.
    void DibujarDetalle(Rect zona)
    {
        ItemData seleccionado = inventory.GetSelectedItem();

        // Las piezas se apilan con una 'y' corriente, en el mismo orden en que se suman en
        // altoDetalle (nombre, imagen, descripcion, USAR, CERRAR): si se agrega una, hay que
        // sumarla en los dos lados.
        float y = zona.y;

        GUI.Label(new Rect(zona.x, y, zona.width, AltoCabecera),
            seleccionado != null ? nombreSeleccionado : "SIN SELECCIÓN", EstiloUI.Subtitulo);
        y += AltoCabecera + EstiloUI.Separacion;

        // Marco de la imagen, centrado. Queda dibujado aunque el item no tenga icono todavia (hoy
        // ninguno de los ItemData del proyecto lo tiene): asi el panel no cambia de alto ni de
        // forma segun el item que este seleccionado.
        var marco = new Rect(zona.x + (zona.width - LadoImagen) * 0.5f, y, LadoImagen, LadoImagen);
        EstiloUI.Rellenar(marco, EstiloUI.Negro);
        EstiloUI.Marco(marco, EstiloUI.GrisMetal, EstiloUI.BordeTarjeta);
        if (imagenSeleccionada != null)
        {
            GUI.DrawTexture(new Rect(marco.x + 6f, marco.y + 6f, marco.width - 12f, marco.height - 12f),
                imagenSeleccionada, ScaleMode.ScaleToFit);
        }
        y = marco.yMax + EstiloUI.Separacion;

        // Descripcion corta: el alto da para las dos lineas de P-05 y lo que sobre se recorta.
        GUI.Label(new Rect(zona.x, y, zona.width, AltoDescripcion), descripcionSeleccionada, EstiloUI.Cuerpo);
        y += AltoDescripcion + EstiloUI.Separacion;

        // USAR es el boton primario (la accion esperada) y se apaga sin seleccion; CERRAR siempre
        // esta disponible, porque es la unica salida del panel con el mouse.
        GUI.enabled = seleccionado != null;
        if (GUI.Button(new Rect(zona.x, y, zona.width, EstiloUI.AltoBoton), "USAR", EstiloUI.BotonPrimario)) state.UseSelected(Time.time);
        GUI.enabled = true;
        y += EstiloUI.AltoBoton + EstiloUI.Separacion;

        if (GUI.Button(new Rect(zona.x, y, zona.width, EstiloUI.AltoBoton), "CERRAR", EstiloUI.BotonSecundario)) state.Toggle();
    }

    void RebuildLabelsIfNeeded()
    {
        int capacity = inventory.Capacity;
        if (builtVersion == state.Version && builtCapacity == capacity) return;

        builtVersion = state.Version;
        builtCapacity = capacity;

        if (slotContents.Length != capacity) slotContents = new GUIContent[capacity];

        var items = inventory.Items;
        for (int i = 0; i < capacity; i++)
        {
            string key = i < SlotKeys.Length ? ((i + 1) % 10).ToString() : string.Empty;
            if (i < items.Count && items[i] != null)
            {
                // IMGUI dibuja texturas, no sprites: se usa la textura del sprite (sin recorte de atlas).
                Texture icon = items[i].icon != null ? items[i].icon.texture : null;
                slotContents[i] = new GUIContent(state.DisplayName(items[i]), icon);
            }
            else
            {
                slotContents[i] = new GUIContent(key);
            }
        }

        countLabel = items.Count + "/" + capacity;

        ItemData selected = inventory.GetSelectedItem();
        nombreSeleccionado = selected != null ? state.DisplayName(selected).ToUpperInvariant() : string.Empty;
        descripcionSeleccionada = selected != null
            ? selected.description
            : "Elegí un ítem de la grilla para ver su descripción.";
        imagenSeleccionada = selected != null && selected.icon != null ? selected.icon.texture : null;
    }
}
