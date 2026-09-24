using UnityEngine;

// Un item que el comerciante tiene para vender (o le puede comprar al jugador).
// Es una clase de datos simple: la logica de comprar/vender vive en ShopManager.
[System.Serializable]
public class ItemComercio
{
    public string nombre = "Item";
    public Sprite icono;
    public int precio = 10;
    public int cantidad = 1;

    // Cuantas unidades de este item tiene el jugador comprado. No hay inventario propio todavia,
    // asi que la tienda misma lo lleva; el dia que exista un inventario real esto se muda ahi.
    [HideInInspector] public int cantidadJugador = 0;
}
