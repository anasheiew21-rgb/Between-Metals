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

    [Tooltip("Si es verdadero, el ítem se elimina del inventario al usarlo.")]
    public bool consumeOnUse = true;

    [Tooltip("Opcional. Sonido propio de este ítem (al recogerlo y al usarlo). Si se deja vacío se " +
             "usa Assets/Audio/Resources/Items/pickup_<itemId>.wav y, si tampoco está, el genérico.")]
    public AudioClip sonido;

    [Tooltip("Opcional. Modelo 3D que se ve en el mapa. Si se deja vacío, el ItemPickup muestra el " +
             "cubo gris de ItemPickup_Base. Los genera Between Metals > Items > Crear modelos 3D.")]
    public GameObject modelo3D;

#if UNITY_EDITOR
    // Solo avisa: no corrige el valor, para no pisar datos del asset sin que nadie lo note.
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            Debug.LogWarning($"ItemData '{name}': itemId está vacío.", this);
        }
    }
#endif
}
