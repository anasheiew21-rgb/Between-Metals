using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Modelos 3D de los items que se recogen del piso, menu "Between Metals > Items".
//
// Hasta ahora todos los items se veian igual: el cubo gris de ItemPickup_Base. Esto les arma un
// modelo propio a cada uno, los guarda como prefab y se los asigna al ItemData (campo modelo3D),
// que es de donde ItemPickup los toma al arrancar. No hay que tocar la escena: cualquier pickup de
// ese item, el que ya esta puesto y el que se ponga despues, se dibuja con su modelo.
//
// Cada item sale de una de dos fuentes, y se intentan en este orden:
//
//   1. Un FBX de un pack, si el item tiene uno en la tabla ModelosDeFbx(). Es el caso de la pocion
//      de vida, que usa Potion_Health del pack Low-Poly Weapons.
//   2. Primitivas de Unity, la tabla Modelos(). Es el respaldo y lo que usan los items que todavia
//      no tienen modelo propio.
//
// El respaldo no es decorativo: los packs de modelos NO estan en el repo (licencia ajena, mismo
// criterio que GeneradorAudio con los .wav), asi que en una copia del proyecto sin el pack bajado
// el item se sigue viendo, con su silueta de primitivas, en vez de desaparecer. Las primitivas
// tambien siguen siendo razonables de por si: a la distancia a la que se ven estos objetos -tirados
// en el piso de un pasillo, alumbrados por una linterna- la silueta es lo unico que se lee.
//
// Las dos fuentes escriben el MISMO prefab, asi que el ItemData no sabe de donde salio el modelo:
// para darle un FBX a otro item alcanza con agregarle una fila a ModelosDeFbx().
//
// Arma y las llaves no estan aca: ya tienen sus modelos de verdad, que arma ProgresionBuilder a
// partir de los FBX de Tripo.
public static class ModelosItemsBuilder
{
    const string CarpetaItems = "Assets/Items";
    const string CarpetaModelos = "Assets/Prefabs/Items/Modelos";
    const string CarpetaMateriales = "Assets/Materials/Items";

    const string CarpetaPack = "Assets/Low-Poly Weapons";
    const string TexturaPociones = CarpetaPack + "/Textures/Potions.tif";

    // Un modelo que viene de un FBX de un pack.
    readonly struct ModeloFbx
    {
        public readonly string Fbx;
        /// <summary>Atlas de color del pack, para armarle el material URP (ver PintarConTextura).</summary>
        public readonly string Textura;
        /// <summary>Nombre del material generado, en CarpetaMateriales.</summary>
        public readonly string Material;
        /// <summary>Alto final en metros. El FBX se mide y se escala a esta medida, no se confia en la del archivo.</summary>
        public readonly float Alto;

        public ModeloFbx(string fbx, string textura, string material, float alto)
        {
            Fbx = fbx;
            Textura = textura;
            Material = material;
            Alto = alto;
        }
    }

    // Que FBX le toca a cada item, por itemId. Un item que no este aca se arma con primitivas.
    static Dictionary<string, ModeloFbx> ModelosDeFbx()
    {
        return new Dictionary<string, ModeloFbx>
        {
            // Mismos 0,25 m que tenia el frasco de primitivas, para que el cambio de modelo no
            // cambie de paso el tamano con el que el item ya estaba balanceado en la escena.
            ["pocion_vida"] = new ModeloFbx(
                CarpetaPack + "/Models/Potion_Health.fbx", TexturaPociones, "Item_PocionModelo", 0.25f)
        };
    }

    // Una pieza del modelo: una primitiva de Unity con su posicion, su escala y su color.
    //
    // Ojo con las medidas de las primitivas de Unity, que no son todas iguales: el cubo mide 1 m de
    // lado, pero el cilindro mide 1 m de DIAMETRO y 2 m de alto. Por eso la escala en Y de un
    // cilindro es la mitad de su altura final, y las cuentas de abajo lo tienen en cuenta.
    readonly struct Pieza
    {
        public readonly PrimitiveType Forma;
        public readonly Vector3 Posicion;
        public readonly Vector3 Escala;
        public readonly string Material;
        public readonly Color Color;

        public Pieza(PrimitiveType forma, Vector3 posicion, Vector3 escala, string material, Color color)
        {
            Forma = forma;
            Posicion = posicion;
            Escala = escala;
            Material = material;
            Color = color;
        }
    }

    // Colores. Apagados a proposito: el juego es oscuro y se mira con linterna, asi que un color
    // saturado se ve como una mancha fluorescente en vez de como un objeto.
    static readonly Color VidrioRojo = new Color(0.58f, 0.09f, 0.11f);
    static readonly Color CorchoOscuro = new Color(0.25f, 0.18f, 0.12f);
    static readonly Color MetalLata = new Color(0.55f, 0.56f, 0.58f);
    static readonly Color EtiquetaOcre = new Color(0.45f, 0.33f, 0.16f);
    static readonly Color MaderaCaja = new Color(0.38f, 0.29f, 0.19f);
    static readonly Color FlejeMetal = new Color(0.30f, 0.31f, 0.33f);

    // Que modelo le toca a cada item, por itemId.
    static Dictionary<string, Pieza[]> Modelos()
    {
        return new Dictionary<string, Pieza[]>
        {
            // Frasco de 0,25 m: cuerpo ancho, cuello angosto y tapon. La silueta de botella es lo
            // que lo hace reconocible de lejos, mas que el color.
            //
            // Hoy es el respaldo: si el pack esta bajado, la pocion se arma con Potion_Health.fbx
            // (ver ModelosDeFbx) y estas piezas no se usan. Se dejan para la copia del proyecto que
            // no tenga el pack, y por eso miden lo mismo que el FBX.
            ["pocion_vida"] = new[]
            {
                new Pieza(PrimitiveType.Cylinder, new Vector3(0f, 0.075f, 0f), new Vector3(0.090f, 0.075f, 0.090f), "Item_PocionVidrio", VidrioRojo),
                new Pieza(PrimitiveType.Cylinder, new Vector3(0f, 0.185f, 0f), new Vector3(0.042f, 0.035f, 0.042f), "Item_PocionVidrio", VidrioRojo),
                new Pieza(PrimitiveType.Cylinder, new Vector3(0f, 0.235f, 0f), new Vector3(0.050f, 0.015f, 0.050f), "Item_PocionTapon", CorchoOscuro)
            },

            // Lata de 0,12 m con la etiqueta como un anillo apenas mas ancho que el cuerpo. Chata y
            // ancha, para que no se confunda con el frasco ni de lejos ni en silueta.
            ["racion_comida"] = new[]
            {
                new Pieza(PrimitiveType.Cylinder, new Vector3(0f, 0.060f, 0f), new Vector3(0.095f, 0.060f, 0.095f), "Item_LataMetal", MetalLata),
                new Pieza(PrimitiveType.Cylinder, new Vector3(0f, 0.060f, 0f), new Vector3(0.099f, 0.028f, 0.099f), "Item_LataEtiqueta", EtiquetaOcre)
            },

            // Cajon de 0,16 m con un fleje cruzado. Es el item de prueba: tiene que leerse como
            // "una caja cualquiera" y no como algo que el jugador necesite.
            ["item_prueba"] = new[]
            {
                new Pieza(PrimitiveType.Cube, new Vector3(0f, 0.080f, 0f), new Vector3(0.160f, 0.160f, 0.160f), "Item_CajaMadera", MaderaCaja),
                new Pieza(PrimitiveType.Cube, new Vector3(0f, 0.080f, 0f), new Vector3(0.168f, 0.030f, 0.168f), "Item_CajaFleje", FlejeMetal)
            }
        };
    }

    [MenuItem("Between Metals/Items/Crear modelos 3D de los items")]
    static void CrearModelos()
    {
        ModeloUtils.AsegurarCarpeta(CarpetaModelos);
        ModeloUtils.AsegurarCarpeta(CarpetaMateriales);

        Dictionary<string, ItemData> items = ItemsPorId();
        Dictionary<string, Pieza[]> modelos = Modelos();

        int creados = 0, asignados = 0;

        foreach (KeyValuePair<string, Pieza[]> entrada in modelos)
        {
            // El FBX manda y las primitivas son el respaldo (ver el comentario de arriba). Las dos
            // guardan el mismo prefab, asi que de aca para abajo da igual cual se uso.
            GameObject prefab = GuardarPrefabDeFbx(entrada.Key) ?? GuardarPrefab(entrada.Key, entrada.Value);
            if (prefab == null) continue;
            creados++;

            if (!items.TryGetValue(entrada.Key, out ItemData item))
            {
                Debug.LogWarning($"ModelosItemsBuilder: no hay ningun ItemData con itemId '{entrada.Key}' en {CarpetaItems}; el modelo quedo creado pero sin asignar.");
                continue;
            }

            if (item.modelo3D == prefab) continue;

            Undo.RecordObject(item, "Asignar modelo 3D");
            item.modelo3D = prefab;
            EditorUtility.SetDirty(item);
            asignados++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"ModelosItemsBuilder: {creados} modelo(s) en {CarpetaModelos}, {asignados} asignado(s) a su ItemData. " +
            "Los items de la escena los toman solos al entrar en Play; no hace falta tocar Prototype.unity.");
    }

    [MenuItem("Between Metals/Items/Quitar los modelos 3D de los items")]
    static void QuitarModelos()
    {
        Dictionary<string, ItemData> items = ItemsPorId();
        int limpiados = 0;

        foreach (string id in Modelos().Keys)
        {
            if (!items.TryGetValue(id, out ItemData item) || item.modelo3D == null) continue;

            Undo.RecordObject(item, "Quitar modelo 3D");
            item.modelo3D = null;
            EditorUtility.SetDirty(item);
            limpiados++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"ModelosItemsBuilder: {limpiados} item(s) vuelven al cubo gris. Los prefabs de {CarpetaModelos} quedan donde estan.");
    }

    // Arma la jerarquia y la guarda como prefab. Se regenera en cada corrida a proposito, igual que
    // ProgresionBuilder con Arma y Llave: asi cambiar una medida de la tabla de arriba y volver a
    // correr el comando alcanza para ver el cambio, sin tener que borrar nada a mano.
    static GameObject GuardarPrefab(string itemId, Pieza[] piezas)
    {
        if (piezas == null || piezas.Length == 0) return null;

        var raiz = new GameObject(NombreDePrefab(itemId));

        try
        {
            foreach (Pieza pieza in piezas)
            {
                GameObject parte = GameObject.CreatePrimitive(pieza.Forma);
                parte.name = pieza.Material;
                parte.transform.SetParent(raiz.transform, false);
                parte.transform.localPosition = pieza.Posicion;
                parte.transform.localScale = pieza.Escala;

                // Las primitivas vienen con collider y el item ya tiene el suyo en la raiz, que es
                // con el que apunta el raycast de PlayerInteraction. Uno por pieza solo agregaria
                // trabajo al motor de fisica y objetivos extra al raycast.
                Object.DestroyImmediate(parte.GetComponent<Collider>());

                var renderer = parte.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = ModeloUtils.MaterialDeColor(CarpetaMateriales, pieza.Material, pieza.Color);
            }

            // Sin sombras: son objetos de 25 cm en un juego que apunta a PCs de gama baja, y su
            // sombra no se ve. Mismo criterio que ya usa AmbienteBuilder con los props chicos.
            ModeloUtils.ApagarSombras(raiz);

            string ruta = CarpetaModelos + "/" + NombreDePrefab(itemId) + ".prefab";
            return PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
        }
        finally
        {
            Object.DestroyImmediate(raiz);
        }
    }

    // Lo mismo que GuardarPrefab pero desde un FBX de un pack: lo mide, lo escala al alto pedido, lo
    // deja parado en Y y lo apoya en el piso. Se regenera en cada corrida, igual que las primitivas.
    //
    // Devuelve null si el item no tiene FBX en la tabla, o si el pack no esta bajado en esta copia
    // del proyecto; el que llama cae en las primitivas.
    static GameObject GuardarPrefabDeFbx(string itemId)
    {
        if (!ModelosDeFbx().TryGetValue(itemId, out ModeloFbx modeloFbx)) return null;

        // El armado es el compartido de ModeloUtils; lo propio de un item del piso son los dos
        // ultimos argumentos: el eje Y (el frasco va parado) y apoyarEnElPiso, porque el origen del
        // pickup es el punto donde el item toca el suelo -ahi lo instancia ItemPickup.MostrarModelo,
        // y las piezas de primitivas estan medidas desde y=0 para arriba-. Sin eso el frasco
        // quedaria enterrado hasta la mitad.
        return ModeloUtils.GuardarPrefabDeFbx(
            modeloFbx.Fbx,
            CarpetaModelos + "/" + NombreDePrefab(itemId) + ".prefab",
            NombreDePrefab(itemId),
            modeloFbx.Alto,
            ModeloUtils.Eje.Y,
            modeloFbx.Textura,
            CarpetaMateriales,
            modeloFbx.Material,
            apoyarEnElPiso: true);
    }

    // "pocion_vida" -> "Modelo_PocionVida"
    static string NombreDePrefab(string itemId)
    {
        string[] partes = itemId.Split('_');
        var nombre = new System.Text.StringBuilder("Modelo");

        foreach (string parte in partes)
        {
            if (parte.Length == 0) continue;
            nombre.Append(char.ToUpperInvariant(parte[0]));
            if (parte.Length > 1) nombre.Append(parte.Substring(1));
        }

        return nombre.ToString();
    }

    // Todos los ItemData del proyecto indexados por itemId. Se buscan con AssetDatabase y no con
    // una lista fija para que un item nuevo no haya que agregarlo en dos lados.
    static Dictionary<string, ItemData> ItemsPorId()
    {
        var porId = new Dictionary<string, ItemData>();

        foreach (string guid in AssetDatabase.FindAssets("t:ItemData", new[] { CarpetaItems }))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item == null || string.IsNullOrWhiteSpace(item.itemId)) continue;

            porId[item.itemId.Trim().ToLowerInvariant()] = item;
        }

        return porId;
    }
}
