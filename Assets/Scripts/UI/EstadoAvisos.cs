using System.Collections.Generic;

// Logica de los avisos breves (P-09): "Inventario lleno", "Intercambio realizado" y companeros.
// Vive aparte de AvisosUI por el mismo motivo que InventoryPanelState vive aparte de InventoryUI:
// se puede probar sin abrir una escena ni depender de Time (el reloj entra por parametro), y es lo
// que mide AvisosSelfTest.
//
// Reglas:
//   - Cada aviso dura Duracion segundos desde que se muestra.
//   - A lo sumo Maximo avisos a la vez; el mas viejo se cae para dejar entrar al nuevo.
//   - Repetir el mismo texto no apila dos avisos iguales: renueva el que ya estaba. Si no, recoger
//     tres veces con el inventario lleno llenaria la pantalla de "Inventario lleno".
public class EstadoAvisos
{
    /// <summary>Segundos que queda en pantalla cada aviso.</summary>
    public const float Duracion = 2.5f;

    /// <summary>Cuantos avisos se muestran a la vez como maximo.</summary>
    public const int Maximo = 3;

    /// <summary>Un aviso vigente.</summary>
    public readonly struct Aviso
    {
        /// <summary>Texto tal cual se muestra.</summary>
        public readonly string Texto;

        /// <summary>Verdadero si es una alerta critica: se dibuja con el borde y el icono en rojo.</summary>
        public readonly bool Critico;

        /// <summary>Momento en que deja de mostrarse.</summary>
        public readonly float Expira;

        public Aviso(string texto, bool critico, float expira)
        {
            Texto = texto;
            Critico = critico;
            Expira = expira;
        }
    }

    readonly List<Aviso> avisos = new List<Aviso>();

    /// <summary>Avisos vigentes la ultima vez que se llamo a <see cref="Vigentes"/>. El mas nuevo al final.</summary>
    public IReadOnlyList<Aviso> Actuales => avisos;

    /// <summary>
    /// Agrega un aviso. Si ya habia uno con el mismo texto, le renueva el plazo en vez de apilar un
    /// duplicado (y le actualiza la criticidad). Un texto vacio no hace nada.
    /// </summary>
    public void Mostrar(string texto, bool critico, float ahora)
    {
        if (string.IsNullOrWhiteSpace(texto)) return;

        Purgar(ahora);

        for (int i = 0; i < avisos.Count; i++)
        {
            if (avisos[i].Texto != texto) continue;

            // Se reinserta al final: el aviso renovado pasa a ser el mas nuevo, asi el orden en
            // pantalla sigue contando la historia de lo que acaba de pasar.
            avisos.RemoveAt(i);
            avisos.Add(new Aviso(texto, critico, ahora + Duracion));
            return;
        }

        if (avisos.Count >= Maximo) avisos.RemoveAt(0);
        avisos.Add(new Aviso(texto, critico, ahora + Duracion));
    }

    /// <summary>Saca los vencidos y devuelve los que siguen vigentes, del mas viejo al mas nuevo.</summary>
    public IReadOnlyList<Aviso> Vigentes(float ahora)
    {
        Purgar(ahora);
        return avisos;
    }

    /// <summary>Borra todos los avisos. Se usa al cerrar la escena o al abrir una pantalla modal.</summary>
    public void Limpiar() => avisos.Clear();

    void Purgar(float ahora)
    {
        // Van ordenados por antiguedad, pero no por vencimiento: renovar un aviso lo manda al final
        // con un plazo mas largo, asi que no alcanza con cortar por el principio.
        for (int i = avisos.Count - 1; i >= 0; i--)
        {
            if (ahora >= avisos[i].Expira) avisos.RemoveAt(i);
        }
    }
}
