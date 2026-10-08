using UnityEngine;

// Puente entre los Animation Event de los clips y el EnemyAI del padre.
//
// Por que hace falta: Unity busca el metodo de un Animation Event SOLO en los componentes del
// GameObject que tiene el Animator. En este proyecto el Animator vive en el hijo "Modelo" (el FBX
// rigueado), mientras que EnemyAI vive en el padre (el que tiene NavMeshAgent + CapsuleCollider),
// asi que un evento que llame directo a "OnAttackHit" nunca encontraria receptor y Unity solo
// avisaria con "has no receiver" en consola. Este componente, que va en el mismo objeto que el
// Animator, recibe el evento y lo reenvia hacia arriba.
//
// EnemyAI lo agrega solo en su Awake (ver AsegurarPuenteDeEventos), asi que no hay que acordarse
// de ponerlo a mano; igual es seguro agregarlo al prefab del modelo.
public class EnemyAnimationEvents : MonoBehaviour
{
    EnemyAI ia;
    bool avisoSinIaLogueado;

    void Awake()
    {
        ia = GetComponentInParent<EnemyAI>();
    }

    // Nombre del metodo que hay que escribir en el Animation Event del clip de ataque (swiping,
    // punch, jump_attack...), en el frame exacto en que la garra toca al jugador.
    public void OnAttackHit()
    {
        if (ia == null) ia = GetComponentInParent<EnemyAI>();

        if (ia == null)
        {
            if (!avisoSinIaLogueado)
            {
                avisoSinIaLogueado = true;
                Debug.LogWarning($"{name}: llego el Animation Event OnAttackHit pero no hay ningun " +
                    "EnemyAI en los padres. El modelo tiene que ser hijo del objeto con EnemyAI.", this);
            }
            return;
        }

        ia.OnAttackHit();
    }
}
