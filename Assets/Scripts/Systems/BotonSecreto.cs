using System;
using UnityEngine;
using UnityEngine.Events;

// Boton escondido que se pulsa con 'E' (hoy, el que esta a los pies del comerciante y abre el
// MuroSecreto del laberinto). Mismo patron que el resto de los interactuables: no detecta nada por
// si solo, PlayerInteraction apunta con su raycast, muestra TextoPrompt y llama a Interactuar().
// Necesita un Collider (en este objeto o en un hijo) para que ese raycast lo alcance.
//
// No sabe nada de muros concretos: recibe una lista de MuroSecreto y, ademas, un UnityEvent para
// enganchar cualquier otra cosa desde el Inspector (abrir una puerta, prender una luz, etc.) sin
// tener que tocar este script.
[DisallowMultipleComponent]
public class BotonSecreto : MonoBehaviour, IInteractable
{
    [Header("Que abre")]
    [Tooltip("Muros que se hunden al pulsar el boton. Pueden ser varios.")]
    [SerializeField] private MuroSecreto[] muros = Array.Empty<MuroSecreto>();

    [Tooltip("Puertas que se abren al pulsar el boton, sin pedir llave. Opcional.")]
    [SerializeField] private PuertaInteractuable[] puertas = Array.Empty<PuertaInteractuable>();

    [Header("Comportamiento")]
    [Tooltip("Si es verdadero, el boton funciona una sola vez y despues queda inerte.")]
    [SerializeField] private bool unSoloUso = true;

    [Header("Textos")]
    [SerializeField] private string textoSinPulsar = "Presiona E para pulsar el boton";

    [Tooltip("Lo que dice el cartel despues de pulsarlo (si es un boton de un solo uso).")]
    [SerializeField] private string textoPulsado = "Algo se movio en el laberinto";

    [Header("Feedback")]
    [Tooltip("Opcional. Suena en la posicion del boton al pulsarlo.")]
    [SerializeField] private AudioClip sonidoPulsar;

    [Tooltip("Opcional. Esto se desplaza al pulsarlo, para que se vea que el boton entro.")]
    [SerializeField] private Transform parteMovil;

    [Tooltip("Cuanto y hacia donde se mueve parteMovil, en metros y en SU espacio local (en un boton de piso, -Y).")]
    [SerializeField] private Vector3 desplazamientoVisual = new Vector3(0f, -0.04f, 0f);

    [Header("Eventos")]
    [SerializeField] private UnityEvent alPulsar = new UnityEvent();

    bool pulsado;

    /// <summary>Verdadero desde que se pulso por primera vez.</summary>
    public bool Pulsado => pulsado;

    /// <summary>Se dispara cada vez que el boton se pulsa con exito.</summary>
    public event Action AlPulsar;

    public string TextoPrompt => pulsado && unSoloUso ? textoPulsado : textoSinPulsar;

    public void Interactuar()
    {
        if (pulsado && unSoloUso) return;

        pulsado = true;

        // Los muros y las puertas ya ignoran por su cuenta la orden repetida, asi que un boton
        // reutilizable no necesita ningun chequeo extra aca.
        for (int i = 0; i < muros.Length; i++)
        {
            if (muros[i] != null) muros[i].Abrir();
            else Debug.LogWarning($"BotonSecreto '{name}': hay un hueco vacio en la lista de muros.", this);
        }

        for (int i = 0; i < puertas.Length; i++)
        {
            if (puertas[i] != null) puertas[i].Abrir();
        }

        if (sonidoPulsar != null && Application.isPlaying)
        {
            AudioSource.PlayClipAtPoint(sonidoPulsar, transform.position);
        }

        HundirParteMovil();

        alPulsar?.Invoke();
        AlPulsar?.Invoke();

        // El cartel que se esta mostrando quedo viejo: PlayerInteraction solo lo refresca cuando
        // cambia de objetivo, asi que se le pide el texto nuevo a mano.
        PromptInteraccion.Instancia?.Mostrar(TextoPrompt);

        Debug.Log($"BotonSecreto '{name}': pulsado. Muros abiertos: {muros.Length}, puertas: {puertas.Length}.");
    }

    // Movimiento de una sola vez, sin animacion: es feedback de que el boton entro, no una
    // mecanica. Una interpolacion aca no aportaria nada y sumaria un Update por boton.
    void HundirParteMovil()
    {
        if (parteMovil == null || desplazamientoVisual == Vector3.zero) return;

        parteMovil.localPosition += desplazamientoVisual;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (GetComponentInChildren<Collider>(true) == null)
        {
            Debug.LogWarning($"BotonSecreto '{name}': no tiene Collider (ni en hijos); el raycast de PlayerInteraction no lo va a detectar.", this);
        }

        if (muros.Length == 0 && puertas.Length == 0 && alPulsar.GetPersistentEventCount() == 0)
        {
            Debug.LogWarning($"BotonSecreto '{name}': no abre ningun muro ni puerta, y no tiene nada enganchado en alPulsar.", this);
        }
    }
#endif
}
