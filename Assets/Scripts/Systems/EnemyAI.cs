using UnityEngine;

// Enemigo con maquina de estados simple: Patrulla <-> Persecucion, mas ataque de cerca.
// Movimiento por fisica (Rigidbody + Collider), no NavMeshAgent: el laberinto generado por
// MapaBuilder no tiene un NavMesh horneado, y hornearlo es un paso de Editor aparte que no se
// puede hacer sin tener Unity abierto. Con Rigidbody el enemigo SI choca contra los muros (antes,
// sin Rigidbody, caia al ultimo recurso de mover el Transform directo, que no respeta colisiones).
// Si mas adelante se hornea un NavMesh, esto se puede migrar a NavMeshAgent.SetDestination sin
// tocar la maquina de estados (Patrullar/Perseguir siguen igual, solo cambia AplicarVelocidad).
[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    enum Estado { Patrulla, Persecucion }

    [Header("Patrulla")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float waitTimeAtWaypoint = 2f;
    [SerializeField] private float patrolSpeed = 2.5f;

    [Header("Deteccion (campo de vision)")]
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private float fieldOfViewAngle = 100f;
    [Tooltip("Altura desde los pies del enemigo hasta donde 'mira'")]
    [SerializeField] private float alturaOjos = 1.6f;
    [Tooltip("Capas que puede bloquear la vision (muros, etc). Si el enemigo se tapa la vista a si mismo, sacale su propia capa de aca")]
    [SerializeField] private LayerMask capasBloqueoVision = ~0;

    [Header("Persecucion")]
    [SerializeField] private float chaseSpeed = 4.5f;
    [SerializeField] private float loseTargetDistance = 15f;
    [Tooltip("Cuantos segundos sin ver al jugador (estando cerca) hasta volver a patrullar")]
    [SerializeField] private float tiempoSinVerParaAbandonar = 3f;

    [Header("Ataque")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.2f;

    Estado estado = Estado.Patrulla;
    int indiceWaypoint;
    float tiempoEsperando;
    float tiempoSinVerJugador;
    float cooldownAtaqueRestante;

    Transform jugador;
    PlayerStats statsJugador;
    Rigidbody rb;
    Collider[] propiosColliders; // para que el rayo de vision no se choque contra si mismo

    void Start()
    {
        // Se busca por PlayerStats en vez de por tag: en esta escena el objeto tageado "Player"
        // es una raiz que no se mueve, el que realmente camina es su hijo sin tag.
        statsJugador = FindAnyObjectByType<PlayerStats>();
        if (statsJugador != null) jugador = statsJugador.transform;

        propiosColliders = GetComponentsInChildren<Collider>();

        // Rigidbody no cinematico: asi lo frena de verdad un muro en vez de atravesarlo.
        // Sin gravedad porque el movimiento ya mantiene su propia altura (Y fija); sin rotacion
        // fisica porque la rotacion la maneja MirarHacia() a mano.
        // GetComponent en vez de confiar solo en [RequireComponent]: ese atributo agrega el
        // Rigidbody cuando el componente se suma desde el editor, pero no si ya estaba puesto en
        // el archivo de escena de antes (por eso el "object reference not set").
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void Update()
    {
        if (jugador == null) return;

        if (cooldownAtaqueRestante > 0f) cooldownAtaqueRestante -= Time.deltaTime;

        bool veAlJugador = PuedeVerAlJugador();

        switch (estado)
        {
            case Estado.Patrulla:
                Patrullar();
                if (veAlJugador) CambiarA(Estado.Persecucion);
                break;

            case Estado.Persecucion:
                Perseguir(veAlJugador);
                break;
        }
    }

    void CambiarA(Estado nuevo)
    {
        estado = nuevo;
        tiempoSinVerJugador = 0f;
        tiempoEsperando = 0f;
    }

    // ---------------------------------------------------------------
    // Patrulla
    // ---------------------------------------------------------------
    void Patrullar()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Transform destino = waypoints[indiceWaypoint];
        if (destino == null) return;

        bool llego = MoverHacia(destino.position, patrolSpeed);
        if (!llego)
        {
            tiempoEsperando = 0f;
            return;
        }

        tiempoEsperando += Time.deltaTime;
        if (tiempoEsperando >= waitTimeAtWaypoint)
        {
            tiempoEsperando = 0f;
            indiceWaypoint = (indiceWaypoint + 1) % waypoints.Length;
        }
    }

    // ---------------------------------------------------------------
    // Persecucion + ataque
    // ---------------------------------------------------------------
    void Perseguir(bool veAlJugador)
    {
        float distancia = Vector3.Distance(transform.position, jugador.position);

        if (veAlJugador) tiempoSinVerJugador = 0f;
        else tiempoSinVerJugador += Time.deltaTime;

        bool perdido = distancia > loseTargetDistance || tiempoSinVerJugador >= tiempoSinVerParaAbandonar;
        if (perdido)
        {
            CambiarA(Estado.Patrulla);
            return;
        }

        if (distancia <= attackRange)
        {
            DetenerMovimiento();
            MirarHacia(jugador.position);
            Atacar();
        }
        else
        {
            MoverHacia(jugador.position, chaseSpeed);
        }
    }

    void Atacar()
    {
        if (cooldownAtaqueRestante > 0f) return;
        cooldownAtaqueRestante = attackCooldown;
        if (statsJugador != null) statsJugador.TakeDamage(attackDamage);
    }

    // ---------------------------------------------------------------
    // Movimiento y vision
    // ---------------------------------------------------------------

    // Mueve hacia el destino (mismo plano Y que el enemigo) a la velocidad dada, via Rigidbody:
    // si en el camino hay un muro, el propio motor de fisica lo frena (no lo atraviesa).
    // Devuelve true cuando ya llego, para saber cuando esperar en un waypoint.
    bool MoverHacia(Vector3 destino, float velocidad)
    {
        Vector3 destinoPlano = new Vector3(destino.x, transform.position.y, destino.z);
        Vector3 diferencia = destinoPlano - transform.position;

        if (diferencia.magnitude < 0.15f)
        {
            DetenerMovimiento();
            return true;
        }

        AplicarVelocidad(diferencia.normalized * velocidad);
        MirarHacia(destinoPlano);
        return false;
    }

    void AplicarVelocidad(Vector3 velocidadDeseada)
    {
        rb.linearVelocity = velocidadDeseada;
    }

    void DetenerMovimiento()
    {
        rb.linearVelocity = Vector3.zero;
    }

    void MirarHacia(Vector3 objetivo)
    {
        Vector3 direccion = objetivo - transform.position;
        direccion.y = 0f;
        if (direccion.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direccion), 10f * Time.deltaTime);
    }

    bool PuedeVerAlJugador()
    {
        Vector3 origen = transform.position + Vector3.up * alturaOjos;
        Vector3 haciaJugador = jugador.position - origen;
        float distancia = haciaJugador.magnitude;

        if (distancia > detectionRadius) return false;

        float angulo = Vector3.Angle(transform.forward, haciaJugador);
        if (angulo > fieldOfViewAngle * 0.5f) return false;

        // RaycastAll (no Raycast simple) para poder ignorar el propio cuerpo del enemigo: con un
        // solo Raycast, si el origen queda pegado a su propio collider, el rayo pega ahi mismo y
        // el enemigo nunca "ve" nada (bug comun: el enemigo se tapa la vista a si mismo).
        Vector3 direccion = haciaJugador / distancia;
        RaycastHit[] impactos = Physics.RaycastAll(origen, direccion, distancia, capasBloqueoVision, QueryTriggerInteraction.Ignore);
        System.Array.Sort(impactos, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in impactos)
        {
            if (EsPropio(hit.collider)) continue;         // su propio cuerpo: se ignora
            return hit.transform == jugador;              // lo primero relevante: jugador (hay vision) o muro (no hay)
        }

        return true; // no choco contra nada en el camino
    }

    bool EsPropio(Collider col)
    {
        if (propiosColliders == null) return false;
        for (int i = 0; i < propiosColliders.Length; i++)
        {
            if (propiosColliders[i] == col) return true;
        }
        return false;
    }

    // Dibuja el radio de deteccion, el cono de vision y el rango de ataque en el editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Vector3 origen = transform.position + Vector3.up * alturaOjos;
        Gizmos.color = Color.yellow;
        Quaternion izquierda = Quaternion.AngleAxis(-fieldOfViewAngle * 0.5f, Vector3.up);
        Quaternion derecha = Quaternion.AngleAxis(fieldOfViewAngle * 0.5f, Vector3.up);
        Gizmos.DrawRay(origen, izquierda * transform.forward * detectionRadius);
        Gizmos.DrawRay(origen, derecha * transform.forward * detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = new Color(0.3f, 0.3f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, loseTargetDistance);

        if (waypoints != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawSphere(waypoints[i].position, 0.25f);
                Transform siguiente = waypoints[(i + 1) % waypoints.Length];
                if (siguiente != null) Gizmos.DrawLine(waypoints[i].position, siguiente.position);
            }
        }
    }
}
