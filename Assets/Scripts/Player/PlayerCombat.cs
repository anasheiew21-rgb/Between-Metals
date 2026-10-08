using System;
using UnityEngine;

// Ataque del jugador (HU-14 / T14-F). Es el UNICO lugar que lee el input de atacar
// (KeyBindings.Action.Attack, clic izquierdo por defecto y remapeable desde el menu de opciones) y
// lo rutea segun lo que haya en la mano:
//
//   * Con la BALLESTA empunada: delega en Ballesta.Disparar(). El tiro tiene su propia cadencia y
//     NO gasta estamina: la ballesta es mecanica, lo que cansa es el brazo del cuerpo a cuerpo. Por
//     eso se decide antes que cualquier chequeo de estamina o de cooldown del golpe.
//   * Con la daga o a mano limpia: el golpe cuerpo a cuerpo de siempre, un SphereCast corto desde la
//     camara; si conecta con un IDamageable (buscado con GetComponentInParent, igual que
//     PlayerInteraction, para tolerar que el collider este en un hijo/mesh) le aplica daño. Cada
//     intento -conecte o no- consume estamina como recurso secundario y arranca un cooldown, igual
//     que EnemyAI hace del otro lado con su propio ataque.
//
// Que el input viva en un solo lado es lo que evita el bug obvio de tener dos armas: si la ballesta
// leyera el clic por su cuenta, con la ballesta en la mano un clic dispararia Y pegaria un golpe.
public class PlayerCombat : MonoBehaviour
{
    [Header("Ataque")]
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackRadius = 0.4f;
    [SerializeField] private float attackCooldown = 0.8f;
    [SerializeField] private LayerMask capasAtaque = ~0;

    [Header("Daño")]
    [Tooltip("Daño de un golpe a mano limpia. Contra los 200 de vida de un enemigo: 10 golpes.")]
    [SerializeField] private float danoBase = 20f;

    [Tooltip("Daño de un golpe con el arma que vende el comerciante. Contra 200 de vida: 3 golpes.")]
    [SerializeField] private float danoConArma = 75f;

    [Header("Cámara")]
    [SerializeField] private Camera playerCamera;

    [Header("Equipo")]
    [Tooltip("Si se deja vacío se busca un EquipoJugador en el jugador o en la escena.")]
    [SerializeField] private EquipoJugador equipo;

    // Gancho para el Animator (aun sin Animator en el proyecto): se dispara justo al ejecutar un
    // ataque valido, haya impactado o no. No reemplaza al cooldown, que es quien decide si el
    // ataque se ejecuta.
    public event Action AlAtacar;

    private PlayerStats stats;
    private float cooldownRestante;

    // Se cachea aparte del campo serializado, para no escribir nunca un campo serializado desde
    // código (en modo edición ensuciaría la escena, mismo criterio que EfectosDeItem).
    private EquipoJugador equipoResuelto;

    /// <summary>
    /// Daño que hace el golpe de ahora: con arma equipada o a mano limpia. Se resuelve en cada
    /// golpe (no se cachea) porque el arma se compra y se vende en medio de la partida.
    /// </summary>
    public float DanoActual => ResolverEquipo()?.ArmaEquipada == true ? danoConArma : danoBase;

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
        // La ballesta se resuelve primero y corta: tiene su propia cadencia y no gasta estamina, asi
        // que no tiene que pasar ni por el cooldown ni por el PuedeAtacar del golpe (si pasara, un
        // jugador sin estamina no podria disparar una ballesta, que es absurdo).
        Ballesta ballesta = ResolverEquipo()?.Ballesta;
        if (ballesta != null)
        {
            if (ballesta.Disparar()) AlAtacar?.Invoke();
            return;
        }

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
            objetivo?.TakeDamage(DanoActual);
        }
    }

    // EquipoJugador se instala solo desde un RuntimeInitializeOnLoadMethod, que no garantiza
    // haber corrido antes que este Start: por eso se resuelve cuando hace falta y no una sola vez
    // al arrancar. Si no aparece ninguno, el jugador pega siempre a mano limpia.
    EquipoJugador ResolverEquipo()
    {
        if (equipo != null) return equipo;
        if (equipoResuelto != null) return equipoResuelto;

        equipoResuelto = GetComponentInParent<EquipoJugador>() ?? FindAnyObjectByType<EquipoJugador>();
        return equipoResuelto;
    }
}
