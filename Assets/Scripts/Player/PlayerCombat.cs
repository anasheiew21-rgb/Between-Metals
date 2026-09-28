using System;
using UnityEngine;

// Ataque cuerpo a cuerpo del jugador (HU-14 / T14-F). Al presionar Atacar dispara un SphereCast
// corto desde la camara; si conecta con un IDamageable (buscado con GetComponentInParent, igual
// que PlayerInteraction, para tolerar que el collider este en un hijo/mesh) le aplica daño. Cada
// intento de ataque -conecte o no- consume estamina como recurso secundario y arranca un
// cooldown, igual que EnemyAI hace del otro lado con su propio ataque.
public class PlayerCombat : MonoBehaviour
{
    [Header("Ataque")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackRadius = 0.4f;
    [SerializeField] private float attackDamage = 25f;
    [SerializeField] private float attackCooldown = 0.8f;
    [SerializeField] private LayerMask capasAtaque = ~0;

    [Header("Cámara")]
    [SerializeField] private Camera playerCamera;

    // Gancho para el Animator (aun sin Animator en el proyecto): se dispara justo al ejecutar un
    // ataque valido, haya impactado o no. No reemplaza al cooldown, que es quien decide si el
    // ataque se ejecuta.
    public event Action AlAtacar;

    private PlayerStats stats;
    private float cooldownRestante;

    void Start()
    {
        stats = GetComponent<PlayerStats>();
    }

    void Update()
    {
        if (cooldownRestante > 0f) cooldownRestante -= Time.deltaTime;

        // Con el menu de pausa abierto, o el jugador muerto, no se ataca (mismo guard de Menu.IsOpen
        // que usa PlayerInteraction para 'E').
        if (Menu.IsOpen || (stats != null && !stats.EstaViva)) return;

        if (KeyBindings.Down(KeyBindings.Action.Attack)) IntentarAtacar();
    }

    void IntentarAtacar()
    {
        // Limite estricto de animacion (T14-F): mientras el cooldown no llego a 0, ningun otro
        // chequeo se evalua ni se dispara AlAtacar.
        if (cooldownRestante > 0f) return;
        if (stats != null && !stats.PuedeAtacar) return;

        if (playerCamera == null)
        {
            Debug.LogWarning("PlayerCombat: no hay una cámara asignada.");
            return;
        }

        cooldownRestante = attackCooldown;
        stats?.ConsumirEstaminaAtaque();
        AlAtacar?.Invoke();

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.SphereCast(ray, attackRadius, out RaycastHit hit, attackRange, capasAtaque, QueryTriggerInteraction.Ignore))
        {
            IDamageable objetivo = hit.collider.GetComponentInParent<IDamageable>();
            objetivo?.TakeDamage(attackDamage);
        }
    }
}
