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
