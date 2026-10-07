using UnityEngine;

// Señal ambiental del laberinto (RF12 / HU-10, tarea T09-F #29).
//
// Es el vocabulario diegético con el que el entorno comunica que algo cambió: un objeto que
// ya está en el mundo desde el principio, apagado y discreto, y que en algún momento se
// enciende con un sonido de mecanismo y una luz cálida que sube progresivamente.
//
// Esta clase NO decide por qué ni cuándo se enciende: solo sabe encenderse. La detección de
// objetivos y los disparadores son la tarea técnica T09-T (#30), que llamará a Encender().
//
// La luz arranca con el componente Light deshabilitado (no solo con intensidad 0) para que no
// cueste nada antes de activarse, y sin sombras, respetando la restricción de "pocas luces en
// tiempo real" del proyecto. Sobrevive al EnvironmentManager porque este solo pisa la luz
// ambiental, la niebla y la Directional: nunca toca luces de tipo Point.
[DisallowMultipleComponent]
public class SenalAmbiental : MonoBehaviour
{
    [Header("Luz")]
    [Tooltip("Si se deja vacío se busca una Light en este objeto o en sus hijos al arrancar")]
    [SerializeField] private Light luz;

    [Tooltip("Mismo ámbar que Luz_Refugio: en este juego la luz cálida ya significa refugio / presencia")]
    [SerializeField] private Color colorLuz = new Color(1f, 0.74f, 0.42f);

    [Tooltip("Alcance corto: ilumina su plaza y derrama hacia arriba, pero no alumbra el laberinto")]
    [Range(5f, 40f)] [SerializeField] private float rango = 22f;

    [SerializeField] private float intensidadFinal = 2.2f;

    [Header("Encendido")]
    [Tooltip("El sonido suena primero y la luz empieza después: da tiempo a girar la cabeza")]
    [Min(0f)] [SerializeField] private float retrasoLuz = 0.5f;

    [Min(0.01f)] [SerializeField] private float duracionSubida = 2f;

    [Tooltip("Fracción inicial de la subida en la que la luz titila antes de asentarse")]
    [Range(0f, 0.9f)] [SerializeField] private float fraccionInestable = 0.35f;

    [Header("Audio")]
    [Tooltip("Si se deja vacío se busca/crea un AudioSource en este objeto al arrancar")]
    [SerializeField] private AudioSource fuenteAudio;

    [Tooltip("Opcional: si se deja vacío se genera por código un golpe de mecanismo. Se puede reemplazar por un archivo real sin tocar la lógica")]
    [SerializeField] private AudioClip sonidoMecanismo;

    [Tooltip("Coherente con la visibilidad de la niebla: lo que se oye y lo que se ve cuentan la misma historia")]
    [Min(1f)] [SerializeField] private float distanciaMaximaSonido = 50f;

    [Range(0f, 1f)] [SerializeField] private float volumen = 0.7f;

    // Clip generado una sola vez y compartido por todas las señales de la partida.
    static AudioClip clipGenerado;

    bool encendiendo;
    float tiempoDesdeActivacion;

    /// <summary>Verdadero desde que se llama a Encender(), aunque la luz todavía esté subiendo.</summary>
    public bool Activada { get; private set; }

    /// <summary>Verdadero cuando la subida terminó y la luz quedó estable.</summary>
    public bool Estable => Activada && !encendiendo;

    /// <summary>La luz de la señal, para que las pruebas puedan leer su estado.</summary>
    public Light Luz => luz;

    void Awake()
    {
        ResolverReferencias();
        Apagar();
    }

    void Update()
    {
        Avanzar(Time.deltaTime);
    }

    /// <summary>
    /// Enciende la señal. Es permanente: no vuelve a apagarse sola ni pulsa. Llamarla de nuevo
    /// mientras ya está activada no reinicia la secuencia.
    /// </summary>
    public void Encender()
    {
        if (Activada) return;

        ResolverReferencias();

        Activada = true;
        encendiendo = true;
        tiempoDesdeActivacion = 0f;

        ReproducirMecanismo();
    }

    /// <summary>
    /// Deja la señal apagada y lista para encenderse. Awake la llama; también sirve para
    /// previsualizar el estado inicial desde el Editor sin dar Play.
    /// </summary>
    public void Apagar()
    {
        Activada = false;
        encendiendo = false;
        tiempoDesdeActivacion = 0f;

        if (luz == null) return;

        luz.type = LightType.Point;
        luz.color = colorLuz;
        luz.range = rango;
        luz.shadows = LightShadows.None; // restricción de rendimiento del proyecto
        luz.intensity = 0f;
        luz.enabled = false;             // deshabilitada: no cuesta nada hasta que se activa
    }

    /// <summary>
    /// Avanza la secuencia de encendido. Update() delega acá; está expuesto para poder verificar
    /// la curva de forma determinista sin depender del reloj del juego (mismo recurso que
    /// EnvironmentManager.AplicarCieloYReflejos(forzarParaPruebas) o el reloj inyectado de
    /// InventoryPanelState).
    /// </summary>
    public void Avanzar(float deltaTiempo)
    {
        if (!encendiendo || luz == null) return;

        tiempoDesdeActivacion += deltaTiempo;

        // Tramo 1: todavía no empezó la luz. Solo se escuchó el mecanismo.
        if (tiempoDesdeActivacion < retrasoLuz) return;

        if (!luz.enabled) luz.enabled = true;

        float progreso = Mathf.Clamp01((tiempoDesdeActivacion - retrasoLuz) / duracionSubida);

        // Tramo 3: terminó la subida, queda fija.
        if (progreso >= 1f)
        {
            luz.intensity = intensidadFinal;
            encendiendo = false;
            return;
        }

        // Tramo 2: sube suave, con una inestabilidad que se apaga sola (algo que prende mal
        // y se asienta). No es un pulso: es parte de la rampa y termina con ella.
        luz.intensity = intensidadFinal * Mathf.SmoothStep(0f, 1f, progreso) * Titileo(progreso);
    }

    float Titileo(float progreso)
    {
        if (fraccionInestable <= 0f || progreso >= fraccionInestable) return 1f;

        float decaimiento = 1f - (progreso / fraccionInestable);
        float oscilacion = Mathf.Sin(tiempoDesdeActivacion * 47f) * 0.45f * decaimiento;
        return Mathf.Max(0.05f, 1f + oscilacion);
    }

    void ResolverReferencias()
    {
        if (luz == null) luz = GetComponentInChildren<Light>(true);
        AsegurarFuenteAudio();
    }

    // AudioSource 3D y ruteado al grupo "sfx" del MainMixer, para que el slider de efectos del
    // menú lo controle (mismo patrón que EnemyAI y PlayerStats).
    void AsegurarFuenteAudio()
    {
        if (fuenteAudio == null) fuenteAudio = GetComponent<AudioSource>();
        if (fuenteAudio == null) fuenteAudio = gameObject.AddComponent<AudioSource>();

        fuenteAudio.playOnAwake = false;
        fuenteAudio.loop = false;
        fuenteAudio.spatialBlend = 1f;                       // 3D: se oye desde dónde viene
        fuenteAudio.rolloffMode = AudioRolloffMode.Linear;
        fuenteAudio.minDistance = 3f;
        fuenteAudio.maxDistance = distanciaMaximaSonido;

        AudioMixerGroupSeguro();
    }

    void AudioMixerGroupSeguro()
    {
        // Si todavía no existe el asset del mixer, el AudioSource suena igual (directo al Master).
        var grupo = AudioPreferences.SfxGroup;
        if (grupo != null) fuenteAudio.outputAudioMixerGroup = grupo;
    }

    void ReproducirMecanismo()
    {
        if (fuenteAudio == null) return;

        AudioClip clip = sonidoMecanismo != null ? sonidoMecanismo : ObtenerClipGenerado();
        if (clip == null) return;

        fuenteAudio.PlayOneShot(clip, volumen); // una sola reproducción por activación
    }

    // Golpe de mecanismo generado por código: un grave amortiguado con un armónico metálico y
    // una cola corta. Evita depender de un asset de audio que el proyecto todavía no tiene; el
    // campo sonidoMecanismo permite reemplazarlo por un archivo real sin tocar nada de esto.
    static AudioClip ObtenerClipGenerado()
    {
        if (clipGenerado != null) return clipGenerado;

        const int frecuenciaMuestreo = 44100;
        const float duracion = 1.1f;
        int muestras = Mathf.RoundToInt(frecuenciaMuestreo * duracion);
        float[] datos = new float[muestras];

        // Ruido determinista: misma semilla = mismo golpe en todas las corridas.
        var azar = new System.Random(2909);

        for (int i = 0; i < muestras; i++)
        {
            float t = i / (float)frecuenciaMuestreo;

            float ataque = Mathf.Clamp01(t / 0.004f);
            float grave = Mathf.Sin(2f * Mathf.PI * 62f * t) * 0.60f * Mathf.Exp(-t * 3.2f);
            float metal = Mathf.Sin(2f * Mathf.PI * 311f * t) * 0.16f * Mathf.Exp(-t * 6.5f);
            float metal2 = Mathf.Sin(2f * Mathf.PI * 466f * t) * 0.07f * Mathf.Exp(-t * 8.5f);
            float golpe = (float)(azar.NextDouble() * 2.0 - 1.0) * 0.22f * Mathf.Exp(-t * 60f);

            datos[i] = Mathf.Clamp((grave + metal + metal2 + golpe) * ataque, -1f, 1f);
        }

        clipGenerado = AudioClip.Create("SenalAmbiental_Mecanismo", muestras, 1, frecuenciaMuestreo, false);
        clipGenerado.SetData(datos, 0);
        return clipGenerado;
    }
}
