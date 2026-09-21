using UnityEngine;

public class FlashlightController : MonoBehaviour
{
    private Light flashlight;

    void Start()
    {
        flashlight = GetComponent<Light>();
    }

    void Update()
    {
        if (KeyBindings.Down(KeyBindings.Action.Flashlight) && !Menu.IsOpen)
        {
            flashlight.enabled = !flashlight.enabled;
        }
    }
}