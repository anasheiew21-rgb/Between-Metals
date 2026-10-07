using UnityEngine;

// Activador de eventos ambientales (RF12 / HU-10, tarea T09-T #30).
//
// Es la pieza que decide CUÁNDO se enciende una señal, separada de la señal misma (#29) y del
// sistema de desbloqueo progresivo del mapa (#31-#33). No sabe qué fue lo que pasó: solo cuenta
// notificaciones y, al llegar a las que hacen falta, enciende las señales que tenga asignadas.
//
// Notificar() es público y sin parámetros a propósito: así puede engancharse desde cualquier
// UnityEvent del Inspector sin que esta clase conozca al que lo dispara. Hoy lo llama el
// onPickedUp de ItemPickup (que el equipo ya dejó puesto "para objetivos, puzzles, etc.");
// mañana lo puede llamar el sistema de zonas desbloqueadas sin tocar nada de acá.
//
// Para un disparo único e inmediato basta con dejar eventosRequeridos en 1.
[DisallowMultipleComponent]
public class ActivadorSenalAmbiental : MonoBehaviour
{
    [Tooltip("Señales que se encienden cuando se completan las notificaciones requeridas.")]
    [SerializeField] private SenalAmbiental[] senales = new SenalAmbiental[0];

    [Tooltip("Cuántas notificaciones hacen falta. 1 = se enciende con la primera.")]
    [Min(1)] [SerializeField] private int eventosRequeridos = 1;

    private int recibidas;

    /// <summary>Notificaciones recibidas hasta ahora. Deja de subir una vez activado.</summary>
    public int Recibidas => recibidas;

    /// <summary>Notificaciones necesarias para encender. Nunca es menor que 1.</summary>
    public int EventosRequeridos => Mathf.Max(1, eventosRequeridos);

    /// <summary>Verdadero desde que se encendieron las señales. Es permanente.</summary>
    public bool YaActivado { get; private set; }

    /// <summary>
    /// Avisa al activador de que ocurrió un evento. Cuando se junten las notificaciones
    /// requeridas enciende las señales una sola vez; las llamadas posteriores no hacen nada.
    /// </summary>
    public void Notificar()
    {
        if (YaActivado) return;

        recibidas++;
        if (recibidas < EventosRequeridos) return;

        Encender();
    }

    // Enciende todas las señales asignadas. Privado: el único camino de entrada es Notificar(),
    // para que no haya dos formas distintas de activar lo mismo.
    private void Encender()
    {
        YaActivado = true;

        foreach (SenalAmbiental senal in senales)
        {
            if (senal != null) senal.Encender();
        }
    }
}
