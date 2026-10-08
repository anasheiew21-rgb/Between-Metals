using UnityEngine;

// Flecha de la ballesta: viaja derecho, le pega al primer IDamageable que encuentra y si no se
// clava donde haya chocado (RF/HU de combate a distancia). La dispara Ballesta; una flecha puesta a
// mano en la escena tambien funciona, con los valores que tenga en el Inspector.
//
// Por que NO usa Rigidbody ni Collider, que es lo primero que uno probaria:
//
//   * Tunneling. A 45 m/s y 60 fps la flecha avanza 75 cm por frame, bastante mas que el grosor de
//     un muro del laberinto. Con colisiones por contacto se atravesaria paredes la mitad de las
//     veces, y el arreglo habitual (collisionDetectionMode = ContinuousDynamic) es justo el modo
//     mas caro del motor de fisica, en un proyecto apuntado a PCs de gama baja.
//   * Aca el movimiento se hace por transform y cada frame se lanza un Raycast por el SEGMENTO que
//     la flecha acaba de recorrer. No hay forma de saltearse un muro: si el muro esta en el
//     segmento, el raycast lo encuentra, vaya la flecha a la velocidad que vaya.
//   * De paso, sin Collider las flechas no chocan entre si, ni con el jugador, ni empujan al
//     enemigo, ni ensucian el NavMesh, y no hay ni un Rigidbody mas en la escena.
//
// Es el mismo criterio con el que PlayerCombat resuelve el cuerpo a cuerpo: un cast contra el mundo
// en el momento del golpe, no colliders viajando.
public class Flecha : MonoBehaviour
{
    [Header("Vuelo")]
    [Tooltip("Metros por segundo. Lo pisa Ballesta al disparar; esto es el valor por defecto.")]
    [SerializeField] private float velocidad = 45f;

    [Tooltip("Daño del impacto. Lo pisa Ballesta al disparar; esto es el valor por defecto.")]
    [SerializeField] private float dano = 100f;

    [Tooltip("Capas contra las que choca. Lo pisa Ballesta al disparar.")]
    [SerializeField] private LayerMask capas = ~0;

    [Header("Vida")]
    [Tooltip("Segundos que la flecha queda clavada en una pared antes de desaparecer.")]
    [SerializeField] private float segundosClavada = 4f;

    [Tooltip("Segundos de vuelo como maximo. Es la red de seguridad para la flecha que no choca " +
             "con nada (un disparo al cielo): sin esto quedaria viajando para siempre.")]
    [SerializeField] private float vidaMaxima = 6f;

    [Tooltip("Cuanto se mete la punta en la superficie al clavarse. Sin esto la flecha queda " +
             "apoyada justo en el plano del muro y se ve flotando.")]
    [SerializeField] private float penetracion = 0.08f;

    // Quien la disparo. El raycast ignora sus colliders: el jugador dispara desde su propia cabeza
    // y sin esto la flecha se clavaria en su CharacterController en el primer frame.
    private Transform duenio;

    private bool clavada;
    private float restante;

    // Buffer compartido para RaycastNonAlloc: el raycast de una flecha se resuelve entero dentro de
    // su propio Update, asi que una sola copia alcanza para todas y no se asigna memoria por frame
    // (Physics.RaycastAll si asigna un array nuevo en cada llamada).
    static readonly RaycastHit[] impactos = new RaycastHit[8];

    /// <summary>Daño que aplica esta flecha al impactar. Lo usan las pruebas.</summary>
    public float Dano => dano;

    /// <summary>Verdadero si la flecha ya choco con algo y dejo de volar.</summary>
    public bool Clavada => clavada;

    /// <summary>
    /// Pone la flecha en vuelo. 'direccion' no hace falta que venga normalizada. Ballesta llama a
    /// esto justo despues de instanciarla, para que la flecha herede el daño y la velocidad del arma
    /// que la disparo en vez de tener su propia copia de esos numeros.
    /// </summary>
    public void Lanzar(Vector3 direccion, float velocidad, float dano, Transform duenio, LayerMask capas)
    {
        if (direccion.sqrMagnitude > Mathf.Epsilon) transform.rotation = Quaternion.LookRotation(direccion.normalized, Vector3.up);

        this.velocidad = Mathf.Max(0.01f, velocidad);
        this.dano = dano;
        this.duenio = duenio;
        this.capas = capas;

        clavada = false;
        restante = vidaMaxima;
    }

    void Start()
    {
        // Para la flecha puesta a mano en la escena, que nunca paso por Lanzar.
        if (restante <= 0f) restante = vidaMaxima;
    }

    void Update()
    {
        if (clavada) return;

        restante -= Time.deltaTime;
        if (restante <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float paso = velocidad * Time.deltaTime;
        if (paso <= 0f) return;

        if (BuscarImpacto(paso, out RaycastHit impacto)) Impactar(impacto);
        else transform.position += transform.forward * paso;
    }

    // El primer impacto del segmento que la flecha va a recorrer este frame, salteando los colliders
    // del que disparo. Se buscan TODOS los del segmento y se elige el mas cercano valido, porque un
    // Raycast simple devolveria el del jugador y la flecha moriria en su propio pecho.
    bool BuscarImpacto(float paso, out RaycastHit impacto)
    {
        impacto = default;

        // QueryTriggerInteraction.Ignore, igual que el SphereCast de PlayerCombat: los triggers del
        // juego son zonas (el collider de recogida de un item, el ExitTrigger de la salida) y una
        // flecha no tiene que clavarse en el aire sobre una de ellas.
        int cuantos = Physics.RaycastNonAlloc(transform.position, transform.forward, impactos, paso,
            capas, QueryTriggerInteraction.Ignore);

        bool hayAlguno = false;
        for (int i = 0; i < cuantos; i++)
        {
            if (EsDelDuenio(impactos[i].collider)) continue;
            if (hayAlguno && impactos[i].distance >= impacto.distance) continue;

            impacto = impactos[i];
            hayAlguno = true;
        }

        return hayAlguno;
    }

    bool EsDelDuenio(Collider collider)
    {
        if (duenio == null || collider == null) return false;
        return collider.transform == duenio || collider.transform.IsChildOf(duenio);
    }

    void Impactar(RaycastHit impacto)
    {
        // GetComponentInParent y no GetComponent, igual que PlayerCombat y PlayerInteraction: en los
        // enemigos el EnemyHealth esta en la raiz y el collider puede estar en un hijo del modelo.
        IDamageable objetivo = impacto.collider.GetComponentInParent<IDamageable>();

        if (objetivo != null)
        {
            objetivo.TakeDamage(dano);
            // En el cuerpo del enemigo la flecha desaparece en el momento: dejarla clavada en algo
            // que se mueve, con un enemigo que al morir queda tirado, se ve peor que no dejar nada.
            Destroy(gameObject);
            return;
        }

        Clavar(impacto);
    }

    // Pared, piso o cualquier otra cosa sin vida: la flecha se para en seco y se queda un rato, que
    // es lo que le dice al jugador donde pego el tiro que fallo.
    void Clavar(RaycastHit impacto)
    {
        clavada = true;
        transform.position = impacto.point + transform.forward * penetracion;

        // Colgada de lo que le pego: si algun dia se le dispara a una puerta que gira o a un muro
        // secreto que se hunde, la flecha se va con el.
        transform.SetParent(impacto.collider.transform, true);

        Destroy(gameObject, segundosClavada);
    }
}
