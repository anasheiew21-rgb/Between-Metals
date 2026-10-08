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

        // Sin Animator este componente no puede hacer nada: ni el blend de locomocion ni el
        // trigger de ataque. Pasaba exactamente eso con los enemigos extra, que siguen siendo la
        // esfera placeholder sin modelo ni Animator, y el sintoma era "el enemigo me persigue pero
        // nunca lo veo atacar" sin una sola linea en la consola que lo explicara.
        //
        // No desactiva el componente ni corta nada: el dano no depende de la animacion (lo aplica
        // EnemyAI.OnAttackHit por distancia y angulo), asi que un enemigo sin modelo sigue siendo
        // peligroso. Solo avisa una vez que le falta la parte visual.
        if (animator == null)
        {
            Debug.LogWarning($"{name}: no tiene Animator en ningun hijo, asi que no va a reproducir " +
                "ninguna animacion (ni la de ataque). Corré Between Metals > Enemigos > " +
                "\"Duplicar modelo del enemigo 1 en los demas\" para darle el modelo del enemigo ya armado.", this);
        }

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
