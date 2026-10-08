using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Instala la ballesta completa, menu "Between Metals > Items".
//
// Deliberadamente SEPARADO de ProgresionBuilder, aunque la ballesta se venda en la misma tienda y se
// equipe con la misma barra rapida que la daga: "Instalar progresion completa" rehace el muro
// secreto, las puertas, las llaves y la boveda, o sea que toca medio laberinto. Agregar un arma no
// tiene por que obligar a eso ni a arriesgar un merge de la escena. Este comando toca SOLO lo de la
// ballesta y se puede correr cuantas veces haga falta.
//
// Que hace, todo idempotente (dos corridas seguidas dejan lo mismo que una):
//
//   1. Crea Assets/Items/Ballesta.asset, el ItemData con itemId "ballesta" (la constante
//      EquipoJugador.IdBallesta, que es el contrato con el que el equipo la reconoce).
//   2. Genera los prefabs del modelo de la ballesta y de la flecha a partir de los FBX del pack
//      Low-Poly Weapons, medidos y con material URP (ver ModeloUtils.GuardarPrefabDeFbx).
//   3. Le pone al prefab de la ballesta el componente Ballesta -el que dispara- con el prefab de la
//      flecha ya enganchado.
//   4. La agrega a la tienda del comerciante a PrecioBallesta de oro.
//   5. La deja en la casilla CasillaBallesta de la barra rapida.
//   6. Le pasa el prefab a EquipoJugador, que es quien la pone en la mano.
//
// El pack Low-Poly Weapons NO esta en el repo (licencia ajena, mismo criterio que GeneradorAudio con
// los .wav). Si falta, los pasos 2 y 3 se saltean con un aviso y el arma funciona igual: EquipoJugador
// le arma un placeholder por codigo al equiparla y Ballesta dispara su flecha de cilindro.
public static class BallestaBuilder
{
    // ---- Contrato con los assets ----
    const string CarpetaItems = "Assets/Items";
    const string ArchivoBallesta = "Ballesta";

    // ---- Economia ----
    // El doble que la daga (50): pega mas (2 tiros contra 3 golpes), no gasta estamina y se puede
    // usar sin acercarse, asi que tiene que ser la compra de la segunda vuelta y no la primera. Con
    // 20 de oro por enemigo (ProgresionBuilder.OroPorEnemigo), son 5 enemigos.
    const int PrecioBallesta = 100;

    // ---- Barra rapida ----
    // La casilla 4 (indice 3). No la 2 ni la 3: esas ya son la pocion y la comida, las dos cosas que
    // el jugador manotea con el enemigo encima, y moverlas para hacerle lugar al arma nueva cambiaria
    // un reflejo ya aprendido. La 4 estaba libre desde que la antorcha dejo de ser un item.
    const int CasillaBallesta = 3;

    // ---- Modelos ----
    const string CarpetaPack = "Assets/Low-Poly Weapons";
    const string FbxBallesta = CarpetaPack + "/Models/Crossbow.fbx";
    const string FbxFlecha = CarpetaPack + "/Models/Arrow_Regular.fbx";
    const string TexturaPack = CarpetaPack + "/Textures/Weapons.tif";

    const string CarpetaPrefabs = "Assets/Prefabs/Items";
    const string CarpetaMateriales = "Assets/Materials/Items";
    const string PrefabBallesta = CarpetaPrefabs + "/Ballesta.prefab";
    const string PrefabFlecha = CarpetaPrefabs + "/Flecha.prefab";

    const float LargoBallesta = 0.65f; // ballesta sostenida con las dos manos, vista en primera persona
    const float LargoFlecha = 0.5f;    // virote, mas corto que una flecha de arco

    [MenuItem("Between Metals/Items/Instalar la ballesta", priority = 820)]
    static void Instalar()
    {
        int grupoUndo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Instalar la ballesta");

        // ---- 1. ItemData ----
        ItemData ballesta = AsegurarItemData();

        // ---- 2. Prefabs de los modelos ----
        // Los dos apuntan a +Z, el eje "para adelante": la ballesta porque cuelga de la camara
        // mirando al frente, y la flecha porque Flecha vuela sobre su propio +Z.
        GameObject prefabFlecha = ModeloUtils.GuardarPrefabDeFbx(FbxFlecha, PrefabFlecha, "Flecha",
            LargoFlecha, ModeloUtils.Eje.Z, TexturaPack, CarpetaMateriales, "Item_Flecha");

        GameObject prefabBallesta = ModeloUtils.GuardarPrefabDeFbx(FbxBallesta, PrefabBallesta, "Ballesta",
            LargoBallesta, ModeloUtils.Eje.Z, TexturaPack, CarpetaMateriales, "Item_Ballesta");

        // ---- 3. El componente que dispara, dentro del prefab ----
        prefabBallesta = PrepararPrefabDeBallesta(prefabBallesta, prefabFlecha);

        // ---- 4, 5 y 6: escena ----
        bool tienda = AgregarALaTienda(ballesta);
        string casilla = AsignarCasilla(ballesta);
        bool equipo = AsignarPrefabAlEquipo(prefabBallesta);

        Undo.CollapseUndoOperations(grupoUndo);

        // Se GUARDA la escena, no solo se la marca sucia como hacen los otros builders. El motivo es
        // un fallo concreto que ya paso: de los 6 pasos, el ItemData y los prefabs van a disco al
        // instante (AssetDatabase escribe solo), pero la tienda, la barra rapida y EquipoJugador son
        // cambios de ESCENA. Si el que corre el comando no guarda, queda la instalacion a medias de
        // la peor clase: el ItemData existe -asi que todo parece hecho y el log dijo "listo"- pero la
        // ballesta no esta en la lista de venta ni en la barra, y no hay forma de comprarla ni de
        // sospechar por que. Mejor guardar y avisar que dejar un estado que miente.
        Scene escena = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        bool guardada = EditorSceneManager.SaveScene(escena);

        Debug.Log(
            "BallestaBuilder: listo.\n" +
            "  ItemData: " + CarpetaItems + "/" + ArchivoBallesta + ".asset (itemId '" + EquipoJugador.IdBallesta + "')\n" +
            "  Modelos: ballesta " + (prefabBallesta != null ? PrefabBallesta : "cubo placeholder (falta el pack)") +
                ", flecha " + (prefabFlecha != null ? PrefabFlecha : "cilindro placeholder (falta el pack)") + "\n" +
            "  En la tienda a " + PrecioBallesta + " de oro: " + (tienda ? "si" : "NO (no hay ShopManager en la escena)") + "\n" +
            "  Barra rapida: " + casilla + "\n" +
            "  EquipoJugador: " + (equipo ? "prefab asignado" : "NO (no hay Inventory en la escena)") + "\n" +
            "  Escena '" + escena.name + "' guardada: " + (guardada ? "si" : "NO; GUARDALA A MANO (Ctrl+S) o los " +
                "tres pasos de escena -tienda, barra rapida y equipo- se pierden al cerrar") + "\n" +
            "  Se dispara con la tecla de Atacar (clic izquierdo por defecto) con la ballesta en la mano.");
    }

    // Deja la escena como antes de instalarla: la saca de la tienda, de la barra rapida y del equipo.
    // Los assets de disco (el ItemData y los prefabs) NO se borran, mismo criterio que
    // ModelosItemsBuilder.QuitarModelos: si el jugador ya la habia comprado en una partida guardada,
    // borrar el ItemData dejaria una referencia rota, y volver a instalarla seria regenerar todo.
    [MenuItem("Between Metals/Items/Quitar la ballesta", priority = 821)]
    static void Quitar()
    {
        var ballesta = AssetDatabase.LoadAssetAtPath<ItemData>(CarpetaItems + "/" + ArchivoBallesta + ".asset");
        if (ballesta == null)
        {
            Debug.Log("BallestaBuilder: no hay ningun " + CarpetaItems + "/" + ArchivoBallesta + ".asset; no hay nada que quitar.");
            return;
        }

        int grupoUndo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Quitar la ballesta");

        bool deLaTienda = QuitarDeLaTienda(ballesta);
        bool deLaBarra = QuitarDeLaBarra(ballesta);

        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("BallestaBuilder: ballesta quitada de la tienda: " + (deLaTienda ? "si" : "no estaba") +
            ", de la barra rapida: " + (deLaBarra ? "si" : "no estaba") + ".\n" +
            "  Los assets de disco quedan donde estan (" + CarpetaItems + "/" + ArchivoBallesta + ".asset y los prefabs).");
    }

    // ---------------------------------------------------------------
    // 1. ItemData
    // ---------------------------------------------------------------

    // Crea el ItemData si no existe y, si ya existe, solo completa lo que esta vacio: un asset que el
    // equipo ya ajusto a mano (icono, descripcion) no se pisa al volver a correr el comando. Mismo
    // criterio que ProgresionBuilder.AsegurarItem con las llaves y la daga.
    static ItemData AsegurarItemData()
    {
        const string nombre = "Ballesta";
        const string descripcion = "Dispara flechas a distancia, con municion infinita. Mas lenta que " +
            "la daga, pero no hace falta acercarse ni gasta estamina.";

        string ruta = CarpetaItems + "/" + ArchivoBallesta + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemData>(ruta);

        if (item == null)
        {
            ModeloUtils.AsegurarCarpeta(CarpetaItems);

            item = ScriptableObject.CreateInstance<ItemData>();
            item.itemId = EquipoJugador.IdBallesta;
            item.itemName = nombre;
            item.description = descripcion;
            // Un arma no se gasta al "usarla" desde el inventario: vive equipada mientras este ahi.
            item.consumeOnUse = false;

            AssetDatabase.CreateAsset(item, ruta);
            AssetDatabase.SaveAssets();
            Debug.Log("BallestaBuilder: creado " + ruta);
            return item;
        }

        bool cambio = false;
        if (string.IsNullOrWhiteSpace(item.itemId)) { item.itemId = EquipoJugador.IdBallesta; cambio = true; }
        if (string.IsNullOrWhiteSpace(item.itemName)) { item.itemName = nombre; cambio = true; }
        if (string.IsNullOrWhiteSpace(item.description)) { item.description = descripcion; cambio = true; }

        if (item.itemId != EquipoJugador.IdBallesta)
        {
            Debug.LogWarning("BallestaBuilder: " + ruta + " tiene itemId '" + item.itemId + "' y se esperaba '" +
                EquipoJugador.IdBallesta + "'. No se toca, pero EquipoJugador no va a reconocerla como arma.", item);
        }

        if (item.consumeOnUse)
        {
            Debug.LogWarning("BallestaBuilder: " + ruta + " tiene consumeOnUse activo; usarla desde el " +
                "inventario la borraria. Conviene desmarcarlo.", item);
        }

        if (cambio)
        {
            EditorUtility.SetDirty(item);
            AssetDatabase.SaveAssets();
        }

        return item;
    }

    // ---------------------------------------------------------------
    // 3. El componente que dispara
    // ---------------------------------------------------------------

    // Le deja al prefab de la ballesta el componente Ballesta con el prefab de la flecha enganchado.
    // Va en el PREFAB y no en el jugador para que EquipoJugador solo tenga que instanciarlo: asi no
    // sabe nada de flechas ni de cadencias, y el arma se puede retocar abriendo su prefab.
    //
    // Devuelve el prefab ya guardado (SaveAsPrefabAsset devuelve una referencia nueva), o null si no
    // habia prefab que preparar.
    static GameObject PrepararPrefabDeBallesta(GameObject prefabBallesta, GameObject prefabFlecha)
    {
        if (prefabBallesta == null) return null;

        // LoadPrefabContents y no AddComponent sobre el asset: abre el prefab en una escena oculta,
        // que es la via soportada para modificar un prefab desde codigo sin instanciarlo en la
        // escena abierta.
        GameObject contenido = PrefabUtility.LoadPrefabContents(PrefabBallesta);
        try
        {
            Ballesta ballesta = contenido.GetComponent<Ballesta>() ?? contenido.AddComponent<Ballesta>();

            // Si el modelo de la flecha no esta, se deja el campo como este en vez de pisarlo con
            // null: eso haria que el jugador vuelva al cilindro sin que nadie lo haya pedido.
            if (prefabFlecha != null)
            {
                var so = new SerializedObject(ballesta);
                so.FindProperty("prefabFlecha").objectReferenceValue = prefabFlecha;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return PrefabUtility.SaveAsPrefabAsset(contenido, PrefabBallesta);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }

    // ---------------------------------------------------------------
    // 4. Tienda
    // ---------------------------------------------------------------

    // Agrega (o actualiza) la entrada en la tienda. Si ya estaba, le corrige el precio en vez de
    // duplicarla. No toca las otras entradas: la daga y los consumibles quedan como estaban.
    static bool AgregarALaTienda(ItemData ballesta)
    {
        ShopManager tienda = Object.FindAnyObjectByType<ShopManager>();
        if (tienda == null) return false;

        var so = new SerializedObject(tienda);
        SerializedProperty items = so.FindProperty("items");

        int indice = IndiceEnLaTienda(items, ballesta);
        if (indice < 0)
        {
            items.arraySize++;
            indice = items.arraySize - 1;
        }

        SerializedProperty entrada = items.GetArrayElementAtIndex(indice);
        entrada.FindPropertyRelative("item").objectReferenceValue = ballesta;
        entrada.FindPropertyRelative("precio").intValue = PrecioBallesta;
        entrada.FindPropertyRelative("cantidad").intValue = 1; // una sola: comprada, no hay otra

        so.ApplyModifiedProperties();
        return true;
    }

    static bool QuitarDeLaTienda(ItemData ballesta)
    {
        ShopManager tienda = Object.FindAnyObjectByType<ShopManager>();
        if (tienda == null) return false;

        var so = new SerializedObject(tienda);
        SerializedProperty items = so.FindProperty("items");

        int indice = IndiceEnLaTienda(items, ballesta);
        if (indice < 0) return false;

        items.DeleteArrayElementAtIndex(indice);
        so.ApplyModifiedProperties();
        return true;
    }

    static int IndiceEnLaTienda(SerializedProperty items, ItemData ballesta)
    {
        for (int i = 0; i < items.arraySize; i++)
        {
            if (items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == ballesta) return i;
        }
        return -1;
    }

    // ---------------------------------------------------------------
    // 5. Barra rapida
    // ---------------------------------------------------------------

    // Solo escribe LA casilla de la ballesta: las otras tres quedan con lo que tengan, asi correr
    // esto no pisa un reparto que el equipo haya ajustado a mano.
    static string AsignarCasilla(ItemData ballesta)
    {
        BarraRapida barra = Object.FindAnyObjectByType<BarraRapida>();
        if (barra == null) return "NO (no hay BarraRapida en la escena; corre antes la progresion)";

        var so = new SerializedObject(barra);
        SerializedProperty items = so.FindProperty("items");

        if (items.arraySize < BarraRapida.Casillas) items.arraySize = BarraRapida.Casillas;

        SerializedProperty casilla = items.GetArrayElementAtIndex(CasillaBallesta);
        var ocupante = casilla.objectReferenceValue as ItemData;

        // Si la casilla ya tiene OTRA cosa, no se la pisa: se avisa y se deja. Perder de la barra un
        // item que alguien puso a mano es peor que no instalar la ballesta en su tecla.
        if (ocupante != null && ocupante != ballesta)
        {
            return $"la casilla {CasillaBallesta + 1} ya tiene '{ocupante.itemName}': NO se toco. " +
                "Vacíala en el Inspector y volve a correr el comando, o movela a mano.";
        }

        casilla.objectReferenceValue = ballesta;
        so.ApplyModifiedProperties();

        return $"casilla {CasillaBallesta + 1} (tecla {CasillaBallesta + 1}) = Ballesta";
    }

    static bool QuitarDeLaBarra(ItemData ballesta)
    {
        BarraRapida barra = Object.FindAnyObjectByType<BarraRapida>();
        if (barra == null) return false;

        var so = new SerializedObject(barra);
        SerializedProperty items = so.FindProperty("items");

        bool cambio = false;
        // Todas las casillas y no solo CasillaBallesta: alguien pudo haberla movido de tecla.
        for (int i = 0; i < items.arraySize; i++)
        {
            SerializedProperty casilla = items.GetArrayElementAtIndex(i);
            if (casilla.objectReferenceValue != ballesta) continue;

            casilla.objectReferenceValue = null;
            cambio = true;
        }

        if (cambio) so.ApplyModifiedProperties();
        return cambio;
    }

    // ---------------------------------------------------------------
    // 6. EquipoJugador
    // ---------------------------------------------------------------

    // EquipoJugador se instala solo en Play, pero si ya esta en la escena se le deja el prefab
    // enganchado para que se vea en el Inspector. Si el prefab no existe (falta el pack) no se toca
    // el campo: pisarlo con null haria que el jugador vuelva al placeholder sin pedirlo.
    static bool AsignarPrefabAlEquipo(GameObject prefabBallesta)
    {
        EquipoJugador equipo = Object.FindAnyObjectByType<EquipoJugador>();

        if (equipo == null)
        {
            Inventory inventario = Object.FindAnyObjectByType<Inventory>();
            if (inventario == null) return false;

            equipo = Undo.AddComponent<EquipoJugador>(inventario.gameObject);
        }

        if (prefabBallesta != null)
        {
            var so = new SerializedObject(equipo);
            so.FindProperty("prefabBallesta").objectReferenceValue = prefabBallesta;
            so.ApplyModifiedProperties();
        }

        return true;
    }
}
