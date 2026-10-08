using UnityEngine;

// Antorcha de pared: hace parpadear una Point Light para que la luz no se vea como una lampara
// fija. Es lo mas barato que da ambiente en un pasillo oscuro, y encaja con la oscuridad permanente
// que fuerza EnvironmentManager.
//
// El parpadeo usa Mathf.PerlinNoise y no Random: el ruido de Perlin es continuo, asi que la llama
// "respira" en vez de dar saltos de un frame al otro. Cada antorcha arranca en un punto distinto
// del ruido (desfase sorteado en Awake) para que no parpadeen todas a la vez.
//
// Costo: una luz sin sombras y una llamada a PerlinNoise por frame. Las sombras de una point light
// en tiempo real son lo caro, y aca estan apagadas a proposito (el proyecto apunta a PCs de pocos
// recursos). Si hiciera falta mas margen, lo primero que conviene bajar es la CANTIDAD de
// antorchas, no su calidad: cada luz que alcanza a un objeto le suma trabajo al shader.
[RequireComponent(typeof(Light))]
public class Antorcha : MonoBehaviour
{
    [Header("Llama")]
    [Tooltip("Intensidad base de la luz. El parpadeo se mueve alrededor de este valor.")]
    [Min(0f)] [SerializeField] private float intensidadBase = 1.6f;

    [Tooltip("Cuanto sube y baja la intensidad, como fraccion de la base.")]
    [Range(0f, 1f)] [SerializeField] private float amplitud = 0.25f;

    [Tooltip("Que tan rapido parpadea. En 0 queda fija y Update no hace nada.")]
    [Min(0f)] [SerializeField] private float velocidad = 1.8f;

    [Tooltip("Cuanto se mueve el foco de la llama, en metros. 0 = la luz no se mueve.")]
    [Range(0f, 0.3f)] [SerializeField] private float temblor = 0.04f;

    Light luz;
    Vector3 posicionBase;
    float desfase;

    void Awake()
    {
        luz = GetComponent<Light>();
        posicionBase = transform.localPosition;

        // Desfase por objeto: sin esto, todas las antorchas del mapa parpadearian sincronizadas y
        // se notaria el truco. Random.value en Awake alcanza: no hace falta que sea reproducible.
        desfase = Random.value * 100f;

        AplicarIntensidad(1f);
    }

    void Update()
    {
        if (velocidad <= 0f || (amplitud <= 0f && temblor <= 0f)) return;

        float t = Time.time * velocidad + desfase;

        // PerlinNoise devuelve 0..1 con 0.5 de promedio: se centra en 0 para que el parpadeo suba
        // y baje alrededor de la intensidad base en vez de solo restarle.
        float ruido = Mathf.PerlinNoise(t, 0f) - 0.5f;
        AplicarIntensidad(1f + ruido * 2f * amplitud);

        if (temblor > 0f)
        {
            // Un segundo corte del ruido (desplazado en Y) para que el temblor no vaya atado al
            // brillo: si usaran el mismo valor, la luz se moveria justo cuando sube de intensidad.
            float rx = Mathf.PerlinNoise(t, 13.7f) - 0.5f;
            float rz = Mathf.PerlinNoise(t, 27.3f) - 0.5f;
            transform.localPosition = posicionBase + new Vector3(rx, 0f, rz) * temblor * 2f;
        }
    }

    void AplicarIntensidad(float factor)
    {
        if (luz != null) luz.intensity = Mathf.Max(0f, intensidadBase * factor);
    }
}
