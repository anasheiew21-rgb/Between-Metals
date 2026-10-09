using UnityEngine;

// Botones con sonido para las pantallas IMGUI del juego (Menu, GameOverUI, VictoryUI, ShopManager,
// InventoryUI). Reemplaza GUILayout.Button/GUI.Button: misma firma y mismo valor de retorno, mas
// el click al apretar y un sonido tenue al pasar el cursor por encima.
//
//   if (SonidosUI.Boton("Jugar", buttonStyle)) Jugar();
//
// Por que un wrapper y no un AudioSource con un UnityEvent por boton: estas pantallas no usan
// Canvas ni objetos de UI, se dibujan por codigo en OnGUI y los "botones" no existen como objetos
// a los que se les pueda enganchar nada. El wrapper es el unico lugar donde se puede meter el
// sonido una vez y que valga para los 25 botones del juego.
//
// Todo pasa por BibliotecaDeSonidos.Reproducir2D, que suena igual con el juego congelado: el menu
// de pausa corre con Time.timeScale = 0 y un sonido atado al tiempo de juego no se oiria.
public static class SonidosUI
{
    // Constantes y no campos serializados: esta clase es estatica y no vive en ningun objeto de la
    // escena, asi que no hay Inspector donde tocarlos. El balance real entre los dos sonidos ya
    // esta en los .wav (el hover se genera mucho mas tenue que el click, ver Tools/GeneradorAudio).
    const float VolumenClick = 0.75f;
    const float VolumenHover = 0.6f;

    // Boton que tenia el cursor encima la ultima vez que se dibujo la pantalla, identificado por
    // su rectangulo: es lo unico que distingue un boton de otro en IMGUI (no hay ids estables).
    // Sirve para tocar el hover UNA vez al entrar y no una vez por frame mientras el cursor
    // sigue ahi.
    static Rect rectConHover;
    static int frameDelUltimoHover = -10;

    /// <summary>
    /// Igual que GUILayout.Button, con sonido. Devuelve true el frame en que se aprieta.
    /// </summary>
    public static bool Boton(string texto, GUIStyle estilo, params GUILayoutOption[] opciones)
    {
        bool apretado = estilo != null
            ? GUILayout.Button(texto, estilo, opciones)
            : GUILayout.Button(texto, opciones);

        RevisarHoverDelUltimo();

        if (apretado) Reproducir(BibliotecaDeSonidos.BotonClick, VolumenClick);
        return apretado;
    }

    /// <summary>
    /// Igual que Boton, pero con el sonido descendente de "volver/cerrar": asi salir de una
    /// pantalla se oye distinto de entrar, sin tener que leerla.
    /// </summary>
    public static bool BotonAtras(string texto, GUIStyle estilo, params GUILayoutOption[] opciones)
    {
        bool apretado = estilo != null
            ? GUILayout.Button(texto, estilo, opciones)
            : GUILayout.Button(texto, opciones);

        RevisarHoverDelUltimo();

        if (apretado) Reproducir(BibliotecaDeSonidos.BotonAtras, VolumenClick);
        return apretado;
    }

    /// <summary>
    /// Igual que GUI.Button (posicion absoluta), con sonido. Es la forma que usan las pantallas que
    /// calculan sus rectangulos a mano en vez de apilar con GUILayout: Menu, PantallaFinal,
    /// ShopManager e InventoryUI.
    /// </summary>
    public static bool Boton(Rect rect, string texto, GUIStyle estilo)
    {
        return BotonAbsoluto(rect, new GUIContent(texto), estilo, BibliotecaDeSonidos.BotonClick);
    }

    /// <summary>Sobrecarga con GUIContent, para los botones que llevan icono (las casillas del inventario).</summary>
    public static bool Boton(Rect rect, GUIContent contenido, GUIStyle estilo)
    {
        return BotonAbsoluto(rect, contenido, estilo, BibliotecaDeSonidos.BotonClick);
    }

    /// <summary>Version "volver/cerrar" de GUI.Button, con el sonido descendente.</summary>
    public static bool BotonAtras(Rect rect, string texto, GUIStyle estilo)
    {
        return BotonAbsoluto(rect, new GUIContent(texto), estilo, BibliotecaDeSonidos.BotonAtras);
    }

    static bool BotonAbsoluto(Rect rect, GUIContent contenido, GUIStyle estilo, string sonido)
    {
        bool apretado = estilo != null
            ? GUI.Button(rect, contenido, estilo)
            : GUI.Button(rect, contenido);

        // Aca el rect llega dado, asi que no hace falta el detour por GUILayoutUtility ni esperar
        // a Repaint para conocerlo; RevisarHover igual descarta los eventos que no son Repaint.
        RevisarHover(rect);

        if (apretado) Reproducir(sonido, VolumenClick);
        return apretado;
    }

    /// <summary>
    /// Toca el sonido de "volver" sin dibujar ningun boton: para cuando se sale de una pantalla
    /// con Esc en vez de con el boton (Menu.VolverAtras, ShopManager).
    /// </summary>
    public static void SonarAtras()
    {
        Reproducir(BibliotecaDeSonidos.BotonAtras, VolumenClick);
    }

    /// <summary>Toca el sonido de click sin dibujar ningun boton.</summary>
    public static void SonarClick()
    {
        Reproducir(BibliotecaDeSonidos.BotonClick, VolumenClick);
    }

    // Version para los botones de GUILayout. El chequeo de Repaint va ANTES de pedir el rect y no
    // dentro de RevisarHover: durante el evento Layout la posicion todavia no esta calculada y
    // GUILayoutUtility.GetLastRect() devuelve un rect de relleno (y segun la version de Unity,
    // encima avisa por consola). Pidiendolo solo en Repaint no hay nada que descartar despues.
    static void RevisarHoverDelUltimo()
    {
        if (!EsRepaint()) return;

        RevisarHover(GUILayoutUtility.GetLastRect());
    }

    static bool EsRepaint()
    {
        return Event.current != null && Event.current.type == EventType.Repaint;
    }

    // Event.current.mousePosition ya viene transformada por el GUI.matrix de la pantalla, asi que
    // compararla con el rect funciona igual con el escalado por resolucion que hacen
    // Menu/GameOverUI/VictoryUI.
    static void RevisarHover(Rect rect)
    {
        if (!EsRepaint()) return;

        // Un boton gris (GUI.enabled = false, como "Comprar" sin oro en la tienda) no responde al
        // click, asi que tampoco tiene que sonar al pasarle por encima.
        if (!GUI.enabled) return;

        // Si en el frame anterior ningun boton tenia el cursor encima, se olvida el ultimo: asi
        // salir de un boton y volver a entrar vuelve a sonar.
        if (Time.frameCount - frameDelUltimoHover > 1) rectConHover = Rect.zero;

        if (!rect.Contains(Event.current.mousePosition)) return;

        frameDelUltimoHover = Time.frameCount;

        if (rect == rectConHover) return; // ya venia sonando sobre este boton

        rectConHover = rect;
        Reproducir(BibliotecaDeSonidos.BotonHover, VolumenHover);
    }

    static void Reproducir(string ruta, float volumen)
    {
        BibliotecaDeSonidos.Reproducir2D(ruta, volumen);
    }

    // Los estaticos sobreviven a salir de Play en el Editor; sin esto, el primer hover de la
    // segunda partida no suena porque el rect "ya estaba" hovereado.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        rectConHover = Rect.zero;
        frameDelUltimoHover = -10;
    }
}
