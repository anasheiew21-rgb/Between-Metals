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

    private Light flashlight;

    void Start()
    {
        flashlight = GetComponent<Light>();
        SincronizarModelo();
    }

    void Update()
    {
        if (KeyBindings.Down(KeyBindings.Action.Flashlight) && !Menu.IsOpen)
        {
            flashlight.enabled = !flashlight.enabled;
            SincronizarModelo();
        }
    }

    // El modelo acompaña el estado del Light, que es el unico lugar donde vive el estado de la
    // linterna: no hay un bool duplicado que se pueda desincronizar.
    void SincronizarModelo()
    {
        if (modeloLinterna != null) modeloLinterna.SetActive(flashlight.enabled);
    }
}
