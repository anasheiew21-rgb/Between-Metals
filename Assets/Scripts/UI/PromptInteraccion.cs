using UnityEngine;
using UnityEngine.SceneManagement;

// Indicacion contextual del HUD (wireframe P-06): el cartel "[E] abrir" que aparece cuando el
// jugador esta mirando un IInteractable en rango. Una sola instancia compartida por todo el juego,
// para no repetir el dibujo por cada objeto interactuable de la escena.
//
// El formato "[E] ..." es cosa de esta clase y no de los IInteractable: sus TextoPrompt siguen
// diciendo "Presiona E para abrir" (hay autotests que los verifican tal cual), y aca se les saca
// ese prefijo para no repetir la tecla dos veces al lado de la ficha de la "E".
public class PromptInteraccion : MonoBehaviour
{
    public static PromptInteraccion Instancia { get; private set; }

    // --- Maquetado, en px del lienzo virtual de 1280x720 de EstiloUI ---
    const float AltoCartel = 40f;
    const float LadoFicha = 28f;      // la ficha cuadrada de la tecla
    const float MargenInferior = 140f; // deja libre abajo el HUD y la barra rapida
    const float PaddingCartel = 12f;

    // Los prefijos que se le sacan al TextoPrompt antes de mostrarlo, del mas largo al mas corto
    // (importa el orden: "Presiona E para " contiene a "Presiona E ").
    static readonly string[] PrefijosTecla = { "Presiona E para ", "Presioná E para ", "Presiona E ", "Presioná E " };

    private string texto;

    // Copia de EstiloUI.Cuerpo sin ajuste de linea: el cartel se mide con CalcSize para ajustarse
    // al largo del texto, y con wordWrap activado esa medida no es la de una sola linea.
    private GUIStyle estiloTexto;

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

    /// <summary>
    /// El texto como se muestra: sin el "Presiona E para " del principio, porque la tecla ya se ve
    /// en la ficha de al lado. Si el TextoPrompt no lo trae, se devuelve igual.
    /// </summary>
    public static string SinPrefijoDeTecla(string prompt)
    {
        if (string.IsNullOrEmpty(prompt)) return string.Empty;

        foreach (string prefijo in PrefijosTecla)
        {
            if (prompt.StartsWith(prefijo, System.StringComparison.OrdinalIgnoreCase)) return prompt.Substring(prefijo.Length);
        }

        return prompt;
    }

    void OnGUI()
    {
        if (string.IsNullOrEmpty(texto)) return;

        // No se superpone con ninguna pantalla modal: todas tapan el centro de la pantalla y el
        // orden de dibujado entre OnGUI de distintos componentes no esta garantizado, asi que no
        // alcanza con confiar en que el velo de la otra pantalla lo tape.
        if (Menu.IsOpen || ShopManager.HayTiendaAbierta || InventoryUI.IsOpen) return;
        if (GameOverUI.EstaMostrando || VictoryUI.EstaMostrando) return;

        float w = EstiloUI.AbrirLienzo();

        // Despues de AbrirLienzo los estilos ya existen, asi que recien aca se puede copiar.
        estiloTexto ??= new GUIStyle(EstiloUI.Cuerpo) { wordWrap = false };

        string etiqueta = SinPrefijoDeTecla(texto);
        float anchoTexto = estiloTexto.CalcSize(new GUIContent(etiqueta)).x;
        float ancho = PaddingCartel * 2f + LadoFicha + 10f + anchoTexto;

        var cartel = new Rect((w - ancho) * 0.5f, EstiloUI.Alto - MargenInferior, ancho, AltoCartel);
        EstiloUI.Rellenar(cartel, EstiloUI.FondoTarjeta);
        EstiloUI.Marco(cartel, EstiloUI.GrisMetal, EstiloUI.BordeTarjeta);

        // Ficha de la tecla: es el acento rojo del HUD. La letra va en blanco humo para que el
        // contraste contra el rojo se mantenga legible en la netbook.
        var ficha = new Rect(cartel.x + PaddingCartel, cartel.y + (AltoCartel - LadoFicha) * 0.5f, LadoFicha, LadoFicha);
        EstiloUI.Rellenar(ficha, EstiloUI.RojoSangre);

        GUI.Label(ficha, "E", EstiloUI.DatoCentrado);

        GUI.Label(new Rect(ficha.xMax + 10f, cartel.y, anchoTexto, AltoCartel), etiqueta, estiloTexto);
    }
}
