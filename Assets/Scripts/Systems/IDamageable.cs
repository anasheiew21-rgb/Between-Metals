// Contrato minimo para cualquier objeto que pueda recibir daño de un ataque (enemigos, props
// destructibles a futuro, etc.). PlayerCombat detecta con que objeto conecto el golpe y le aplica
// el daño sin necesitar conocer el tipo concreto (mismo patron que IInteractable).
public interface IDamageable
{
    float VidaActual { get; }

    void TakeDamage(float cantidad);
}
