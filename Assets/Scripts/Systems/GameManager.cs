using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Gestor central del estado de partida (HU-12/13). Es una clase estatica, no un componente:
// no necesita Update ni vivir en ningun GameObject en particular, igual que KeyBindings. Se
// autoconecta a PlayerStats.AlMorir en cada carga de escena para que la Derrota se dispare sola
// (sin cablear nada a mano en el Editor); ExitTrigger llama a Ganar() directamente al interactuar
// con la salida. GameOverUI y VictoryUI se enganchan a AlPerder/AlGanar para saber cuando
// mostrarse: esta clase solo decide el estado y congela la partida, no dibuja nada.
public static class GameManager
{
    public enum Estado { Jugando, Victoria, Derrota }

    static Estado estadoActual = Estado.Jugando;
    public static Estado EstadoActual => estadoActual;

    public static event Action AlGanar;
    public static event Action AlPerder;

    // Se recrea en cada carga de escena: Reiniciar recarga la escena y RuntimeInitializeOnLoadMethod
    // corre una sola vez (mismo motivo que GameOverUI/Menu/PromptInteraccion, ver #55).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoWire()
    {
        Wire();
        SceneManager.sceneLoaded -= OnSceneLoadedRewire;
        SceneManager.sceneLoaded += OnSceneLoadedRewire;
    }

    static void OnSceneLoadedRewire(Scene s, LoadSceneMode m) => Wire();

    static void Wire()
    {
        estadoActual = Estado.Jugando;
        AlGanar = null;
        AlPerder = null;

        // La PlayerStats vieja (si la habia) ya fue destruida junto con la escena anterior, asi
        // que no hace falta desuscribirse de ella antes de enganchar la nueva.
        PlayerStats stats = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
        if (stats != null) stats.AlMorir += Perder;
    }

    public static void Ganar()
    {
        if (estadoActual != Estado.Jugando) return;

        estadoActual = Estado.Victoria;
        Finalizar();
        AlGanar?.Invoke();
    }

    public static void Perder()
    {
        if (estadoActual != Estado.Jugando) return;

        estadoActual = Estado.Derrota;
        Finalizar();
        AlPerder?.Invoke();
    }

    // Pausa la partida y congela el control del jugador. Mismo mecanismo que ya usa ShopManager
    // para bloquear a PlayerController/MouseLook mientras la tienda esta abierta.
    static void Finalizar()
    {
        ShopManager tienda = UnityEngine.Object.FindAnyObjectByType<ShopManager>();
        if (tienda != null && ShopManager.HayTiendaAbierta) tienda.CerrarTienda();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        PlayerController controlador = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (controlador != null) controlador.enabled = false;

        MouseLook camara = UnityEngine.Object.FindAnyObjectByType<MouseLook>();
        if (camara != null) camara.enabled = false;

        PlayerCombat combate = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
        if (combate != null) combate.enabled = false;

        PromptInteraccion.Instancia?.Ocultar();
    }
}
