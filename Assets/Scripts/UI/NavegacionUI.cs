using UnityEngine;
using UnityEngine.SceneManagement;

// Los cuatro saltos de escena que ofrece la interfaz, en un solo lugar: los piden Menu (Jugar,
// Reiniciar, Salir), GameOverUI y VictoryUI (Nueva partida, Volver al menu), y antes cada pantalla
// repetia su propio LoadScene con su propio reseteo de Time.timeScale.
//
// Lo delicado y por lo que conviene que este centralizado: Time.timeScale es estado GLOBAL que
// sobrevive al cambio de escena, asi que una pantalla que carga otra escena sin descongelar el
// tiempo deja la partida nueva arrancando detenida. El cursor corre la misma suerte.
//
// Menu.IsOpen tambien es global, pero lo baja Menu antes de llamar aca: es suyo y nadie mas lo
// escribe.
public static class NavegacionUI
{
    /// <summary>Nombre de la escena del menu de inicio, el que crea MenuPrincipalBuilder.</summary>
    public const string EscenaMenuPrincipal = "MenuPrincipal";

    /// <summary>Nombre de la escena de juego.</summary>
    public const string EscenaDeJuego = "Prototype";

    /// <summary>
    /// Verdadero si esa escena esta en las Build Settings y se puede cargar. Las pantallas lo
    /// consultan para dejar el boton en gris en vez de tirar un error al apretarlo (la escena del
    /// menu no existe hasta que alguien corre MenuPrincipalBuilder).
    /// </summary>
    public static bool SePuedeCargar(string escena)
    {
        return !string.IsNullOrWhiteSpace(escena) && Application.CanStreamedLevelBeLoaded(escena);
    }

    /// <summary>
    /// Carga la escena pedida descongelando el tiempo antes. Devuelve false y deja un error en la
    /// consola si la escena esta vacia o no esta en las Build Settings.
    /// </summary>
    public static bool Cargar(string escena, string nombreDelCampo, Object contexto = null)
    {
        if (string.IsNullOrWhiteSpace(escena))
        {
            Debug.LogError($"NavegacionUI: '{nombreDelCampo}' esta vacio; no sabe que escena cargar.", contexto);
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(escena))
        {
            Debug.LogError($"NavegacionUI: la escena '{escena}' no esta en las Build Settings. Corré Between Metals > Menu > Crear escena de menu principal (o revisá Between Metals > Menu > Revisar Build Settings).", contexto);
            return false;
        }

        Descongelar();
        SceneManager.LoadScene(escena);
        return true;
    }

    /// <summary>Vuelve a cargar la escena actual: es la "Nueva partida" de las pantallas de fin de partida.</summary>
    public static void NuevaPartida()
    {
        Descongelar();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>Cierra el juego. En el editor solo sale del modo de juego.</summary>
    public static void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Time.timeScale vuelve a 1 y el cursor se deja libre: la escena que viene decide si lo
    // bloquea (MouseLook lo hace en su Start) y asi no se pierde el raton en el menu de inicio.
    static void Descongelar()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
