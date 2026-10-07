using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Pone el modelo de la linterna en primera persona debajo del objeto Flashlight de la escena y lo
// engancha al campo modeloLinterna de FlashlightController, que es quien lo muestra y lo oculta con
// la tecla F. El modelo es una malla estatica (sin huesos ni animaciones), asi que todo lo que hace
// falta es importarla bien, armarle el material y acomodarla.
//
// Nada de esto esta hardcodeado a mano: la escala y el centrado se MIDEN sobre los bounds del mesh
// importado (el FBX viene de Tripo y no se sabe en que unidades ni centrado en que punto), asi que
// el modelo queda de LargoDeseado metros y centrado en su contenedor sin importar como venga.
//
// Son tres niveles a proposito, uno por cada cosa que puede cambiar sin tocar las otras:
//   Modelo_Linterna  pose en pantalla (la que se toca si hay que acomodar el modelo en la mano)
//   └── Ajuste       escala, orientacion y centrado medidos sobre el mesh
//       └── Flashlight  la instancia del FBX, con su transform tal cual lo importo Unity
// El transform del FBX no se pisa porque puede traer la conversion de unidades y de ejes del
// exportador: se mide con el adentro y se corrige por arriba.
//
// Sobre el material: el pack trae tambien un metallic y un roughness, pero con nombres de otra
// familia (flashlight_3d_model_*) que los del color y la normal (tripo_node_*), el mismo sintoma
// que tenia el alien cuando sus mapas pertenecian a otro UV. Quedaron en Textures/ con el sufijo
// _sinUsar y el material usa valores constantes de metallic/smoothness; si alguna vez se confirma
// que el UV coincide, se cambian las dos lineas marcadas mas abajo.
//
// Menu: Between Metals > Jugador
public static class LinternaModeloSetup
{
    const string CarpetaModelo = "Assets/Models/Player/Flashlight";
    const string FbxPath = CarpetaModelo + "/Flashlight.fbx";
    const string BaseColorPath = CarpetaModelo + "/Textures/Flashlight_BaseColor.png";
    const string NormalPath = CarpetaModelo + "/Textures/Flashlight_Normal.png";
    const string CarpetaMateriales = CarpetaModelo + "/Materials";
    const string MaterialPath = CarpetaMateriales + "/Flashlight.mat";

    const string NombreContenedor = "Modelo_Linterna";
    const string NombreAjuste = "Ajuste";

    const float LargoDeseado = 0.22f;      // linterna de mano real: 22 cm de punta a punta
    const float NearClipViewmodel = 0.05f; // con el 0.3 que tenia la camara, un objeto a 30 cm se corta

    // Pose en pantalla, relativa al objeto Flashlight (que ya esta 0.2 m delante de la camara):
    // abajo a la derecha, como si la sostuviera la mano derecha.
    static readonly Vector3 PosicionMano = new Vector3(0.14f, -0.13f, 0.13f);
    static readonly Vector3 RotacionMano = new Vector3(6f, -8f, 0f);

    [MenuItem("Between Metals/Jugador/Poner modelo de linterna en primera persona", priority = 400)]
    static void Poner()
    {
        FlashlightController controlador = BuscarControlador();
        if (controlador == null)
        {
            EditorUtility.DisplayDialog("Modelo de linterna",
                "No hay ningun FlashlightController en la escena abierta. Abri Assets/Scenes/Prototype.unity.", "OK");
            return;
        }

        ConfigurarImportadores();

        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        Material material = ConseguirMaterial();
        if (modelo == null || material == null)
        {
            EditorUtility.DisplayDialog("Modelo de linterna",
                "Falta el modelo (" + FbxPath + ") o no se pudo crear el material (" + MaterialPath + ").", "OK");
            return;
        }

        Transform anterior = controlador.transform.Find(NombreContenedor);
        if (anterior != null && !EditorUtility.DisplayDialog("Modelo de linterna",
                "Ya existe " + NombreContenedor + " debajo de " + controlador.name + ". Se va a reemplazar.",
                "Reemplazar", "Cancelar"))
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        int grupoUndo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Poner modelo de linterna");

        if (anterior != null) Undo.DestroyObjectImmediate(anterior.gameObject);

        // El contenedor lleva la pose de la mano y es el objeto que FlashlightController prende y
        // apaga: desactivarlo alcanza para que el modelo no se dibuje ni se recorra.
        var contenedor = new GameObject(NombreContenedor);
        Undo.RegisterCreatedObjectUndo(contenedor, "Crear " + NombreContenedor);
        contenedor.transform.SetParent(controlador.transform, false);
        contenedor.transform.localPosition = PosicionMano;
        contenedor.transform.localRotation = Quaternion.Euler(RotacionMano);

        // El nodo de ajuste arranca en identidad: es el sistema de referencia en el que se mide el
        // modelo, y recien despues de medir se le escriben la escala, el giro y el centrado.
        var ajuste = new GameObject(NombreAjuste);
        Undo.RegisterCreatedObjectUndo(ajuste, "Crear " + NombreAjuste);
        ajuste.transform.SetParent(contenedor.transform, false);

        var instancia = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        Undo.RegisterCreatedObjectUndo(instancia, "Crear instancia del modelo de linterna");
        instancia.transform.SetParent(ajuste.transform, false);

        Acomodar(ajuste.transform, instancia, out float escala, out int vertices);
        int ranuras = AplicarMaterial(instancia, material);

        // Un viewmodel no tiene por que proyectar ni recibir sombras: esta pegado a la camara y las
        // sombras que tiraria serian las de su propia linterna, encima del pasillo que ilumina.
        foreach (Renderer renderer in instancia.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        string nearClip = AjustarNearClip(controlador);
        EngancharAlControlador(controlador, contenedor);

        // El estado inicial del modelo tiene que coincidir con el de la luz, igual que hace
        // FlashlightController.SincronizarModelo en Start.
        Light luz = controlador.GetComponent<Light>();
        contenedor.SetActive(luz == null || luz.enabled);

        Undo.CollapseUndoOperations(grupoUndo);
        Selection.activeGameObject = contenedor;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("LinternaModeloSetup: modelo puesto bajo " + controlador.name + "/" + NombreContenedor +
            " (escala " + escala.ToString("F4") + ", " + vertices + " vertices, " + ranuras +
            " ranuras de material), " + nearClip +
            ". Si la linterna aparece apuntando al jugador, corre 'Dar vuelta el modelo de linterna'. " +
            "Revisa la escena y guardala (Ctrl+S).");
    }

    // Por si el FBX viene con la lente para atras: mirando solo los bounds no se puede saber que
    // punta es la cabeza, asi que se deja el giro de 180 a un clic en vez de adivinar.
    [MenuItem("Between Metals/Jugador/Dar vuelta el modelo de linterna", priority = 401)]
    static void DarVuelta()
    {
        Transform ajuste = BuscarAjuste();
        if (ajuste == null) return;

        // El giro va en el nodo de ajuste y no en el contenedor: la orientacion del modelo y la pose
        // de la mano viven separadas, asi acomodar una no pisa la otra.
        // Se gira la pose entera del nodo -rotacion Y posicion- alrededor del origen del contenedor,
        // porque la posicion del nodo es justamente el centrado del modelo: girar solo la rotacion
        // dejaria el modelo descentrado la misma distancia, para el otro lado.
        Quaternion vuelta = Quaternion.Euler(0f, 180f, 0f);
        Undo.RecordObject(ajuste, "Dar vuelta el modelo de linterna");
        ajuste.localRotation = vuelta * ajuste.localRotation;
        ajuste.localPosition = vuelta * ajuste.localPosition;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("LinternaModeloSetup: modelo girado 180 grados. Guarda la escena (Ctrl+S).");
    }

    [MenuItem("Between Metals/Jugador/Quitar modelo de linterna", priority = 402)]
    static void Quitar()
    {
        FlashlightController controlador = BuscarControlador();
        Transform contenedor = controlador != null ? controlador.transform.Find(NombreContenedor) : null;
        if (contenedor == null)
        {
            Debug.Log("LinternaModeloSetup: no hay ningun " + NombreContenedor + " en la escena abierta.");
            return;
        }

        Undo.DestroyObjectImmediate(contenedor.gameObject);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // Variante para -executeMethod LinternaModeloSetup.Run (batch/CI), al estilo de
    // EnemigosExtraBuilder.Run: abre Prototype.unity, pone el modelo y guarda.
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
        Poner();
        EditorSceneManager.SaveOpenScenes();
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // ---------------------------------------------------------------- import

    static void ConfigurarImportadores()
    {
        if (AssetImporter.GetAtPath(FbxPath) is ModelImporter modelo)
        {
            // Sin materiales del FBX: los mapas embebidos apuntan a la carpeta .fbm original, que no
            // viajo al proyecto, asi que el material se arma a mano en ConseguirMaterial.
            modelo.materialImportMode = ModelImporterMaterialImportMode.None;
            modelo.animationType = ModelImporterAnimationType.None;
            modelo.importAnimation = false;
            modelo.importBlendShapes = false;
            modelo.importCameras = false;
            modelo.importLights = false;
            modelo.importVisibility = false;
            modelo.generateSecondaryUV = false; // el viewmodel nunca entra en un lightmap
            modelo.isReadable = false;          // no se lee el mesh en runtime: ahorra la copia en RAM
            modelo.SaveAndReimport();
        }

        if (AssetImporter.GetAtPath(BaseColorPath) is TextureImporter color)
        {
            color.textureType = TextureImporterType.Default;
            color.sRGBTexture = true;
            color.mipmapEnabled = true;
            color.maxTextureSize = 512; // lo que mide el PNG: no tiene sentido pedir mas
            color.SaveAndReimport();
        }

        if (AssetImporter.GetAtPath(NormalPath) is TextureImporter normal)
        {
            normal.textureType = TextureImporterType.NormalMap;
            normal.mipmapEnabled = true;
            normal.maxTextureSize = 128;
            normal.SaveAndReimport();
        }
    }

    static Material ConseguirMaterial()
    {
        var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("LinternaModeloSetup: no se encontro el shader 'Universal Render Pipeline/Lit'.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(CarpetaMateriales))
                AssetDatabase.CreateFolder(CarpetaModelo, "Materials");

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.SetColor("_BaseColor", Color.white);
        if (baseColor != null) material.SetTexture("_BaseMap", baseColor);

        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
        }

        // Valores constantes en lugar del metallic/roughness del pack: ver el comentario de arriba.
        // Una linterna es carcasa semi-mate con partes metalicas, de ahi un metallic intermedio.
        material.SetFloat("_Metallic", 0.45f);
        material.SetFloat("_Smoothness", 0.55f);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    // ---------------------------------------------------------------- escena

    static FlashlightController BuscarControlador()
    {
        return Object.FindAnyObjectByType<FlashlightController>(FindObjectsInactive.Include);
    }

    static Transform BuscarAjuste()
    {
        FlashlightController controlador = BuscarControlador();
        Transform contenedor = controlador != null ? controlador.transform.Find(NombreContenedor) : null;
        if (contenedor == null)
        {
            Debug.Log("LinternaModeloSetup: no hay ningun " + NombreContenedor + " en la escena abierta.");
            return null;
        }

        Transform ajuste = contenedor.Find(NombreAjuste);
        if (ajuste == null)
            Debug.Log("LinternaModeloSetup: " + NombreContenedor + " no tiene el nodo " + NombreAjuste + " adentro.");

        return ajuste;
    }

    // Mide el modelo y deja el nodo de ajuste con la escala, el giro y el centrado que hacen que el
    // modelo mida LargoDeseado metros, quede centrado en el origen del contenedor y tenga su lado
    // largo sobre +Z (el eje al que apunta la camara, y por lo tanto el haz de la linterna).
    static void Acomodar(Transform ajuste, GameObject instancia, out float escala, out int vertices)
    {
        escala = 1f;

        if (!Medir(ajuste, instancia, out Bounds bounds, out vertices))
        {
            Debug.LogWarning("LinternaModeloSetup: el modelo no tiene ningun mesh, no se puede medir.");
            return;
        }

        Vector3 tam = bounds.size;
        float largo = Mathf.Max(tam.x, Mathf.Max(tam.y, tam.z));
        if (largo <= Mathf.Epsilon)
        {
            Debug.LogWarning("LinternaModeloSetup: el modelo mide 0, se deja la escala en 1.");
            return;
        }

        escala = LargoDeseado / largo;

        Quaternion giro;
        if (largo == tam.x) giro = Quaternion.Euler(0f, -90f, 0f);      // +X local -> +Z
        else if (largo == tam.y) giro = Quaternion.Euler(90f, 0f, 0f);  // +Y local -> +Z
        else giro = Quaternion.identity;                                // ya esta sobre +Z

        ajuste.localScale = Vector3.one * escala;
        ajuste.localRotation = giro;
        // Con esta posicion el centro del modelo cae en el origen del contenedor: asi la pose de la
        // mano es una posicion en pantalla y no queda mezclada con el centrado que traiga el FBX.
        ajuste.localPosition = -(giro * (bounds.center * escala));
    }

    // Bounds de todos los meshes del modelo expresados en el espacio local de 'referencia', que
    // llega todavia en identidad. Se mide asi y no con Renderer.bounds porque esos son bounds de
    // MUNDO: colgados de la camara saldrian rotados por donde mire el jugador, y la caja envolvente
    // de un objeto rotado es mas grande que el objeto.
    static bool Medir(Transform referencia, GameObject instancia, out Bounds bounds, out int vertices)
    {
        bounds = new Bounds();
        vertices = 0;
        bool hayAlguno = false;

        Matrix4x4 aReferencia = referencia.worldToLocalMatrix;
        foreach (MeshFilter filtro in instancia.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filtro.sharedMesh;
            if (mesh == null) continue;

            vertices += mesh.vertexCount;
            // El producto incluye el transform del nodo raiz del FBX, que es justamente lo que no se
            // toca: cualquier conversion de unidades o de ejes que traiga queda dentro de la medida.
            Bounds caja = Transformar(aReferencia * filtro.transform.localToWorldMatrix, mesh.bounds);

            if (!hayAlguno)
            {
                bounds = caja;
                hayAlguno = true;
            }
            else bounds.Encapsulate(caja);
        }

        return hayAlguno;
    }

    // Una caja rotada no se puede transformar transformando centro y tamaño: hay que pasar las 8
    // esquinas por la matriz y volver a envolverlas.
    static Bounds Transformar(Matrix4x4 matriz, Bounds caja)
    {
        Vector3 e = caja.extents;
        var resultado = new Bounds(matriz.MultiplyPoint3x4(caja.center - e), Vector3.zero);

        for (int i = 1; i < 8; i++)
        {
            var esquina = new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);
            resultado.Encapsulate(matriz.MultiplyPoint3x4(caja.center + esquina));
        }

        return resultado;
    }

    static int AplicarMaterial(GameObject instancia, Material material)
    {
        int ranuras = 0;

        foreach (Renderer renderer in instancia.GetComponentsInChildren<Renderer>(true))
        {
            var materiales = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
            for (int i = 0; i < materiales.Length; i++) materiales[i] = material;
            renderer.sharedMaterials = materiales;
            ranuras += materiales.Length;
        }

        return ranuras;
    }

    // La camara tenia near clip 0.3: cualquier cosa a menos de 30 cm se corta, y un modelo en
    // primera persona vive justamente ahi. Con reversed-Z la precision de profundidad aguanta de
    // sobra el rango 0.05-1000 de este laberinto.
    static string AjustarNearClip(FlashlightController controlador)
    {
        Camera camara = controlador.GetComponentInParent<Camera>();
        if (camara == null)
        {
            Debug.LogWarning("LinternaModeloSetup: no hay ninguna Camera arriba del FlashlightController, " +
                "no se pudo revisar el near clip: si el modelo se ve cortado, bajalo a mano.");
            return "sin camara para revisar el near clip";
        }

        float anterior = camara.nearClipPlane;
        if (anterior <= NearClipViewmodel) return "near clip de la camara ya en " + anterior;

        Undo.RecordObject(camara, "Bajar near clip para el viewmodel");
        camara.nearClipPlane = NearClipViewmodel;
        return "near clip de la camara " + anterior + " -> " + NearClipViewmodel;
    }

    static void EngancharAlControlador(FlashlightController controlador, GameObject contenedor)
    {
        Undo.RecordObject(controlador, "Enganchar el modelo de linterna");
        var serializado = new SerializedObject(controlador);
        SerializedProperty campo = serializado.FindProperty("modeloLinterna");
        if (campo == null)
        {
            Debug.LogWarning("LinternaModeloSetup: FlashlightController no tiene el campo 'modeloLinterna'.");
            return;
        }

        campo.objectReferenceValue = contenedor;
        serializado.ApplyModifiedProperties();
    }
}
