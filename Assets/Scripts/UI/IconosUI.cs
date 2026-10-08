using UnityEngine;

// Iconos de la interfaz (Etapa 12): lineales, minimalistas, trazado de 2 px, blancos o rojos y sin
// rellenos de color. Se generan por codigo en vez de importarse como PNG por tres motivos:
//
//   1. El manual los define por su trazo, no por un dibujo concreto: un trazo parametrico es la
//      forma mas directa de garantizar los 2 px en cualquier tamano.
//   2. No suma archivos binarios ni .meta al repo (ver el aviso de CLAUDE.md sobre GUIDs).
//   3. El mismo icono sirve para IMGUI (Textura) y para el Canvas del HUD (Sprite), sin dos copias
//      del asset que se puedan desincronizar.
//
// Cada icono es una o varias polilineas sobre una grilla de 32x32. El rasterizado mide la distancia
// de cada pixel a la polilinea mas cercana y pinta los que caen dentro de la mitad del grosor, con
// 1 px de suavizado: eso es antialias del trazo, no un degradado de estilo.
public static class IconosUI
{
    /// <summary>Lado de la textura de cada icono, en pixeles.</summary>
    public const int Lado = 32;

    /// <summary>Grosor del trazo, en pixeles de la textura.</summary>
    public const float Grosor = 2f;

    // El color se aplica al dibujar (GUI.color / Image.color), asi que las texturas se generan
    // blancas y una sola vez por forma: pintarlas de rojo no necesita una segunda textura.
    static Texture2D vida, estamina, linterna, inventario, trueque, alerta;
    static Sprite spriteLinterna, spriteVida, spriteEstamina;

    /// <summary>Corazon de contorno. Vida.</summary>
    public static Texture2D Vida => vida ??= Rasterizar(FormaCorazon());

    /// <summary>Rayo de contorno. Estamina.</summary>
    public static Texture2D Estamina => estamina ??= Rasterizar(FormaRayo());

    /// <summary>Linterna de contorno con su haz. Mano/linterna del HUD.</summary>
    public static Texture2D Linterna => linterna ??= Rasterizar(FormaLinterna());

    /// <summary>Caja de contorno con tapa. Inventario.</summary>
    public static Texture2D Inventario => inventario ??= Rasterizar(FormaCaja());

    /// <summary>Dos flechas opuestas. Intercambio del comerciante.</summary>
    public static Texture2D Trueque => trueque ??= Rasterizar(FormaTrueque());

    /// <summary>Triangulo con signo. Avisos y alertas criticas.</summary>
    public static Texture2D Alerta => alerta ??= Rasterizar(FormaAlerta());

    /// <summary>La linterna como Sprite, para el HUD en Canvas.</summary>
    public static Sprite SpriteLinterna => spriteLinterna ??= ASprite(Linterna);

    /// <summary>El corazon como Sprite, para el HUD en Canvas.</summary>
    public static Sprite SpriteVida => spriteVida ??= ASprite(Vida);

    /// <summary>El rayo como Sprite, para el HUD en Canvas.</summary>
    public static Sprite SpriteEstamina => spriteEstamina ??= ASprite(Estamina);

    // ---------------------------------------------------------------
    // Formas. Coordenadas en la grilla de 32x32, con el (0,0) abajo a la izquierda.
    // ---------------------------------------------------------------

    // Corazon parametrico clasico (x = sin^3 t), muestreado en 36 puntos y reescalado a la grilla.
    // Sale mas parejo que una polilinea dibujada a mano y queda cerrado sin empalmes a la vista.
    static Vector2[][] FormaCorazon()
    {
        const int muestras = 36;
        var puntos = new Vector2[muestras + 1];

        for (int i = 0; i <= muestras; i++)
        {
            float t = i / (float)muestras * Mathf.PI * 2f;
            float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
            float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
            // La curva va de -17..17 en x y -17..13 en y: se normaliza a 4..28 con 4 px de margen.
            puntos[i] = new Vector2(16f + x * 0.72f, 15f + y * 0.72f);
        }

        return new[] { puntos };
    }

    // Rayo cerrado de 7 vertices.
    static Vector2[][] FormaRayo()
    {
        return new[]
        {
            new[]
            {
                new Vector2(18f, 29f),
                new Vector2(9f, 17f),
                new Vector2(15f, 17f),
                new Vector2(13f, 3f),
                new Vector2(23f, 16f),
                new Vector2(17f, 16f),
                new Vector2(18f, 29f)
            }
        };
    }

    // Cuerpo rectangular, cabezal y tres rayas de haz. Es el icono de la "mano con linterna".
    static Vector2[][] FormaLinterna()
    {
        return new[]
        {
            // Cuerpo
            new[]
            {
                new Vector2(4f, 11f), new Vector2(16f, 11f),
                new Vector2(16f, 21f), new Vector2(4f, 21f), new Vector2(4f, 11f)
            },
            // Cabezal, mas alto que el cuerpo
            new[]
            {
                new Vector2(16f, 8f), new Vector2(21f, 10f),
                new Vector2(21f, 22f), new Vector2(16f, 24f), new Vector2(16f, 8f)
            },
            // Haz: tres rayas cortas, no un cono relleno (el manual prohibe los rellenos)
            new[] { new Vector2(24f, 16f), new Vector2(29f, 16f) },
            new[] { new Vector2(23f, 22f), new Vector2(27f, 25f) },
            new[] { new Vector2(23f, 10f), new Vector2(27f, 7f) }
        };
    }

    // Caja con la linea de la tapa.
    static Vector2[][] FormaCaja()
    {
        return new[]
        {
            new[]
            {
                new Vector2(4f, 5f), new Vector2(28f, 5f),
                new Vector2(28f, 24f), new Vector2(4f, 24f), new Vector2(4f, 5f)
            },
            new[] { new Vector2(4f, 18f), new Vector2(28f, 18f) },
            new[] { new Vector2(13f, 24f), new Vector2(13f, 28f) },
            new[] { new Vector2(19f, 24f), new Vector2(19f, 28f) }
        };
    }

    // Dos flechas, una hacia cada lado: "das" arriba, "recibis" abajo.
    static Vector2[][] FormaTrueque()
    {
        return new[]
        {
            new[] { new Vector2(4f, 21f), new Vector2(26f, 21f) },
            new[] { new Vector2(21f, 26f), new Vector2(26f, 21f), new Vector2(21f, 16f) },
            new[] { new Vector2(28f, 11f), new Vector2(6f, 11f) },
            new[] { new Vector2(11f, 16f), new Vector2(6f, 11f), new Vector2(11f, 6f) }
        };
    }

    // Triangulo de advertencia con el palo y el punto del signo.
    static Vector2[][] FormaAlerta()
    {
        return new[]
        {
            new[]
            {
                new Vector2(16f, 28f), new Vector2(29f, 5f),
                new Vector2(3f, 5f), new Vector2(16f, 28f)
            },
            new[] { new Vector2(16f, 22f), new Vector2(16f, 13f) },
            new[] { new Vector2(16f, 9f), new Vector2(16f, 9f) } // segmento de largo 0: el punto
        };
    }

    // ---------------------------------------------------------------
    // Rasterizado
    // ---------------------------------------------------------------

    // Pinta las polilineas en una textura blanca con alfa segun la distancia al trazo. El color
    // final lo decide quien dibuja (GUI.color o Image.color), que es lo que permite tener el mismo
    // icono en blanco y en rojo sin duplicar texturas.
    static Texture2D Rasterizar(Vector2[][] trazos)
    {
        var t = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false)
        {
            name = "IconosUI",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixeles = new Color[Lado * Lado];
        float mitad = Grosor * 0.5f;

        for (int y = 0; y < Lado; y++)
        {
            for (int x = 0; x < Lado; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);

                float d = float.MaxValue;
                for (int i = 0; i < trazos.Length; i++)
                {
                    Vector2[] trazo = trazos[i];
                    if (trazo.Length == 1) d = Mathf.Min(d, Vector2.Distance(p, trazo[0]));

                    for (int j = 0; j < trazo.Length - 1; j++)
                    {
                        d = Mathf.Min(d, DistanciaASegmento(p, trazo[j], trazo[j + 1]));
                    }
                }

                // 1 px de transicion a cada lado del borde del trazo.
                float alfa = Mathf.Clamp01(mitad + 0.5f - d);
                pixeles[y * Lado + x] = new Color(1f, 1f, 1f, alfa);
            }
        }

        t.SetPixels(pixeles);
        t.Apply();
        return t;
    }

    static float DistanciaASegmento(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float largo = ab.sqrMagnitude;
        if (largo <= Mathf.Epsilon) return Vector2.Distance(p, a);

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / largo);
        return Vector2.Distance(p, a + ab * t);
    }

    static Sprite ASprite(Texture2D textura)
    {
        Sprite s = Sprite.Create(textura, new Rect(0f, 0f, textura.width, textura.height), new Vector2(0.5f, 0.5f));
        s.name = "IconosUI_Sprite";
        s.hideFlags = HideFlags.HideAndDontSave;
        return s;
    }
}
