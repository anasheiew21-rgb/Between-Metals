using System.Collections;
using UnityEngine;

// Feedback visual de daño de los enemigos (HU-14 / T14-F). Escucha el AlRecibirDaño de
// EnemyHealth y tiñe de rojo el material del enemigo un instante; no sabe nada de IA ni de
// PlayerCombat (mismo desacople que EnemyAI usa con AlMorir).
[RequireComponent(typeof(EnemyHealth))]
public class EnemyHitFeedback : MonoBehaviour
{
    [SerializeField] private float duracionFlash = 0.2f;

    EnemyHealth salud;
    Renderer render;
    Color colorOriginal;
    Coroutine flashActual;

    void Start()
    {
        salud = GetComponent<EnemyHealth>();
        render = GetComponentInChildren<Renderer>();

        if (render != null) colorOriginal = render.material.color;
        if (salud != null) salud.AlRecibirDaño += ManejarDaño;
    }

    void OnDestroy()
    {
        if (salud != null) salud.AlRecibirDaño -= ManejarDaño;
    }

    void ManejarDaño(float actual, float maximo)
    {
        if (render == null) return;

        // Si ya habia un flash en curso (golpes seguidos), se reinicia en vez de apilar
        // corrutinas: siempre queda 0.2s de rojo desde el ultimo golpe, no se van sumando.
        if (flashActual != null) StopCoroutine(flashActual);
        flashActual = StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        render.material.color = Color.red;
        yield return new WaitForSeconds(duracionFlash);
        render.material.color = colorOriginal;
        flashActual = null;
    }
}
