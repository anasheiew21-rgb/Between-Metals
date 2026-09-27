using UnityEngine;
using UnityEngine.SceneManagement;

// Cartelito en pantalla ("Presiona E para...") cuando el jugador esta mirando un IInteractable
// en rango. Una sola instancia compartida por todo el juego, para no repetir el dibujo por cada
// objeto interactuable de la escena.
public class PromptInteraccion : MonoBehaviour
{
    public static PromptInteraccion Instancia { get; private set; }

    private string texto;
    private GUIStyle estilo;

    void Awake()
    {
        Instancia = this;
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    // Se crea sola si la escena no tiene una, para no depender de agregarla a mano
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
        if (Instancia != null) return;
        new GameObject("PromptInteraccion").AddComponent<PromptInteraccion>();
    }

    public void Mostrar(string mensaje) { texto = mensaje; }
    public void Ocultar() { texto = null; }

    void OnGUI()
    {
        if (string.IsNullOrEmpty(texto)) return;
        if (Menu.IsOpen || ShopManager.HayTiendaAbierta) return; // no se superpone con otros menus

        ConstruirEstilo();

        // Misma pantalla virtual de 720 de alto que usa Menu, para que escale igual
        float s = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;

        GUI.Label(new Rect(0f, 600f, w, 50f), texto, estilo);
    }

    void ConstruirEstilo()
    {
        if (estilo != null) return;

        estilo = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        estilo.normal.textColor = Color.white;
    }
}
