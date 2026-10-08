using UnityEngine;
using UnityEngine.SceneManagement;

// Interfaz del inventario (RF06, HU-05, #16): panel con los lugares del inventario, selección,
// uso del ítem seleccionado y aviso breve al recoger algo. La logica de que mostrar vive en
// InventoryPanelState; esta clase traduce teclas a acciones, dibuja y, al abrir/cerrar el panel,
// congela el juego igual que ShopManager.AbrirTienda/CerrarTienda (Time.timeScale, cursor y
// controles de movimiento/mirada del jugador).
// Mismo estilo que Menu, GameOverUI y PromptInteraccion: IMGUI (OnGUI) sobre una pantalla
// virtual de 720 de alto, y se crea sola al cargar la escena.
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

    // Pantalla virtual (misma que Menu/GameOverUI/PromptInteraccion)
    const float VirtualHeight = 720f;
    const int Columns = 5;
    const float SlotWidth = 112f, SlotMaxHeight = 64f, Gap = 8f, Padding = 16f;
    const float PanelTop = 120f, PanelMaxBottom = 530f;
    const float HeaderHeight = 32f, DescriptionHeight = 60f, FooterHeight = 24f;
    // El cartel de PromptInteraccion ocupa y = 600..650; el aviso va justo arriba.
    const float MessageTop = 548f, MessageHeight = 40f;

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
    string descriptionLabel = string.Empty;
    string footerLabel;
    int builtVersion = -1;
    int builtCapacity = -1;

    GUIStyle titleStyle, slotStyle, textStyle, countStyle, messageStyle;

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
        if (FindAnyObjectByType<PlayerStats>() == null) return; // sin jugador (ej. un futuro menu de inicio), no hace falta
        new GameObject("InventoryUI").AddComponent<InventoryUI>();
    }

    void Awake()
    {
        state = new InventoryPanelState(null, ToggleKey.ToString(), () => Time.time);
        footerLabel = "1-0 / rueda: seleccionar    " + UseKey + ": usar    " + ToggleKey + ": cerrar";
    }

    void OnDestroy()
    {
        // Por si el objeto se destruye (ej. recarga de escena) con el panel todavia abierto: sin
        // esto, Time.timeScale y los controles del jugador quedarian pisados para siempre (Time.timeScale
        // no se resetea solo entre escenas, igual que ya maneja GameOverUI.Reintentar/Menu.Restart).
        if (state != null && state.IsOpen) CerrarInventario();
        state?.Dispose();
    }

    void Update()
    {
        ResolveInventory();

        bool estabaAbierto = state.IsOpen;

        state.SetBlocked(Menu.IsOpen || ShopManager.HayTiendaAbierta || GameOverUI.EstaMostrando);

        if (state.IsBlocked || inventory == null)
        {
            if (estabaAbierto) CerrarInventario(); // el bloqueo forzo el cierre: hay que restaurar
            return;
        }

        if (Input.GetKeyDown(ToggleKey)) state.Toggle();

        if (state.IsOpen != estabaAbierto)
        {
            if (state.IsOpen) AbrirInventario();
            else CerrarInventario();
        }

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

    // Misma logica que ShopManager.AbrirTienda/CerrarTienda: congela el juego, libera el cursor
    // (para que la flecha del raton aparezca sobre el panel) y bloquea el movimiento/mirada
    // mientras el inventario esta abierto.
    void AbrirInventario()
    {
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

    void OnGUI()
    {
        if (inventory == null || state.IsBlocked) return;

        string message = state.GetMessage(Time.time);
        if (message.Length == 0 && !state.IsOpen) return;

        ConstruirEstilos();

        float s = Screen.height / VirtualHeight;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;

        if (message.Length > 0)
        {
            GUI.Label(new Rect(0f, MessageTop, w, MessageHeight), message, messageStyle);
        }

        if (state.IsOpen) DrawPanel(w);
    }

    void DrawPanel(float screenWidth)
    {
        RebuildLabelsIfNeeded();

        int capacity = builtCapacity;
        int columns = Mathf.Min(Columns, capacity);
        int rows = (capacity + columns - 1) / columns;

        // Si la capacidad crece, los lugares se achican para no invadir el aviso ni el cartel.
        float fixedHeight = Padding * 2f + HeaderHeight + DescriptionHeight + FooterHeight;
        float rowSpace = PanelMaxBottom - PanelTop - fixedHeight;
        float slotHeight = Mathf.Min(SlotMaxHeight, rowSpace / rows - Gap);

        float panelWidth = columns * SlotWidth + (columns - 1) * Gap + Padding * 2f;
        float panelHeight = fixedHeight + rows * (slotHeight + Gap);
        float x0 = (screenWidth - panelWidth) / 2f;

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(x0, PanelTop, panelWidth, panelHeight), Texture2D.whiteTexture);
        GUI.color = previousColor;

        float y = PanelTop + Padding;
        GUI.Label(new Rect(x0 + Padding, y, panelWidth - Padding * 2f, HeaderHeight), "Inventario", titleStyle);
        GUI.Label(new Rect(x0 + Padding, y, panelWidth - Padding * 2f, HeaderHeight), countLabel, countStyle);
        y += HeaderHeight;

        Color previousBackground = GUI.backgroundColor;
        for (int i = 0; i < capacity; i++)
        {
            int row = i / columns, col = i % columns;
            Rect slot = new Rect(
                x0 + Padding + col * (SlotWidth + Gap),
                y + row * (slotHeight + Gap),
                SlotWidth, slotHeight);

            GUI.backgroundColor = i == state.SelectedIndex ? new Color(1f, 0.8f, 0.2f) : previousBackground;
            if (SonidosUI.Boton(slot, slotContents[i], slotStyle)) state.SelectSlot(i); // clic izquierdo: selecciona, igual que las teclas 1-0
        }
        GUI.backgroundColor = previousBackground;
        y += rows * (slotHeight + Gap);

        GUI.Label(new Rect(x0 + Padding, y, panelWidth - Padding * 2f, DescriptionHeight), descriptionLabel, textStyle);
        y += DescriptionHeight;
        GUI.Label(new Rect(x0 + Padding, y, panelWidth - Padding * 2f, FooterHeight), footerLabel, textStyle);
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
                slotContents[i] = new GUIContent(key + "  " + state.DisplayName(items[i]), icon);
            }
            else
            {
                slotContents[i] = new GUIContent(key + "  vacío");
            }
        }

        countLabel = items.Count + "/" + capacity;

        ItemData selected = inventory.GetSelectedItem();
        descriptionLabel = selected != null
            ? state.DisplayName(selected) + ": " + selected.description
            : "Ningún ítem seleccionado";
    }

    void ConstruirEstilos()
    {
        if (titleStyle != null) return;

        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        titleStyle.normal.textColor = Color.white;

        textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft, wordWrap = true, clipping = TextClipping.Clip };
        textStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

        countStyle = new GUIStyle(textStyle) { fontSize = 18, alignment = TextAnchor.MiddleRight };

        slotStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter,
            imagePosition = ImagePosition.ImageLeft,
            wordWrap = true,
            clipping = TextClipping.Clip
        };
        slotStyle.normal.textColor = Color.white;

        messageStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        messageStyle.normal.textColor = new Color(1f, 0.9f, 0.6f);
    }
}
