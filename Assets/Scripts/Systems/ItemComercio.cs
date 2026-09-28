using UnityEngine;

// Un item que el comerciante tiene para vender (o le puede comprar al jugador), vinculado al
// ItemData real del inventario (RF06): nombre/icono se leen de ahi (item.itemName/item.icon), asi
// que esta clase no los duplica. Es una clase de datos simple: la logica de comprar/vender vive
// en ShopManager. cantidad es el stock que le queda a la tienda, no lo que tiene el jugador
// (eso ahora lo lleva el Inventory real, via ShopManager.inventarioJugador).
[System.Serializable]
public class ItemComercio
{
    public ItemData item;
    public int precio = 10;
    public int cantidad = 1;
}
