using UnityEngine;

// Datos de un ítem, separados del inventario: un ScriptableObject por ítem,
// reutilizable como asset sin duplicar datos entre instancias.
[CreateAssetMenu(fileName = "NewItem", menuName = "Between Metals/Inventario/Item")]
public class ItemData : ScriptableObject
{
    [Tooltip("Identificador único del ítem, para lógica (puzzle, comerciante). No es el nombre mostrado al jugador.")]
    public string itemId;

    public string itemName;

    [TextArea]
    public string description;

    public Sprite icon;
}
