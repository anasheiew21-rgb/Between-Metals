using System;
using System.Collections.Generic;
using UnityEngine;

// Núcleo del inventario (RF06/HU-05). Sin UI todavía: expone eventos para que una futura
// pantalla de inventario, o los sistemas de recolección/puzzle/comerciante (RF07/RF08/RF09),
// se conecten sin tener que modificar esta clase.
public class Inventory : MonoBehaviour
{
    [Header("Capacidad")]
    [Tooltip("PROVISIONAL: la capacidad oficial del inventario todavía no está definida en el documento funcional (RF06). Este valor es solo de prueba.")]
    [SerializeField] private int capacity = 10;

    private readonly List<ItemData> items = new List<ItemData>();
    private int selectedIndex = -1;

    public int Capacity => capacity;
    public int Count => items.Count;
    public bool IsFull => items.Count >= capacity;
    public IReadOnlyList<ItemData> Items => items;

    public event Action<ItemData> OnItemAdded;
    public event Action<ItemData> OnItemRemoved;
    public event Action<int> OnSelectionChanged;
    public event Action OnInventoryChanged;

    public bool AddItem(ItemData item)
    {
        if (item == null || IsFull) return false;

        items.Add(item);
        OnItemAdded?.Invoke(item);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool RemoveItem(ItemData item)
    {
        return RemoveAt(items.IndexOf(item));
    }

    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= items.Count) return false;

        ItemData removed = items[index];
        items.RemoveAt(index);

        if (selectedIndex == index) selectedIndex = -1;
        else if (selectedIndex > index) selectedIndex--;

        OnItemRemoved?.Invoke(removed);
        OnInventoryChanged?.Invoke();
        return true;
    }

    // "Usar" un ítem equivale, por ahora, a consumirlo (quitarlo del inventario).
    // El efecto concreto de usar cada ítem (abrir algo, entregarlo a un NPC) lo define
    // quien escuche OnItemRemoved, no esta clase.
    public bool UseItem(ItemData item) => RemoveItem(item);

    public bool UseSelected() => selectedIndex >= 0 && RemoveAt(selectedIndex);

    public void SelectItem(int index)
    {
        if (index < -1 || index >= items.Count) return;

        selectedIndex = index;
        OnSelectionChanged?.Invoke(selectedIndex);
    }

    public void ClearSelection() => SelectItem(-1);

    public ItemData GetSelectedItem()
    {
        return selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : null;
    }

    public bool HasItem(ItemData item) => items.Contains(item);
}
