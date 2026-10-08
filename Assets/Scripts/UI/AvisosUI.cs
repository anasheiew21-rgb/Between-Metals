using UnityEngine;
using UnityEngine.SceneManagement;

// Avisos breves en pantalla (P-09): "Inventario lleno", "Intercambio realizado" y los que vayan
// apareciendo. Una sola instancia para todo el juego, igual que PromptInteraccion, y se crea sola
// al cargar la escena como el resto de las pantallas del proyecto.
//
// Quien quiera avisar algo llama al estatico y no se preocupa por si la instancia existe:
//
//     AvisosUI.Mostrar("Intercambio realizado");
//     AvisosUI.Alertar("Inventario lleno");   // critico: borde e icono en rojo
//
// La cola y los plazos los lleva EstadoAvisos; esta clase solo traduce el reloj del juego y dibuja.
// Usa Time.unscaledTime a proposito: los avisos del comerciante y del inventario salen con el juego
// congelado (Time.timeScale = 0), y con el reloj escalado no se irian nunca de la pantalla.
public class AvisosUI : MonoBehaviour
{
    // Esquina superior derecha del lienzo virtual: no choca con el titulo de los menus (arriba al
    // centro), ni con el HUD (abajo a la izquierda), ni con el cartel de PromptInteraccion (abajo
    // al centro), que son los otros tres lugares ocupados de la pantalla.
    const float Ancho = 340f;
    const float AltoAviso = 44f;
    const float MargenDerecho = 24f;
    const float MargenSuperior = 24f;
    const float LadoIcono = 20f;

    public static AvisosUI Instancia { get; private set; }

    readonly EstadoAvisos estado = new EstadoAvisos();

    /// <summary>El estado interno, para los autotests y para quien quiera inspeccionar la cola.</summary>
    public EstadoAvisos Estado => estado;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCrear()
    {
        EnsureExists();
        SceneManager.sceneLoaded -= OnSceneLoadedRecrear;
        SceneManager.sceneLoaded += OnSceneLoadedRecrear;
    }

    static void OnSceneLoadedRecrear(Scene s, LoadSceneMode m) => EnsureExists();

    /// <summary>Crea el AvisosUI de la escena si todavia no hay uno.</summary>
    public static void EnsureExists()
    {
        if (Instancia != null) return;
        new GameObject("AvisosUI").AddComponent<AvisosUI>();
    }

    void Awake()
    {
        Instancia = this;
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    /// <summary>Aviso informativo. Si no hay instancia (fuera del modo de juego) no hace nada.</summary>
    public static void Mostrar(string texto) => Encolar(texto, false);

    /// <summary>Alerta critica: mismo cartel con el borde y el icono en rojo sangre.</summary>
    public static void Alertar(string texto) => Encolar(texto, true);

    // No llama a EnsureExists a proposito, igual que PromptInteraccion.Instancia?.Ocultar(): en el
    // juego la instancia ya existe (la pone AutoCrear al cargar la escena), y asi un aviso disparado
    // desde un autotest de editor no deja un GameObject suelto en la escena abierta.
    static void Encolar(string texto, bool critico)
    {
        Instancia?.estado.Mostrar(texto, critico, Time.unscaledTime);
    }

    void OnGUI()
    {
        var vigentes = estado.Vigentes(Time.unscaledTime);
        if (vigentes.Count == 0) return;

        float w = EstiloUI.AbrirLienzo();
        float x = w - Ancho - MargenDerecho;

        // El mas nuevo arriba: se recorre la lista al reves, que la tiene del mas viejo al mas nuevo.
        for (int i = 0; i < vigentes.Count; i++)
        {
            EstadoAvisos.Aviso aviso = vigentes[vigentes.Count - 1 - i];
            var fila = new Rect(x, MargenSuperior + i * (AltoAviso + EstiloUI.Separacion), Ancho, AltoAviso);

            // Tarjeta: fondo #161616 con borde de 1 px gris, o de 2 px rojo si es critico. Es la
            // misma regla que el resto de las tarjetas del juego, no un cartel con estilo propio.
            Color borde = aviso.Critico ? EstiloUI.RojoSangre : EstiloUI.GrisMetal;
            float grosor = aviso.Critico ? EstiloUI.BordeTarjetaActiva : EstiloUI.BordeTarjeta;
            EstiloUI.Rellenar(fila, EstiloUI.FondoTarjeta);
            EstiloUI.Marco(fila, borde, grosor);

            var icono = new Rect(fila.x + 12f, fila.y + (AltoAviso - LadoIcono) * 0.5f, LadoIcono, LadoIcono);
            Color previo = GUI.color;
            GUI.color = borde;
            GUI.DrawTexture(icono, IconosUI.Alerta);
            GUI.color = previo;

            var texto = new Rect(icono.xMax + 10f, fila.y, fila.width - LadoIcono - 34f, AltoAviso);
            GUI.Label(texto, aviso.Texto, EstiloUI.Cuerpo);
        }
    }
}
