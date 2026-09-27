using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Enemigo con maquina de estados: Patrulla (o Deambular, si su ruta no es valida) -> Persecucion
// -> Investigar -> Patrulla, mas ataque de cerca. HU-08 / RF10 / RF11 (issue #58).
// Migrado de Rigidbody a NavMeshAgent: usa el NavMesh que arma NavMeshRuntimeBuilder en tiempo de
// carga. El Rigidbody se mantiene en la escena (lo exige [RequireComponent]) pero se vuelve
// cinematico: ya no empuja el movimiento, solo evita que otros sistemas de fisica lo ignoren.
[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    public enum Estado { Patrulla, Persecucion, Investigar }

    [Header("Patrulla")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float waitTimeAtWaypoint = 2f;
    [SerializeField] private float patrolSpeed = 2.5f;

    [Header("Deambular (si la ruta de patrulla no tiene 2 o mas waypoints validos)")]
    [SerializeField] private float radioDeambular = 25f;

    [Header("Deteccion (campo de vision)")]
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private float fieldOfViewAngle = 100f;
    [Tooltip("Altura desde los pies del enemigo hasta donde 'mira'")]
    [SerializeField] private float alturaOjos = 1.6f;
    [Tooltip("Capas que puede bloquear la vision (muros, etc). Si el enemigo se tapa la vista a si mismo, sacale su propia capa de aca")]
    [SerializeField] private LayerMask capasBloqueoVision = ~0;

    [Header("Oido (RF11: la via principal es evadir o esconderse)")]
    [SerializeField] private float radioOido = 12f;
    [Tooltip("Velocidad horizontal (CharacterController) del jugador a partir de la cual el enemigo lo puede oir")]
    [SerializeField] private float umbralVelocidadRuido = 6f;

    [Header("Persecucion")]
    [SerializeField] private float chaseSpeed = 4.5f;
    [SerializeField] private float loseTargetDistance = 15f;
    [Tooltip("Cuantos segundos sin ver al jugador (estando cerca) hasta pasar a investigar")]
    [SerializeField] private float tiempoSinVerParaAbandonar = 3f;

    [Header("Investigacion (ultima posicion conocida)")]
    [SerializeField] private float tiempoInvestigacion = 3f;

    [Header("Ataque")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.2f;

    const float IntervaloActualizarDestinoPersecucion = 0.2f;
    const float IntervaloDeteccion = 0.1f; // ~10 chequeos de vision por segundo (en vez de cada frame)
    const float TiempoMaximoDeambular = 15f;
    const float RadioBusquedaNavMesh = 3f;
    const int IntentosElegirPuntoDeambular = 10;
    const int MaxImpactosVision = 8;

    Estado estado = Estado.Patrulla;
    int indiceWaypoint;
    float tiempoEsperando;
    float tiempoSinVerJugador;
    float cooldownAtaqueRestante;
    float tiempoInvestigando;
    float tiempoDesdeUltimoDestino;
    float tiempoDesdeUltimaDeteccion;
    bool veAlJugadorCache;
    bool tienePuntoDeambular;
    float tiempoDeambulando;
    Vector3 puntoDeambular;
    Vector3 ultimaPosicionConocida;
    bool avisoNavMeshLogueado;
    bool avisoRutaLogueado;

    Transform jugador;
    PlayerStats statsJugador;
    CharacterController controladorJugador;
    NavMeshAgent agent;
    Rigidbody rb;
    Collider[] propiosColliders; // para que el rayo de vision no se choque contra si mismo
    readonly List<Transform> rutaValida = new List<Transform>();
    readonly RaycastHit[] impactosVision = new RaycastHit[MaxImpactosVision];
    // NavMeshPath no se puede construir en un inicializador de campo (Unity lo prohibe fuera de
    // Awake/Start), asi que se crea de manera perezosa la primera vez que hace falta.
    NavMeshPath caminoTemporal;

    NavMeshPath CaminoTemporal()
    {
        if (caminoTemporal == null) caminoTemporal = new NavMeshPath();
        return caminoTemporal;
    }

    public int CantidadWaypointsValidos => rutaValida.Count;
    public bool EstaDeambulando => rutaValida.Count < 2;
    public Estado EstadoActual => estado;

    void Start()
    {
        // Se busca por PlayerStats en vez de por tag: en esta escena el objeto tageado "Player"
        // es una raiz que no se mueve, el que realmente camina es su hijo sin tag.
        statsJugador = FindAnyObjectByType<PlayerStats>();
        if (statsJugador != null)
        {
            jugador = statsJugador.transform;
            controladorJugador = statsJugador.GetComponent<CharacterController>();
        }

        propiosColliders = GetComponentsInChildren<Collider>();

        InicializarNavegacion();
    }

    void Update()
    {
        if (jugador == null || agent == null) return;

        if (!agent.isOnNavMesh)
        {
            AsegurarSobreNavMesh();
            return;
        }

        if (cooldownAtaqueRestante > 0f) cooldownAtaqueRestante -= Time.deltaTime;

        // RF10: no hace falta chequear la vision cada frame, con ~10 veces por segundo alcanza.
        tiempoDesdeUltimaDeteccion += Time.deltaTime;
        if (tiempoDesdeUltimaDeteccion >= IntervaloDeteccion)
        {
            tiempoDesdeUltimaDeteccion = 0f;
            veAlJugadorCache = PuedeVerAlJugador();
        }
        bool veAlJugador = veAlJugadorCache;

        if (veAlJugador)
        {
            ultimaPosicionConocida = jugador.position;
            if (estado != Estado.Persecucion) CambiarA(Estado.Persecucion);
        }
        else if (estado == Estado.Patrulla)
        {
            RevisarOido();
        }

        switch (estado)
        {
            case Estado.Patrulla:
                Patrullar();
                break;

            case Estado.Persecucion:
                Perseguir(veAlJugador);
                break;

            case Estado.Investigar:
                Investigar();
                break;
        }
    }

    void CambiarA(Estado nuevo)
    {
        estado = nuevo;
        tiempoSinVerJugador = 0f;
        tiempoEsperando = 0f;
        tiempoInvestigando = 0f;
        tiempoDesdeUltimoDestino = 0f;
        tienePuntoDeambular = false;
        tiempoDeambulando = 0f;

        if (agent != null) agent.isStopped = false;
    }

    // ---------------------------------------------------------------
    // Navegacion (HU-08, #58)
    // ---------------------------------------------------------------

    // Publico y llamable repetidas veces (CP-ENE-07): agrega el NavMeshAgent si falta, lo ubica
    // sobre el NavMesh y reconstruye la ruta de patrulla valida. No agrega/quita componentes de
    // mas: el Rigidbody que ya trae el objeto solo se pasa a cinematico.
    public void InicializarNavegacion()
    {
        AsegurarAgente();
        AsegurarSobreNavMesh();
        ConstruirRutaValida();
    }

    void AsegurarAgente()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();

        agent.speed = patrolSpeed;
        agent.stoppingDistance = Mathf.Max(0.05f, attackRange - 0.3f);
        agent.acceleration = 12f;
        agent.updateRotation = false; // la rotacion la maneja MirarHacia() a mano, igual que antes

        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
    }

    // Inicializacion perezosa: si el NavMesh todavia no estaba armado cuando el agente se creo,
    // se reintenta cada frame (desde Update) hasta que SamplePosition encuentre un punto cercano.
    void AsegurarSobreNavMesh()
    {
        if (agent == null || agent.isOnNavMesh) return;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, RadioBusquedaNavMesh, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);

            // Si el enemigo arranca lejos del NavMesh (por ejemplo, con la Y de diseno en vez de
            // la de la geometria real del piso), Warp() solo no alcanza para que isOnNavMesh de
            // verdadero: hay que apagar y prender el agente ya con la posicion correcta para que
            // se vuelva a registrar sobre el NavMesh.
            if (!agent.isOnNavMesh)
            {
                agent.enabled = false;
                transform.position = hit.position;
                agent.enabled = true;
            }
        }
        else if (!avisoNavMeshLogueado)
        {
            avisoNavMeshLogueado = true;
            Debug.LogWarning($"{name}: no se encontro NavMesh a {RadioBusquedaNavMesh} m de {transform.position}. HU-08 #58");
        }
    }

    // Descarta waypoints null o sin camino completo hacia ellos. Con 2 o mas validos, patrulla
    // ciclica; si no, EstaDeambulando pasa a valer true y se usa ElegirPuntoDeambular().
    void ConstruirRutaValida()
    {
        rutaValida.Clear();
        List<string> descartados = null;

        if (waypoints != null)
        {
            for (int i = 0; i < waypoints.Length; i++)
            {
                Transform wp = waypoints[i];
                if (wp == null)
                {
                    if (descartados == null) descartados = new List<string>();
                    descartados.Add($"[{i}] null");
                    continue;
                }

                NavMeshPath camino = CaminoTemporal();
                bool caminoCompleto = NavMesh.CalculatePath(transform.position, wp.position, NavMesh.AllAreas, camino)
                    && camino.status == NavMeshPathStatus.PathComplete;

                if (!caminoCompleto)
                {
                    if (descartados == null) descartados = new List<string>();
                    descartados.Add($"[{i}] {wp.name} (sin camino completo)");
                    continue;
                }

                rutaValida.Add(wp);
            }
        }

        if (!avisoRutaLogueado && descartados != null)
        {
            avisoRutaLogueado = true;
            Debug.LogWarning($"{name}: waypoints descartados: {string.Join(", ", descartados)}. HU-08 #58");
        }

        indiceWaypoint = 0;
    }

    // ---------------------------------------------------------------
    // Patrulla / Deambular
    // ---------------------------------------------------------------
    void Patrullar()
    {
        if (agent != null) agent.speed = patrolSpeed;

        if (EstaDeambulando)
        {
            Deambular();
            return;
        }

        Transform destino = rutaValida[indiceWaypoint];
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
            indiceWaypoint = (indiceWaypoint + 1) % rutaValida.Count;
        }
    }

    void Deambular()
    {
        if (!tienePuntoDeambular && !ElegirPuntoDeambular(out puntoDeambular))
        {
            return; // no se encontro ningun punto valido este frame; se reintenta el proximo
        }
        tienePuntoDeambular = true;

        bool llego = MoverHacia(puntoDeambular, patrolSpeed);
        tiempoDeambulando += Time.deltaTime;

        if (llego || tiempoDeambulando >= TiempoMaximoDeambular)
        {
            tienePuntoDeambular = false;
            tiempoDeambulando = 0f;
        }
    }

    // Publico para las pruebas (CP-ENE-03): intenta varios puntos aleatorios dentro de
    // radioDeambular y solo acepta uno si esta sobre el NavMesh y tiene camino completo.
    public bool ElegirPuntoDeambular(out Vector3 punto)
    {
        for (int intento = 0; intento < IntentosElegirPuntoDeambular; intento++)
        {
            Vector2 circulo = Random.insideUnitCircle * radioDeambular;
            Vector3 candidato = transform.position + new Vector3(circulo.x, 0f, circulo.y);

            if (!NavMesh.SamplePosition(candidato, out NavMeshHit hit, radioDeambular, NavMesh.AllAreas))
                continue;

            NavMeshPath camino = CaminoTemporal();
            bool caminoCompleto = NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, camino)
                && camino.status == NavMeshPathStatus.PathComplete;

            if (caminoCompleto)
            {
                punto = hit.position;
                return true;
            }
        }

        punto = transform.position;
        return false;
    }

    // ---------------------------------------------------------------
    // Persecucion + ataque
    // ---------------------------------------------------------------
    void Perseguir(bool veAlJugador)
    {
        if (agent != null) agent.speed = chaseSpeed;

        float distancia = Vector3.Distance(transform.position, jugador.position);

        if (veAlJugador) tiempoSinVerJugador = 0f;
        else tiempoSinVerJugador += Time.deltaTime;

        bool perdido = distancia > loseTargetDistance || tiempoSinVerJugador >= tiempoSinVerParaAbandonar;
        if (perdido)
        {
            CambiarA(Estado.Investigar);
            return;
        }

        if (distancia <= attackRange)
        {
            DetenerMovimiento();
            MirarHacia(jugador.position);
            Atacar();
            return;
        }

        // El destino se actualiza como maximo cada 0.2s (no hace falta recalcular el camino cada
        // frame), pero la rotacion hacia el jugador se sigue actualizando siempre para que no se
        // vea trabada.
        tiempoDesdeUltimoDestino += Time.deltaTime;
        if (tiempoDesdeUltimoDestino >= IntervaloActualizarDestinoPersecucion)
        {
            tiempoDesdeUltimoDestino = 0f;
            MoverHacia(jugador.position, chaseSpeed);
        }
        MirarHacia(jugador.position);
    }

    void Atacar()
    {
        if (cooldownAtaqueRestante > 0f) return;
        cooldownAtaqueRestante = attackCooldown;
        if (statsJugador != null) statsJugador.TakeDamage(attackDamage);
    }

    // ---------------------------------------------------------------
    // Investigar (ultima posicion conocida, tras perder de vista o por oido)
    // ---------------------------------------------------------------
    void Investigar()
    {
        if (agent != null) agent.speed = patrolSpeed;

        bool llego = MoverHacia(ultimaPosicionConocida, patrolSpeed);
        if (!llego) return;

        tiempoInvestigando += Time.deltaTime;
        if (tiempoInvestigando >= tiempoInvestigacion)
        {
            CambiarA(Estado.Patrulla);
        }
    }

    // RF11: si el jugador corre (mas rapido que umbralVelocidadRuido) dentro de radioOido, no lo
    // ve pero hay un camino completo hasta el, el enemigo va a investigar el ruido.
    void RevisarOido()
    {
        float distancia = Vector3.Distance(transform.position, jugador.position);
        float velocidadJugador = VelocidadHorizontalJugador();

        if (!DebeInvestigarPorRuido(distancia, velocidadJugador, radioOido, umbralVelocidadRuido, false))
            return;

        NavMeshPath camino = CaminoTemporal();
        bool caminoCompleto = NavMesh.CalculatePath(transform.position, jugador.position, NavMesh.AllAreas, camino)
            && camino.status == NavMeshPathStatus.PathComplete;
        if (!caminoCompleto) return;

        ultimaPosicionConocida = jugador.position;
        CambiarA(Estado.Investigar);
    }

    float VelocidadHorizontalJugador()
    {
        if (controladorJugador == null) return 0f;
        Vector3 v = controladorJugador.velocity;
        v.y = 0f;
        return v.magnitude;
    }

    // Helper estatico puro (sin estado), para poder probar la tabla de verdad sin escena (CP-ENE-05).
    public static bool DebeInvestigarPorRuido(float distancia, float velocidadJugador, float radioOido, float umbralVelocidadRuido, bool loVe)
    {
        if (loVe) return false;
        if (distancia > radioOido) return false;
        return velocidadJugador > umbralVelocidadRuido;
    }

    // ---------------------------------------------------------------
    // Movimiento y vision
    // ---------------------------------------------------------------

    // Mueve el NavMeshAgent hacia el destino a la velocidad dada. Devuelve true cuando ya llego
    // (dentro de stoppingDistance y sin velocidad), para saber cuando esperar/reelegir destino.
    bool MoverHacia(Vector3 destino, float velocidad)
    {
        if (agent == null || !agent.isOnNavMesh) return false;

        agent.speed = velocidad;
        agent.isStopped = false;
        agent.SetDestination(destino);
        MirarHacia(destino);

        return !agent.pathPending
            && agent.remainingDistance <= agent.stoppingDistance
            && (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f);
    }

    void DetenerMovimiento()
    {
        if (agent != null) agent.isStopped = true;
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

        // RaycastNonAlloc (no RaycastAll) para no reservar memoria cada frame: mismo buffer fijo
        // reutilizado, ordenado a mano por distancia para poder ignorar el propio collider del
        // enemigo (si el origen queda pegado a su propio cuerpo, el primer impacto seria consigo
        // mismo y nunca "veria" nada).
        Vector3 direccion = haciaJugador / distancia;
        int cantidad = Physics.RaycastNonAlloc(origen, direccion, impactosVision, distancia, capasBloqueoVision, QueryTriggerInteraction.Ignore);
        OrdenarPorDistancia(impactosVision, cantidad);

        for (int i = 0; i < cantidad; i++)
        {
            RaycastHit hit = impactosVision[i];
            if (EsPropio(hit.collider)) continue;         // su propio cuerpo: se ignora
            return hit.transform == jugador;              // lo primero relevante: jugador (hay vision) o muro (no hay)
        }

        return true; // no choco contra nada en el camino
    }

    static void OrdenarPorDistancia(RaycastHit[] impactos, int cantidad)
    {
        for (int i = 1; i < cantidad; i++)
        {
            RaycastHit actual = impactos[i];
            int j = i - 1;
            while (j >= 0 && impactos[j].distance > actual.distance)
            {
                impactos[j + 1] = impactos[j];
                j--;
            }
            impactos[j + 1] = actual;
        }
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

    // Dibuja los radios de vision, de oido y de deambular, el rango de ataque y la ruta de
    // patrulla (la ya validada en juego, o los waypoints crudos en edicion, para quien arme el mapa).
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

        // RF11: radio de oido
        Gizmos.color = new Color(1f, 0.4f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radioOido);

        // Radio de deambular (se usa cuando la ruta de patrulla no tiene 2+ waypoints validos)
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, radioDeambular);

        IList<Transform> ruta = Application.isPlaying && rutaValida.Count > 0 ? (IList<Transform>)rutaValida : waypoints;
        if (ruta != null && ruta.Count > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < ruta.Count; i++)
            {
                if (ruta[i] == null) continue;
                Gizmos.DrawSphere(ruta[i].position, 0.25f);
                Transform siguiente = ruta[(i + 1) % ruta.Count];
                if (siguiente != null) Gizmos.DrawLine(ruta[i].position, siguiente.position);
            }
        }
    }
}
