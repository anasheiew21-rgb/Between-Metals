using UnityEngine;
using UnityEngine.SceneManagement;

// Pantalla de "Moriste" que aparece cuando PlayerStats.AlMorir se dispara: pausa el juego y
// ofrece Reintentar (recarga la escena actual). Mismo estilo IMGUI que Menu.cs. Se crea sola,
// como el resto de los menus del proyecto.
public class GameOverUI : MonoBehaviour
{
    PlayerStats stats;
    bool mostrando;
    GUIStyle tituloStyle, botonStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCrear()
    {
        if (FindAnyObjectByType<GameOverUI>() != null) return;
        if (FindAnyObjectByType<PlayerStats>() == null) return; // sin jugador (ej. un futuro menu de inicio), no hace falta
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
    }

    void MostrarPantalla()
    {
        mostrando = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Reintentar()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnGUI()
    {
        if (!mostrando) return;

        ConstruirEstilos();

        // Misma pantalla virtual de 720 de alto que usa Menu, para que escale igual
        float s = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float w = Screen.width / s;

        Color colorPrevio = GUI.color;
        GUI.color = new Color(0.05f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(0, 0, w, 720f), Texture2D.whiteTexture);
        GUI.color = colorPrevio;

        GUILayout.BeginArea(new Rect((w - 400f) / 2f, 260f, 400f, 220f));
        GUILayout.Label("Moriste", tituloStyle);
        GUILayout.Space(30f);
        if (GUILayout.Button("Reintentar", botonStyle)) Reintentar();
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
        tituloStyle.normal.textColor = new Color(0.9f, 0.2f, 0.2f);

        botonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 24,
            fixedHeight = 48f
        };
    }
}
