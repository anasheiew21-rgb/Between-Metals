using System.Collections;
using UnityEngine;

// Hace que el cuerpo del enemigo desaparezca un rato despues de morir, en vez de quedar parado en
// el laberinto para siempre. Al morir, EnemyAI apaga la IA y el NavMeshAgent (ManejarMuerte) pero
// el GameObject se queda donde esta: sin esto, un enemigo muerto sigue "ahi" bloqueando el pasillo,
// lo que rompe el clima y se puede confundir con un enemigo que dejo de reaccionar por un bug.
//
// Se autoagrega desde EnemyHealth.Awake(), mismo patron que EnemyHitFeedback y BotinEnemigo: con
// tener EnemyHealth alcanza, sin pasos manuales en el Editor.
[RequireComponent(typeof(EnemyHealth))]
public class EnemyDespawn : MonoBehaviour
{
    [Tooltip("Segundos que el cuerpo queda en el piso, quieto, antes de desaparecer. Le da tiempo " +
             "al jugador de ver la animacion de muerte y a BotinEnemigo de soltar su moneda.")]
    [SerializeField] private float tiempoAntesDeDesaparecer = 3f;

    EnemyHealth salud;

    void Awake()
    {
        salud = GetComponent<EnemyHealth>();
        if (salud != null) salud.AlMorir += ManejarMuerte;
    }

    void OnDestroy()
    {
        if (salud != null) salud.AlMorir -= ManejarMuerte;
    }

    void ManejarMuerte()
    {
        StartCoroutine(Desaparecer());
    }

    IEnumerator Desaparecer()
    {
        yield return new WaitForSeconds(tiempoAntesDeDesaparecer);

        // Destroy y no SetActive(false): un cuerpo muerto no tiene ningun motivo para volver a
        // activarse mas adelante (a diferencia de la moneda de MonedaPickup, que se desactiva para
        // que otros sistemas la puedan seguir referenciando despues de recogerla).
        Destroy(gameObject);
    }
}
