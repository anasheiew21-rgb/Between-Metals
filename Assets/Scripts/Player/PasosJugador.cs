using UnityEngine;

// Pasos del jugador. Lo agrega PlayerController en Start (ver el comentario de alla): no hay que
// acordarse de ponerlo a mano en la escena, pero si esta puesto en el Inspector funciona igual y
// se respetan los valores que tenga configurados.
//
// La cadencia se mide por DISTANCIA recorrida, no por tiempo: un paso cada N metros. Asi el ritmo
// acompana solo lo que el cuerpo hace de verdad -caminar, correr, frenar en una esquina, quedarse
// quieto- sin tener que replicar aca la maquina de estados de PlayerController. Con un temporizador
// fijo los pasos siguen sonando al mismo ritmo mientras el jugador se empuja contra una pared sin
// avanzar, que es el bug clasico de este sistema.
//
// Mismo criterio que EnemyAnimator, que alimenta su parametro Speed de la velocidad real del
// agente en vez de una constante por estado.
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PasosJugador : MonoBehaviour
{
    [Header("Cadencia")]
    [Tooltip("Metros entre pasos caminando. Mas chico = pasos mas seguidos")]
    [SerializeField] private float zancadaCaminando = 2.6f;

    [Tooltip("Metros entre pasos corriendo. Algo mas largo que caminando: la zancada se estira")]
    [SerializeField] private float zancadaCorriendo = 2.9f;

    [Tooltip("Velocidad horizontal minima (m/s) para que cuente como movimiento")]
    [SerializeField] private float velocidadMinima = 0.6f;

    [Header("Sonido")]
    [Tooltip("Volumen de los pasos caminando")]
    [Range(0f, 1f)] [SerializeField] private float volumenCaminando = 0.5f;

    [Tooltip("Volumen de los pasos corriendo")]
    [Range(0f, 1f)] [SerializeField] private float volumenCorriendo = 0.75f;

    [Tooltip("Cuanto se mueve el tono al azar en cada paso (0 = siempre igual)")]
    [Range(0f, 0.3f)] [SerializeField] private float variacionDeTono = 0.09f;

    [Tooltip("Volumen del golpe al caer. 0 lo desactiva")]
    [Range(0f, 1f)] [SerializeField] private float volumenAterrizaje = 0.85f;

    CharacterController cuerpo;
    PlayerStats stats;
    AudioSource fuente;

    float distanciaAcumulada;
    int ultimoPaso = -1;
    bool estabaEnElSuelo = true;

    void Start()
    {
        cuerpo = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();

        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.loop = false;
        // 2D: son los pies del propio jugador, siempre a la misma distancia del oido. En 3D la
        // distancia a la camara (que esta en un hijo, a la altura de la cabeza) haria que los
        // pasos sonaran corridos hacia abajo y con paneo raro al girar.
        fuente.spatialBlend = 0f;
        AudioPreferences.RutearASfx(fuente);

        // El primer paso suena en cuanto arranca a caminar, no despues de una zancada entera.
        distanciaAcumulada = zancadaCaminando;
    }

    void Update()
    {
        if (fuente == null) return;

        // Muerto no camina. Mismo corte que PlayerController.Update, por el mismo motivo: si
        // algun dia esto corre sin la pantalla de Game Over que congela el tiempo, igual para.
        if (stats != null && !stats.EstaViva) return;

        bool enElSuelo = cuerpo.isGrounded;

        // Aterrizaje: un solo golpe, mas fuerte que un paso normal. Se chequea antes de acumular
        // distancia para que el salto no sume al contador mientras esta en el aire.
        if (enElSuelo && !estabaEnElSuelo)
        {
            Reproducir(volumenAterrizaje);
            distanciaAcumulada = 0f; // recien aterrizado, la proxima zancada arranca de cero
        }
        estabaEnElSuelo = enElSuelo;

        if (!enElSuelo) return; // en el aire no hay pasos

        // Solo el plano horizontal: caer o subir una rampa no cuenta como zancada.
        Vector3 horizontal = cuerpo.velocity;
        horizontal.y = 0f;
        float velocidad = horizontal.magnitude;

        if (velocidad < velocidadMinima)
        {
            // Quieto: se deja el contador cargado para que el proximo arranque suene al instante.
            distanciaAcumulada = Mathf.Max(distanciaAcumulada, ZancadaActual());
            return;
        }

        distanciaAcumulada += velocidad * Time.deltaTime;

        if (distanciaAcumulada < ZancadaActual()) return;

        distanciaAcumulada = 0f;
        Reproducir(Corriendo ? volumenCorriendo : volumenCaminando);
    }

    // PlayerStats.estaCorriendo lo escribe PlayerController cada frame con si ESTE frame se esta
    // corriendo de verdad (tecla apretada, moviendose y con estamina), asi que es la misma fuente
    // de verdad que usa el movimiento: los pasos no pueden desincronizarse de la velocidad real.
    bool Corriendo => stats != null && stats.estaCorriendo;

    float ZancadaActual()
    {
        float zancada = Corriendo ? zancadaCorriendo : zancadaCaminando;
        return Mathf.Max(zancada, 0.1f); // una zancada de 0 dispararia un paso por frame
    }

    // Nunca repite el mismo clip dos veces seguidas: con 4 variantes, repetir es lo que mas se
    // nota y lo que delata que son samples.
    void Reproducir(float volumen)
    {
        if (volumen <= 0f) return;

        AudioClip[] pasos = BibliotecaDeSonidos.Pasos;
        if (pasos.Length == 0) return;

        int indice;
        if (pasos.Length == 1 || ultimoPaso < 0)
        {
            // Primer paso de la partida: todavia no hay ninguno que evitar, sortea entre todos.
            indice = Random.Range(0, pasos.Length);
        }
        else
        {
            // Sortea entre los que NO son el ultimo y despues corrige el indice: reparto uniforme
            // entre esos, sin el sesgo que tendria reintentar hasta que salga distinto.
            indice = Random.Range(0, pasos.Length - 1);
            if (indice >= ultimoPaso) indice++;
        }
        ultimoPaso = indice;

        fuente.pitch = 1f + Random.Range(-variacionDeTono, variacionDeTono);
        fuente.PlayOneShot(pasos[indice], Mathf.Clamp01(volumen));
    }
}
