using UnityEngine;

// Genera la zona segura de inicio (estilo "Glade" de Maze Runner): un claro con piso de cesped,
// muros perimetrales altos con una unica entrada hacia el laberinto, y una luz calida central que
// sirva de refugio visual frente a la oscuridad permanente del EnvironmentManager. Todo se construye
// en runtime a partir de primitivas para no depender de una escena/prefab hecho a mano, y tambien
// se puede previsualizar desde el Editor sin dar Play via el menu contextual del componente.
public class SpawnZoneBuilder : MonoBehaviour
{
    const string NombreContenedor = "ZonaSeguraGenerada";

    [Header("Estructura")]
    [Tooltip("Ancho (X), alto (Y) y profundidad (Z) de la zona segura")]
    [SerializeField] Vector3 zoneSize = new Vector3(30f, 10f, 30f);
    [SerializeField] float wallThickness = 1f;
    [Tooltip("Ancho del hueco en el muro frontal (+Z) que conecta con el laberinto")]
    [SerializeField] float entranceWidth = 4f;

    [Header("Materiales")]
    [SerializeField] Material grassMaterial;
    [SerializeField] Material wallMaterial;

    [Header("Iluminacion de refugio")]
    [SerializeField] Color lightColor = new Color(1f, 0.74f, 0.42f);
    [SerializeField] float lightRange = 25f;
    [SerializeField] float lightIntensity = 3f;

    // Punto de entrada principal: reconstruye la zona segura desde cero (limpiando la anterior si
    // existe) para poder iterar sobre los parametros del Inspector sin acumular copias en la escena.
    [ContextMenu("Construir Zona Segura")]
    public void ConstruirZonaSegura()
    {
        LimpiarZonaSegura();

        Transform contenedor = new GameObject(NombreContenedor).transform;
        contenedor.SetParent(transform, false);

        ConstruirPiso(contenedor);
        ConstruirMuros(contenedor);
        ConstruirLuz(contenedor);
    }

    [ContextMenu("Limpiar Zona Segura")]
    public void LimpiarZonaSegura()
    {
        Transform contenedorExistente = transform.Find(NombreContenedor);
        if (contenedorExistente == null) return;

        DestruirObjeto(contenedorExistente.gameObject);
    }

    void ConstruirPiso(Transform contenedor)
    {
        GameObject piso = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piso.name = "Piso";
        piso.transform.SetParent(contenedor, false);

        // El Cube primitivo ya trae BoxCollider, que es justo lo que necesita el piso.
        const float espesorPiso = 0.2f;
        piso.transform.localPosition = new Vector3(0f, -espesorPiso * 0.5f, 0f);
        piso.transform.localScale = new Vector3(zoneSize.x, espesorPiso, zoneSize.z);

        if (grassMaterial != null) piso.GetComponent<Renderer>().sharedMaterial = grassMaterial;
    }

    void ConstruirMuros(Transform contenedor)
    {
        float mitadX = zoneSize.x * 0.5f;
        float mitadZ = zoneSize.z * 0.5f;

        CrearMuro(contenedor, "Muro_Trasero", new Vector3(0f, 0f, -mitadZ), new Vector3(zoneSize.x, zoneSize.y, wallThickness));
        CrearMuro(contenedor, "Muro_Izquierdo", new Vector3(-mitadX, 0f, 0f), new Vector3(wallThickness, zoneSize.y, zoneSize.z));
        CrearMuro(contenedor, "Muro_Derecho", new Vector3(mitadX, 0f, 0f), new Vector3(wallThickness, zoneSize.y, zoneSize.z));

        // Muro frontal (+Z): se parte en dos mitades dejando exactamente "entranceWidth" de hueco
        // centrado, que es la abertura hacia el laberinto.
        float anchoHueco = Mathf.Clamp(entranceWidth, 0f, zoneSize.x);
        float anchoSegmento = (zoneSize.x - anchoHueco) * 0.5f;

        if (anchoSegmento <= 0f)
        {
            Debug.LogWarning("SpawnZoneBuilder: entranceWidth cubre todo el ancho de la zona, no se genera muro frontal.");
            return;
        }

        float offsetX = anchoHueco * 0.5f + anchoSegmento * 0.5f;
        CrearMuro(contenedor, "Muro_Frontal_Izquierdo", new Vector3(-offsetX, 0f, mitadZ), new Vector3(anchoSegmento, zoneSize.y, wallThickness));
        CrearMuro(contenedor, "Muro_Frontal_Derecho", new Vector3(offsetX, 0f, mitadZ), new Vector3(anchoSegmento, zoneSize.y, wallThickness));
    }

    void CrearMuro(Transform contenedor, string nombre, Vector3 posicionLocalEnPiso, Vector3 escala)
    {
        GameObject muro = GameObject.CreatePrimitive(PrimitiveType.Cube);
        muro.name = nombre;
        muro.transform.SetParent(contenedor, false);
        // posicionLocalEnPiso trae Y=0 (a nivel de piso); se sube medio alto para que la base del
        // muro apoye en el piso en vez de quedar centrada atravesandolo.
        muro.transform.localPosition = posicionLocalEnPiso + new Vector3(0f, zoneSize.y * 0.5f, 0f);
        muro.transform.localScale = escala;

        if (wallMaterial != null) muro.GetComponent<Renderer>().sharedMaterial = wallMaterial;
    }

    void ConstruirLuz(Transform contenedor)
    {
        GameObject luzObj = new GameObject("Luz_Refugio");
        luzObj.transform.SetParent(contenedor, false);
        luzObj.transform.localPosition = new Vector3(0f, zoneSize.y * 0.7f, 0f);

        Light luz = luzObj.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = lightColor;
        luz.range = lightRange;
        luz.intensity = lightIntensity;
    }

    static void DestruirObjeto(GameObject objeto)
    {
        if (Application.isPlaying) Destroy(objeto);
        else DestroyImmediate(objeto);
    }
}
