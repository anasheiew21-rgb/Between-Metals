using UnityEngine;

// Traduce el estado de EnemyAI/EnemyHealth a los parametros de Creature.controller (Speed/Attack/
// Die). No sabe nada de NavMesh ni de IA mas alla de esos dos enganches, mismo desacople que usa
// EnemyHitFeedback con EnemyHealth.AlRecibirDaño.
//
// Speed se alimenta de la velocidad real del NavMeshAgent (EnemyAI.VelocidadActual), no de una
// constante por estado: asi el blend idle/walking/run acompana lo que el cuerpo hace de verdad y la
// animacion no se ve "patinando" cuando el agente frena en una esquina o al llegar a un waypoint.
public class EnemyAnimator : MonoBehaviour
{
    static readonly int SpeedParam = Animator.StringToHash("Speed");
    static readonly int AttackParam = Animator.StringToHash("Attack");
    static readonly int DieParam = Animator.StringToHash("Die");

    [Tooltip("Suavizado del parametro Speed (segundos). 0 lo pasa tal cual; un valor chico evita " +
             "que el blend salte de golpe entre caminar y correr")]
    [SerializeField] private float suavizadoVelocidad = 0.1f;

    Animator animator;
    EnemyAI enemyAI;
    EnemyHealth salud;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        enemyAI = GetComponent<EnemyAI>();
        salud = GetComponent<EnemyHealth>();

        // Red de seguridad por si este componente se usa sin EnemyAI (o si alguien volvio a
        // prender Apply Root Motion en el Animator del modelo): el desplazamiento lo manda la
        // navegacion, la animacion se queda en el lugar. Ver EnemyAI.PrepararModelo.
        if (animator != null) animator.applyRootMotion = false;

        if (enemyAI != null) enemyAI.AlAtacar += ManejarAtaque;
        if (salud != null) salud.AlMorir += ManejarMuerte;
    }

    void OnDestroy()
    {
        if (enemyAI != null) enemyAI.AlAtacar -= ManejarAtaque;
        if (salud != null) salud.AlMorir -= ManejarMuerte;
    }

    void Update()
    {
        if (animator == null || enemyAI == null) return;

        if (suavizadoVelocidad > 0f)
        {
            animator.SetFloat(SpeedParam, enemyAI.VelocidadActual, suavizadoVelocidad, Time.deltaTime);
        }
        else
        {
            animator.SetFloat(SpeedParam, enemyAI.VelocidadActual);
        }
    }

    void ManejarAtaque()
    {
        if (animator != null) animator.SetTrigger(AttackParam);
    }

    void ManejarMuerte()
    {
        if (animator != null) animator.SetTrigger(DieParam);
        enabled = false;
    }
}
