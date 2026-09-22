using UnityEngine;

public class TestInteractable : MonoBehaviour, IInteractable
{
    public void Interact()
    {
        Debug.Log("TestInteractable: Interacción ejecutada correctamente.");
    }
}
