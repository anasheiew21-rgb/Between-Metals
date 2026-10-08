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
    [SerializeField] private string textoPulsado = "El laberinto ya cambió";

    [Header("Aviso de que el laberinto cambio")]
    [Tooltip("Muestra el aviso en pantalla y suena el metal cuando el boton abre algo.")]
    [SerializeField] private bool anunciarCambio = true;

    [Tooltip("El aviso que aparece en pantalla. Sale como alerta: borde e icono en rojo.")]
    [SerializeField] private string mensajeCambio = "Algo en el laberinto ha cambiado";

    [Tooltip("Opcional. Si se deja vacio usa el sonido de muros de la BibliotecaDeSonidos.")]
    [SerializeField] private AudioClip sonidoCambio;

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
        // reutilizable no necesita ningun chequeo extra aca. Se cuenta lo que realmente se abrio:
        // un boton que no mueve nada no tiene por que anunciar que el laberinto cambio.
        int abiertos = 0;

        for (int i = 0; i < muros.Length; i++)
        {
            if (muros[i] != null)
            {
                muros[i].Abrir();
                abiertos++;
            }
            else Debug.LogWarning($"BotonSecreto '{name}': hay un hueco vacio en la lista de muros.", this);
        }

        for (int i = 0; i < puertas.Length; i++)
        {
            if (puertas[i] != null)
            {
                puertas[i].Abrir();
                abiertos++;
            }
        }

        if (sonidoPulsar != null && Application.isPlaying)
        {
            AudioSource.PlayClipAtPoint(sonidoPulsar, transform.position);
        }

        if (abiertos > 0) AnunciarCambio();

        HundirParteMovil();

        alPulsar?.Invoke();
        AlPulsar?.Invoke();

        // El cartel que se esta mostrando quedo viejo: PlayerInteraction solo lo refresca cuando
        // cambia de objetivo, asi que se le pide el texto nuevo a mano.
        PromptInteraccion.Instancia?.Mostrar(TextoPrompt);

        Debug.Log($"BotonSecreto '{name}': pulsado. Muros abiertos: {muros.Length}, puertas: {puertas.Length}.");
    }

    /// <summary>
    /// Avisa que el laberinto se movio, por los dos canales a la vez: el sonido de los muros de
    /// metal corriendose y un aviso en pantalla.
    ///
    /// El sonido va en 2D y no en la posicion del muro a proposito. Un muro puede estar a media
    /// cuadra del boton -en el mapa actual, el boton esta a los pies del comerciante y el muro en
    /// la celda noreste-, y un AudioSource 3D se apaga a los 20 metros: el jugador apretaria el
    /// boton y no se enteraria de nada. Lo que hay que comunicar no es "un muro se movio ALLA",
    /// es "el laberinto que estas recorriendo ya no es el mismo", y eso no tiene una posicion.
    /// </summary>
    void AnunciarCambio()
    {
        if (!anunciarCambio) return;

        // Application.isPlaying: Reproducir2D crea su AudioSource compartido, y sin este chequeo
        // un autotest de editor que llame a Interactuar() dejaria un GameObject suelto en la
        // escena abierta. AvisosUI no lo necesita: ya se ignora solo si no hay instancia.
        if (Application.isPlaying)
        {
            AudioClip clip = sonidoCambio != null ? sonidoCambio : BibliotecaDeSonidos.Clip(BibliotecaDeSonidos.MuroDeslizando);
            BibliotecaDeSonidos.Reproducir2D(clip);
        }

        // Como alerta y no como aviso comun: va con el borde y el icono en rojo, que es lo que
        // el manual de la Etapa 12 reserva para lo que el jugador no se puede perder.
        if (!string.IsNullOrWhiteSpace(mensajeCambio)) AvisosUI.Alertar(mensajeCambio);
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
