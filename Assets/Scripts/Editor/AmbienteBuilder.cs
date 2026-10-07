using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Decoracion y atmosfera del laberinto. El mapa que genera MapaBuilder es correcto pero esteril:
// paredes y piso rectos, todo del mismo alto, nada que mirar. Esto le agrega escombros, cajas,
// columnas, matas y antorchas, mas cornisas arriba de algunos muros para romper la silueta.
//
// Menu: Between Metals > Ambiente
//
// Criterios, porque son los que explican casi todas las decisiones de abajo:
//
//   - TODO va bajo una raiz propia ("Ambiente"), igual que BarrerasBuilder y ProgresionBuilder: se
//     borra de una sin tocar el mapa, y volver a generar el mapa no lo arrastra.
//   - Siembra determinista (System.Random con semilla fija): el mismo mapa se decora siempre igual,
//     asi dos personas del equipo ven lo mismo y un bug se puede reproducir. Cambiar la semilla da
//     otra decoracion.
//   - Nada en el medio de un pasillo: cada prop se pega a una pared de su celda. Un laberinto
//     donde los adornos te trababan al correr seria peor que uno vacio.
//   - Zonas prohibidas alrededor del spawn, la salida, el comerciante y todo lo de "Progresion":
//     la decoracion no puede tapar una llave, una puerta ni el boton secreto.
//   - Lo chico (escombros, matas) va sin Collider y sin sombras: son cientos de objetos y no
//     tienen por que costar fisica ni iluminacion. Lo grande (cajas, columnas) si tiene Collider,
//     porque algo de 1 m que se atraviesa se nota.
//   - Todo se marca como BatchingStatic: static batching los junta en pocos draw calls, que es la
//     diferencia entre "ambiente" y "el juego se arrastra" en una PC de pocos recursos.
public static class AmbienteBuilder
{
    const string NombreRaiz = "Ambiente";
    const string CarpetaMateriales = "Assets/Materials/Ambiente";
    const string RutaPerfilAtmosfera = "Assets/Settings/AtmosferaProfile.asset";

    const int Semilla = 20261007; // mismo numero = misma decoracion

    // ---- Densidad: probabilidad por celda del laberinto (hay 13 x 11 = 143 celdas) ----
    const float ProbEscombros = 0.45f;
    const float ProbCaja = 0.16f;
    const float ProbColumna = 0.10f;
    const float ProbMata = 0.35f;
    const float ProbCornisa = 0.30f;   // por muro del laberinto, no por celda
    const int UnaAntorchaCadaNCeldas = 7;

    // ---- Medidas ----
    const float MargenPared = 0.55f;   // cuanto se separa un prop de la cara del muro
    const float AlturaAntorcha = 2.6f;
    const float RangoLuzAntorcha = 9f;

    static readonly Color ColorEscombro = new Color(0.33f, 0.31f, 0.28f);
    static readonly Color ColorCaja = new Color(0.36f, 0.25f, 0.14f);
    static readonly Color ColorColumna = new Color(0.42f, 0.41f, 0.39f);
    static readonly Color ColorMata = new Color(0.13f, 0.22f, 0.11f);
    static readonly Color ColorAntorcha = new Color(0.25f, 0.17f, 0.10f);
    static readonly Color ColorLuzAntorcha = new Color(1f, 0.64f, 0.29f);

    // Lugares que la decoracion no puede tapar, con el radio libre que se les deja.
    static readonly (string nombre, float radio)[] ZonasProhibidas =
    {
        ("Punto_Inicio", 8f),
        ("Punto_Salida", 10f),
        ("Punto_Comerciante", 6f),
        ("Punto_Zona_Spawn", 8f),
    };

    // ---------------------------------------------------------------
    // Decoracion
    // ---------------------------------------------------------------

    [MenuItem("Between Metals/Ambiente/Decorar mapa", priority = 700)]
    static void Decorar()
    {
        Transform raiz = EscanerMapa.BuscarRaiz();
        if (raiz == null)
        {
            Debug.LogError("AmbienteBuilder: no hay un objeto '" + EscanerMapa.NombreRaiz + "' en la escena. Genera el mapa primero.");
            return;
        }

        bool[,] paredes = EscanerMapa.LeerParedes(raiz);
        if (paredes == null)
        {
            Debug.LogError("AmbienteBuilder: no se encontraron muros bajo '" + EscanerMapa.NombreRaiz + "'.");
            return;
        }

        int grupoUndo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Decorar mapa");

        GameObject anterior = GameObject.Find(NombreRaiz);
        if (anterior != null) Undo.DestroyObjectImmediate(anterior);

        var contenedor = new GameObject(NombreRaiz);
        Undo.RegisterCreatedObjectUndo(contenedor, "Crear " + NombreRaiz);

        // Un grupo por tipo: en una Hierarchy con cientos de objetos, poder colapsar "Escombros" y
        // revisar solo "Antorchas" es la diferencia entre poder trabajar y no.
        Transform grupoEscombros = NuevoGrupo(contenedor.transform, "Escombros");
        Transform grupoCajas = NuevoGrupo(contenedor.transform, "Cajas");
        Transform grupoColumnas = NuevoGrupo(contenedor.transform, "Columnas");
        Transform grupoMatas = NuevoGrupo(contenedor.transform, "Matas");
        Transform grupoAntorchas = NuevoGrupo(contenedor.transform, "Antorchas");
        Transform grupoCornisas = NuevoGrupo(contenedor.transform, "Cornisas");

        var azar = new System.Random(Semilla);
        List<Vector3> prohibidas = LeerZonasProhibidas();

        int escombros = 0, cajas = 0, columnas = 0, matas = 0, antorchas = 0;
        int celdasSalteadas = 0;
        int contadorAntorchas = 0;

        for (int r = 0; r < MapaLayout.Filas; r++)
        {
            for (int c = 0; c < MapaLayout.Columnas; c++)
            {
                Vector3 centro = raiz.TransformPoint(MapaLayout.GrillaALocal(2 * r + 1, 2 * c + 1));

                if (EstaProhibida(centro, prohibidas))
                {
                    celdasSalteadas++;
                    continue;
                }

                // Lados de la celda que son pared: son los unicos contra los que se puede apoyar algo.
                List<Vector3> ladosConPared = LadosConPared(paredes, r, c);
                if (ladosConPared.Count == 0) continue;

                if (azar.NextDouble() < ProbEscombros) escombros += SembrarEscombros(grupoEscombros, centro, ladosConPared, azar);
                if (azar.NextDouble() < ProbMata) matas += SembrarMatas(grupoMatas, centro, ladosConPared, azar);
                if (azar.NextDouble() < ProbCaja) cajas += SembrarCaja(grupoCajas, centro, ladosConPared, azar);
                if (azar.NextDouble() < ProbColumna) columnas += SembrarColumna(grupoColumnas, centro, ladosConPared, azar);

                // Las antorchas no van por probabilidad sino cada N celdas decorables: son luces, y
                // la cantidad tiene que ser previsible para que el rendimiento tambien lo sea.
                if (++contadorAntorchas % UnaAntorchaCadaNCeldas == 0)
                {
                    antorchas += SembrarAntorcha(grupoAntorchas, centro, ladosConPared, azar);
                }
            }
        }

        int cornisas = SembrarCornisas(raiz, grupoCornisas, azar);

        Undo.CollapseUndoOperations(grupoUndo);
        Selection.activeGameObject = contenedor;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            "AmbienteBuilder: mapa decorado (semilla " + Semilla + ").\n" +
            "  Escombros: " + escombros + "   Matas: " + matas + "   Cajas: " + cajas + "\n" +
            "  Columnas: " + columnas + "   Cornisas: " + cornisas + "\n" +
            "  Antorchas (luces en tiempo real): " + antorchas + "\n" +
            "  Celdas salteadas por estar en una zona protegida: " + celdasSalteadas + "\n" +
            "  Si pesa: subi UnaAntorchaCadaNCeldas (menos luces) antes de tocar cualquier otra cosa.");
    }

    [MenuItem("Between Metals/Ambiente/Quitar decoracion", priority = 701)]
    static void Quitar()
    {
        GameObject raiz = GameObject.Find(NombreRaiz);
        if (raiz == null)
        {
            Debug.Log("AmbienteBuilder: no hay ningun objeto '" + NombreRaiz + "' en la escena.");
            return;
        }

        Undo.DestroyObjectImmediate(raiz);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // ---------------------------------------------------------------
    // Props
    // ---------------------------------------------------------------

    // Monton de pedazos chicos apoyados contra un muro, con rotacion y tamano sorteados. Sin
    // Collider: son decorativos y asi no se traba nadie con una piedra de 20 cm.
    static int SembrarEscombros(Transform padre, Vector3 centro, List<Vector3> lados, System.Random azar)
    {
        Vector3 lado = lados[azar.Next(lados.Count)];
        Vector3 contraPared = ContraLaPared(centro, lado, azar);
        int cuantos = azar.Next(2, 6);

        for (int i = 0; i < cuantos; i++)
        {
            float lado3d = Sortear(azar, 0.18f, 0.5f);
            Vector3 jitter = new Vector3(Sortear(azar, -0.9f, 0.9f), 0f, Sortear(azar, -0.9f, 0.9f));

            GameObject go = Primitiva(azar.NextDouble() < 0.5 ? PrimitiveType.Cube : PrimitiveType.Sphere,
                padre, "Escombro", contraPared + jitter + Vector3.up * lado3d * 0.35f,
                Vector3.one * lado3d, ColorEscombro, "Escombro", conCollider: false, conSombras: false);

            go.transform.localRotation = RotacionLibre(azar);
        }

        return cuantos;
    }

    // Matas de musgo/vegetacion: esferas achatadas y oscuras en el borde del piso. Tampoco llevan
    // Collider, por el mismo motivo que los escombros.
    static int SembrarMatas(Transform padre, Vector3 centro, List<Vector3> lados, System.Random azar)
    {
        Vector3 lado = lados[azar.Next(lados.Count)];
        Vector3 contraPared = ContraLaPared(centro, lado, azar);
        int cuantas = azar.Next(1, 4);

        for (int i = 0; i < cuantas; i++)
        {
            float ancho = Sortear(azar, 0.4f, 1f);
            float alto = Sortear(azar, 0.12f, 0.28f);
            Vector3 jitter = new Vector3(Sortear(azar, -1.1f, 1.1f), 0f, Sortear(azar, -1.1f, 1.1f));

            Primitiva(PrimitiveType.Sphere, padre, "Mata",
                contraPared + jitter, new Vector3(ancho, alto, ancho), ColorMata, "Mata",
                conCollider: false, conSombras: false);
        }

        return cuantas;
    }

    // Caja de madera apoyada contra un muro. Con Collider: mide cerca de un metro y atravesarla se
    // notaria. Solo gira en Y, porque una caja volcada necesitaria apoyarse de otra forma.
    static int SembrarCaja(Transform padre, Vector3 centro, List<Vector3> lados, System.Random azar)
    {
        Vector3 lado = lados[azar.Next(lados.Count)];
        float arista = Sortear(azar, 0.6f, 1f);

        GameObject go = Primitiva(PrimitiveType.Cube, padre, "Caja",
            ContraLaPared(centro, lado, azar) + Vector3.up * arista * 0.5f,
            Vector3.one * arista, ColorCaja, "Caja", conCollider: true, conSombras: true);

        go.transform.localRotation = Quaternion.Euler(0f, Sortear(azar, 0f, 360f), 0f);
        return 1;
    }

    // Columna pegada a un muro: es lo que mas rompe la sensacion de "pasillo de caja de zapatos",
    // porque corta la linea recta del muro a la altura de la vista.
    static int SembrarColumna(Transform padre, Vector3 centro, List<Vector3> lados, System.Random azar)
    {
        Vector3 lado = lados[azar.Next(lados.Count)];
        float diametro = Sortear(azar, 0.45f, 0.7f);
        float alto = Sortear(azar, 2.5f, MapaLayout.AltoMuro);

        // El cilindro primitivo mide 2 de alto, de ahi el /2 en la escala.
        Primitiva(PrimitiveType.Cylinder, padre, "Columna",
            ContraLaPared(centro, lado, azar) + Vector3.up * alto * 0.5f,
            new Vector3(diametro, alto * 0.5f, diametro), ColorColumna, "Columna",
            conCollider: true, conSombras: true);

        return 1;
    }

    // Antorcha: un soporte chico pegado al muro, a la altura de la cabeza, y una Point Light calida
    // con parpadeo (componente Antorcha). Sin Collider y sin sombras: es lo que la hace barata.
    static int SembrarAntorcha(Transform padre, Vector3 centro, List<Vector3> lados, System.Random azar)
    {
        Vector3 lado = lados[azar.Next(lados.Count)];

        // Pegada a la cara del muro, no a MargenPared como los props del piso.
        Vector3 posicion = centro + lado * (MapaLayout.AnchoCalle * 0.5f - 0.3f) + Vector3.up * AlturaAntorcha;

        var raizAntorcha = new GameObject("Antorcha");
        Undo.RegisterCreatedObjectUndo(raizAntorcha, "Crear Antorcha");
        raizAntorcha.transform.SetParent(padre, true);
        raizAntorcha.transform.position = posicion;

        Primitiva(PrimitiveType.Cube, raizAntorcha.transform, "Soporte", posicion,
            new Vector3(0.12f, 0.45f, 0.12f), ColorAntorcha, "Antorcha", conCollider: false, conSombras: false);

        var luzGO = new GameObject("Luz");
        Undo.RegisterCreatedObjectUndo(luzGO, "Crear Luz");
        luzGO.transform.SetParent(raizAntorcha.transform, true);
        // Separada del muro hacia el centro del pasillo, para que ilumine el pasillo y no la pared.
        luzGO.transform.position = posicion - lado * 0.4f + Vector3.up * 0.25f;

        Light luz = luzGO.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = ColorLuzAntorcha;
        luz.range = RangoLuzAntorcha;
        luz.intensity = 1.6f;
        // Sin sombras: una point light con sombras en tiempo real es lo mas caro que se puede
        // poner, y multiplicado por la cantidad de antorchas hundiria el frame rate.
        luz.shadows = LightShadows.None;

        luzGO.AddComponent<Antorcha>();

        return 1;
    }

    // Bloque arriba de un muro del laberinto, un poco mas angosto y de alto sorteado. No cambia el
    // alto del muro (bajarlo dejaria ver por encima y arruinaria el laberinto): lo que hace es
    // romper la linea recta del borde superior, que es lo que se lee como "todo igual".
    static int SembrarCornisas(Transform raiz, Transform padre, System.Random azar)
    {
        Transform grupo = raiz.Find("02_Muros_Laberinto");
        if (grupo == null) return 0;

        Material material = ModeloUtils.MaterialDeColor(CarpetaMateriales, "Cornisa", ColorColumna);
        int puestas = 0;

        var muros = new List<Transform>();
        foreach (Transform muro in grupo) muros.Add(muro);

        foreach (Transform muro in muros)
        {
            if (!muro.gameObject.activeSelf) continue;
            if (azar.NextDouble() >= ProbCornisa) continue;

            Vector3 tamano = muro.lossyScale;
            float alto = Sortear(azar, 0.4f, 1.6f);
            float encogido = Sortear(azar, 0.55f, 0.85f);

            // Se encoge solo el lado CORTO del muro (su grosor): encoger el largo dejaria huecos
            // visibles entre cornisas de muros contiguos.
            bool largoEnX = tamano.x >= tamano.z;
            var tamanoCornisa = new Vector3(
                largoEnX ? tamano.x : tamano.x * encogido,
                alto,
                largoEnX ? tamano.z * encogido : tamano.z);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(go, "Crear Cornisa");
            go.name = "Cornisa_" + muro.name;
            go.transform.SetParent(padre, true);
            go.transform.position = muro.position + Vector3.up * (tamano.y * 0.5f + alto * 0.5f);
            go.transform.localScale = tamanoCornisa;

            if (go.TryGetComponent(out Collider collider)) Object.DestroyImmediate(collider);
            if (go.TryGetComponent(out Renderer renderer)) renderer.sharedMaterial = material;

            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            puestas++;
        }

        return puestas;
    }

    // ---------------------------------------------------------------
    // Atmosfera: niebla y post-procesado
    // ---------------------------------------------------------------

    // Crea (o actualiza) un perfil de Volume propio con los ajustes de terror y se lo pone al
    // "Global Volume" de la escena. No se toca SampleSceneProfile, que es un sobrante de la
    // plantilla de Unity y lo podrian estar usando otras escenas.
    [MenuItem("Between Metals/Ambiente/Configurar atmosfera (niebla + post-procesado)", priority = 710)]
    static void ConfigurarAtmosfera()
    {
        int grupoUndo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Configurar atmosfera");

        VolumeProfile perfil = AsegurarPerfil();

        Volume volumen = Object.FindAnyObjectByType<Volume>();
        if (volumen == null)
        {
            var go = new GameObject("Global Volume");
            Undo.RegisterCreatedObjectUndo(go, "Crear Global Volume");
            volumen = go.AddComponent<Volume>();
            volumen.isGlobal = true;
        }

        Undo.RecordObject(volumen, "Asignar perfil de atmosfera");
        volumen.isGlobal = true;
        volumen.sharedProfile = perfil;
        EditorUtility.SetDirty(volumen);

        // El post-procesado no se ve si la camara no lo pide, y es lo primero que desconcierta
        // cuando "el Volume esta configurado y no pasa nada".
        string estadoCamara = ActivarPostProcesadoEnCamaras();

        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            "AmbienteBuilder: atmosfera configurada.\n" +
            "  Perfil: " + RutaPerfilAtmosfera + " (Vignette, Film Grain, Color Adjustments, Tonemapping y Bloom)\n" +
            "  " + estadoCamara + "\n" +
            "  La niebla y la oscuridad las sigue manejando EnvironmentManager en la escena:\n" +
            "  ahi estan 'densidadNiebla' (hoy 0.03), 'colorNiebla' y 'colorLuzAmbiental'.");
    }

    static VolumeProfile AsegurarPerfil()
    {
        var perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RutaPerfilAtmosfera);
        if (perfil == null)
        {
            ModeloUtils.AsegurarCarpeta("Assets/Settings");
            perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(perfil, RutaPerfilAtmosfera);
        }

        // Menos exposicion, menos saturacion y algo mas de contraste: el look "cueva humeda" se
        // consigue mas con esto que con la niebla, y no cuesta practicamente nada.
        var color = AsegurarComponente<ColorAdjustments>(perfil);
        Fijar(color.postExposure, -0.35f);
        Fijar(color.contrast, 12f);
        Fijar(color.saturation, -22f);

        // Vignette: oscurece los bordes. Es el efecto que mas "encierra" la imagen y es casi gratis.
        var vignette = AsegurarComponente<Vignette>(perfil);
        Fijar(vignette.intensity, 0.45f);
        Fijar(vignette.smoothness, 0.45f);

        // Grano: ensucia la imagen y disimula el banding de los degradados oscuros, que en una
        // escena casi negra se nota muchisimo.
        var grano = AsegurarComponente<FilmGrain>(perfil);
        Fijar(grano.type, FilmGrainLookup.Medium1);
        Fijar(grano.intensity, 0.35f);
        Fijar(grano.response, 0.8f);

        // Neutral: comprime las luces altas sin lavar los negros, que es lo que hace falta cuando
        // casi todo el cuadro esta en la parte baja del rango.
        var tono = AsegurarComponente<Tonemapping>(perfil);
        Fijar(tono.mode, TonemappingMode.Neutral);

        // Bloom con umbral alto: solo florecen las cosas que de verdad brillan (las balizas de las
        // puertas y las antorchas), no el pasillo entero. Es el efecto mas caro de los cinco: si
        // hace falta rendimiento, este es el primero que se apaga.
        var bloom = AsegurarComponente<Bloom>(perfil);
        Fijar(bloom.intensity, 0.55f);
        Fijar(bloom.threshold, 1.05f);
        Fijar(bloom.scatter, 0.6f);

        EditorUtility.SetDirty(perfil);
        AssetDatabase.SaveAssets();
        return perfil;
    }

    static T AsegurarComponente<T>(VolumeProfile perfil) where T : VolumeComponent
    {
        return perfil.Has<T>() && perfil.TryGet(out T existente) ? existente : perfil.Add<T>(true);
    }

    // Un parametro de Volume no se aplica si no esta "overrideState": con solo escribirle el valor
    // queda guardado pero inerte, que es el error clasico al armar un perfil por codigo.
    static void Fijar<T>(VolumeParameter<T> parametro, T valor)
    {
        parametro.overrideState = true;
        parametro.value = valor;
    }

    static string ActivarPostProcesadoEnCamaras()
    {
        int activadas = 0;
        foreach (Camera camara in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            UniversalAdditionalCameraData datos = camara.GetUniversalAdditionalCameraData();
            if (datos == null || datos.renderPostProcessing) continue;

            Undo.RecordObject(datos, "Activar post-procesado");
            datos.renderPostProcessing = true;
            EditorUtility.SetDirty(datos);
            activadas++;
        }

        return activadas > 0
            ? "Post-procesado activado en " + activadas + " camara(s)."
            : "Las camaras ya tenian el post-procesado activado.";
    }

    // ---------------------------------------------------------------
    // Utilidades
    // ---------------------------------------------------------------

    // Direcciones (en mundo) de los lados de la celda que son pared. Fila 0 es el norte (+Z) y
    // columna 0 el oeste (-X), igual que en MapaLayout.
    static List<Vector3> LadosConPared(bool[,] paredes, int r, int c)
    {
        int tf = 2 * r + 1;
        int tc = 2 * c + 1;
        var lados = new List<Vector3>(4);

        if (paredes[tf - 1, tc]) lados.Add(Vector3.forward);  // norte
        if (paredes[tf + 1, tc]) lados.Add(Vector3.back);     // sur
        if (paredes[tf, tc - 1]) lados.Add(Vector3.left);     // oeste
        if (paredes[tf, tc + 1]) lados.Add(Vector3.right);    // este

        return lados;
    }

    // Punto del piso pegado a la pared de ese lado, con un corrimiento sorteado a lo largo de ella:
    // asi los props no quedan todos alineados en el centro del muro.
    static Vector3 ContraLaPared(Vector3 centro, Vector3 lado, System.Random azar)
    {
        float distancia = MapaLayout.AnchoCalle * 0.5f - MargenPared;
        Vector3 aLoLargo = new Vector3(lado.z, 0f, lado.x); // perpendicular al lado, sobre el piso
        return centro + lado * distancia + aLoLargo * Sortear(azar, -distancia, distancia);
    }

    static bool EstaProhibida(Vector3 punto, List<Vector3> prohibidas)
    {
        for (int i = 0; i < prohibidas.Count; i++)
        {
            // El radio viaja en la componente Y, que en el piso no se usa para nada.
            float radio = prohibidas[i].y;
            Vector3 centro = new Vector3(prohibidas[i].x, punto.y, prohibidas[i].z);
            if (Vector3.Distance(punto, centro) < radio) return true;
        }
        return false;
    }

    // Puntos que no se pueden decorar: los marcadores del mapa mas todo lo que haya creado
    // ProgresionBuilder (llaves, puertas, boton, balizas), que es justo lo que no se puede tapar.
    static List<Vector3> LeerZonasProhibidas()
    {
        var lista = new List<Vector3>();

        foreach ((string nombre, float radio) in ZonasProhibidas)
        {
            GameObject go = GameObject.Find(nombre);
            if (go != null) lista.Add(new Vector3(go.transform.position.x, radio, go.transform.position.z));
        }

        GameObject progresion = GameObject.Find("Progresion");
        if (progresion != null)
        {
            // Solo los hijos directos: los nietos son piezas (Hoja, Cuerpo, Pulsador) que ya estan
            // cubiertas por el radio de su padre.
            foreach (Transform hijo in progresion.transform)
            {
                lista.Add(new Vector3(hijo.position.x, 6f, hijo.position.z));
            }
        }

        return lista;
    }

    static Transform NuevoGrupo(Transform padre, string nombre)
    {
        var go = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
        go.transform.SetParent(padre, false);
        return go.transform;
    }

    static GameObject Primitiva(PrimitiveType tipo, Transform padre, string nombre, Vector3 posicion,
        Vector3 escala, Color color, string material, bool conCollider, bool conSombras)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
        go.name = nombre;
        go.transform.SetParent(padre, true);
        go.transform.position = posicion;
        go.transform.localScale = escala;

        if (!conCollider && go.TryGetComponent(out Collider collider)) Object.DestroyImmediate(collider);

        if (go.TryGetComponent(out Renderer renderer))
        {
            renderer.sharedMaterial = ModeloUtils.MaterialDeColor(CarpetaMateriales, material, color);
            if (!conSombras)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        return go;
    }

    static Quaternion RotacionLibre(System.Random azar)
    {
        return Quaternion.Euler(Sortear(azar, 0f, 360f), Sortear(azar, 0f, 360f), Sortear(azar, 0f, 360f));
    }

    static float Sortear(System.Random azar, float minimo, float maximo)
    {
        return minimo + (float)azar.NextDouble() * (maximo - minimo);
    }
}
