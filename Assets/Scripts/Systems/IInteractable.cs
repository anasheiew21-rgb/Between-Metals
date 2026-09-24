// Contrato minimo para cualquier objeto con el que el jugador pueda interactuar presionando 'E'
// (comerciante, puertas, cofres, palancas, etc.). PlayerInteraction detecta con que objeto se
// esta mirando y se encarga de mostrar el cartel y de invocar Interactuar().
public interface IInteractable
{
    // Texto que se muestra en el cartel de pantalla, ej. "Presiona E para comerciar"
    string TextoPrompt { get; }

    // Que pasa cuando el jugador presiona 'E' mirando a este objeto, dentro del rango
    void Interactuar();
}
