using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Convierte la habitacion del comerciante (la caja vacia que deja MapaBuilder arriba de la
// escalera) en algo que se lea como una tienda: luz calida de refugio, mostrador, estanterias con
// mercaderia y una puerta en el hueco de entrada.
//
// Todo es decoracion: no toca el GameObject del comerciante, ni NPCMerchant, ni ShopManager, ni el
// inventario, ni la UI. Se puede borrar entero desde el menu "Quitar tienda" sin que el juego pierda
// nada funcional (la unica excepcion es la puerta, ver mas abajo).
//
// La habitacion NO se mide a ojo ni se hardcodea en coordenadas de mundo: se ancla en
// 'Punto_Comerciante' (el marcador que deja MapaBuilder en el centro, un metro sobre el piso) y la
// orientacion sale de donde estan los dos tramos del muro de entrada. Si MapaBuilder cambia el
// tamano o la posicion de la habitacion, esto la sigue sin tocar una constante.
//
// Dos medidas del resto del proyecto que condicionan el diseno y no se pueden ignorar:
//
//   * PlayerInteraction.interactionDistance = 3 m. El mostrador se interpone entre el jugador y el
//     comerciante, asi que si fuera mas profundo o estuviera mas lejos, el raycast ya no llegaria
//     al collider del comerciante y la tienda no se podria abrir. De ahi DistanciaMostrador.
//
//   * El collider del comerciante mide 1,35 m de alto (ComercianteModeloSetup.AlturaEnano). Un
//     mostrador de altura normal (1,1 m) lo taparia casi entero y dejaria el raycast sin blanco:
//     de ahi AltoMostrador = 0,95, que deja el pecho y la cabeza del enano a la vista por encima.
public static class TiendaBuilder
{
    const string NombreRaiz = "Tienda_Comerciante";
    const string CarpetaMateriales = "Assets/Materials/Tienda";
    const int Semilla = 20261008; // mismo numero = misma mercaderia en los estantes

    // Medidas de la habitacion, espejo de las de MapaBuilder (TamanoHabitacionCeldas = 3,
    // AltoParedHabitacion = 4, GrosorParedHabitacion = 0.4, AnchoPuertaHabitacion = 3).
    const float LadoHabitacion = 3f * MapaLayout.AnchoCalle; // 18 m
    const float AltoPared = 4f;
    const float GrosorPared = 0.4f;
    const float AnchoHueco = 3f;

    // --- Luz ---
    const float AlturaLuz = 3f;
    const float RangoLuz = 18f;
    const float IntensidadLuz = 2.1f;
    static readonly Color ColorLuz = new Color(1f, 0.62f, 0.28f);

    // --- Mostrador ---
    const float DistanciaMostrador = 1.5f; // del centro de la habitacion hacia la entrada
    const float AnchoMostrador = 5f;
    const float AltoMostrador = 0.95f;
    const float FondoMostrador = 0.7f;

    // --- Estanterias ---
    const float FondoEstante = 0.55f;
    const float GrosorTabla = 0.1f;
    static readonly float[] AlturasFondo = { 0.75f, 1.45f, 2.15f };
    static readonly float[] AlturasLado = { 0.9f, 1.8f };
    const float LargoEstanteFondo = 7f;
    const float LargoEstanteLado = 5f;

    // --- Puerta ---
    const float AnchoHoja = AnchoHueco - 0.2f; // un poco mas angosta que el hueco, para que no raspe
    const float AltoHoja = AltoPared - 0.1f;
    const float GrosorHoja = 0.15f;
    const float AnchoJamba = 0.12f;

    static readonly Color ColorMadera = new Color(0.34f, 0.22f, 0.13f);
    static readonly Color ColorMaderaClara = new Color(0.47f, 0.33f, 0.19f);
    static readonly Color ColorMetal = new Color(0.36f, 0.35f, 0.33f);
    static readonly Color[] ColoresMercaderia =
    {
        new Color(0.45f, 0.38f, 0.22f),
        new Color(0.30f, 0.34f, 0.40f),
        new Color(0.42f, 0.26f, 0.24f),
        new Color(0.27f, 0.33f, 0.26f),
    };

    [MenuItem("Between Metals/Comerciante/Construir tienda", priority = 710)]
    static void Construir()
    {
        if (!ResolverHabitacion(out Vector3 centro, out Vector3 hacia, out Vector3 lateral)) return;

        // Rehacer en vez de acumular: dos corridas seguidas dejan una sola tienda.
        GameObject anterior = GameObject.Find(NombreRaiz);
        if (anterior != null) Undo.DestroyObjectImmediate(anterior);

        var raiz = new GameObject(NombreRaiz);
        Undo.RegisterCreatedObjectUndo(raiz, "Crear " + NombreRaiz);

        float piso = centro.y;
        var azar = new System.Random(Semilla);

        ConstruirLuz(raiz.transform, centro);
        ConstruirMostrador(raiz.transform, centro, hacia, lateral);
        int bultos = ConstruirEstanterias(raiz.transform, centro, hacia, lateral, azar);
        ConstruirEntrada(raiz.transform, centro, hacia, lateral, piso);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log($"TiendaBuilder: tienda construida en {centro} (entrada hacia {-hacia}).\n" +
                  $"  1 Point Light calida (sin sombras), mostrador, 2 estanterias con {bultos} bultos y " +
                  "una puerta sin llave en el hueco de entrada.\n" +
                  "  Todo cuelga de '" + NombreRaiz + "': se borra con Between Metals/Comerciante/Quitar tienda.");
    }

    [MenuItem("Between Metals/Comerciante/Quitar tienda", priority = 711)]
    static void Quitar()
    {
        GameObject raiz = GameObject.Find(NombreRaiz);
        if (raiz == null)
        {
            Debug.Log("TiendaBuilder: no hay ningun objeto '" + NombreRaiz + "' en la escena.");
            return;
        }

        Undo.DestroyObjectImmediate(raiz);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("TiendaBuilder: tienda quitada.");
    }

    // ---------------------------------------------------------------
    // Donde esta la habitacion y para que lado mira
    // ---------------------------------------------------------------

    // 'centro' es el centro de la habitacion al nivel del PISO. 'hacia' apunta del centro al muro
    // del fondo (es decir, a espaldas del comerciante) y 'lateral' es el eje perpendicular, los dos
    // horizontales y unitarios.
    static bool ResolverHabitacion(out Vector3 centro, out Vector3 hacia, out Vector3 lateral)
    {
        centro = Vector3.zero;
        hacia = Vector3.right;
        lateral = Vector3.forward;

        // Punto_Comerciante lo deja MapaBuilder en 'centroHabitacion + Vector3.up', asi que el piso
        // queda un metro mas abajo. Si no esta, se cae al objeto del comerciante, que MapaBuilder
        // pone en el mismo lugar (mismo orden de busqueda que usa ProgresionBuilder).
        GameObject marcador = GameObject.Find("Punto_Comerciante");
        if (marcador != null)
        {
            centro = marcador.transform.position - Vector3.up;
        }
        else
        {
            var comerciante = Object.FindAnyObjectByType<NPCMerchant>();
            if (comerciante == null)
            {
                Debug.LogError("TiendaBuilder: no se encontro 'Punto_Comerciante' ni ningun NPCMerchant " +
                               "en la escena. Genera el mapa primero (Between Metals/Mapa).");
                return false;
            }
            centro = comerciante.transform.position - Vector3.up;
        }

        // La orientacion sale de los dos tramos del muro de entrada: su punto medio es el centro del
        // hueco, y del centro de la habitacion al hueco va justo al reves de 'hacia'. Asi no hay que
        // repetir aca el 'haciaComerciante = Vector3.right' de MapaBuilder.
        Transform entradaA = BuscarEnEscena("Muro_Habitacion_Entrada_A");
        Transform entradaB = BuscarEnEscena("Muro_Habitacion_Entrada_B");

        if (entradaA != null && entradaB != null)
        {
            Vector3 hueco = (entradaA.position + entradaB.position) * 0.5f;
            Vector3 delCentroAlHueco = hueco - centro;
            delCentroAlHueco.y = 0f;

            if (delCentroAlHueco.sqrMagnitude > 0.01f) hacia = -delCentroAlHueco.normalized;
            else Debug.LogWarning("TiendaBuilder: el muro de entrada quedo sobre el centro de la habitacion; se asume que la entrada esta al oeste.");
        }
        else
        {
            Debug.LogWarning("TiendaBuilder: no se encontraron 'Muro_Habitacion_Entrada_A/_B'; se asume " +
                             "que la entrada esta al oeste (como la deja MapaBuilder).");
        }

        // Se redondea al eje mas cercano: la habitacion es una caja alineada a los ejes, y una
        // direccion con decimales dejaria el mobiliario girado un pelo respecto de las paredes.
        hacia = Mathf.Abs(hacia.x) >= Mathf.Abs(hacia.z)
            ? new Vector3(Mathf.Sign(hacia.x), 0f, 0f)
            : new Vector3(0f, 0f, Mathf.Sign(hacia.z));
        lateral = Vector3.Cross(Vector3.up, hacia);

        return true;
    }

    static Transform BuscarEnEscena(string nombre)
    {
        GameObject go = GameObject.Find(nombre);
        return go != null ? go.transform : null;
    }

    // ---------------------------------------------------------------
    // 1. Luz calida
    // ---------------------------------------------------------------

    // Una sola Point Light naranja, colgada en el medio de la habitacion. El contraste con el
    // laberinto lo da el color: EnvironmentManager fuerza oscuridad permanente y las antorchas de
    // AmbienteBuilder son chicas y espaciadas, asi que una luz que llena un cuarto entero se lee
    // como refugio sin necesidad de agregar mas.
    //
    // Sin sombras a proposito: una point light con sombras en tiempo real es lo mas caro que se
    // puede poner en un proyecto apuntado a PCs de pocos recursos (mismo criterio que
    // AmbienteBuilder.SembrarAntorcha). Si hace falta mas calidez, antes de encender sombras conviene
    // subir IntensidadLuz.
    static void ConstruirLuz(Transform raiz, Vector3 centro)
    {
        var go = new GameObject("Luz_Tienda");
        Undo.RegisterCreatedObjectUndo(go, "Crear Luz_Tienda");
        go.transform.SetParent(raiz, true);
        go.transform.position = centro + Vector3.up * AlturaLuz;

        Light luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = ColorLuz;
        luz.range = RangoLuz;
        luz.intensity = IntensidadLuz;
        luz.shadows = LightShadows.None;

        // El mismo parpadeo de las antorchas, pero mucho mas suave: esto es una lampara de tienda,
        // no una llama. Sin temblor, para que la luz no se mueva de donde se la puso.
        var antorcha = go.AddComponent<Antorcha>();
        var so = new SerializedObject(antorcha);
        so.FindProperty("intensidadBase").floatValue = IntensidadLuz;
        so.FindProperty("amplitud").floatValue = 0.08f;
        so.FindProperty("velocidad").floatValue = 0.7f;
        so.FindProperty("temblor").floatValue = 0f;
        so.ApplyModifiedProperties();
    }

    // ---------------------------------------------------------------
    // 2. Mostrador
    // ---------------------------------------------------------------

    // Mesada entre el jugador y el comerciante: cuerpo macizo con collider (el jugador choca y
    // tiene que comerciar por encima) y una tapa un poco mas ancha, que es lo que le da el aspecto
    // de mesada y no de cajon.
    static void ConstruirMostrador(Transform raiz, Vector3 centro, Vector3 hacia, Vector3 lateral)
    {
        Transform grupo = Grupo(raiz, "Mostrador");

        // Hacia la entrada, o sea del lado del jugador. A 1,5 m del comerciante: con el fondo de
        // 0,7 m y el radio del CharacterController, el jugador queda a poco mas de 2 m del
        // comerciante, dentro de los 3 m de PlayerInteraction.
        Vector3 baseMostrador = centro - hacia * DistanciaMostrador;

        Pieza(grupo, "Cuerpo",
            baseMostrador + Vector3.up * (AltoMostrador * 0.5f),
            Dim(lateral, AnchoMostrador, AltoMostrador, hacia, FondoMostrador),
            "Madera", ColorMadera, conCollider: true);

        Pieza(grupo, "Tapa",
            baseMostrador + Vector3.up * (AltoMostrador + 0.04f),
            Dim(lateral, AnchoMostrador + 0.35f, 0.08f, hacia, FondoMostrador + 0.25f),
            "MaderaClara", ColorMaderaClara, conCollider: false);

        // Dos refuerzos en las puntas: rompen la silueta de "cubo escalado" por casi nada.
        for (int i = 0; i < 2; i++)
        {
            float signo = i == 0 ? 1f : -1f;
            Pieza(grupo, "Refuerzo_" + (i == 0 ? "A" : "B"),
                baseMostrador + lateral * (signo * AnchoMostrador * 0.5f) + Vector3.up * (AltoMostrador * 0.5f),
                Dim(lateral, 0.12f, AltoMostrador - 0.05f, hacia, FondoMostrador + 0.1f),
                "Metal", ColorMetal, conCollider: false);
        }
    }

    // ---------------------------------------------------------------
    // 3. Estanterias
    // ---------------------------------------------------------------

    // Tablas delgadas apiladas contra el muro del fondo (a espaldas del comerciante) y contra un
    // lateral, con bultos encima que hacen de mercaderia. Sin collider: son decoracion de pared y un
    // collider ahi solo le daria trabajo al horneado del NavMesh (NavMeshRuntimeBuilder recolecta
    // PhysicsColliders).
    static int ConstruirEstanterias(Transform raiz, Vector3 centro, Vector3 hacia, Vector3 lateral,
        System.Random azar)
    {
        float mitad = LadoHabitacion * 0.5f;
        // Cara interna del muro, menos medio fondo de estante: la tabla queda apoyada contra la
        // pared sin atravesarla.
        float distanciaPared = mitad - GrosorPared * 0.5f - FondoEstante * 0.5f;

        int bultos = 0;

        bultos += Estanteria(raiz, "Estanteria_Fondo", centro + hacia * distanciaPared,
            lateral, hacia, LargoEstanteFondo, AlturasFondo, azar);

        bultos += Estanteria(raiz, "Estanteria_Lado", centro + lateral * distanciaPared,
            hacia, lateral, LargoEstanteLado, AlturasLado, azar);

        return bultos;
    }

    // 'ejeLargo' es la direccion en la que corre la tabla; 'ejeFondo' apunta del centro de la
    // habitacion a la pared contra la que se apoya.
    static int Estanteria(Transform raiz, string nombre, Vector3 baseEstante, Vector3 ejeLargo,
        Vector3 ejeFondo, float largo, float[] alturas, System.Random azar)
    {
        Transform grupo = Grupo(raiz, nombre);
        int bultos = 0;

        float altoTotal = alturas[alturas.Length - 1] + 0.25f;

        // Montantes: los dos parantes verticales. Van primero para que las tablas se vean apoyadas.
        for (int i = 0; i < 2; i++)
        {
            float signo = i == 0 ? 1f : -1f;
            Pieza(grupo, "Montante_" + (i == 0 ? "A" : "B"),
                baseEstante + ejeLargo * (signo * largo * 0.5f) + Vector3.up * (altoTotal * 0.5f),
                Dim(ejeLargo, 0.12f, altoTotal, ejeFondo, FondoEstante),
                "Madera", ColorMadera, conCollider: false);
        }

        for (int t = 0; t < alturas.Length; t++)
        {
            Pieza(grupo, "Tabla_" + (t + 1).ToString("00"),
                baseEstante + Vector3.up * alturas[t],
                Dim(ejeLargo, largo, GrosorTabla, ejeFondo, FondoEstante),
                "MaderaClara", ColorMaderaClara, conCollider: false);

            // Mercaderia: tres o cuatro bultos por tabla, repartidos a lo largo con una posicion
            // sorteada pero con semilla fija, asi la tienda se ve igual en cada corrida.
            int cuantos = 3 + azar.Next(2);
            for (int b = 0; b < cuantos; b++)
            {
                float fraccion = (b + 0.5f) / cuantos + (float)(azar.NextDouble() - 0.5) * 0.12f;
                float desplazamiento = (fraccion - 0.5f) * (largo - 0.6f);
                float lado = 0.22f + (float)azar.NextDouble() * 0.16f;

                Vector3 pos = baseEstante
                    + ejeLargo * desplazamiento
                    + Vector3.up * (alturas[t] + GrosorTabla * 0.5f + lado * 0.5f)
                    + ejeFondo * (float)((azar.NextDouble() - 0.5) * 0.12);

                Pieza(grupo, "Bulto_" + (t + 1).ToString("00") + "_" + (b + 1).ToString("00"),
                    pos, new Vector3(lado, lado, lado),
                    "Mercaderia_" + (bultos % ColoresMercaderia.Length),
                    ColoresMercaderia[bultos % ColoresMercaderia.Length], conCollider: false);

                bultos++;
            }
        }

        return bultos;
    }

    // ---------------------------------------------------------------
    // 4. Entrada y puerta
    // ---------------------------------------------------------------

    // MapaBuilder ya deja el muro de entrada partido en dos con un hueco de 3 m en el medio, asi que
    // no hay que adaptar nada del muro: lo que falta es enmarcar el hueco con dos jambas y colgarle
    // una hoja.
    //
    // La puerta NO pide llave: se abre con 'E' y punto. Es a proposito, porque dentro de esta
    // habitacion esta el boton secreto que ProgresionBuilder usa como segundo camino a la llave de
    // la salida; una puerta con llave aca podria dejar ese camino cerrado.
    static void ConstruirEntrada(Transform raiz, Vector3 centro, Vector3 hacia, Vector3 lateral, float piso)
    {
        Transform grupo = Grupo(raiz, "Entrada");

        float mitad = LadoHabitacion * 0.5f;
        Vector3 hueco = centro - hacia * mitad;

        // Jambas: tapan el canto de los dos tramos del muro y encuadran la puerta.
        for (int i = 0; i < 2; i++)
        {
            float signo = i == 0 ? 1f : -1f;
            Pieza(grupo, "Jamba_" + (i == 0 ? "A" : "B"),
                hueco + lateral * (signo * (AnchoHueco * 0.5f + AnchoJamba * 0.5f)) + Vector3.up * (AltoPared * 0.5f),
                Dim(lateral, AnchoJamba, AltoPared, hacia, GrosorPared + 0.12f),
                "Madera", ColorMadera, conCollider: false);
        }

        // PuertaInteractuable gira ESTE objeto, no la hoja: el pivote tiene que estar en la bisagra,
        // en el borde del hueco, y la hoja colgar hacia su +X local. Por eso la rotacion se arma con
        // 'lateral' como el +X del objeto (ver la jerarquia documentada en PuertaInteractuable).
        Quaternion rotacion = Quaternion.LookRotation(Vector3.Cross(lateral, Vector3.up), Vector3.up);
        Vector3 bisagra = hueco - lateral * (AnchoHoja * 0.5f);
        bisagra.y = piso;

        var go = new GameObject("Puerta_Tienda");
        Undo.RegisterCreatedObjectUndo(go, "Crear Puerta_Tienda");
        go.transform.SetParent(grupo, false);
        go.transform.SetPositionAndRotation(bisagra, rotacion);

        GameObject hoja = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(hoja, "Crear Hoja");
        hoja.name = "Hoja";
        hoja.transform.SetParent(go.transform, false);
        hoja.transform.localScale = new Vector3(AnchoHoja, AltoHoja, GrosorHoja);
        // Medio ancho hacia +X (el borde izquierdo queda en la bisagra) y medio alto hacia arriba
        // (se apoya en el piso), igual que la hoja que arma ProgresionBuilder.
        hoja.transform.localPosition = new Vector3(AnchoHoja * 0.5f, AltoHoja * 0.5f, 0f);
        PonerMaterial(hoja, "Puerta", ColorMadera, conSombras: true);

        var puerta = Undo.AddComponent<PuertaInteractuable>(go);
        var so = new SerializedObject(puerta);
        so.FindProperty("llaveRequerida").objectReferenceValue = null;
        so.FindProperty("consumirLlave").boolValue = false;
        // Negativo y no positivo: PuertaInteractuable gira la raiz 'anguloAbierta' grados en Y, y con
        // +90 la hoja (su +X local) termina apuntando a -hacia, o sea barriendo hacia AFUERA, contra
        // el jugador que acaba de subir la escalera. Con -90 se abre hacia adentro de la tienda.
        so.FindProperty("anguloAbierta").floatValue = -90f;
        so.FindProperty("duracionApertura").floatValue = 1f;
        so.ApplyModifiedProperties();
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    static Transform Grupo(Transform padre, string nombre)
    {
        var go = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
        go.transform.SetParent(padre, false);
        return go.transform;
    }

    // Escala de un cubo apoyado contra una pared, expresada en los ejes de la habitacion en vez de
    // en X/Z del mundo: 'ejeLargo' y 'ejeFondo' son siempre ejes (+-X o +-Z), asi que alcanza con
    // repartir largo y fondo segun cual de los dos cae en cada componente.
    static Vector3 Dim(Vector3 ejeLargo, float largo, float alto, Vector3 ejeFondo, float fondo)
    {
        Vector3 l = Abs(ejeLargo);
        Vector3 f = Abs(ejeFondo);
        return new Vector3(l.x * largo + f.x * fondo, alto, l.z * largo + f.z * fondo);
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    static GameObject Pieza(Transform padre, string nombre, Vector3 posicion, Vector3 escala,
        string material, Color color, bool conCollider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
        go.name = nombre;
        go.transform.SetParent(padre, true);
        go.transform.position = posicion;
        go.transform.localScale = escala;

        if (!conCollider && go.TryGetComponent(out Collider collider)) Object.DestroyImmediate(collider);

        PonerMaterial(go, material, color, conSombras: false);

        // Todo el mobiliario es fijo: marcarlo para batching junta los draw calls, que es lo que
        // importa en una PC de pocos recursos (mismo criterio que AmbienteBuilder.Primitiva).
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static void PonerMaterial(GameObject go, string nombre, Color color, bool conSombras)
    {
        if (!go.TryGetComponent(out Renderer renderer)) return;

        renderer.sharedMaterial = ModeloUtils.MaterialDeColor(CarpetaMateriales, nombre, color);
        if (!conSombras)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }
    }
}
