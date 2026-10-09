using UnityEngine;

// Maquetado compartido de las dos pantallas de fin de partida (wireframe P-07): victoria y
// derrota. Las dos son la misma tarjeta -titulo, una linea de contexto y los botones NUEVA PARTIDA
// y VOLVER AL MENÚ- y lo unico que cambia es el texto y el color del titulo, asi que el dibujo vive
// aca y GameOverUI/VictoryUI solo se encargan de cuando mostrarse.
public static class PantallaFinal
{
    const float AnchoPanel = 520f;
    const float AltoTitulo = 62f;
    const float AnchoRegla = 96f;
    const float AltoMensaje = 26f;

    /// <summary>
    /// Dibuja la pantalla completa (velo incluido) y atiende sus dos botones. Llamala desde OnGUI
    /// cuando la pantalla tenga que estar visible.
    /// </summary>
    /// <param name="titulo">Se muestra en MAYUSCULAS.</param>
    /// <param name="alerta">
    /// Verdadero en la derrota: el titulo va en rojo sangre, el color que el manual reserva para las
    /// alertas criticas. Falso en la victoria, que lo lleva en blanco humo. Es lo unico que distingue
    /// visualmente una pantalla de la otra.
    /// </param>
    /// <param name="mensaje">Una linea de contexto debajo del titulo.</param>
    /// <param name="escenaMenuPrincipal">Escena que carga VOLVER AL MENÚ.</param>
    /// <param name="contexto">Objeto que se pasa al Debug.LogError si la escena no esta en las Build Settings.</param>
    public static void Dibujar(string titulo, bool alerta, string mensaje, string escenaMenuPrincipal, Object contexto)
    {
        EstiloUI.AbrirLienzo();

        // Mas opaco que un menu: cuando la partida termino no hay nada atras que valga la pena ver.
        EstiloUI.Velo(0.92f);

        float altoPanel = EstiloUI.Padding * 2f + AltoTitulo + 12f + 2f + 14f + AltoMensaje
            + 16f + EstiloUI.AltoBoton + EstiloUI.Separacion + EstiloUI.AltoBoton;

        Rect area = EstiloUI.AreaSegura;
        var panel = new Rect(
            area.x + (area.width - AnchoPanel) * 0.5f,
            area.y + (area.height - altoPanel) * 0.5f,
            AnchoPanel, altoPanel);

        GUI.Box(panel, GUIContent.none, EstiloUI.Tarjeta);

        float y = panel.y + EstiloUI.Padding;

        GUI.Label(new Rect(panel.x, y, panel.width, AltoTitulo), titulo.ToUpperInvariant(),
            alerta ? EstiloUI.TituloAlerta : EstiloUI.Titulo);
        y += AltoTitulo + 12f;

        // La rayita siempre es roja, igual que la del titulo de los menus: es el acento de la
        // identidad, no un indicador de si se gano o se perdio (eso lo dice el color del titulo).
        EstiloUI.Rellenar(new Rect(panel.center.x - AnchoRegla * 0.5f, y, AnchoRegla, 2f), EstiloUI.RojoSangre);
        y += 2f + 14f;

        GUI.Label(new Rect(panel.x + EstiloUI.Padding, y, panel.width - EstiloUI.Padding * 2f, AltoMensaje), mensaje, EstiloUI.CuerpoCentrado);
        y += AltoMensaje + 16f;

        float anchoBoton = panel.width - EstiloUI.Padding * 2f;

        if (SonidosUI.Boton(new Rect(panel.x + EstiloUI.Padding, y, anchoBoton, EstiloUI.AltoBoton), "NUEVA PARTIDA", EstiloUI.BotonPrimario))
        {
            NavegacionUI.NuevaPartida();
        }
        y += EstiloUI.AltoBoton + EstiloUI.Separacion;

        // Gris si la escena del menu todavia no existe (nadie corrio MenuPrincipalBuilder): se ve
        // que el boton esta, pero no se puede apretar para que no tire un error al vacio.
        GUI.enabled = NavegacionUI.SePuedeCargar(escenaMenuPrincipal);
        if (SonidosUI.BotonAtras(new Rect(panel.x + EstiloUI.Padding, y, anchoBoton, EstiloUI.AltoBoton), "VOLVER AL MENÚ", EstiloUI.BotonSecundario))
        {
            NavegacionUI.Cargar(escenaMenuPrincipal, "escenaMenuPrincipal", contexto);
        }
        GUI.enabled = true;
    }
}
