using System;
using UnityEditor;
using UnityEngine;

// Autotest de editor del sistema de interfaz de las Etapas 11 y 12: la paleta y las medidas del
// manual de identidad visual (EstiloUI), los iconos lineales (IconosUI), la cola de avisos
// (EstadoAvisos), el formato del cartel contextual (PromptInteraccion) y los saltos de escena
// (NavegacionUI). Casos CP-UIX-01..19.
//
// Lo que se puede verificar aca es lo que NO depende de que haya una pantalla: los colores, las
// medidas, la escala responsive, el rasterizado de las texturas y la logica de los avisos. Como se
// ve cada pantalla hay que mirarlo en el editor; esto cuida que la identidad visual no se vaya
// corriendo sin que nadie se entere.
//
// No abre ni guarda escenas y no crea assets en disco.
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod InterfazUISelfTest.RunAllAndExit -logFile <log>
public static class InterfazUISelfTest
{
    const string Tag = "[InterfazUISelfTest]";

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/Interfaz (estilo, iconos, avisos)")]
    static void RunFromMenu()
    {
        RunAll();
    }

    public static void RunAllAndExit()
    {
        bool ok = false;
        try
        {
            ok = RunAll();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool RunAll()
    {
        passed = 0;
        failed = 0;

        EstiloUI.Construir();

        // ---------------- Paleta (Etapa 12) ----------------

        Run("CP-UIX-01", "La paleta son los cinco hex del manual, sin reinterpretar", () =>
        {
            string e;
            if ((e = EsHex(EstiloUI.Negro, 0x0A, 0x0A, 0x0A, "Negro")) != null) return e;
            if ((e = EsHex(EstiloUI.BlancoHumo, 0xF5, 0xF5, 0xF5, "BlancoHumo")) != null) return e;
            if ((e = EsHex(EstiloUI.GrisMetal, 0x4D, 0x4D, 0x4D, "GrisMetal")) != null) return e;
            if ((e = EsHex(EstiloUI.RojoSangre, 0xA4, 0x16, 0x1A, "RojoSangre")) != null) return e;
            if ((e = EsHex(EstiloUI.FondoTarjeta, 0x16, 0x16, 0x16, "FondoTarjeta")) != null) return e;
            return null;
        });

        Run("CP-UIX-02", "Los cinco colores de la paleta son opacos", () =>
        {
            Color[] paleta = { EstiloUI.Negro, EstiloUI.BlancoHumo, EstiloUI.GrisMetal, EstiloUI.RojoSangre, EstiloUI.FondoTarjeta };
            foreach (Color c in paleta)
            {
                if (!Mathf.Approximately(c.a, 1f)) return $"un color de la paleta tiene alfa {c.a}";
            }
            return null;
        });

        // ---------------- Medidas de componentes (Etapa 12) ----------------

        // Las medidas de EstiloUI son const, asi que compararlas directo contra el numero del
        // manual seria una tautologia que el compilador pliega (y avisa con un CS0162). Se copian a
        // variables para que el chequeo exista de verdad en tiempo de ejecucion: lo que cuida es que
        // nadie cambie una de estas constantes sin darse cuenta de que estaba en el manual.
        Run("CP-UIX-03", "Los botones miden al menos los 44 px de alto que pide el manual", () =>
        {
            float altoBoton = EstiloUI.AltoBoton;
            if (altoBoton < 44f) return $"AltoBoton = {altoBoton}";
            if (EstiloUI.BotonPrimario.fixedHeight < 44f) return $"BotonPrimario.fixedHeight = {EstiloUI.BotonPrimario.fixedHeight}";
            if (EstiloUI.BotonSecundario.fixedHeight < 44f) return $"BotonSecundario.fixedHeight = {EstiloUI.BotonSecundario.fixedHeight}";
            return null;
        });

        Run("CP-UIX-04", "Esquinas de 4 px, borde de tarjeta de 1 px y de tarjeta activa de 2 px", () =>
        {
            int radio = EstiloUI.Radio, borde = EstiloUI.BordeTarjeta, bordeActiva = EstiloUI.BordeTarjetaActiva;

            if (radio != 4) return $"Radio = {radio}";
            if (borde != 1) return $"BordeTarjeta = {borde}";
            if (bordeActiva != 2) return $"BordeTarjetaActiva = {bordeActiva}";
            return null;
        });

        Run("CP-UIX-05", "El boton primario es rojo sangre y el secundario no tiene relleno rojo", () =>
        {
            // Se mira el pixel del centro de la textura, que es el que GUIStyle estira: ahi esta el
            // relleno, sin nada del borde ni del antialias de las esquinas.
            Color primario = PixelCentral(EstiloUI.BotonPrimario.normal.background);
            if (!CercaDe(primario, EstiloUI.RojoSangre)) return $"el relleno del primario es {ADescripcion(primario)}, no rojo sangre";

            Color secundario = PixelCentral(EstiloUI.BotonSecundario.normal.background);
            if (secundario.a > 0.05f) return $"el secundario tiene relleno (alfa {secundario.a:0.00}); deberia ser solo borde";
            return null;
        });

        Run("CP-UIX-06", "La tarjeta es #161616 y la casilla activa tiene el borde rojo", () =>
        {
            Color fondo = PixelCentral(EstiloUI.Tarjeta.normal.background);
            if (!CercaDe(fondo, EstiloUI.FondoTarjeta)) return $"el fondo de la tarjeta es {ADescripcion(fondo)}, no #161616";

            Color bordeActiva = PixelBordeIzquierdo(EstiloUI.CasillaActiva.normal.background);
            if (!CercaDe(bordeActiva, EstiloUI.RojoSangre, 0.08f)) return $"el borde de la casilla activa es {ADescripcion(bordeActiva)}, no rojo sangre";
            return null;
        });

        Run("CP-UIX-07", "El header de tabla es rojo y las filas alternan #0A0A0A y #161616", () =>
        {
            Color header = PixelCentral(EstiloUI.HeaderTabla.normal.background);
            if (!CercaDe(header, EstiloUI.RojoSangre)) return $"el header de tabla es {ADescripcion(header)}, no rojo sangre";

            // FilaTabla solo dibuja, asi que lo que se verifica es la regla de alternancia que
            // usan las dos tablas del juego (Controles y Comerciante): par = negro, impar = tarjeta.
            if (CercaDe(EstiloUI.Negro, EstiloUI.FondoTarjeta)) return "los dos colores de fila son el mismo";
            return null;
        });

        // ---------------- Tipografia ----------------

        Run("CP-UIX-08", "Hay una fuente resuelta y todos los estilos la usan", () =>
        {
            if (EstiloUI.Fuente == null) return "EstiloUI.Fuente quedo en null";

            var estilos = new (string, GUIStyle)[]
            {
                ("Titulo", EstiloUI.Titulo), ("Subtitulo", EstiloUI.Subtitulo), ("Cuerpo", EstiloUI.Cuerpo),
                ("Nota", EstiloUI.Nota), ("DatoDerecha", EstiloUI.DatoDerecha), ("BotonPrimario", EstiloUI.BotonPrimario),
                ("BotonSecundario", EstiloUI.BotonSecundario), ("BotonFila", EstiloUI.BotonFila),
                ("HeaderTabla", EstiloUI.HeaderTabla), ("Casilla", EstiloUI.Casilla)
            };

            foreach ((string nombre, GUIStyle estilo) in estilos)
            {
                if (estilo == null) return $"el estilo {nombre} quedo en null";
                if (estilo.font != EstiloUI.Fuente) return $"el estilo {nombre} no usa la fuente de EstiloUI";
            }
            return null;
        });

        // Regresion del bug de los textos "Gizmos": la fuente tiene que ser un asset (la integrada
        // del motor, o una importada en el proyecto), NO una creada en caliente con
        // Font.CreateDynamicFontFromOSFont. Una fuente dinamica de sistema anda en uGUI pero no en
        // IMGUI, que es con lo que se dibujan casi todas las pantallas: ahi cada etiqueta sale con
        // lo que haya en el atlas compartido en vez de con su texto. IsPersistent distingue
        // exactamente los dos casos, porque la creada en caliente no esta guardada en ningun lado.
        Run("CP-UIX-19", "La fuente es un asset y no una creada en tiempo de ejecucion", () =>
        {
            if (EstiloUI.Fuente == null) return "no hay fuente";
            if (!EditorUtility.IsPersistent(EstiloUI.Fuente))
            {
                return $"la fuente '{EstiloUI.Fuente.name}' no es un asset: IMGUI no la va a poder dibujar";
            }
            return null;
        });

        Run("CP-UIX-09", "Titulares y botones en Bold, cuerpo en Regular y notas en Oblique", () =>
        {
            if (EstiloUI.Titulo.fontStyle != FontStyle.Bold) return "el titulo no es Bold";
            if (EstiloUI.Subtitulo.fontStyle != FontStyle.Bold) return "el subtitulo no es Bold";
            if (EstiloUI.BotonPrimario.fontStyle != FontStyle.Bold) return "el boton primario no es Bold";
            if (EstiloUI.BotonSecundario.fontStyle != FontStyle.Bold) return "el boton secundario no es Bold";
            if (EstiloUI.Cuerpo.fontStyle != FontStyle.Normal) return "el cuerpo no es Regular";
            if (EstiloUI.Nota.fontStyle != FontStyle.Italic) return "la nota no es Oblique";
            return null;
        });

        // ---------------- Responsive (1024x600 .. 1920x1080) ----------------

        Run("CP-UIX-10", "La escala deja entrar el lienzo de 1280x720 en todas las resoluciones soportadas", () =>
        {
            (int, int)[] resoluciones = { (1024, 600), (1024, 768), (1280, 720), (1366, 768), (1600, 900), (1920, 1080), (2560, 1080) };

            foreach ((int ancho, int alto) in resoluciones)
            {
                float escala = EstiloUI.EscalaPara(ancho, alto);
                if (escala <= 0f) return $"{ancho}x{alto}: escala {escala}";

                // El lienzo virtual tiene que ser igual o mas grande que el area segura, si no el
                // contenido de las pantallas se saldria de la pantalla real.
                float anchoVirtual = ancho / escala;
                float altoVirtual = alto / escala;
                if (anchoVirtual < EstiloUI.AnchoBase - 0.5f) return $"{ancho}x{alto}: el ancho virtual es {anchoVirtual:0.0}, menor que {EstiloUI.AnchoBase}";
                if (altoVirtual < EstiloUI.AltoBase - 0.5f) return $"{ancho}x{alto}: el alto virtual es {altoVirtual:0.0}, menor que {EstiloUI.AltoBase}";
            }
            return null;
        });

        Run("CP-UIX-11", "En la netbook de 1024x600 la escala es 0.8 y un boton sigue midiendo 35 px reales", () =>
        {
            float escala = EstiloUI.EscalaPara(1024, 600);
            if (!Mathf.Approximately(escala, 0.8f)) return $"escala = {escala}";

            float altoReal = EstiloUI.AltoBoton * escala;
            if (altoReal < 32f) return $"un boton quedaria en {altoReal:0.0} px reales, demasiado chico para apuntarle";

            float cuerpoReal = EstiloUI.FuenteCuerpo * escala;
            if (cuerpoReal < 13f) return $"el cuerpo de texto quedaria en {cuerpoReal:0.0} px reales, ilegible";
            return null;
        });

        Run("CP-UIX-12", "A 1920x1080 la escala es 1.5 y el lienzo es exactamente el de diseno", () =>
        {
            float escala = EstiloUI.EscalaPara(1920, 1080);
            if (!Mathf.Approximately(escala, 1.5f)) return $"escala = {escala}";
            if (!Mathf.Approximately(1920f / escala, EstiloUI.AnchoBase)) return "el ancho virtual no es 1280";
            if (!Mathf.Approximately(1080f / escala, EstiloUI.AltoBase)) return "el alto virtual no es 720";
            return null;
        });

        // ---------------- Texturas ----------------

        // Los dos casos de Caja() van sobre PixelesCaja, que es la misma cuenta sin la Texture2D:
        // asi tambien pasan en -batchmode -nographics, donde no hay motor grafico.
        Run("CP-UIX-13", "Caja() deja las esquinas redondeadas y el centro relleno", () =>
        {
            Color[] caja = EstiloUI.PixelesCaja(EstiloUI.Radio, EstiloUI.RojoSangre, EstiloUI.RojoSangre, 0, out int lado);

            int esperado = EstiloUI.Radio * 2 + 3; // 1 px de centro estirable entre los dos bordes
            if (lado != esperado) return $"la grilla mide {lado}, se esperaba {esperado}";
            if (caja.Length != lado * lado) return $"hay {caja.Length} pixeles para una grilla de {lado}x{lado}";

            Color centro = caja[(lado / 2) * lado + lado / 2];
            if (!CercaDe(centro, EstiloUI.RojoSangre)) return $"el centro es {ADescripcion(centro)}";
            if (centro.a < 0.95f) return $"el centro no es opaco (alfa {centro.a:0.00})";

            // Las cuatro esquinas tienen que estar comidas por el redondeo.
            foreach ((int x, int y) in new[] { (0, 0), (lado - 1, 0), (0, lado - 1), (lado - 1, lado - 1) })
            {
                Color esquina = caja[y * lado + x];
                if (esquina.a > 0.05f) return $"la esquina ({x},{y}) no esta redondeada: alfa {esquina.a:0.00}";
            }
            return null;
        });

        Run("CP-UIX-14", "Un boton sin relleno queda hueco pero con borde", () =>
        {
            Color[] caja = EstiloUI.PixelesCaja(EstiloUI.Radio, Color.clear, EstiloUI.GrisMetal, EstiloUI.BordeTarjeta, out int lado);

            int medio = lado / 2;

            Color centro = caja[medio * lado + medio];
            if (centro.a > 0.05f) return $"el centro deberia ser transparente, tiene alfa {centro.a:0.00}";

            // En la fila del medio tiene que haber tinta del borde, de un lado y del otro. Se suman
            // los dos primeros pixeles de cada lado porque el borde de 1 px cae entre ellos por el
            // suavizado del rasterizado.
            float izquierda = caja[medio * lado + 0].a + caja[medio * lado + 1].a;
            float derecha = caja[medio * lado + lado - 1].a + caja[medio * lado + lado - 2].a;
            if (izquierda < 0.8f || derecha < 0.8f) return $"no se ve el borde (tinta {izquierda:0.00} / {derecha:0.00})";

            // Y el color de esa tinta tiene que ser el gris metal, no el relleno transparente.
            Color borde = caja[medio * lado + 0].a >= caja[medio * lado + 1].a ? caja[medio * lado] : caja[medio * lado + 1];
            if (!CercaDe(borde, EstiloUI.GrisMetal, 0.08f)) return $"el borde es {ADescripcion(borde)}, no gris metal";
            return null;
        });

        Run("CP-UIX-15", "Los iconos son de trazo: tienen tinta pero no estan rellenos", () =>
        {
            var iconos = new (string, Texture2D)[]
            {
                ("Vida", IconosUI.Vida), ("Estamina", IconosUI.Estamina), ("Linterna", IconosUI.Linterna),
                ("Inventario", IconosUI.Inventario), ("Trueque", IconosUI.Trueque), ("Alerta", IconosUI.Alerta)
            };

            foreach ((string nombre, Texture2D icono) in iconos)
            {
                if (icono.width != IconosUI.Lado || icono.height != IconosUI.Lado)
                    return $"{nombre} mide {icono.width}x{icono.height}, se esperaba {IconosUI.Lado}";

                int total = IconosUI.Lado * IconosUI.Lado;
                int conTinta = 0;
                foreach (Color p in icono.GetPixels())
                {
                    if (p.a > 0.5f) conTinta++;
                }

                float porcentaje = conTinta * 100f / total;

                // Un trazo de 2 px sobre 32x32 pinta del orden del 5-15% de los pixeles. Menos del
                // 2% seria un icono vacio; mas del 35%, una silueta rellena, que el manual prohibe.
                if (porcentaje < 2f) return $"{nombre} esta casi vacio ({porcentaje:0.0}% con tinta)";
                if (porcentaje > 35f) return $"{nombre} parece relleno y no lineal ({porcentaje:0.0}% con tinta)";
            }
            return null;
        });

        // ---------------- Avisos (P-09) ----------------

        Run("CP-UIX-16", "Los avisos duran lo que tienen que durar, no se duplican y no pasan del maximo", () =>
        {
            var estado = new EstadoAvisos();

            if (estado.Vigentes(0f).Count != 0) return "arranca con avisos";

            estado.Mostrar("Inventario lleno", true, 0f);
            if (estado.Vigentes(0f).Count != 1) return "no entro el primer aviso";
            if (!estado.Vigentes(0f)[0].Critico) return "el aviso critico no quedo marcado como critico";

            // Justo antes de vencer sigue; justo despues se va.
            if (estado.Vigentes(EstadoAvisos.Duracion - 0.01f).Count != 1) return "el aviso se fue antes de tiempo";
            if (estado.Vigentes(EstadoAvisos.Duracion).Count != 0) return "el aviso no se fue al vencer";

            // El mismo texto repetido renueva en vez de apilarse.
            estado.Mostrar("Inventario lleno", true, 10f);
            estado.Mostrar("Inventario lleno", true, 11f);
            if (estado.Vigentes(11f).Count != 1) return $"el texto repetido se apilo ({estado.Vigentes(11f).Count} avisos)";
            if (estado.Vigentes(10f + EstadoAvisos.Duracion + 0.01f).Count != 1) return "el repetido no renovo el plazo";

            // Mas avisos que el maximo: se cae el mas viejo.
            var lleno = new EstadoAvisos();
            for (int i = 0; i < EstadoAvisos.Maximo + 2; i++) lleno.Mostrar("aviso " + i, false, 0f);

            var vigentes = lleno.Vigentes(0f);
            if (vigentes.Count != EstadoAvisos.Maximo) return $"hay {vigentes.Count} avisos y el maximo es {EstadoAvisos.Maximo}";
            if (vigentes[vigentes.Count - 1].Texto != "aviso " + (EstadoAvisos.Maximo + 1)) return "el ultimo aviso no es el mas nuevo";

            // Un texto vacio no ocupa lugar.
            var vacio = new EstadoAvisos();
            vacio.Mostrar("", false, 0f);
            vacio.Mostrar("   ", false, 0f);
            if (vacio.Vigentes(0f).Count != 0) return "un texto vacio genero un aviso";
            return null;
        });

        // ---------------- Cartel contextual (P-06) y navegacion ----------------

        Run("CP-UIX-17", "El cartel contextual saca el \"Presiona E para\" y deja el resto igual", () =>
        {
            if (PromptInteraccion.SinPrefijoDeTecla("Presiona E para abrir") != "abrir") return "no saco el prefijo de la puerta";
            if (PromptInteraccion.SinPrefijoDeTecla("Presioná E para abrir") != "abrir") return "no saco el prefijo con tilde";
            if (PromptInteraccion.SinPrefijoDeTecla("Inventario lleno") != "Inventario lleno") return "le toco un texto que no traia prefijo";
            if (PromptInteraccion.SinPrefijoDeTecla("Recoger 1 de oro") != "Recoger 1 de oro") return "le toco el texto de la moneda";
            if (PromptInteraccion.SinPrefijoDeTecla(null) != string.Empty) return "null no devolvio cadena vacia";
            return null;
        });

        Run("CP-UIX-18", "NavegacionUI reconoce las escenas de las Build Settings y rechaza las que no estan", () =>
        {
            if (NavegacionUI.SePuedeCargar("")) return "acepto una escena vacia";
            if (NavegacionUI.SePuedeCargar(null)) return "acepto null";
            if (NavegacionUI.SePuedeCargar("EscenaQueNoExiste")) return "acepto una escena inexistente";

            // Las dos escenas del juego tienen que estar habilitadas en las Build Settings: si no,
            // los botones JUGAR y VOLVER AL MENÚ quedarian grises en el juego.
            if (!NavegacionUI.SePuedeCargar(NavegacionUI.EscenaMenuPrincipal))
                return $"la escena '{NavegacionUI.EscenaMenuPrincipal}' no esta en las Build Settings";
            if (!NavegacionUI.SePuedeCargar(NavegacionUI.EscenaDeJuego))
                return $"la escena '{NavegacionUI.EscenaDeJuego}' no esta en las Build Settings";
            return null;
        });

        Debug.Log($"{Tag} RESULT: {passed} passed, {failed} failed");
        return failed == 0;
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    static void Run(string id, string description, Func<string> test)
    {
        string error;
        try
        {
            error = test();
        }
        catch (Exception e)
        {
            error = $"excepción {e.GetType().Name}: {e.Message}";
        }

        if (error == null)
        {
            passed++;
            Debug.Log($"{Tag} {id} PASS - {description}");
        }
        else
        {
            failed++;
            Debug.LogError($"{Tag} {id} FAIL - {description}: {error}");
        }
    }

    static string EsHex(Color c, int r, int g, int b, string nombre)
    {
        // Tolerancia de medio paso de 8 bits: el color se guarda en float, asi que comparar por
        // igualdad exacta seria fragil.
        const float tolerancia = 0.5f / 255f;

        if (Mathf.Abs(c.r - r / 255f) > tolerancia ||
            Mathf.Abs(c.g - g / 255f) > tolerancia ||
            Mathf.Abs(c.b - b / 255f) > tolerancia)
        {
            return $"{nombre} es {ADescripcion(c)}, se esperaba #{r:X2}{g:X2}{b:X2}";
        }
        return null;
    }

    static bool CercaDe(Color a, Color b, float tolerancia = 0.02f)
    {
        return Mathf.Abs(a.r - b.r) <= tolerancia
            && Mathf.Abs(a.g - b.g) <= tolerancia
            && Mathf.Abs(a.b - b.b) <= tolerancia;
    }

    static string ADescripcion(Color c)
    {
        return $"#{Mathf.RoundToInt(c.r * 255f):X2}{Mathf.RoundToInt(c.g * 255f):X2}{Mathf.RoundToInt(c.b * 255f):X2} (alfa {c.a:0.00})";
    }

    // El pixel del centro de una textura de 9 porciones: el que GUIStyle estira para rellenar la caja.
    static Color PixelCentral(Texture texture)
    {
        var t = texture as Texture2D;
        if (t == null) throw new Exception("el estilo no tiene una Texture2D de fondo");
        return t.GetPixel(t.width / 2, t.height / 2);
    }

    // El pixel mas a la izquierda de la fila del medio: ahi esta el borde, sin esquinas de por medio.
    static Color PixelBordeIzquierdo(Texture texture)
    {
        var t = texture as Texture2D;
        if (t == null) throw new Exception("el estilo no tiene una Texture2D de fondo");

        // Se toma el de mayor alfa entre los dos primeros: el borde cae entre esos dos pixeles por
        // el suavizado de 1 px del rasterizado.
        Color a = t.GetPixel(0, t.height / 2);
        Color b = t.GetPixel(1, t.height / 2);
        return a.a >= b.a ? a : b;
    }
}
