using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Coloca en la escena abierta toda la progresion de llaves, puertas, boton secreto y economia,
// sobre el laberinto que ya genero MapaBuilder. Mismo criterio que BarrerasBuilder: no regenera el
// mapa, lee el que hay; todo lo que crea queda bajo una raiz propia ("Progresion") para poder
// borrarlo de una sin tocar nada mas.
//
// Menu: Between Metals > Progresion > Instalar progresion completa
//
// Las posiciones se escriben en coordenadas de la grilla de paredes de MapaLayout, que es el mismo
// sistema con el que MapaBuilder nombra los muros (Muro_..._F<fila>-<fila>_C<col>-<col>, 1-based)
// y con el que BarrerasBuilder nombra las barreras (Barrera_NN_T<fila>-<col>, 0-based). Aca se usa
// 0-based, igual que las barreras: fila 1 es la celda mas al NORTE, columna 1 la mas al OESTE.
//
// La cadena que arma, sobre el laberinto de semilla 424242 que hay hoy en Prototype.unity:
//
//   1. Llave_Interior: fondo del callejon sin salida del extremo sureste, celda (12,10).
//      Se puede recoger desde el principio, sin pasar por ninguna puerta.
//   2. Puerta_01_Interior: hueco (2,21), al sur de la celda del noreste. Pide la Llave_Interior
//      y la gasta. Al abrirse da acceso a la Llave_Salida.
//   3. Muro_Secreto: la barrera dinamica que estaba en el hueco (1,20) pasa a ser un MuroSecreto
//      cerrado, la SEGUNDA entrada a esa misma celda. Lo abre el boton escondido en la habitacion
//      del comerciante, asi que hay dos caminos a la llave de la salida: la puerta con llave, o
//      el atajo secreto.
//   4. Llave_Salida: dentro de la celda (0,10), que con la puerta y el muro cerrados queda
//      completamente sellada (los otros dos lados son el muro perimetral).
//   5. Puerta_02_Salida: en el hueco del perimetro oeste (11,0), el que da a Punto_Salida (donde
//      ExitTrigger termina la partida). Pide la Llave_Salida.
public static class ProgresionBuilder
{
    const string NombreRaiz = "Progresion";
    const string CarpetaItems = "Assets/Items";
    // Tiene que coincidir con el nombre de archivo que usa MenuPrincipalBuilder.
    const string NombreEscenaMenuPrincipal = "MenuPrincipal";
    const string CarpetaMateriales = "Assets/Materials/Progresion";

    // ---- Contrato con los assets de Assets/Items ----
    const string IdLlaveInterior = "llave_interior";
    const string IdLlaveSalida = "llave_salida";
    const string ArchivoLlaveInterior = "Llave_Interior";
    const string ArchivoLlaveSalida = "Llave_Salida";
    const string ArchivoArma = "Arma";

    // ---- Economia ----
    const int PrecioArma = 50;
    const int OroPorEnemigo = 20;
    const float VidaEnemigo = 200f;

    // ---- Modelos 3D ----
    // Los FBX de Tripo llegan sin saber en que unidades ni con que eje para adelante, asi que no se
    // usan directo: ProgresionBuilder arma con ellos un prefab medido y orientado (ver ModeloUtils).
    // Si alguno falta, el builder sigue adelante con las primitivas de siempre y lo avisa.
    const string FbxArma = "Assets/TripoModels/ornate_dagger_3d_model/ornate_dagger_3d_model.fbx";
    const string FbxLlave = "Assets/TripoModels/antique_key_3d_model/antique_key_3d_model.fbx";
    const string CarpetaPrefabs = "Assets/Prefabs/Items";
    const string PrefabArma = CarpetaPrefabs + "/Arma.prefab";
    const string PrefabLlave = CarpetaPrefabs + "/Llave.prefab";

    const float LargoArma = 0.45f;  // daga vista en primera persona, de punta a pomo
    const float LargoLlave = 0.3f;  // llave antigua apoyada en el piso

    // ---- Ubicaciones, en tiles de la grilla de MapaLayout (fila, columna), 0-based ----
    //
    // La cadena del nivel, toda sobre la esquina NORESTE del laberinto:
    //
    //   1. El jugador recoge la Llave_Interior en la zona segura de inicio.
    //   2. Llega al comerciante y pulsa el boton escondido en su habitacion.
    //   3. Eso hunde el Muro_Secreto del hueco (1,20), que es la UNICA entrada a la celda (0,10)
    //      (el hueco (2,21), su otra salida, queda tapado por un muro nuevo: TileSello).
    //   4. Desde esa celda llega a Puerta_01, metida en un hueco abierto a proposito en el muro
    //      perimetral norte, y la abre con la Llave_Interior.
    //   5. Detras hay una habitacion cerrada -la Boveda- construida afuera del laberinto, con la
    //      Llave_Salida adentro. No tiene otra entrada que esa puerta.
    //   6. Con esa llave abre Puerta_02, en el hueco del perimetro oeste que da a Punto_Salida.
    static readonly Vector2Int TileMuroSecreto = new Vector2Int(1, 20);     // hueco que hoy ocupa Barrera_10_T01-20
    static readonly Vector2Int TileSello = new Vector2Int(2, 21);           // se tapa con un muro nuevo
    static readonly Vector2Int TilePuertaInterior = new Vector2Int(0, 21);  // hueco que se abre en el perimetro norte
    static readonly Vector2Int TilePuertaSalida = new Vector2Int(11, 0);    // hueco del perimetro oeste, el de la salida

    // ---- Boveda: la habitacion cerrada detras de Puerta_01 ----
    // Se construye como geometria nueva pegada al borde norte del laberinto, mismo criterio que
    // MapaBuilder con la habitacion del comerciante: asi no hay que destrozar pasillos del
    // laberinto, y la habitacion sobrevive a volver a generar el mapa.
    //
    // Su pared sur es el propio muro perimetral del laberinto, que ya mide 6 m de fondo y 8 de
    // alto: el unico hueco es el de la puerta. Por eso solo hacen falta piso y tres paredes.
    const float AnchoBoveda = 12f;   // interior, eje X
    const float FondoBoveda = 12f;   // interior, eje Z (hacia el norte, saliendo del laberinto)
    const float GrosorParedBoveda = 0.4f;
    const float GrosorPisoBoveda = 0.2f;

    // ---- Medidas ----
    const float AnchoHoja = MapaLayout.AnchoCalle - 0.2f; // un poco mas angosta que el hueco, para que no raspe los muros
    const float GrosorHoja = 0.4f;
    const float LadoLlave = 0.4f;        // lado del cubo de respaldo, si falta el modelo
    const float AlturaLlave = 0.12f;     // despegada del piso lo justo para que no haga z-fighting
    const float LadoColliderLlave = 0.5f; // minimo del Collider: una llave es muy fina para apuntarle con el raycast
    const float LadoBoton = 0.45f;
    const float AltoBoton = 0.12f;

    // ---- Balizas (haz vertical estilo beacon) ----
    // Un color por puerta, para que el jugador aprenda a leerlos de lejos: ambar = puerta con
    // llave, verde = la salida.
    static readonly Color ColorBalizaInterior = new Color(1f, 0.72f, 0.25f);
    static readonly Color ColorBalizaSalida = new Color(0.35f, 1f, 0.5f);

    // Habitacion del comerciante: el piso esta a y = 4 (MapaBuilder.AlturaHabitacion) y el
    // comerciante en el centro. El boton va en el piso, a dos metros de el: cerca como para que
    // sea "su" boton, lo bastante aparte como para que el raycast de PlayerInteraction no se
    // confunda entre los dos (el del comerciante esta a la altura de la cabeza, este en el suelo).
    const float DesplazamientoBoton = 2f;

    [MenuItem("Between Metals/Progresion/Instalar progresion completa", priority = 500)]
    static void Instalar()
    {
        Transform raiz = EscanerMapa.BuscarRaiz();
        if (raiz == null)
        {
            Debug.LogError("ProgresionBuilder: no hay un objeto '" + EscanerMapa.NombreRaiz + "' en la escena. Genera el mapa primero (Between Metals > Mapa > Generar mapa).");
            return;
        }

        bool[,] paredes = EscanerMapa.LeerParedes(raiz);
        if (paredes == null)
        {
            Debug.LogError("ProgresionBuilder: no se encontraron muros bajo '" + EscanerMapa.NombreRaiz + "'.");
            return;
        }

        var grafo = new GrafoLaberinto(paredes);

        int grupoUndo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Instalar progresion");

        // ---- Assets de items ----
        ItemData llaveInterior = AsegurarItem(ArchivoLlaveInterior, IdLlaveInterior, "Llave Interior",
            "Abre la puerta que lleva a la llave de la salida.");
        ItemData llaveSalida = AsegurarItem(ArchivoLlaveSalida, IdLlaveSalida, "Llave de la Salida",
            "Abre la puerta de la salida del laberinto.");
        ItemData arma = AsegurarItem(ArchivoArma, EquipoJugador.IdArma, "Arma",
            "Se equipa sola en la mano. Cada golpe pega mucho mas que a mano limpia.");

        // ---- Prefabs de los modelos 3D ----
        // El arma apunta a +Z (adelante, el eje en el que la cuelga EquipoJugador de la camara);
        // la llave a +X, acostada, porque va apoyada en el piso.
        GameObject prefabArma = AsegurarPrefabDeModelo(FbxArma, PrefabArma, "Arma", LargoArma, ModeloUtils.Eje.Z, conCollider: false);
        GameObject prefabLlave = AsegurarPrefabDeModelo(FbxLlave, PrefabLlave, "Llave", LargoLlave, ModeloUtils.Eje.X, conCollider: true);

        // ---- Raiz limpia ----
        GameObject anterior = GameObject.Find(NombreRaiz);
        if (anterior != null) Undo.DestroyObjectImmediate(anterior);

        var contenedor = new GameObject(NombreRaiz);
        Undo.RegisterCreatedObjectUndo(contenedor, "Crear " + NombreRaiz);

        // Toda la obra sobre el mapa (muros nuevos y reemplazos del perimetro) vive en un grupo
        // propio, para que se distinga de un pentazo del laberinto original.
        var obra = new GameObject("Obra_Mapa");
        Undo.RegisterCreatedObjectUndo(obra, "Crear Obra_Mapa");
        obra.transform.SetParent(contenedor.transform, false);

        Material materialMuro = MaterialDeMuro(raiz);

        // ---- Muro secreto (reciclando la barrera dinamica que ya esta en ese hueco) ----
        MuroSecreto muro = ConvertirBarreraEnMuroSecreto(raiz, contenedor.transform, grafo);

        // ---- Obra sobre el laberinto ----
        // El sello es lo que convierte al muro secreto en una puerta de verdad: sin el, la celda
        // (0,10) se alcanza igual por el pasillo del este y el boton no haria falta para nada.
        bool sellado = SellarHueco(raiz, obra.transform, grafo, materialMuro);

        // Hueco en el perimetro norte para la puerta de la boveda.
        int perimetroPartido = AbrirHuecoEnPerimetro(raiz, obra.transform, TilePuertaInterior, materialMuro);

        Vector3 centroBoveda = ConstruirBoveda(raiz, obra.transform, materialMuro);

        // ---- Puertas ----
        // La interior no se valida contra el grafo: su hueco lo acabamos de abrir nosotros, asi que
        // el mapa de paredes que se leyo al arrancar todavia dice que ahi hay muro.
        PuertaInteractuable puertaInterior = CrearPuerta(raiz, contenedor.transform, grafo, paredes,
            "Puerta_01_Interior", TilePuertaInterior, llaveInterior, Validacion.Ninguna);

        PuertaInteractuable puertaSalida = CrearPuerta(raiz, contenedor.transform, grafo, paredes,
            "Puerta_02_Salida", TilePuertaSalida, llaveSalida, Validacion.SinMuro);

        // ---- Llaves ----
        // La primera queda en el piso de la zona segura de inicio, a la vista desde el spawn; la
        // segunda, dentro de la celda que sella Puerta_01_Interior.
        Vector3 posicionSpawn = ResolverPuntoDeSpawn(out string origenSpawn);
        CrearLlave(contenedor.transform, "Llave_Interior", posicionSpawn, llaveInterior, prefabLlave);
        CrearLlave(contenedor.transform, "Llave_Salida", centroBoveda + Vector3.up * AlturaLlave, llaveSalida, prefabLlave);

        // ---- Balizas ----
        // Van en el centro del hueco de cada puerta, sueltas bajo "Progresion" y NO colgadas de la
        // puerta: la puerta gira al abrirse y se llevaria el haz de paseo hasta dentro de un muro.
        CrearBaliza(contenedor.transform, "Baliza_Puerta_01", CentroDeTile(raiz, TilePuertaInterior), ColorBalizaInterior, puertaInterior);
        CrearBaliza(contenedor.transform, "Baliza_Puerta_Salida", CentroDeTile(raiz, TilePuertaSalida), ColorBalizaSalida, puertaSalida);

        // ---- Boton secreto ----
        CrearBoton(contenedor.transform, muro);

        // ---- Economia y jugador ----
        int enemigos = ConfigurarEnemigos();
        bool tienda = AgregarArmaALaTienda(arma);
        bool equipo = AsegurarEquipoJugador(prefabArma);
        string barra = ConfigurarBarraRapida(arma);
        bool menu = ConfigurarMenuComoPausa();

        Undo.CollapseUndoOperations(grupoUndo);
        Selection.activeGameObject = contenedor;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log(
            "ProgresionBuilder: listo.\n" +
            "  Muro secreto: " + (muro != null ? muro.name : "NO se encontro la barrera del hueco " + Tile(TileMuroSecreto)) + "\n" +
            "  Puertas: " + (puertaInterior != null ? "Puerta_01_Interior " : "") + (puertaSalida != null ? "Puerta_02_Salida" : "") + "\n" +
            "  Modelos: arma " + (prefabArma != null ? PrefabArma : "cubo placeholder") +
                ", llaves " + (prefabLlave != null ? PrefabLlave : "cubo placeholder") + "\n" +
            "  Obra: hueco " + Tile(TileSello) + " sellado: " + (sellado ? "si" : "NO") +
                ", muros del perimetro norte partidos: " + perimetroPartido + "\n" +
            "  Boveda construida en " + centroBoveda.ToString("F1") + " (" + AnchoBoveda + " x " + FondoBoveda + " m)\n" +
            "  Llave_Interior puesta en " + posicionSpawn.ToString("F1") + " (" + origenSpawn + ")\n" +
            "  Llave_Salida puesta dentro de la boveda\n" +
            "  Balizas: haz ambar en Puerta_01_Interior y haz verde en Puerta_02_Salida\n" +
            "  Barra rapida: " + barra + "\n" +
            "  Enemigos configurados (vida " + VidaEnemigo + ", botin " + OroPorEnemigo + " de oro): " + enemigos + "\n" +
            "  Arma en la tienda a " + PrecioArma + " de oro: " + (tienda ? "si" : "NO (no hay ShopManager en la escena)") + "\n" +
            "  EquipoJugador: " + (equipo ? "si" : "NO (no hay Inventory en la escena)") + "\n" +
            "  Menu pasado a modo Pausa: " + (menu ? "si" : "NO (no hay Menu en la escena)"));
    }

    [MenuItem("Between Metals/Progresion/Quitar progresion", priority = 501)]
    static void Quitar()
    {
        GameObject raiz = GameObject.Find(NombreRaiz);
        if (raiz == null)
        {
            Debug.Log("ProgresionBuilder: no hay ningun objeto '" + NombreRaiz + "' en la escena.");
            return;
        }

        // Los muros del perimetro que se partieron para abrir el hueco de la puerta quedaron
        // desactivados, no destruidos: se vuelven a prender todos antes de borrar la raiz. Prender
        // todo (y no solo los que tocamos) no necesita llevar registro de nada y deja exactamente
        // el estado en el que los deja MapaBuilder, que es con todos activos.
        int reactivados = ReactivarMurosDelMapa();

        // El muro secreto es la barrera dinamica reciclada: se avisa, porque volver a correr
        // BarrerasBuilder es la forma de recuperarla.
        Debug.Log("ProgresionBuilder: se borra '" + NombreRaiz + "' y se reactivan " + reactivados +
            " muros del mapa. Si querés recuperar la barrera dinamica de ese hueco, volvé a correr " +
            "Between Metals > Barreras > Colocar barreras dinamicas.");

        Undo.DestroyObjectImmediate(raiz);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // ---------------------------------------------------------------
    // Items
    // ---------------------------------------------------------------

    // Crea el ItemData si no existe y, si ya existe, solo completa lo que esta vacio: un asset que
    // el equipo ya ajusto a mano (icono, descripcion) no se pisa al volver a correr el builder.
    static ItemData AsegurarItem(string archivo, string id, string nombre, string descripcion)
    {
        string ruta = CarpetaItems + "/" + archivo + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(ruta);

        if (item == null)
        {
            ModeloUtils.AsegurarCarpeta(CarpetaItems);

            item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = id;
            item.itemName = nombre;
            item.description = descripcion;
            // Las llaves y el arma no se gastan al "usarlas" desde el inventario: la llave la
            // consume la puerta al abrirse y el arma vive equipada mientras este en el inventario.
            item.consumeOnUse = false;

            AssetDatabase.CreateAsset(item, ruta);
            AssetDatabase.SaveAssets();
            Debug.Log("ProgresionBuilder: creado " + ruta);
            return item;
        }

        bool cambio = false;
        if (string.IsNullOrWhiteSpace(item.itemId)) { item.itemId = id; cambio = true; }
        if (string.IsNullOrWhiteSpace(item.itemName)) { item.itemName = nombre; cambio = true; }
        if (string.IsNullOrWhiteSpace(item.description)) { item.description = descripcion; cambio = true; }

        if (item.itemId != id)
        {
            Debug.LogWarning("ProgresionBuilder: " + ruta + " tiene itemId '" + item.itemId + "' y se esperaba '" + id + "'. No se toca, pero la puerta (o el equipamiento del arma) no va a reconocerlo.", item);
        }

        if (cambio)
        {
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
        }
        return item;
    }

    // ---------------------------------------------------------------
    // Muro secreto
    // ---------------------------------------------------------------

    // Recicla la BarreraDinamica que ya esta en el hueco objetivo en vez de construir un muro
    // nuevo: la geometria (el hijo "Cuerpo" de 6x8x6, con su material igual al de los muros del
    // laberinto) ya esta puesta y alineada, y asi el muro secreto se ve exactamente igual que
    // cualquier otra pared. Se la saca de 'Barreras_Dinamicas' para que GestorBarreras deje de
    // contarla y para que "Quitar barreras dinamicas" no se la lleve.
    static MuroSecreto ConvertirBarreraEnMuroSecreto(Transform raiz, Transform padre, GrafoLaberinto grafo)
    {
        BarreraDinamica objetivo = null;
        foreach (BarreraDinamica barrera in Object.FindObjectsByType<BarreraDinamica>(FindObjectsInactive.Include))
        {
            EscanerMapa.TileDe(raiz, barrera.transform.position, out int tf, out int tc);
            if (tf == TileMuroSecreto.x && tc == TileMuroSecreto.y)
            {
                objetivo = barrera;
                break;
            }
        }

        if (objetivo == null)
        {
            Debug.LogError("ProgresionBuilder: no hay ninguna BarreraDinamica en el hueco " + Tile(TileMuroSecreto) +
                ". Corré primero Between Metals > Barreras > Colocar barreras dinamicas, o cambiá TileMuroSecreto en ProgresionBuilder.");
            return null;
        }

        if (!grafo.EsHuecoAbierto(grafo.IdHueco(TileMuroSecreto.x, TileMuroSecreto.y)))
        {
            Debug.LogWarning("ProgresionBuilder: el tile " + Tile(TileMuroSecreto) + " no es un hueco abierto del laberinto; el muro secreto no va a desbloquear nada.");
        }

        GameObject go = objetivo.gameObject;
        Undo.RecordObject(go.transform, "Mover muro secreto");

        Undo.DestroyObjectImmediate(objetivo);

        go.name = "Muro_Secreto_T" + TileMuroSecreto.x.ToString("00") + "-" + TileMuroSecreto.y.ToString("00");
        Undo.SetTransformParent(go.transform, padre, "Reparentar muro secreto");

        var muro = Undo.AddComponent<MuroSecreto>(go);

        // El cuerpo lo dejo BarrerasBuilder escondido bajo el piso (las barreras arrancan
        // abiertas). El muro secreto arranca cerrado: se sube ahora para que en el Editor se vea
        // como lo va a encontrar el jugador, sin tener que entrar a Play.
        Transform cuerpo = go.transform.Find("Cuerpo");
        if (cuerpo != null)
        {
            Undo.RecordObject(cuerpo, "Subir cuerpo del muro secreto");
            cuerpo.localPosition = new Vector3(0f, MapaLayout.AltoMuro * 0.5f, 0f);
        }

        return muro;
    }

    // ---------------------------------------------------------------
    // Obra sobre el mapa: sello, hueco en el perimetro y boveda
    // ---------------------------------------------------------------

    // Tapa el hueco TileSello con un muro nuevo del tamano de un tile. Es lo que deja la celda
    // (0,10) accesible unicamente por el muro secreto: sin esto, el boton del comerciante no
    // desbloquearia nada, porque a esa celda se llega igual por el pasillo del este.
    //
    // El muro va bajo "Progresion/Obra_Mapa" y no dentro de 02_Muros_Laberinto a proposito: asi
    // EscanerMapa (que solo lee los dos grupos del mapa) sigue viendo el laberinto original, y
    // GestorBarreras hace sus cuentas de alcanzabilidad sobre un grafo mas permisivo que la
    // realidad. Es el lado seguro del error: nunca va a cerrar una barrera creyendo que hay un
    // camino que en realidad tapamos nosotros, porque el camino que tapamos no esta en su grafo.
    static bool SellarHueco(Transform raiz, Transform padre, GrafoLaberinto grafo, Material material)
    {
        if (!grafo.EsHuecoAbierto(grafo.IdHueco(TileSello.x, TileSello.y)))
        {
            Debug.LogWarning("ProgresionBuilder: el tile " + Tile(TileSello) + " ya no es un hueco abierto; no hace falta sellarlo.");
            return false;
        }

        CrearMuroDeTiles(raiz, padre, "Muro_Sello_T" + TileSello.x.ToString("00") + "-" + TileSello.y.ToString("00"),
            TileSello.x, TileSello.x, TileSello.y, TileSello.y, material);
        return true;
    }

    // Abre un hueco del tamano de un tile en el muro perimetral: desactiva la pieza (o piezas) que
    // lo cubren y las reemplaza por los tramos que quedan a cada lado. Es la misma solucion que usa
    // MapaBuilder para la entrada de la habitacion del comerciante (Muro_Habitacion_Entrada_A/_B).
    //
    // Las piezas originales se desactivan, no se destruyen: "Quitar progresion" las vuelve a
    // prender y el perimetro queda como lo dejo MapaBuilder.
    static int AbrirHuecoEnPerimetro(Transform raiz, Transform padre, Vector2Int tile, Material material)
    {
        Transform grupo = raiz.Find("01_Muros_Perimetro");
        if (grupo == null)
        {
            Debug.LogError("ProgresionBuilder: no existe '01_Muros_Perimetro'; no se puede abrir el hueco de la puerta.");
            return 0;
        }

        int partidos = 0;

        // Copia de los hijos antes de tocar nada: se van a agregar objetos nuevos mientras se
        // recorre, y hacerlo sobre el Transform vivo saltearia o repetiria piezas.
        var piezas = new List<Transform>();
        foreach (Transform pieza in grupo) piezas.Add(pieza);

        foreach (Transform pieza in piezas)
        {
            if (!pieza.gameObject.activeSelf) continue;
            if (!RangoDeTiles(raiz, pieza, out int r0, out int r1, out int c0, out int c1)) continue;
            if (tile.x < r0 || tile.x > r1 || tile.y < c0 || tile.y > c1) continue;

            // Solo se sabe partir en una direccion: a lo largo de columnas si la pieza es una tira
            // horizontal, o de filas si es vertical. El perimetro de MapaBuilder siempre es asi.
            bool tiraHorizontal = c1 > c0;

            Undo.RecordObject(pieza.gameObject, "Desactivar " + pieza.name);
            pieza.gameObject.SetActive(false);
            partidos++;

            if (tiraHorizontal)
            {
                if (c0 <= tile.y - 1) CrearMuroDeTiles(raiz, padre, pieza.name + "_A", r0, r1, c0, tile.y - 1, material);
                if (tile.y + 1 <= c1) CrearMuroDeTiles(raiz, padre, pieza.name + "_B", r0, r1, tile.y + 1, c1, material);
            }
            else
            {
                if (r0 <= tile.x - 1) CrearMuroDeTiles(raiz, padre, pieza.name + "_A", r0, tile.x - 1, c0, c1, material);
                if (tile.x + 1 <= r1) CrearMuroDeTiles(raiz, padre, pieza.name + "_B", tile.x + 1, r1, c0, c1, material);
            }
        }

        if (partidos == 0)
        {
            Debug.LogWarning("ProgresionBuilder: ninguna pieza del perimetro cubre el tile " + Tile(tile) +
                "; puede que el hueco ya estuviera abierto.");
        }

        return partidos;
    }

    // Habitacion cerrada pegada al borde norte del laberinto, justo detras del hueco de la puerta.
    // Su pared sur es el propio muro perimetral (6 m de fondo, 8 de alto), asi que solo se
    // construyen piso y tres paredes: la unica abertura es la puerta.
    // Devuelve el centro del piso, que es donde va la Llave_Salida.
    static Vector3 ConstruirBoveda(Transform raiz, Transform padre, Material material)
    {
        var grupo = new GameObject("Boveda");
        Undo.RegisterCreatedObjectUndo(grupo, "Crear Boveda");
        grupo.transform.SetParent(padre, false);

        Vector3 hueco = CentroDeTile(raiz, TilePuertaInterior);

        // Cara externa del perimetro: el tile de la puerta mide un AnchoCalle de fondo, asi que su
        // borde norte esta a medio tile del centro.
        float zSur = hueco.z + MapaLayout.AnchoCalle * 0.5f;
        float zNorte = zSur + FondoBoveda;
        float g = GrosorParedBoveda;
        float anchoTotal = AnchoBoveda + 2f * g;
        float medioAncho = AnchoBoveda * 0.5f;
        float alto = MapaLayout.AltoMuro;

        // Piso: arranca justo en la cara externa del perimetro para no superponerse con
        // Suelo_Plano_Principal (que llega exactamente hasta ahi) y evitar el z-fighting de dos
        // superficies coplanares.
        CrearBloque(grupo.transform, "Boveda_Piso",
            new Vector3(hueco.x, -GrosorPisoBoveda * 0.5f, (zSur + zNorte + g) * 0.5f),
            new Vector3(anchoTotal, GrosorPisoBoveda, FondoBoveda + g),
            material);

        CrearBloque(grupo.transform, "Boveda_Pared_Norte",
            new Vector3(hueco.x, alto * 0.5f, zNorte + g * 0.5f),
            new Vector3(anchoTotal, alto, g),
            material);

        CrearBloque(grupo.transform, "Boveda_Pared_Oeste",
            new Vector3(hueco.x - medioAncho - g * 0.5f, alto * 0.5f, (zSur + zNorte) * 0.5f),
            new Vector3(g, alto, FondoBoveda),
            material);

        CrearBloque(grupo.transform, "Boveda_Pared_Este",
            new Vector3(hueco.x + medioAncho + g * 0.5f, alto * 0.5f, (zSur + zNorte) * 0.5f),
            new Vector3(g, alto, FondoBoveda),
            material);

        return new Vector3(hueco.x, 0f, (zSur + zNorte) * 0.5f);
    }

    // Muro que cubre un rectangulo de tiles de la grilla, con la misma convencion que MapaBuilder:
    // un cubo del alto de los muros, apoyado en el piso, centrado en el rectangulo.
    static GameObject CrearMuroDeTiles(Transform raiz, Transform padre, string nombre, int r0, int r1, int c0, int c1, Material material)
    {
        Vector3 centro = raiz.TransformPoint(MapaLayout.GrillaALocal((r0 + r1) * 0.5f, (c0 + c1) * 0.5f));
        var tamano = new Vector3(
            (c1 - c0 + 1) * MapaLayout.AnchoCalle,
            MapaLayout.AltoMuro,
            (r1 - r0 + 1) * MapaLayout.AnchoCalle);

        return CrearBloque(padre, nombre, centro + Vector3.up * MapaLayout.AltoMuro * 0.5f, tamano, material);
    }

    // Cubo con su BoxCollider (el que trae la primitiva), que es lo que hace que bloquee al jugador
    // y que NavMeshRuntimeBuilder lo hornee como obstaculo para los enemigos.
    static GameObject CrearBloque(Transform padre, string nombre, Vector3 centro, Vector3 tamano, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
        go.name = nombre;
        go.transform.SetParent(padre, true);
        go.transform.position = centro;
        go.transform.localScale = tamano;

        if (material != null && go.TryGetComponent(out Renderer renderer)) renderer.sharedMaterial = material;

        return go;
    }

    // Rango de tiles que cubre un muro, deducido de su posicion y su escala. Misma cuenta que hace
    // EscanerMapa.LeerParedes para reconstruir la grilla.
    static bool RangoDeTiles(Transform raiz, Transform muro, out int r0, out int r1, out int c0, out int c1)
    {
        r0 = r1 = c0 = c1 = 0;

        MapaLayout.LocalAGrilla(raiz.InverseTransformPoint(muro.position), out float fila, out float columna);

        Vector3 tamano = muro.lossyScale;
        float tilesZ = tamano.z / MapaLayout.AnchoCalle;
        float tilesX = tamano.x / MapaLayout.AnchoCalle;
        if (tilesZ < 0.5f || tilesX < 0.5f) return false;

        r0 = Mathf.RoundToInt(fila - (tilesZ - 1f) / 2f);
        r1 = Mathf.RoundToInt(fila + (tilesZ - 1f) / 2f);
        c0 = Mathf.RoundToInt(columna - (tilesX - 1f) / 2f);
        c1 = Mathf.RoundToInt(columna + (tilesX - 1f) / 2f);
        return true;
    }

    // Vuelve a activar todos los muros de los dos grupos del mapa. Es la inversa de lo que hace
    // AbrirHuecoEnPerimetro, sin necesidad de llevar registro: MapaBuilder los deja todos activos.
    static int ReactivarMurosDelMapa()
    {
        Transform raiz = EscanerMapa.BuscarRaiz();
        if (raiz == null) return 0;

        int reactivados = 0;
        foreach (string nombreGrupo in new[] { "01_Muros_Perimetro", "02_Muros_Laberinto" })
        {
            Transform grupo = raiz.Find(nombreGrupo);
            if (grupo == null) continue;

            foreach (Transform muro in grupo)
            {
                if (muro.gameObject.activeSelf) continue;

                Undo.RecordObject(muro.gameObject, "Reactivar " + muro.name);
                muro.gameObject.SetActive(true);
                reactivados++;
            }
        }

        return reactivados;
    }

    // Mismo material que los muros del laberinto, para que lo que agregamos no se note distinto.
    // Igual que BarrerasBuilder.MaterialDeMuro.
    static Material MaterialDeMuro(Transform raiz)
    {
        Transform grupo = raiz.Find("02_Muros_Laberinto");
        if (grupo == null || grupo.childCount == 0) return null;

        Renderer renderer = grupo.GetChild(0).GetComponent<Renderer>();
        return renderer != null ? renderer.sharedMaterial : null;
    }

    // ---------------------------------------------------------------
    // Puertas
    // ---------------------------------------------------------------

    // tile par en filas  -> el muro corre de este a oeste y el pasillo pasa norte-sur: la hoja
    //                       barre sobre el eje X y la bisagra va en el borde oeste del hueco.
    // tile impar en filas -> el muro corre de norte a sur: la hoja barre sobre Z y la bisagra va
    //                       en el borde sur. Se consigue girando la raiz -90 en Y, asi el +X local
    //                       (donde cuelga la hoja) apunta al norte.
    // Que se le exige al tile donde va la puerta:
    //   Grafo   -> tiene que ser un hueco abierto entre dos celdas del laberinto.
    //   SinMuro -> basta con que no haya muro (sirve para los huecos del perimetro, que no son
    //              "huecos entre celdas" y por eso el grafo no los reconoce).
    //   Ninguna -> no se chequea nada, porque el hueco lo abrio este mismo builder y el mapa de
    //              paredes que se leyo al arrancar ya quedo viejo.
    enum Validacion { Grafo, SinMuro, Ninguna }

    static PuertaInteractuable CrearPuerta(Transform raiz, Transform padre, GrafoLaberinto grafo, bool[,] paredes,
        string nombre, Vector2Int tile, ItemData llave, Validacion validacion)
    {
        if (tile.x < 0 || tile.x >= MapaLayout.FilasGrilla || tile.y < 0 || tile.y >= MapaLayout.ColumnasGrilla)
        {
            Debug.LogError("ProgresionBuilder: el tile " + Tile(tile) + " de '" + nombre + "' se sale de la grilla.");
            return null;
        }

        if (validacion == Validacion.Grafo && !grafo.EsHuecoAbierto(grafo.IdHueco(tile.x, tile.y)))
        {
            Debug.LogError("ProgresionBuilder: el tile " + Tile(tile) + " de '" + nombre + "' no es un hueco abierto entre dos celdas: ahi la puerta quedaria incrustada en un muro.");
            return null;
        }

        if (validacion == Validacion.SinMuro && paredes[tile.x, tile.y])
        {
            Debug.LogError("ProgresionBuilder: el tile " + Tile(tile) + " de '" + nombre + "' esta tapado por un muro; ahi no hay nada que cerrar con una puerta.");
            return null;
        }

        bool muroHorizontal = tile.x % 2 == 0;
        Vector3 centro = raiz.TransformPoint(MapaLayout.GrillaALocal(tile.x, tile.y));
        Quaternion rotacion = muroHorizontal ? Quaternion.identity : Quaternion.Euler(0f, -90f, 0f);

        // La bisagra se corre medio ancho de hoja desde el centro del hueco, sobre el eje en el
        // que la hoja se extiende (su +X local).
        Vector3 bisagra = centro - rotacion * Vector3.right * (AnchoHoja * 0.5f);

        var go = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(bisagra, rotacion);

        GameObject hoja = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(hoja, "Crear Hoja");
        hoja.name = "Hoja";
        hoja.transform.SetParent(go.transform, false);
        hoja.transform.localScale = new Vector3(AnchoHoja, MapaLayout.AltoMuro, GrosorHoja);
        // Medio ancho hacia +X (para que su borde izquierdo quede en la bisagra) y medio alto
        // hacia arriba (para que se apoye en el piso).
        hoja.transform.localPosition = new Vector3(AnchoHoja * 0.5f, MapaLayout.AltoMuro * 0.5f, 0f);
        PonerMaterial(hoja, "Puerta", new Color(0.42f, 0.28f, 0.16f));

        var puerta = Undo.AddComponent<PuertaInteractuable>(go);

        var so = new SerializedObject(puerta);
        so.FindProperty("llaveRequerida").objectReferenceValue = llave;
        so.FindProperty("consumirLlave").boolValue = true;
        so.FindProperty("anguloAbierta").floatValue = 90f;
        so.FindProperty("duracionApertura").floatValue = 1.2f;
        so.ApplyModifiedProperties();

        return puerta;
    }

    // ---------------------------------------------------------------
    // Llaves
    // ---------------------------------------------------------------

    // Instancia el prefab de la llave (el modelo medido) o, si falta, un cubo de respaldo. El
    // Collider va en trigger: asi no traba al jugador pero el raycast de PlayerInteraction la
    // detecta igual (usa QueryTriggerInteraction.Collide).
    //
    // Lleva tambien un PuntoObligatorio, que es exactamente para lo que se escribio esa clase:
    // mientras la llave siga en el mapa, GestorBarreras no va a cerrar ninguna barrera que la deje
    // inalcanzable. Al recogerla, ItemPickup desactiva el objeto y el PuntoObligatorio se da de
    // baja solo.
    static void CrearLlave(Transform padre, string nombre, Vector3 posicion, ItemData item, GameObject prefab)
    {
        GameObject go;

        if (prefab != null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
            go.transform.localScale = Vector3.one * LadoLlave;
            PonerMaterial(go, "Llave", new Color(0.95f, 0.78f, 0.25f));

            if (go.TryGetComponent(out Collider collider)) collider.isTrigger = true;
        }

        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.position = posicion;

        var pickup = Undo.AddComponent<ItemPickup>(go);
        var so = new SerializedObject(pickup);
        so.FindProperty("item").objectReferenceValue = item;
        so.ApplyModifiedProperties();

        Undo.AddComponent<PuntoObligatorio>(go);
    }

    // Centro de un tile de la grilla, en coordenadas de mundo y a nivel de piso.
    static Vector3 CentroDeTile(Transform raiz, Vector2Int tile)
    {
        return raiz.TransformPoint(MapaLayout.GrillaALocal(tile.x, tile.y));
    }

    // Igual, pero a la altura a la que se apoya una llave.
    static Vector3 PosicionDeTile(Transform raiz, Vector2Int tile)
    {
        return CentroDeTile(raiz, tile) + Vector3.up * AlturaLlave;
    }

    // ---------------------------------------------------------------
    // Balizas
    // ---------------------------------------------------------------

    // El haz lo arma Baliza por codigo en Awake; aca solo se coloca el objeto y se le configuran el
    // color y la puerta que marca. En el Editor, sin Play, lo que se ve es su gizmo.
    static void CrearBaliza(Transform padre, string nombre, Vector3 posicion, Color color, PuertaInteractuable puerta)
    {
        var go = new GameObject(nombre);
        Undo.RegisterCreatedObjectUndo(go, "Crear " + nombre);
        go.transform.SetParent(padre, false);
        go.transform.position = posicion;

        var baliza = Undo.AddComponent<Baliza>(go);

        var so = new SerializedObject(baliza);
        so.FindProperty("color").colorValue = color;
        so.FindProperty("puerta").objectReferenceValue = puerta;
        // apagarAlAbrirse se deja en false: el haz sigue prendido despues de abrir la puerta, que
        // es lo que sirve para volver a encontrar el camino (y es lo que hace el beacon original).
        so.ApplyModifiedProperties();
    }

    // El spawn de este juego no es el Punto_Inicio del laberinto: el jugador arranca dentro de la
    // zona segura que arma SpawnZoneBuilder, al este del mapa. Se resuelve por orden de confianza,
    // y se informa de donde salio para que quede en el log y se pueda corregir si hace falta.
    static Vector3 ResolverPuntoDeSpawn(out string origen)
    {
        // 1) El piso de la zona segura: su centro es el punto mas visible (es donde esta la luz
        //    calida del refugio) y su cara de arriba da la altura exacta del suelo.
        GameObject piso = BuscarEnJerarquia("ZonaSeguraGenerada", "Piso");
        if (piso != null)
        {
            origen = "centro de la zona segura (ZonaSeguraGenerada/Piso)";
            float alturaPiso = piso.transform.position.y + piso.transform.lossyScale.y * 0.5f;
            Vector3 centro = piso.transform.position;
            return new Vector3(centro.x, alturaPiso + AlturaLlave, centro.z);
        }

        // 2) Donde esta parado el jugador, apoyada en lo que tenga debajo.
        PlayerController jugador = Object.FindAnyObjectByType<PlayerController>();
        if (jugador != null)
        {
            origen = "posicion del jugador en la escena";
            Vector3 desde = jugador.transform.position + Vector3.up;
            if (Physics.Raycast(desde, Vector3.down, out RaycastHit hit, 10f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                // Corrida un par de metros para no dejarla dentro del propio jugador.
                return hit.point + Vector3.up * AlturaLlave + jugador.transform.forward * 2f;
            }
            return jugador.transform.position + Vector3.up * AlturaLlave;
        }

        // 3) Ultimo recurso: el marcador de inicio del laberinto.
        GameObject inicio = GameObject.Find("Punto_Inicio");
        if (inicio != null)
        {
            origen = "Punto_Inicio (no se encontro la zona segura ni el jugador)";
            return inicio.transform.position + Vector3.up * AlturaLlave;
        }

        origen = "ORIGEN DEL MUNDO: no se encontro la zona segura, ni el jugador, ni Punto_Inicio";
        return Vector3.up * AlturaLlave;
    }

    // GameObject.Find no sirve para buscar por ruta parcial, y los dos nombres que hacen falta
    // ("ZonaSeguraGenerada" y su hijo) estan dentro de una jerarquia generada.
    static GameObject BuscarEnJerarquia(string nombrePadre, string nombreHijo)
    {
        GameObject padre = GameObject.Find(nombrePadre);
        if (padre == null) return null;

        Transform hijo = padre.transform.Find(nombreHijo);
        return hijo != null ? hijo.gameObject : null;
    }

    // ---------------------------------------------------------------
    // Prefabs de los modelos 3D
    // ---------------------------------------------------------------

    // Arma el prefab de un FBX: lo mide, lo escala al largo pedido, lo orienta y lo centra (ver
    // ModeloUtils), y lo guarda en Assets/Prefabs/Items. Se regenera en cada corrida a proposito:
    // el prefab es una pieza derivada del FBX, no un asset que se edite a mano. Lo que SI se edita
    // a mano es la pose, que vive en el componente que lo usa (EquipoJugador) o en la instancia de
    // la escena, no acá.
    //
    // Devuelve null, con un aviso, si el FBX no esta: el resto del builder sigue con primitivas.
    static GameObject AsegurarPrefabDeModelo(string rutaFbx, string rutaPrefab, string nombre, float largo, ModeloUtils.Eje eje, bool conCollider)
    {
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(rutaFbx);
        if (modelo == null)
        {
            Debug.LogWarning("ProgresionBuilder: no se encontro el modelo '" + rutaFbx + "'; se usa la primitiva de respaldo.");
            return null;
        }

        ModeloUtils.ConfigurarMallaEstatica(rutaFbx);
        ModeloUtils.AsegurarCarpeta(CarpetaPrefabs);

        // Tres niveles: la raiz es la que recibe la pose final, "Ajuste" se queda con la escala y
        // la orientacion medidas, y adentro va el FBX con su transform intacto.
        var raizTemporal = new GameObject(nombre);
        try
        {
            var ajuste = new GameObject("Ajuste");
            ajuste.transform.SetParent(raizTemporal.transform, false);

            var instancia = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
            instancia.transform.SetParent(ajuste.transform, false);

            float escala = ModeloUtils.Acomodar(ajuste.transform, instancia, largo, eje, out Bounds bounds);
            ModeloUtils.ApagarSombras(instancia);

            if (conCollider)
            {
                // El Collider es para APUNTARLE con el raycast, no para fisica: se agranda a un
                // minimo porque una llave de 1 cm de grosor seria imposible de enfocar.
                BoxCollider collider = raizTemporal.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.center = Vector3.zero;
                collider.size = new Vector3(
                    Mathf.Max(bounds.size.x, LadoColliderLlave),
                    Mathf.Max(bounds.size.y, LadoColliderLlave),
                    Mathf.Max(bounds.size.z, LadoColliderLlave));
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raizTemporal, rutaPrefab);
            Debug.Log("ProgresionBuilder: " + rutaPrefab + " generado desde " + rutaFbx +
                " (escala " + escala.ToString("F4") + ", largo " + largo + " m).");
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(raizTemporal);
        }
    }

    // ---------------------------------------------------------------
    // Boton secreto
    // ---------------------------------------------------------------

    static void CrearBoton(Transform padre, MuroSecreto muro)
    {
        GameObject comerciante = GameObject.Find("Punto_Comerciante") ?? GameObject.Find("Comerciante");
        if (comerciante == null)
        {
            Debug.LogError("ProgresionBuilder: no se encontro 'Punto_Comerciante' ni 'Comerciante' en la escena; el boton secreto no se coloca.");
            return;
        }

        // En el piso de la habitacion (el comerciante esta un metro sobre el piso, ver
        // MapaBuilder.Punto), corrido hacia el sur para que no se superponga con su collider.
        Vector3 posicion = comerciante.transform.position + new Vector3(0f, -1f, -DesplazamientoBoton);

        var go = new GameObject("Boton_Secreto");
        Undo.RegisterCreatedObjectUndo(go, "Crear Boton_Secreto");
        go.transform.SetParent(padre, false);
        go.transform.position = posicion;

        GameObject pulsador = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(pulsador, "Crear Pulsador");
        pulsador.name = "Pulsador";
        pulsador.transform.SetParent(go.transform, false);
        pulsador.transform.localScale = new Vector3(LadoBoton, AltoBoton, LadoBoton);
        pulsador.transform.localPosition = new Vector3(0f, AltoBoton * 0.5f, 0f);
        PonerMaterial(pulsador, "Boton", new Color(0.75f, 0.12f, 0.12f));

        var boton = Undo.AddComponent<BotonSecreto>(go);

        var so = new SerializedObject(boton);
        SerializedProperty muros = so.FindProperty("muros");
        muros.arraySize = muro != null ? 1 : 0;
        if (muro != null) muros.GetArrayElementAtIndex(0).objectReferenceValue = muro;

        so.FindProperty("textoSinPulsar").stringValue = "Presiona E para pulsar el boton";
        so.FindProperty("textoPulsado").stringValue = "Algo se movio en el laberinto";
        so.FindProperty("parteMovil").objectReferenceValue = pulsador.transform;
        so.FindProperty("desplazamientoVisual").vector3Value = new Vector3(0f, -AltoBoton * 0.5f, 0f);
        so.ApplyModifiedProperties();
    }

    // ---------------------------------------------------------------
    // Economia y jugador
    // ---------------------------------------------------------------

    // Sube la vida de todos los enemigos de la escena y les deja el botin ya configurado. El
    // componente igual se autoagrega en Play (EnemyHealth.Awake), pero puesto aca queda visible y
    // editable en el Inspector, que es lo que hace falta para ajustar el oro enemigo por enemigo.
    static int ConfigurarEnemigos()
    {
        var enemigos = Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include);

        foreach (EnemyHealth salud in enemigos)
        {
            var so = new SerializedObject(salud);
            so.FindProperty("maxHealth").floatValue = VidaEnemigo;
            so.ApplyModifiedProperties();

            BotinEnemigo botin = salud.GetComponent<BotinEnemigo>();
            if (botin == null) botin = Undo.AddComponent<BotinEnemigo>(salud.gameObject);

            var soBotin = new SerializedObject(botin);
            soBotin.FindProperty("oro").intValue = OroPorEnemigo;
            soBotin.ApplyModifiedProperties();
        }

        return enemigos.Length;
    }

    // Agrega (o actualiza) la entrada del arma en la tienda. Si ya estaba, le corrige el precio en
    // vez de duplicarla, para que volver a correr el builder sea idempotente.
    static bool AgregarArmaALaTienda(ItemData arma)
    {
        ShopManager tienda = Object.FindAnyObjectByType<ShopManager>();
        if (tienda == null) return false;

        var so = new SerializedObject(tienda);
        SerializedProperty items = so.FindProperty("items");

        int indice = -1;
        for (int i = 0; i < items.arraySize; i++)
        {
            if (items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == arma)
            {
                indice = i;
                break;
            }
        }

        if (indice < 0)
        {
            items.arraySize++;
            indice = items.arraySize - 1;
        }

        SerializedProperty entrada = items.GetArrayElementAtIndex(indice);
        entrada.FindPropertyRelative("item").objectReferenceValue = arma;
        entrada.FindPropertyRelative("precio").intValue = PrecioArma;
        entrada.FindPropertyRelative("cantidad").intValue = 1; // una sola: comprada, no hay otra

        so.ApplyModifiedProperties();
        return true;
    }

    // EquipoJugador se instala solo en Play, pero puesto aca en el Editor queda con sus campos a
    // la vista, y con el prefab del arma ya enganchado.
    static bool AsegurarEquipoJugador(GameObject prefabArma)
    {
        EquipoJugador equipo = Object.FindAnyObjectByType<EquipoJugador>();

        if (equipo == null)
        {
            Inventory inventario = Object.FindAnyObjectByType<Inventory>();
            if (inventario == null) return false;

            equipo = Undo.AddComponent<EquipoJugador>(inventario.gameObject);
        }

        // Si el modelo no esta, se deja el campo como este: borrarlo haria que el jugador vuelva
        // al cubo placeholder sin que nadie lo haya pedido.
        if (prefabArma != null)
        {
            var so = new SerializedObject(equipo);
            so.FindProperty("prefabArma").objectReferenceValue = prefabArma;
            so.ApplyModifiedProperties();
        }

        return true;
    }

    // Carga las casillas de la barra rapida: la 1 con el arma (el pedido explicito) y las dos
    // siguientes con los consumibles de Assets/Items. Un ItemData que falte deja su casilla vacia,
    // que la UI dibuja en gris.
    static string ConfigurarBarraRapida(ItemData arma)
    {
        BarraRapida barra = Object.FindAnyObjectByType<BarraRapida>();

        if (barra == null)
        {
            Inventory inventario = Object.FindAnyObjectByType<Inventory>();
            if (inventario == null) return "NO (no hay Inventory en la escena)";

            barra = Undo.AddComponent<BarraRapida>(inventario.gameObject);
        }

        var asignados = new ItemData[BarraRapida.Casillas];
        asignados[0] = arma;
        asignados[1] = AssetDatabase.LoadAssetAtPath<ItemData>(CarpetaItems + "/PocionDeVida.asset");
        asignados[2] = AssetDatabase.LoadAssetAtPath<ItemData>(CarpetaItems + "/RacionDeComida.asset");
        // La casilla 4 queda libre: la Antorcha ya no existe como item. La UI la dibuja en gris
        // hasta que se le asigne algo desde el Inspector.
        asignados[3] = null;

        var so = new SerializedObject(barra);
        SerializedProperty items = so.FindProperty("items");
        items.arraySize = BarraRapida.Casillas;
        for (int i = 0; i < BarraRapida.Casillas; i++)
        {
            items.GetArrayElementAtIndex(i).objectReferenceValue = asignados[i];
        }
        so.ApplyModifiedProperties();

        var nombres = new List<string>();
        for (int i = 0; i < asignados.Length; i++)
        {
            nombres.Add((i + 1) + "=" + (asignados[i] != null ? asignados[i].itemName : "vacia"));
        }
        return string.Join(", ", nombres);
    }

    // El menu de la escena de juego pasa a ser solo el de pausa: la pantalla de inicio ahora es
    // una escena aparte (ver MenuPrincipalBuilder).
    static bool ConfigurarMenuComoPausa()
    {
        Menu menu = Object.FindAnyObjectByType<Menu>();
        if (menu == null) return false;

        Undo.RecordObject(menu, "Menu a modo Pausa");
        menu.modo = Menu.Modo.Pausa;
        menu.openOnStart = false;
        menu.oscurecerFondo = 0.75f;
        // El boton "Menu principal" de la pausa apunta a la escena que crea MenuPrincipalBuilder;
        // hasta que exista, el menu lo muestra en gris.
        menu.escenaMenuPrincipal = NombreEscenaMenuPrincipal;
        EditorUtility.SetDirty(menu);
        return true;
    }

    // ---------------------------------------------------------------
    // Utilidades
    // ---------------------------------------------------------------

    // Un material por color, guardado como asset: asi se puede retocar desde el Inspector y lo
    // comparten todas las piezas del mismo tipo (un material por objeto serian draw calls de mas).
    static void PonerMaterial(GameObject go, string nombre, Color color)
    {
        if (!go.TryGetComponent(out Renderer renderer)) return;

        renderer.sharedMaterial = ModeloUtils.MaterialDeColor(CarpetaMateriales, nombre, color);
    }

    static string Tile(Vector2Int tile) => "(" + tile.x + ", " + tile.y + ")";
}
