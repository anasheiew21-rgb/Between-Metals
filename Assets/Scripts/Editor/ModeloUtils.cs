using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Utilidades para meter en el juego un FBX del que no se sabe nada: ni en que unidades viene, ni
// centrado en que punto, ni con que eje para adelante. Los modelos de Tripo que usa el proyecto
// llegan asi, y adivinar una escala a mano termina siempre en un objeto gigante o invisible.
//
// La idea es la misma que ya aplicaba LinternaModeloSetup para la linterna: se MIDEN los bounds de
// los meshes y de esa medida salen la escala, el giro y el centrado. Aca esta generalizado para
// que lo usen tambien el arma y las llaves (ProgresionBuilder), con el largo y el eje destino como
// parametros.
//
// Jerarquia que espera, de tres niveles, uno por cada cosa que puede cambiar sin tocar las otras:
//   Raiz        pose final (la que se toca para acomodar el objeto en la mano o en el piso)
//   └── Ajuste  escala, orientacion y centrado, todo medido sobre el mesh
//       └── FBX la instancia del modelo, con su transform tal cual lo importo Unity
// El transform del FBX no se pisa nunca porque puede traer la conversion de unidades y de ejes del
// exportador: se mide con el adentro y se corrige por arriba.
//
// TODO: LinternaModeloSetup todavia tiene su propia copia de Medir/Transformar/Acomodar. Cuando
// haya que tocarla, conviene que pase a usar esto y quede una sola implementacion.
public static class ModeloUtils
{
    public enum Eje { X, Y, Z }

    /// <summary>
    /// Escala, gira y centra el modelo dentro de 'ajuste' (que tiene que llegar en identidad) para
    /// que su lado mas largo mida 'largoDeseado' metros y apunte a 'ejeDestino', y para que su
    /// centro caiga en el origen del padre de 'ajuste'.
    /// </summary>
    /// <param name="boundsFinales">
    /// Bounds del modelo ya acomodado, en el espacio del PADRE de 'ajuste': es la caja real que
    /// ocupa el objeto, para por ejemplo calcularle un Collider.
    /// </param>
    /// <returns>La escala uniforme que se aplico, o 1 si no se pudo medir.</returns>
    public static float Acomodar(Transform ajuste, GameObject instancia, float largoDeseado, Eje ejeDestino, out Bounds boundsFinales)
    {
        boundsFinales = new Bounds();

        if (!Medir(ajuste, instancia, out Bounds bounds, out _))
        {
            Debug.LogWarning($"ModeloUtils: '{instancia.name}' no tiene ningun mesh, no se puede medir.", instancia);
            return 1f;
        }

        Vector3 tam = bounds.size;
        float largo = Mathf.Max(tam.x, Mathf.Max(tam.y, tam.z));
        if (largo <= Mathf.Epsilon)
        {
            Debug.LogWarning($"ModeloUtils: '{instancia.name}' mide 0, se deja la escala en 1.", instancia);
            return 1f;
        }

        float escala = largoDeseado / largo;

        // FromToRotation en vez de una tabla de Euler: para ejes perpendiculares da el giro de 90
        // grados exacto, y para el mismo eje da identidad, sin casos especiales que mantener.
        Vector3 desde = largo == tam.x ? Vector3.right : largo == tam.y ? Vector3.up : Vector3.forward;
        Quaternion giro = Quaternion.FromToRotation(desde, Direccion(ejeDestino));

        ajuste.localScale = Vector3.one * escala;
        ajuste.localRotation = giro;
        // Con esta posicion el centro del modelo cae en el origen del padre: asi la pose final es
        // una posicion limpia y no queda mezclada con el centrado que traiga el FBX.
        ajuste.localPosition = -(giro * (bounds.center * escala));

        // Los bounds ya acomodados: el tamano se reordena segun el giro, y el centro queda en cero
        // justamente porque es lo que acaba de compensar localPosition.
        boundsFinales = new Bounds(Vector3.zero, Abs(giro * (tam * escala)));
        return escala;
    }

    /// <summary>
    /// Bounds de todos los meshes del modelo expresados en el espacio local de 'referencia'. Se
    /// mide asi y no con Renderer.bounds porque esos son bounds de MUNDO: colgado de la camara, o
    /// de un objeto girado, saldrian rotados, y la caja envolvente de un objeto rotado es mas
    /// grande que el objeto.
    /// </summary>
    public static bool Medir(Transform referencia, GameObject instancia, out Bounds bounds, out int vertices)
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
            // El producto incluye el transform del nodo raiz del FBX, que es justamente lo que no
            // se toca: cualquier conversion de unidades o de ejes que traiga queda dentro de la medida.
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

    /// <summary>
    /// Pasa una caja por una matriz. No alcanza con transformar centro y tamano: si la matriz gira,
    /// hay que pasar las 8 esquinas y volver a envolverlas.
    /// </summary>
    public static Bounds Transformar(Matrix4x4 matriz, Bounds caja)
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

    /// <summary>
    /// Apaga sombras en todo el modelo. Para objetos chicos (un item en el piso, un arma pegada a
    /// la camara) la sombra no aporta nada y el proyecto apunta a PCs de pocos recursos.
    /// </summary>
    public static void ApagarSombras(GameObject instancia)
    {
        foreach (Renderer renderer in instancia.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    /// <summary>
    /// Deja el FBX importado como lo que es: una malla estatica. Los modelos de Tripo llegan con
    /// animationType Generic, que le cuelga un Animator al vuelo a algo que no se mueve. No toca
    /// los materiales: los que genera Unity al importar ya vienen con sus texturas de la carpeta
    /// .fbm, a diferencia del caso de la linterna.
    /// </summary>
    public static void ConfigurarMallaEstatica(string rutaFbx)
    {
        if (AssetImporter.GetAtPath(rutaFbx) is not ModelImporter importador) return;

        bool cambio = false;

        if (importador.animationType != ModelImporterAnimationType.None) { importador.animationType = ModelImporterAnimationType.None; cambio = true; }
        if (importador.importAnimation) { importador.importAnimation = false; cambio = true; }
        if (importador.importBlendShapes) { importador.importBlendShapes = false; cambio = true; }
        if (importador.importCameras) { importador.importCameras = false; cambio = true; }
        if (importador.importLights) { importador.importLights = false; cambio = true; }
        if (importador.importVisibility) { importador.importVisibility = false; cambio = true; }
        // El item nunca entra en un lightmap, y leer el mesh en runtime solo gastaria RAM.
        if (importador.generateSecondaryUV) { importador.generateSecondaryUV = false; cambio = true; }
        if (importador.isReadable) { importador.isReadable = false; cambio = true; }

        // Reimportar es caro: solo si de verdad hay algo que cambiar.
        if (cambio) importador.SaveAndReimport();
    }

    /// <summary>
    /// Material de color plano guardado como asset, uno por nombre: se puede retocar desde el
    /// Inspector y lo comparten todas las piezas del mismo tipo (un material por objeto serian
    /// draw calls de mas, que es justo lo que no puede pagar una PC de pocos recursos).
    /// Si el asset ya existe no se toca, para no pisar un color que el equipo haya ajustado.
    /// </summary>
    public static Material MaterialDeColor(string carpeta, string nombre, Color color)
    {
        string ruta = carpeta + "/" + nombre + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material != null) return material;

        AsegurarCarpeta(carpeta);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        material = new Material(shader) { name = nombre };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);

        AssetDatabase.CreateAsset(material, ruta);
        AssetDatabase.SaveAssets();
        return material;
    }

    /// <summary>
    /// Arma un prefab a partir de un FBX: lo mide, lo escala a 'largo' metros, lo orienta a
    /// 'ejeDestino', lo centra y lo guarda en 'rutaPrefab'. Es la version compartida del patron que
    /// ya usaban ProgresionBuilder (arma y llaves) y ModelosItemsBuilder (items del piso).
    ///
    /// Jerarquia que deja, la de tres niveles que documenta la cabecera de esta clase:
    ///   Raiz        pose final, la que toca el que lo usa
    ///   └── Ajuste  escala, orientacion y centrado, todo medido sobre el mesh
    ///       └── FBX la instancia del modelo, con su transform tal cual lo importo Unity
    ///
    /// Se regenera en cada corrida a proposito: el prefab es una pieza derivada del FBX, no un asset
    /// que se edite a mano.
    /// </summary>
    /// <param name="rutaTextura">
    /// Opcional, para los FBX de packs de terceros: les pone un material URP del proyecto con esa
    /// textura (ver PintarConTextura). Los FBX de Tripo no lo necesitan.
    /// </param>
    /// <param name="apoyarEnElPiso">
    /// true deja la BASE del modelo en el origen en vez de su centro. Es lo que necesita un objeto
    /// tirado en el piso; un arma que cuelga de la camara, no.
    /// </param>
    /// <returns>El prefab guardado, o null -con un aviso- si el FBX no esta en el proyecto.</returns>
    public static GameObject GuardarPrefabDeFbx(string rutaFbx, string rutaPrefab, string nombre,
        float largo, Eje ejeDestino, string rutaTextura = null, string carpetaMateriales = null,
        string nombreMaterial = null, bool apoyarEnElPiso = false)
    {
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(rutaFbx);
        if (modelo == null)
        {
            // Log y no LogWarning: que falte el FBX de un pack que no esta en el repo es un caso
            // previsto, y el que llama tiene su placeholder de respaldo.
            Debug.Log($"ModeloUtils: no esta '{rutaFbx}'; '{nombre}' no se genera y queda su placeholder.");
            return null;
        }

        ConfigurarMallaEstatica(rutaFbx);
        AsegurarCarpeta(System.IO.Path.GetDirectoryName(rutaPrefab).Replace('\\', '/'));

        var raiz = new GameObject(nombre);

        try
        {
            var ajuste = new GameObject("Ajuste");
            ajuste.transform.SetParent(raiz.transform, false);

            var instancia = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
            instancia.transform.SetParent(ajuste.transform, false);

            float escala = Acomodar(ajuste.transform, instancia, largo, ejeDestino, out Bounds bounds);

            // Acomodar deja el modelo centrado en el origen; apoyarlo es subirlo medio alto.
            if (apoyarEnElPiso) ajuste.transform.localPosition += Vector3.up * (bounds.size.y * 0.5f);

            if (rutaTextura != null && carpetaMateriales != null)
            {
                PintarConTextura(instancia, carpetaMateriales, nombreMaterial ?? nombre, rutaTextura);
            }

            ApagarSombras(instancia);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, rutaPrefab);
            Debug.Log($"ModeloUtils: {rutaPrefab} generado desde {rutaFbx} " +
                $"(escala {escala:F4}, largo {largo} m sobre el eje {ejeDestino}).");
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(raiz);
        }
    }

    /// <summary>
    /// Reemplaza los materiales de un modelo por un material URP del proyecto con esa textura. Es lo
    /// que hace falta para los FBX de los packs de terceros: sus .mat apuntan al shader built-in y
    /// bajo URP se dibujan magenta. No se edita el .mat del pack porque es ajeno y se puede
    /// reimportar encima. Devuelve false, con un aviso, si la textura no esta.
    /// </summary>
    public static bool PintarConTextura(GameObject instancia, string carpetaMateriales,
        string nombreMaterial, string rutaTextura)
    {
        var textura = AssetDatabase.LoadAssetAtPath<Texture>(rutaTextura);
        if (textura == null)
        {
            Debug.LogWarning($"ModeloUtils: no esta la textura '{rutaTextura}'; '{instancia.name}' queda " +
                "con el material del pack y bajo URP se va a ver magenta.");
            return false;
        }

        Material material = MaterialConTextura(carpetaMateriales, nombreMaterial, textura);

        foreach (Renderer renderer in instancia.GetComponentsInChildren<Renderer>(true))
        {
            // sharedMaterials y no sharedMaterial: un mesh puede traer mas de un submesh, y en estos
            // packs todos los submeshes salen del mismo atlas.
            int cuantos = Mathf.Max(renderer.sharedMaterials.Length, 1);
            var materiales = new Material[cuantos];
            for (int i = 0; i < cuantos; i++) materiales[i] = material;
            renderer.sharedMaterials = materiales;
        }

        return true;
    }

    /// <summary>
    /// Material URP con una textura de color, guardado como asset, uno por nombre. Es el caso de un
    /// FBX que viene con su atlas: el material que trae el pack apunta al shader built-in y bajo URP
    /// se dibuja magenta, asi que el proyecto se arma el suyo con la misma textura en vez de editar
    /// un .mat de terceros. Si el asset ya existe no se toca, igual que MaterialDeColor.
    /// </summary>
    public static Material MaterialConTextura(string carpeta, string nombre, Texture textura)
    {
        string ruta = carpeta + "/" + nombre + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material != null) return material;

        AsegurarCarpeta(carpeta);

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        material = new Material(shader) { name = nombre };

        // _BaseMap es el nombre de la propiedad en URP y _MainTex el del built-in: se setean las dos
        // para que el material quede con la textura puesta con cualquiera de los dos shaders.
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", textura);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", textura);

        // Atlas de color plano y juego oscuro: con el smoothness por defecto el objeto se lee como
        // plastico mojado y el reflejo de la linterna le come la silueta.
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.2f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.2f);

        AssetDatabase.CreateAsset(material, ruta);
        AssetDatabase.SaveAssets();
        return material;
    }

    /// <summary>Crea la carpeta de assets y las que le falten por encima. No hace nada si ya existe.</summary>
    public static void AsegurarCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;

        string padre = System.IO.Path.GetDirectoryName(ruta).Replace('\\', '/');
        string nombre = System.IO.Path.GetFileName(ruta);
        AsegurarCarpeta(padre);
        AssetDatabase.CreateFolder(padre, nombre);
    }

    static Vector3 Direccion(Eje eje)
    {
        return eje switch
        {
            Eje.X => Vector3.right,
            Eje.Y => Vector3.up,
            _ => Vector3.forward,
        };
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}
