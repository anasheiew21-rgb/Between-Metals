using UnityEngine;
using UnityEngine.SceneManagement;

// Pantalla de "Victoria" que aparece cuando GameManager.AlGanar se dispara (HU-13). Para cuando
// esto pasa, GameManager.Finalizar() ya puso el juego en pausa y congelo el control del jugador,
// asi que esta clase solo se encarga de dibujar el cartel. Mismo patron y mismo estilo IMGUI que
// GameOverUI: se crea sola y se recrea en cada carga de escena.
public class VictoryUI : MonoBehaviour
{
    // Menu la consulta para no forzar el cursor bloqueado por encima de esta pantalla, mismo
    // motivo que GameOverUI.EstaMostrando.
    public static bool EstaMostrando { get; private set; }

    bool mostrando;
    GUIStyle tituloStyle, botonStyle;

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

    void JugarDeNuevo()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnGUI()
    {
        if (!mostrando) return;

        ConstruirEstilos();

        // Misma pantalla virtual de 720 de alto que usa Menu/GameOverUI, para que escale igual
        float s = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;

        Color colorPrevio = GUI.color;
        GUI.color = new Color(0f, 0.05f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(0, 0, w, 720f), Texture2D.whiteTexture);
        GUI.color = colorPrevio;

        GUILayout.BeginArea(new Rect((w - 400f) / 2f, 260f, 400f, 220f));
        GUILayout.Label("Victoria", tituloStyle);
        GUILayout.Space(30f);
        if (GUILayout.Button("Jugar de nuevo", botonStyle)) JugarDeNuevo();
        GUILayout.EndArea();
    }

    void ConstruirEstilos()
    {
        if (tituloStyle != null) return;

        tituloStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 44,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        tituloStyle.normal.textColor = new Color(0.2f, 0.9f, 0.3f);

        botonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 24,
            fixedHeight = 48f
        };
    }
}
