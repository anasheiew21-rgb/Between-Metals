using UnityEngine;

// Linterna del jugador: la tecla Linterna (F por defecto, ver KeyBindings) prende y apaga el Light
// de este objeto y, junto con el, el modelo que se ve en primera persona.
//
// El modelo va en un hijo aparte y no como mesh de este mismo objeto a proposito: asi el Light
// sigue centrado en la camara -el haz apunta siempre al centro de la pantalla, que es lo que el
// jugador espera al recorrer el laberinto- mientras el modelo se dibuja corrido hacia la mano.
// Si modeloLinterna queda sin asignar el script funciona igual, solo con la luz.
public class FlashlightController : MonoBehaviour
{
    [Header("Modelo en primera persona")]
    [Tooltip("Modelo que se muestra y se oculta junto con la luz. Opcional.")]
    [SerializeField] private GameObject modeloLinterna;

    [Header("Sonido")]
    [Tooltip("Volumen del click del interruptor")]
    [Range(0f, 1f)] [SerializeField] private float volumenClick = 0.7f;

    private Light flashlight;
    private AudioSource fuente;

    void Start()
    {
        flashlight = GetComponent<Light>();
        SincronizarModelo();

        // AudioSource propio y no PlayClipAtPoint: la linterna esta en la mano del jugador, el
        // click tiene que sonar siempre igual de cerca y de fuerte. En 2D, por lo mismo que los
        // pasos (ver PasosJugador): este objeto es hijo de la camara y a 3D el paneo se movería
        // con cada giro de cabeza.
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.loop = false;
        fuente.spatialBlend = 0f;
        AudioPreferences.RutearASfx(fuente);
    }

    void Update()
    {
        if (KeyBindings.Down(KeyBindings.Action.Flashlight) && !Menu.IsOpen)
        {
            flashlight.enabled = !flashlight.enabled;
            SincronizarModelo();

            // Dos clicks distintos (encender mas agudo que apagar) para que el jugador sepa en
            // que estado quedo la linterna sin mirar nada, que es justo lo que hace falta cuando
            // la esta prendiendo porque no ve.
            Reproducir(flashlight.enabled
                ? BibliotecaDeSonidos.LinternaEncender
                : BibliotecaDeSonidos.LinternaApagar);
        }
    }

    void Reproducir(string ruta)
    {
        if (fuente == null || volumenClick <= 0f) return;

        AudioClip clip = BibliotecaDeSonidos.Clip(ruta);
        if (clip != null) fuente.PlayOneShot(clip, volumenClick);
    }

    // El modelo acompaña el estado del Light, que es el unico lugar donde vive el estado de la
    // linterna: no hay un bool duplicado que se pueda desincronizar.
    void SincronizarModelo()
    {
        if (modeloLinterna != null) modeloLinterna.SetActive(flashlight.enabled);
    }
}
