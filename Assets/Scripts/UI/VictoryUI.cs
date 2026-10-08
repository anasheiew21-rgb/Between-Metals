using UnityEngine;
using UnityEngine.SceneManagement;

// Pantalla de victoria (wireframe P-07) que aparece cuando GameManager.AlGanar se dispara (HU-13).
// Para cuando esto pasa, GameManager.Finalizar() ya puso el juego en pausa y congelo el control del
// jugador, asi que esta clase solo se encarga de decidir cuando mostrarse: el dibujo lo hace
// PantallaFinal, la misma tarjeta que usa GameOverUI. Mismo patron que ella: se crea sola y se
// recrea en cada carga de escena.
public class VictoryUI : MonoBehaviour
{
    // Menu la consulta para no forzar el cursor bloqueado por encima de esta pantalla, mismo
    // motivo que GameOverUI.EstaMostrando.
    public static bool EstaMostrando { get; private set; }

    [Tooltip("Escena que carga el boton VOLVER AL MENÚ")]
    [SerializeField] private string escenaMenuPrincipal = NavegacionUI.EscenaMenuPrincipal;

    bool mostrando;

    // Se recrea en cada carga de escena: Reiniciar recarga la escena y RuntimeInitializeOnLoadMethod corre una sola vez (#55).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCrear()
    {
        EnsureExists();
        SceneManager.sceneLoaded -= OnSceneLoadedRecrear;
        SceneManager.sceneLoaded += OnSceneLoadedRecrear;
    }

    static void OnSceneLoadedRecrear(Scene s, LoadSceneMode m) => EnsureExists();

    public static void EnsureExists()
    {
        if (FindAnyObjectByType<VictoryUI>() != null) return;
        if (FindAnyObjectByType<PlayerStats>() == null) return; // sin jugador, no hace falta
        new GameObject("VictoryUI").AddComponent<VictoryUI>();
    }

    void Start()
    {
        GameManager.AlGanar += MostrarPantalla;
    }

    void OnDestroy()
    {
        GameManager.AlGanar -= MostrarPantalla;
        EstaMostrando = false; // por si la escena se recarga con la pantalla abierta
    }

    void MostrarPantalla()
    {
        mostrando = true;
        EstaMostrando = true;
        // Time.timeScale y el cursor ya los dejo listos GameManager.Finalizar() antes de disparar
        // AlGanar; aca no hace falta repetirlos.
    }

    void OnGUI()
    {
        if (!mostrando) return;

        // El titulo va en blanco humo y no en el verde de antes: la paleta de la Etapa 12 tiene
        // cuatro colores y el verde no es uno. El rojo queda reservado para la derrota, que es la
        // alerta critica de las dos pantallas.
        PantallaFinal.Dibujar("Victoria", false,
            "Saliste del laberinto.", escenaMenuPrincipal, this);
    }
}
