using UnityEngine;

// Traduce el estado de EnemyAI/EnemyHealth a los parametros de Creature.controller (Speed/Attack/
// Die). No sabe nada de NavMesh ni de IA mas alla de esos dos enganches, mismo desacople que usa
// EnemyHitFeedback con EnemyHealth.AlRecibirDaño.
public class EnemyAnimator : MonoBehaviour
{
    static readonly int SpeedParam = Animator.StringToHash("Speed");
    static readonly int AttackParam = Animator.StringToHash("Attack");
    static readonly int DieParam = Animator.StringToHash("Die");

    Animator animator;
    EnemyAI enemyAI;
    EnemyHealth salud;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        enemyAI = GetComponent<EnemyAI>();
        salud = GetComponent<EnemyHealth>();

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
        animator.SetFloat(SpeedParam, enemyAI.VelocidadActual);
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
