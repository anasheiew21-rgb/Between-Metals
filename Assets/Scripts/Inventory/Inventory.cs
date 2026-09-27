using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

// Núcleo del inventario (RF06/HU-05). Sin UI todavía: expone eventos para que una futura
// pantalla de inventario, o los sistemas de recolección/puzzle/comerciante (RF07/RF08/RF09),
// se conecten sin tener que modificar esta clase.
//
// Los eventos se disparan en orden y sin protección: si un listener lanza una excepción,
// los eventos siguientes de esa misma operación no se disparan (comportamiento estándar de C#).
public class Inventory : MonoBehaviour
{
    [Header("Capacidad")]
    [Tooltip("PROVISIONAL: la capacidad oficial del inventario todavía no está definida en el documento funcional (RF06). Este valor es solo de prueba.")]
    [Min(1)]
    [SerializeField] private int capacity = 10;

    // Inicializada en la declaración (no en Awake) para que funcione también con
    // AddComponent en modo edición, donde Awake puede no ejecutarse.
    private readonly List<ItemData> items = new List<ItemData>();
    private ReadOnlyCollection<ItemData> itemsView;
    private int selectedIndex = -1;

    /// <summary>Cantidad máxima de ítems. Nunca es menor que 1.</summary>
    public int Capacity => Mathf.Max(1, capacity);

    /// <summary>Cantidad de ítems en el inventario.</summary>
    public int Count => items.Count;

    /// <summary>Verdadero si no entra ningún ítem más.</summary>
    public bool IsFull => items.Count >= Capacity;

    /// <summary>Vista de solo lectura de los ítems, en orden de ingreso. No se puede castear a List.</summary>
    public IReadOnlyList<ItemData> Items => itemsView ??= items.AsReadOnly();

    /// <summary>Se dispara después de agregar un ítem.</summary>
    public event Action<ItemData> OnItemAdded;

    /// <summary>Se dispara después de quitar un ítem.</summary>
    public event Action<ItemData> OnItemRemoved;

    /// <summary>Se dispara cuando la selección cambia; recibe el nuevo índice (-1 = sin selección).</summary>
    public event Action<int> OnSelectionChanged;

    /// <summary>Se dispara al usar un ítem, antes de consumirlo (si corresponde).</summary>
    public event Action<ItemData> OnItemUsed;

    /// <summary>Se dispara al final de cualquier operación que cambió el contenido.</summary>
    public event Action OnInventoryChanged;

    /// <summary>Agrega un ítem al final. Devuelve false, sin disparar eventos, si es null o el inventario está lleno.</summary>
    public bool AddItem(ItemData item)
    {
        if (item == null || IsFull) return false;

        items.Add(item);
        OnItemAdded?.Invoke(item);
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>Quita la primera aparición del ítem. Devuelve false si no está.</summary>
    public bool RemoveItem(ItemData item)
    {
        return RemoveAt(items.IndexOf(item));
    }

    /// <summary>
    /// Quita el ítem en el índice y ajusta la selección para que siga apuntando al mismo ítem
    /// (o a ninguno, si se quitó el seleccionado). Devuelve false, sin disparar eventos, si el índice es inválido.
    /// Orden de eventos: OnItemRemoved, OnSelectionChanged (solo si cambió), OnInventoryChanged.
    /// </summary>
    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= items.Count) return false;

        ItemData removed = items[index];
        items.RemoveAt(index);

        int previousSelection = selectedIndex;
        if (selectedIndex == index) selectedIndex = -1;
        else if (selectedIndex > index) selectedIndex--;

        OnItemRemoved?.Invoke(removed);
        if (selectedIndex != previousSelection) OnSelectionChanged?.Invoke(selectedIndex);
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Usa la primera aparición del ítem: dispara OnItemUsed y, si item.consumeOnUse es verdadero,
    /// lo quita (con los eventos de RemoveAt). Devuelve false si es null o no está en el inventario.
    /// </summary>
    public bool UseItem(ItemData item)
    {
        return item != null && UseAt(items.IndexOf(item));
    }

    /// <summary>Usa el ítem seleccionado, igual que UseItem. Devuelve false si no hay selección válida.</summary>
    public bool UseSelected()
    {
        return UseAt(selectedIndex);
    }

    /// <summary>
    /// Selecciona el índice. -1 equivale a ClearSelection. Devuelve false, sin cambios, si está fuera de rango.
    /// Si el índice ya estaba seleccionado devuelve true sin disparar OnSelectionChanged.
    /// </summary>
    public bool SelectItem(int index)
    {
        if (index == -1)
        {
            ClearSelection();
            return true;
        }

        if (index < 0 || index >= items.Count) return false;
        if (index == selectedIndex) return true;

        selectedIndex = index;
        OnSelectionChanged?.Invoke(selectedIndex);
        return true;
    }

    /// <summary>Quita la selección. No dispara OnSelectionChanged si ya no había selección.</summary>
    public void ClearSelection()
    {
        if (selectedIndex == -1) return;

        selectedIndex = -1;
        OnSelectionChanged?.Invoke(-1);
    }

    /// <summary>Ítem seleccionado, o null si no hay selección.</summary>
    public ItemData GetSelectedItem()
    {
        return selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : null;
    }

    /// <summary>Verdadero si el ítem está al menos una vez.</summary>
    public bool HasItem(ItemData item) => items.Contains(item);

    /// <summary>Cuántas veces está el ítem en el inventario. 0 si es null.</summary>
    public int CountOf(ItemData item)
    {
        if (item == null) return 0;

        int count = 0;
        foreach (ItemData i in items)
        {
            if (i == item) count++;
        }
        return count;
    }

    // Usa el ítem de un índice concreto, así UseSelected consume justo el seleccionado
    // aunque el mismo ItemData esté repetido antes en la lista.
    private bool UseAt(int index)
    {
        if (index < 0 || index >= items.Count) return false;

        ItemData item = items[index];
        OnItemUsed?.Invoke(item);

        // Un listener de OnItemUsed pudo haber modificado el inventario: se vuelve a ubicar el ítem.
        if (item.consumeOnUse)
        {
            int current = index < items.Count && items[index] == item ? index : items.IndexOf(item);
            RemoveAt(current);
        }
        return true;
    }
}
