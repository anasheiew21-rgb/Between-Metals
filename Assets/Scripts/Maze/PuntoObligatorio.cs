using System.Collections.Generic;
using UnityEngine;

// Marca un lugar que el jugador tiene que poder alcanzar siempre (hoy el comerciante; mas adelante las llaves).
// Esta activo mientras su GameObject y este componente esten habilitados: al recoger una llave basta con
// destruirla o desactivarla para que las barreras dejen de protegerla.
public class PuntoObligatorio : MonoBehaviour
{
    static readonly List<PuntoObligatorio> activos = new List<PuntoObligatorio>();

    public static IReadOnlyList<PuntoObligatorio> Activos => activos;

    void OnEnable() => activos.Add(this);
    void OnDisable() => activos.Remove(this);
}
