using UnityEngine;

// Sistema de diseno de la interfaz (Etapa 12 - Manual de Identidad Visual). Es el unico lugar
// donde viven la paleta, la tipografia, las medidas y los GUIStyle del juego: Menu, InventoryUI,
// ShopManager, GameOverUI, VictoryUI, PromptInteraccion y AvisosUI dibujan todos con estos
// estilos, asi que un cambio de identidad se hace aca y se ve en las siete pantallas.
//
// Por que IMGUI y no uGUI: el proyecto ya dibuja sus pantallas con OnGUI (el flujo de Esc, cursor
// y Time.timeScale esta armado alrededor de eso) y OnGUI sigue corriendo con el juego congelado.
// Lo que uGUI aporta -Image.fillAmount- solo lo necesita el HUD, que por eso sigue en Canvas
// (PlayerUI) leyendo los mismos colores de esta clase.
//
// Reglas del manual que esta clase hace cumplir:
//   - Paleta 60/30/10: negro de fondo, blanco humo y gris metal para texto/UI secundaria, y rojo
//     sangre SOLO en acentos, botones primarios, barras de progreso y alertas.
//   - Helvetica: Bold en MAYUSCULAS para titulares y botones, Regular para cuerpo, Oblique para
//     notas. Las mayusculas las pone cada pantalla en el texto; el estilo no las puede forzar.
//   - Sin sombras, sin degradados, sin glow: todas las texturas son color plano, y los unicos
//     pixeles intermedios que se generan son el antialias de las esquinas de 4px.
//   - Responsive de 1024x600 a 1920x1080 (ver el bloque de escala).
public static class EstiloUI
{
    // ---------------------------------------------------------------
    // Paleta (Etapa 12). Los hex son los del manual, sin reinterpretar.
    // ---------------------------------------------------------------

    /// <summary>#0A0A0A - fondo principal (el 60% de la pantalla).</summary>
    public static readonly Color Negro = Hex(0x0A0A0A);

    /// <summary>#F5F5F5 - texto principal (parte del 30%).</summary>
    public static readonly Color BlancoHumo = Hex(0xF5F5F5);

    /// <summary>#4D4D4D - bordes, texto secundario y controles inactivos (parte del 30%).</summary>
    public static readonly Color GrisMetal = Hex(0x4D4D4D);

    /// <summary>#A4161A - el 10%: acentos, botones primarios, barras de progreso y alertas.</summary>
    public static readonly Color RojoSangre = Hex(0xA4161A);

    /// <summary>#161616 - fondo de tarjetas y filas impares de las tablas.</summary>
    public static readonly Color FondoTarjeta = Hex(0x161616);

    // Variantes derivadas del rojo para los estados de los botones primarios. No son colores
    // nuevos de la paleta: son el mismo rojo mas claro y mas oscuro, que es lo unico que el manual
    // deja para dar realimentacion sin usar sombras ni glow.
    static readonly Color RojoHover = Hex(0xC01A1F);
    static readonly Color RojoActivo = Hex(0x7E1115);

    // Gris mas oscuro que GrisMetal, para el relleno de los botones secundarios al pasar el mouse.
    static readonly Color GrisHover = Hex(0x262626);

    // ---------------------------------------------------------------
    // Escala responsive: de netbook escolar 1024x600 a 1920x1080
    // ---------------------------------------------------------------
    //
    // Todas las pantallas se maquetan en un lienzo virtual fijo de 1280x720 y despues se escalan
    // con GUI.matrix. El factor es el MINIMO entre el que necesita el ancho y el que necesita el
    // alto, no Screen.height/720 como antes: asi el lienzo entra completo en cualquier proporcion
    // (16:9, 16:10 y 4:3) en vez de desbordarse por el costado en las pantallas angostas.
    //
    //   1024x600  -> min(0.800, 0.833) = 0.800   lienzo util 1280 x 750
    //   1366x768  -> min(1.067, 1.067) = 1.067   lienzo util 1280 x 720
    //   1920x1080 -> min(1.500, 1.500) = 1.500   lienzo util 1280 x 720
    //
    // Como 1 px virtual nunca baja de 0.8 px reales, los cuerpos de texto de 18 px virtuales se
    // dibujan a 14 px reales en la netbook y los botones de 44 px de alto a 35 px: se mantiene
    // legible y clickeable en el piso de resolucion que pide el manual.
    public const float AnchoBase = 1280f;
    public const float AltoBase = 720f;

    /// <summary>Factor de escala del lienzo virtual a la pantalla real.</summary>
    public static float Escala => EscalaPara(Screen.width, Screen.height);

    /// <summary>
    /// El factor de escala que le corresponde a una resolucion. Separado de <see cref="Escala"/>
    /// para poder verificarlo sin depender del tamano real de la pantalla (lo usa InterfazUISelfTest).
    /// </summary>
    public static float EscalaPara(float anchoPantalla, float altoPantalla)
    {
        return Mathf.Min(anchoPantalla / AnchoBase, altoPantalla / AltoBase);
    }

    /// <summary>Ancho del lienzo virtual en la pantalla actual. Nunca menor que AnchoBase.</summary>
    public static float Ancho => Screen.width / Escala;

    /// <summary>Alto del lienzo virtual en la pantalla actual. Nunca menor que AltoBase.</summary>
    public static float Alto => Screen.height / Escala;

    /// <summary>
    /// Caja de 1280x720 centrada en el lienzo, donde cada pantalla maqueta su contenido. Como la
    /// escala es el minimo de los dos ejes, el lienzo virtual siempre es igual o mas grande que
    /// esta caja: lo que se dibuje adentro entra completo en cualquier resolucion soportada, y lo
    /// que sobra (las franjas de una pantalla 4:3, por ejemplo) queda de margen.
    /// </summary>
    public static Rect AreaSegura => new Rect(
        (Ancho - AnchoBase) * 0.5f,
        (Alto - AltoBase) * 0.5f,
        AnchoBase, AltoBase);

    /// <summary>
    /// Pone GUI.matrix en la escala del lienzo virtual y construye los estilos si hace falta.
    /// Se llama al principio de cada OnGUI; devuelve el ancho virtual, que es lo que las pantallas
    /// usan para centrarse. El alto se lee de <see cref="Alto"/>.
    /// </summary>
    public static float AbrirLienzo()
    {
        Construir();
        float s = Escala;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        return Screen.width / s;
    }

    // ---------------------------------------------------------------
    // Medidas (px del lienzo virtual de 1280x720)
    // ---------------------------------------------------------------

    /// <summary>Alto minimo de boton que pide el manual.</summary>
    public const float AltoBoton = 44f;

    /// <summary>Radio de las esquinas de botones y tarjetas.</summary>
    public const int Radio = 4;

    /// <summary>Borde de una tarjeta en reposo.</summary>
    public const int BordeTarjeta = 1;

    /// <summary>Borde de una tarjeta activa (y color rojo, no gris).</summary>
    public const int BordeTarjetaActiva = 2;

    /// <summary>Relleno interno estandar de paneles y tarjetas.</summary>
    public const float Padding = 16f;

    /// <summary>Separacion vertical entre controles de una misma lista.</summary>
    public const float Separacion = 8f;

    /// <summary>Alto de una fila de tabla (header incluido).</summary>
    public const float AltoFila = 34f;

    /// <summary>Ancho de las columnas de menu (Jugar/Configuracion/Salir y compania).</summary>
    public const float AnchoColumna = 420f;

    // Tamanos de letra, en px virtuales.
    public const int FuenteTitulo = 48;
    public const int FuenteSubtitulo = 28;
    public const int FuenteBoton = 20;
    public const int FuenteDato = 20;
    public const int FuenteCuerpo = 18;
    public const int FuenteNota = 16;

    // ---------------------------------------------------------------
    // Estilos. Null hasta el primer Construir(); usalos siempre despues de AbrirLienzo().
    // ---------------------------------------------------------------

    /// <summary>Helvetica Bold, MAYUSCULAS, centrado. Para el titulo de cada pantalla.</summary>
    public static GUIStyle Titulo { get; private set; }

    /// <summary>Como Titulo pero en rojo sangre. Para la pantalla de derrota, que es una alerta.</summary>
    public static GUIStyle TituloAlerta { get; private set; }

    /// <summary>Helvetica Bold, MAYUSCULAS. Encabezado de seccion dentro de una pantalla.</summary>
    public static GUIStyle Subtitulo { get; private set; }

    /// <summary>Helvetica Regular, blanco humo. Cuerpo de texto.</summary>
    public static GUIStyle Cuerpo { get; private set; }

    /// <summary>Como Cuerpo pero centrado.</summary>
    public static GUIStyle CuerpoCentrado { get; private set; }

    /// <summary>Helvetica Oblique, gris metal. Notas y pies de ayuda.</summary>
    public static GUIStyle Nota { get; private set; }

    /// <summary>Como Nota pero centrada.</summary>
    public static GUIStyle NotaCentrada { get; private set; }

    /// <summary>Helvetica Bold centrado, para numeros y valores (la ficha de la tecla, el valor de un selector).</summary>
    public static GUIStyle DatoCentrado { get; private set; }

    /// <summary>Helvetica Bold a la derecha, para numeros y valores (el oro, el 5/12 del inventario).</summary>
    public static GUIStyle DatoDerecha { get; private set; }

    /// <summary>Como DatoDerecha pero en rojo sangre, para un valor que es una alerta (inventario lleno).</summary>
    public static GUIStyle DatoDerechaAlerta { get; private set; }

    /// <summary>Boton primario: relleno rojo sangre, 44 px de alto, esquinas de 4 px.</summary>
    public static GUIStyle BotonPrimario { get; private set; }

    /// <summary>Boton secundario: sin relleno, borde gris de 1 px, mismo alto que el primario.</summary>
    public static GUIStyle BotonSecundario { get; private set; }

    /// <summary>Boton secundario compacto, para las filas de una tabla.</summary>
    public static GUIStyle BotonFila { get; private set; }

    /// <summary>Tarjeta: fondo #161616 y borde gris de 1 px.</summary>
    public static GUIStyle Tarjeta { get; private set; }

    /// <summary>Casilla de la grilla del inventario, en reposo.</summary>
    public static GUIStyle Casilla { get; private set; }

    /// <summary>Casilla de la grilla del inventario, seleccionada (borde rojo de 2 px).</summary>
    public static GUIStyle CasillaActiva { get; private set; }

    /// <summary>Header de tabla: fondo rojo sangre y texto en mayusculas.</summary>
    public static GUIStyle HeaderTabla { get; private set; }

    // Texturas planas que las pantallas usan con GUI.DrawTexture (velo, filas, barras).
    static Texture2D texNegro, texTarjeta, texRojo, texGris, texBlanco;

    static Font fuente;
    static bool construido;

    /// <summary>La Helvetica resuelta (o su sustituta). Null hasta el primer Construir().</summary>
    public static Font Fuente => fuente;

    // ---------------------------------------------------------------
    // Construccion
    // ---------------------------------------------------------------

    /// <summary>
    /// Arma fuente, texturas y estilos. Idempotente y barata despues de la primera vez, asi que
    /// se puede llamar desde cualquier OnGUI sin cachear nada del lado de la pantalla.
    /// </summary>
    public static void Construir()
    {
        // Se comprueba tambien una textura y no solo el flag: al salir del modo de juego en el
        // editor, las texturas creadas en tiempo de ejecucion se destruyen, y si el dominio no se
        // recarga (Enter Play Mode Options) el flag quedaria en true apuntando a texturas muertas.
        if (construido && texBlanco != null) return;
        construido = true;

        fuente = ResolverFuente();

        texNegro = Plano(Negro);
        texTarjeta = Plano(FondoTarjeta);
        texRojo = Plano(RojoSangre);
        texGris = Plano(GrisMetal);
        texBlanco = Plano(BlancoHumo);

        // --- Texto ---
        // Las variantes (centrado, derecha, alerta) se arman aca y no en cada pantalla a proposito:
        // un GUIStyle nuevo por frame dentro de OnGUI es basura que el recolector tiene que juntar,
        // y este juego apunta a maquinas de bajos recursos.
        Titulo = Texto(FuenteTitulo, FontStyle.Bold, TextAnchor.MiddleCenter, BlancoHumo);
        TituloAlerta = Texto(FuenteTitulo, FontStyle.Bold, TextAnchor.MiddleCenter, RojoSangre);
        Subtitulo = Texto(FuenteSubtitulo, FontStyle.Bold, TextAnchor.MiddleLeft, BlancoHumo);

        Cuerpo = Texto(FuenteCuerpo, FontStyle.Normal, TextAnchor.MiddleLeft, BlancoHumo);
        Cuerpo.wordWrap = true;
        CuerpoCentrado = Texto(FuenteCuerpo, FontStyle.Normal, TextAnchor.MiddleCenter, BlancoHumo);
        CuerpoCentrado.wordWrap = true;

        Nota = Texto(FuenteNota, FontStyle.Italic, TextAnchor.MiddleLeft, GrisMetal);
        Nota.wordWrap = true;
        NotaCentrada = Texto(FuenteNota, FontStyle.Italic, TextAnchor.MiddleCenter, GrisMetal);
        NotaCentrada.wordWrap = true;

        DatoCentrado = Texto(FuenteDato, FontStyle.Bold, TextAnchor.MiddleCenter, BlancoHumo);
        DatoDerecha = Texto(FuenteDato, FontStyle.Bold, TextAnchor.MiddleRight, BlancoHumo);
        DatoDerechaAlerta = Texto(FuenteDato, FontStyle.Bold, TextAnchor.MiddleRight, RojoSangre);

        // --- Botones ---
        // El primario es el unico control con relleno rojo: es el acento del 10% de la paleta.
        BotonPrimario = Boton(FuenteBoton, AltoBoton,
            Caja(Radio, RojoSangre, RojoSangre, 0),
            Caja(Radio, RojoHover, RojoHover, 0),
            Caja(Radio, RojoActivo, RojoActivo, 0),
            BlancoHumo, BlancoHumo);

        // El secundario no tiene relleno: solo el borde gris de 1 px, que al pasar el mouse pasa a
        // blanco humo con un relleno gris muy oscuro. Nunca rojo, para no competir con el primario.
        BotonSecundario = Boton(FuenteBoton, AltoBoton,
            Caja(Radio, Color.clear, GrisMetal, BordeTarjeta),
            Caja(Radio, GrisHover, BlancoHumo, BordeTarjeta),
            Caja(Radio, GrisMetal, BlancoHumo, BordeTarjeta),
            BlancoHumo, BlancoHumo);

        // Mismo lenguaje que el secundario pero mas bajo: entra en una fila de tabla de 34 px.
        BotonFila = Boton(FuenteNota, AltoFila - 6f,
            Caja(Radio, Color.clear, GrisMetal, BordeTarjeta),
            Caja(Radio, GrisHover, BlancoHumo, BordeTarjeta),
            Caja(Radio, GrisMetal, BlancoHumo, BordeTarjeta),
            BlancoHumo, BlancoHumo);
        BotonFila.fontStyle = FontStyle.Bold;

        // --- Contenedores ---
        Tarjeta = Contenedor(Caja(Radio, FondoTarjeta, GrisMetal, BordeTarjeta), BordeTarjeta, (int)Padding);

        // La casilla de la grilla del inventario es una tarjeta chica (unos 99x72 px): con el
        // relleno de 16 px de las tarjetas grandes no le quedaria lugar al nombre del item.
        const int RellenoCasilla = 6;

        Casilla = Contenedor(Caja(Radio, FondoTarjeta, GrisMetal, BordeTarjeta), BordeTarjeta, RellenoCasilla);
        Casilla.alignment = TextAnchor.MiddleCenter;
        Casilla.font = fuente;
        Casilla.fontSize = FuenteNota;
        Casilla.normal.textColor = BlancoHumo;
        Casilla.wordWrap = true;
        Casilla.clipping = TextClipping.Clip;
        Casilla.imagePosition = ImagePosition.ImageAbove;

        CasillaActiva = new GUIStyle(Casilla)
        {
            border = BordeDe(Radio, BordeTarjetaActiva)
        };

        // Todos los estados con la misma textura: la casilla es un GUI.Button, y si solo se
        // cambiara 'normal', pasar el mouse por encima de la casilla seleccionada le devolveria el
        // borde gris y se perderia de vista cual estaba elegida.
        Fondo(CasillaActiva, Caja(Radio, FondoTarjeta, RojoSangre, BordeTarjetaActiva));

        // --- Tabla ---
        HeaderTabla = Texto(FuenteNota, FontStyle.Bold, TextAnchor.MiddleLeft, BlancoHumo);
        HeaderTabla.normal.background = texRojo;
        HeaderTabla.padding = new RectOffset(10, 10, 0, 0);
    }

    // La tipografia del manual es Helvetica. Se usa la fuente integrada de Unity
    // (LegacyRuntime.ttf), que ES Arial: la sustituta metrica estandar de Helvetica, con los mismos
    // anchos de caracter y la misma altura de x. La identidad se mantiene y no hay que pedirle al
    // equipo que instale nada.
    //
    // Por que NO se pide la Helvetica del sistema con Font.CreateDynamicFontFromOSFont, que seria
    // la forma de conseguirla de verdad en las maquinas que la tengan: una fuente dinamica creada
    // en tiempo de ejecucion anda en uGUI pero NO en IMGUI, y siete de las ocho pantallas del juego
    // se dibujan con IMGUI. IMGUI no le pide los caracteres al atlas antes de dibujar, asi que cada
    // etiqueta sale con lo que haya quedado en el atlas compartido en vez de con su propio texto
    // (se veia la palabra "Gizmos" repetida en todas, y con el estilo ignorado: sin centrar, sin
    // negrita y sin el tamano que le tocaba).
    //
    // Si el equipo quiere la Helvetica real, el camino es importarla como asset de fuente
    // (Assets/UI/Helvetica.ttf, Font Size 32, Character Set Unicode) y devolverla desde aca con un
    // Resources.Load o una referencia serializada: un asset de fuente si funciona en IMGUI, porque
    // su atlas lo arma el importador y no el motor en caliente.
    static Font ResolverFuente()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f != null) return f;

        // Con font = null, IMGUI dibuja con la fuente del skin por defecto: la interfaz pierde la
        // tipografia pero se lee, que es mejor que no verse.
        Debug.LogWarning("EstiloUI: no se pudo cargar la fuente integrada de Unity; la interfaz va a usar la del skin por defecto.");
        return null;
    }

    static GUIStyle Texto(int tamano, FontStyle estilo, TextAnchor alineacion, Color color)
    {
        var s = new GUIStyle
        {
            font = fuente,
            fontSize = tamano,
            fontStyle = estilo,
            alignment = alineacion,
            richText = false
        };
        s.normal.textColor = color;
        return s;
    }

    // Las 9 porciones de un fondo hecho con Caja(): la textura mide 2k+3 px de lado (k = el mayor
    // entre el radio y el grosor del borde), asi que a cada lado le tocan exactamente k+1 px y
    // queda 1 px de centro para estirar. Pasarse de ese numero hace que GUIStyle recorte mal y el
    // borde se deforme al crecer la caja, que es justo lo que las 9 porciones vienen a evitar.
    static RectOffset BordeDe(int radio, int grosor)
    {
        int b = Mathf.Max(radio, grosor) + 1;
        return new RectOffset(b, b, b, b);
    }

    static GUIStyle Boton(int tamano, float alto, Texture2D normal, Texture2D hover, Texture2D activo, Color colorTexto, Color colorHover)
    {
        var s = new GUIStyle
        {
            font = fuente,
            fontSize = tamano,
            fontStyle = FontStyle.Bold, // el manual pide Bold en todos los botones
            alignment = TextAnchor.MiddleCenter,
            fixedHeight = alto,
            border = BordeDe(Radio, BordeTarjeta),
            padding = new RectOffset(12, 12, 0, 0),
            margin = new RectOffset(0, 0, (int)(Separacion * 0.5f), (int)(Separacion * 0.5f))
        };

        s.normal.background = normal;
        s.hover.background = hover;
        s.active.background = activo;
        s.focused.background = normal;
        // onNormal/onHover son los que usa GUI.enabled = false: sin ellos el boton gris se dibuja
        // con la textura del tema del editor y rompe la paleta.
        s.onNormal.background = normal;
        s.onHover.background = hover;
        s.onActive.background = activo;

        s.normal.textColor = colorTexto;
        s.hover.textColor = colorHover;
        s.active.textColor = colorHover;
        s.focused.textColor = colorTexto;
        s.onNormal.textColor = colorTexto;
        s.onHover.textColor = colorHover;
        s.onActive.textColor = colorHover;
        return s;
    }

    static GUIStyle Contenedor(Texture2D fondo, int grosor, int relleno)
    {
        var s = new GUIStyle
        {
            border = BordeDe(Radio, grosor),
            padding = new RectOffset(relleno, relleno, relleno, relleno)
        };
        Fondo(s, fondo);
        return s;
    }

    // La misma textura en los seis estados. Un contenedor no tiene realimentacion de mouse, y
    // dejar un estado sin textura hace que Unity lo dibuje con el fondo de su propio tema.
    static void Fondo(GUIStyle s, Texture2D fondo)
    {
        s.normal.background = fondo;
        s.onNormal.background = fondo;
        s.hover.background = fondo;
        s.onHover.background = fondo;
        s.active.background = fondo;
        s.onActive.background = fondo;
    }

    // ---------------------------------------------------------------
    // Dibujo inmediato (lo que no entra en un GUIStyle)
    // ---------------------------------------------------------------

    /// <summary>
    /// Velo negro sobre todo el lienzo, para separar el menu de lo que haya atras (la partida
    /// congelada, o la foto del menu de inicio). alfa 0 no dibuja nada.
    /// </summary>
    public static void Velo(float alfa)
    {
        if (alfa <= 0f) return;
        Rellenar(new Rect(0f, 0f, Ancho, Alto), new Color(Negro.r, Negro.g, Negro.b, alfa));
    }

    /// <summary>Rectangulo de color plano. Es el unico primitivo de dibujo: sin degradados ni sombras.</summary>
    public static void Rellenar(Rect r, Color color)
    {
        Color previo = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(r, texBlanco);
        GUI.color = previo;
    }

    /// <summary>
    /// Barra de progreso: canal #161616 con borde gris y relleno rojo sangre segun t (0..1).
    /// El color se puede cambiar para los estados criticos, pero el rojo es el del manual.
    /// </summary>
    public static void Barra(Rect r, float t, Color colorRelleno)
    {
        Rellenar(r, FondoTarjeta);
        Marco(r, GrisMetal, BordeTarjeta);

        float interior = r.width - BordeTarjeta * 2f;
        float ancho = Mathf.Clamp01(t) * interior;
        if (ancho > 0f)
        {
            Rellenar(new Rect(r.x + BordeTarjeta, r.y + BordeTarjeta, ancho, r.height - BordeTarjeta * 2f), colorRelleno);
        }
    }

    /// <summary>Borde de 'grosor' px por dentro del rectangulo, sin relleno.</summary>
    public static void Marco(Rect r, Color color, float grosor)
    {
        Rellenar(new Rect(r.x, r.y, r.width, grosor), color);
        Rellenar(new Rect(r.x, r.yMax - grosor, r.width, grosor), color);
        Rellenar(new Rect(r.x, r.y + grosor, grosor, r.height - grosor * 2f), color);
        Rellenar(new Rect(r.xMax - grosor, r.y + grosor, grosor, r.height - grosor * 2f), color);
    }

    /// <summary>
    /// Fondo de una fila de tabla. Las filas alternan #0A0A0A y #161616, como pide el manual;
    /// 'indice' es el numero de fila (0 = la primera despues del header).
    /// </summary>
    public static void FilaTabla(Rect r, int indice)
    {
        Rellenar(r, indice % 2 == 0 ? Negro : FondoTarjeta);
    }

    /// <summary>
    /// Barra de 0 a 1 que se puede arrastrar con el mouse: el control de volumen y el de
    /// sensibilidad. Devuelve el valor nuevo (el mismo que entro si nadie la toco).
    ///
    /// Esta escrita a mano en vez de usar GUI.HorizontalSlider porque el slider nativo se dibuja
    /// con las texturas del skin del editor -riel gris claro y perilla con degradado- y no hay
    /// forma de llevarlo a la paleta del manual. Aca el riel es una tarjeta y el relleno es rojo
    /// sangre, igual que cualquier otra barra de progreso del juego.
    /// </summary>
    public static float BarraArrastrable(Rect r, float valor)
    {
        int id = GUIUtility.GetControlID(FocusType.Passive);
        Event e = Event.current;

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if (r.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = id;
                    valor = ValorEn(r, e.mousePosition);
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                // Una vez agarrada sigue el mouse aunque se salga del rectangulo, que es como se
                // espera que responda un control de volumen.
                if (GUIUtility.hotControl == id)
                {
                    valor = ValorEn(r, e.mousePosition);
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == id)
                {
                    GUIUtility.hotControl = 0;
                    e.Use();
                }
                break;

            case EventType.Repaint:
                Barra(r, valor, RojoSangre);
                break;
        }

        return valor;
    }

    static float ValorEn(Rect r, Vector2 mouse)
    {
        return r.width <= 0f ? 0f : Mathf.Clamp01((mouse.x - r.x) / r.width);
    }

    // ---------------------------------------------------------------
    // Texturas
    // ---------------------------------------------------------------

    static Texture2D Plano(Color color)
    {
        var t = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "EstiloUI_Plano",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        t.SetPixel(0, 0, color);
        t.Apply();
        return t;
    }

    /// <summary>
    /// Textura de 9 porciones para un rectangulo de esquinas redondeadas: relleno, borde de
    /// 'grosor' px y radio 'radio'. El centro mide 1 px, que es el que GUIStyle estira, asi que el
    /// borde no se deforma por mas que la caja crezca.
    ///
    /// Las esquinas se calculan con la distancia al centro del circulo correspondiente y se
    /// suavizan 1 px. Ese degradado de 1 px es antialias, no un efecto: sin el, una esquina de
    /// 4 px se ve como una escalera.
    /// </summary>
    public static Texture2D Caja(int radio, Color relleno, Color borde, int grosor)
    {
        Color[] pixeles = PixelesCaja(radio, relleno, borde, grosor, out int lado);

        var t = new Texture2D(lado, lado, TextureFormat.RGBA32, false)
        {
            name = "EstiloUI_Caja",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        t.SetPixels(pixeles);
        t.Apply();
        return t;
    }

    /// <summary>
    /// Los pixeles de <see cref="Caja"/>, sin crear la textura. Separado para poder verificar la
    /// matematica de las esquinas y del borde sin depender del motor grafico: lo usa el autotest.
    /// 'lado' sale con el ancho (y alto) de la grilla.
    /// </summary>
    public static Color[] PixelesCaja(int radio, Color relleno, Color borde, int grosor, out int lado)
    {
        int k = Mathf.Max(radio, grosor);
        lado = k * 2 + 3; // 1 px de centro estirable entre los dos bordes de k+1

        var pixeles = new Color[lado * lado];
        float r = radio;
        float centro = (lado - 1) * 0.5f;

        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                // Distancia firmada al borde del rectangulo redondeado, medida desde el centro:
                // negativa adentro, positiva afuera.
                float dx = Mathf.Abs(x - centro) - (centro - r);
                float dy = Mathf.Abs(y - centro) - (centro - r);
                float d = (dx > 0f && dy > 0f)
                    ? Mathf.Sqrt(dx * dx + dy * dy) - r   // zona de esquina: circulo
                    : Mathf.Max(dx, dy) - r;              // lados rectos

                // Cobertura del pixel: 1 adentro, 0 afuera, con 1 px de transicion.
                float dentro = Mathf.Clamp01(0.5f - d);
                // Cobertura de la zona interior al borde, para saber donde pintar el relleno.
                float interior = grosor > 0 ? Mathf.Clamp01(0.5f - (d + grosor)) : dentro;

                Color c = grosor > 0 ? Mezclar(borde, relleno, interior) : relleno;
                c.a *= dentro;
                pixeles[y * lado + x] = c;
            }
        }

        return pixeles;
    }

    // Mezcla respetando el alfa de los dos lados: Color.Lerp sobre un color transparente deja un
    // halo del color de abajo, que es justo lo que hay que evitar en los botones sin relleno.
    static Color Mezclar(Color desde, Color hacia, float t)
    {
        float a = Mathf.Lerp(desde.a, hacia.a, t);
        if (a <= 0f) return new Color(desde.r, desde.g, desde.b, 0f);

        // Premultiplicado, para que un relleno transparente no destina el borde opaco.
        float r = Mathf.Lerp(desde.r * desde.a, hacia.r * hacia.a, t) / a;
        float g = Mathf.Lerp(desde.g * desde.a, hacia.g * hacia.a, t) / a;
        float b = Mathf.Lerp(desde.b * desde.a, hacia.b * hacia.a, t) / a;
        return new Color(r, g, b, a);
    }

    static Color Hex(int rgb)
    {
        return new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            1f);
    }
}
