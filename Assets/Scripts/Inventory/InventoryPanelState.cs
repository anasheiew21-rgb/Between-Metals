using System;

// Lógica de la interfaz del inventario (RF06, HU-05, #16), separada del dibujo para poder
// probarla sin escena: no es MonoBehaviour y no lee input ni Time. InventoryUI le pasa las
// teclas ya traducidas a acciones y el tiempo actual.
public class InventoryPanelState
{
    /// <summary>Segundos que dura visible un mensaje breve.</summary>
    public const float MessageDuration = 2f;

    private readonly string inventoryKeyLabel;
    private readonly Func<float> clock;

    private Inventory inventory;
    private int selectedIndex = -1;
    private string message = string.Empty;
    private float messageExpiresAt;

    /// <summary>Verdadero si el panel está abierto.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Verdadero mientras otro menú (pausa, tienda, Game Over) bloquea el panel.</summary>
    public bool IsBlocked { get; private set; }

    /// <summary>Verdadero si ya se mostró la pista de la tecla del inventario.</summary>
    public bool HintShown { get; private set; }

    /// <summary>Índice seleccionado según los eventos del inventario, o -1.</summary>
    public int SelectedIndex => selectedIndex;

    /// <summary>Inventario conectado, o null.</summary>
    public Inventory Inventory => inventory;

    /// <summary>Aumenta cada vez que cambia el contenido o la selección; la UI rearma sus textos solo cuando cambia.</summary>
    public int Version { get; private set; }

    /// <param name="inventory">Inventario a mostrar (puede ser null y conectarse después con Bind).</param>
    /// <param name="inventoryKeyLabel">Nombre de la tecla que abre el inventario, para la pista del primer aviso.</param>
    /// <param name="clock">Reloj que se usa solo para los avisos disparados por el evento OnItemAdded.</param>
    public InventoryPanelState(Inventory inventory, string inventoryKeyLabel, Func<float> clock)
    {
        this.inventoryKeyLabel = inventoryKeyLabel;
        this.clock = clock;
        Bind(inventory);
    }

    /// <summary>Conecta otro inventario (o null), desuscribiendo el anterior.</summary>
    public void Bind(Inventory newInventory)
    {
        Unbind();

        inventory = newInventory;
        Version++;
        if (inventory == null) return;

        // El inventario no expone el índice: se toma la primera aparición del ítem seleccionado.
        ItemData selected = inventory.GetSelectedItem();
        selectedIndex = -1;
        if (selected != null)
        {
            for (int i = 0; i < inventory.Items.Count; i++)
            {
                if (inventory.Items[i] == selected) { selectedIndex = i; break; }
            }
        }

        inventory.OnItemAdded += HandleItemAdded;
        inventory.OnSelectionChanged += HandleSelectionChanged;
        inventory.OnInventoryChanged += HandleInventoryChanged;
    }

    /// <summary>Desuscribe los eventos del inventario y lo desconecta.</summary>
    public void Unbind()
    {
        if (inventory != null)
        {
            inventory.OnItemAdded -= HandleItemAdded;
            inventory.OnSelectionChanged -= HandleSelectionChanged;
            inventory.OnInventoryChanged -= HandleInventoryChanged;
        }
        inventory = null;
        selectedIndex = -1;
        IsOpen = false;
        Version++;
    }

    /// <summary>Igual que Unbind.</summary>
    public void Dispose() => Unbind();

    /// <summary>Bloquea o desbloquea el panel. Al bloquearse se cierra.</summary>
    public void SetBlocked(bool blocked)
    {
        IsBlocked = blocked;
        if (blocked) IsOpen = false;
    }

    /// <summary>Abre o cierra el panel. Devuelve el nuevo IsOpen, o false si está bloqueado.</summary>
    public bool Toggle()
    {
        if (IsBlocked) return false;

        IsOpen = !IsOpen;
        return IsOpen;
    }

    /// <summary>Selecciona el lugar indicado (0 = primero). False si el panel está cerrado/bloqueado o el lugar está vacío.</summary>
    public bool SelectSlot(int slot)
    {
        if (!CanAct() || slot < 0 || slot >= inventory.Count) return false;
        return inventory.SelectItem(slot);
    }

    /// <summary>
    /// Mueve la selección delta lugares entre los ítems existentes, dando la vuelta.
    /// Sin selección, delta positivo va al primero y negativo al último.
    /// </summary>
    public bool Cycle(int delta)
    {
        if (!CanAct() || delta == 0) return false;

        int count = inventory.Count;
        if (count == 0) return false;

        int target;
        if (selectedIndex < 0 || selectedIndex >= count) target = delta > 0 ? 0 : count - 1;
        else target = ((selectedIndex + delta) % count + count) % count;

        return inventory.SelectItem(target);
    }

    /// <summary>Usa el ítem seleccionado. Si se pudo, muestra "Usaste nombre".</summary>
    public bool UseSelected(float now)
    {
        if (!CanAct()) return false;

        ItemData item = inventory.GetSelectedItem();
        if (item == null) return false;

        string itemName = DisplayName(item);
        if (!inventory.UseSelected()) return false;

        SetMessage("Usaste " + itemName, now);
        return true;
    }

    /// <summary>Muestra "Recogiste nombre". La primera vez agrega la pista de la tecla del inventario.</summary>
    public void NotifyItemAdded(ItemData item, float now)
    {
        if (item == null) return;

        string text = "Recogiste " + DisplayName(item);
        if (!HintShown)
        {
            text += " — " + inventoryKeyLabel + ": inventario";
            HintShown = true;
        }
        SetMessage(text, now);
    }

    /// <summary>Mensaje breve vigente, o cadena vacía si expiró.</summary>
    public string GetMessage(float now) => now < messageExpiresAt ? message : string.Empty;

    /// <summary>Nombre visible del ítem: itemName, o el nombre del asset si está vacío.</summary>
    public string DisplayName(ItemData item)
    {
        if (item == null) return string.Empty;
        return string.IsNullOrWhiteSpace(item.itemName) ? item.name : item.itemName;
    }

    private bool CanAct() => !IsBlocked && IsOpen && inventory != null;

    private void SetMessage(string text, float now)
    {
        message = text;
        messageExpiresAt = now + MessageDuration;
    }

    private void HandleItemAdded(ItemData item) => NotifyItemAdded(item, clock != null ? clock() : 0f);

    private void HandleSelectionChanged(int index)
    {
        selectedIndex = index;
        Version++;
    }

    private void HandleInventoryChanged() => Version++;
}
