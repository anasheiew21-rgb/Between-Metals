using UnityEngine;
using UnityEngine.SceneManagement;

// Pantalla de derrota (wireframe P-07) que aparece cuando PlayerStats.AlMorir se dispara: pausa el
// juego y ofrece NUEVA PARTIDA (recarga la escena actual) y VOLVER AL MENÚ. El dibujo lo hace
// PantallaFinal, que es la misma tarjeta que usa VictoryUI. Se crea sola, como el resto de las
// pantallas del proyecto.
public class GameOverUI : MonoBehaviour
{
    // Menu la consulta para no forzar el cursor bloqueado por encima de esta pantalla: Update()
    // sigue corriendo aunque Time.timeScale sea 0, asi que sin este chequeo Menu le ganaria de
    // mano al cursor libre todos los frames.
    public static bool EstaMostrando { get; private set; }

    [Tooltip("Escena que carga el boton VOLVER AL MENÚ")]
    [SerializeField] private string escenaMenuPrincipal = NavegacionUI.EscenaMenuPrincipal;

    PlayerStats stats;
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
        if (FindAnyObjectByType<GameOverUI>() != null) return;
        if (FindAnyObjectByType<PlayerStats>() == null) return; // sin jugador (ej. el menu de inicio), no hace falta
        new GameObject("GameOverUI").AddComponent<GameOverUI>();
    }

    void Start()
    {
        stats = FindAnyObjectByType<PlayerStats>();
        if (stats != null) stats.AlMorir += MostrarPantalla;
        else Debug.LogWarning("GameOverUI: no se encontro ningun PlayerStats en la escena.");
    }

    void OnDestroy()
    {
        if (stats != null) stats.AlMorir -= MostrarPantalla;
        EstaMostrando = false; // por si la escena se recarga con la pantalla abierta
    }

    void MostrarPantalla()
    {
        // Si por lo que sea la tienda estaba abierta, se cierra primero: si no, CerrarTienda()
        // volveria a bloquear el cursor por encima de esta pantalla (ella tambien lo toca).
        ShopManager tienda = FindAnyObjectByType<ShopManager>();
        if (tienda != null) tienda.CerrarTienda();

        mostrando = true;
        EstaMostrando = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnGUI()
    {
        if (!mostrando) return;

        PantallaFinal.Dibujar("Derrota", true,
            "El laberinto se quedó con vos.", escenaMenuPrincipal, this);
    }
}
