using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Le agrega a los clips de ataque del enemigo el Animation Event "OnAttackHit" que EnemyAI espera
// para aplicar el dano en el frame exacto del impacto.
//
// El problema que resuelve: los FBX de ataque (swiping, punch, jump_attack...) se importaron sin
// ningun evento, asi que OnAttackHit() NUNCA llegaba y el dano caia solo por el respaldo de
// EnemyAI.ActualizarGolpePendiente() — un temporizador ciego de medio segundo que no tiene nada que
// ver con el momento en que la garra toca al jugador. El golpe se sentia desconectado de la
// animacion (pegaba antes o despues de verse el zarpazo) y, encima, cada enemigo dejaba un warning
// en consola pidiendo justamente este paso.
//
// Por que una herramienta de Editor y no datos a mano: los eventos de un clip de FBX viven en el
// importador del modelo (ModelImporter.clipAnimations), no en un asset aparte que se pueda editar
// de forma razonable a mano. Mismo patron que ya usa AnimacionControllerBuilder para marcar en loop
// los clips de idle/walk/run.
//
// Menu: Between Metals > Enemigos
public static class EventoGolpeSetup
{
    const string CarpetaAnimaciones = "Assets/Animacion";
    const string NombreDelEvento = "OnAttackHit";
    const string MenuAgregar = "Between Metals/Enemigos/Agregar Animation Event de golpe a los ataques";

    // Clips que son un ataque. "swiping" es el que usa Creature.controller hoy (ver
    // AnimacionControllerBuilder.ConectarAtaqueYMuerte); los otros se dejan listos para cuando el
    // enemigo tenga mas de un ataque, asi no hay que volver a pasar por aca.
    static readonly string[] ClipsDeAtaque = { "swiping", "punch", "jump_attack", "jump_attack_alt" };

    // Momento del impacto, como fraccion del clip. 0.4 es una estimacion razonable para un zarpazo
    // (el brazo sale, conecta antes de la mitad y despues se recupera), NO una medicion: conviene
    // abrir el clip en la ventana de Animation y mover el evento al frame donde la garra toca de
    // verdad. Lo importante es que el evento exista y caiga dentro del swing; afinarlo es cosmetico.
    const float MomentoDelImpacto = 0.4f;

    [MenuItem(MenuAgregar, priority = 303)]
    public static void Agregar()
    {
        var agregados = new List<string>();
        var yaTenian = new List<string>();
        var noEncontrados = new List<string>();

        foreach (string nombre in ClipsDeAtaque)
        {
            string ruta = CarpetaAnimaciones + "/" + nombre + ".fbx";
            var importador = AssetImporter.GetAtPath(ruta) as ModelImporter;
            if (importador == null)
            {
                noEncontrados.Add(nombre);
                continue;
            }

            switch (AgregarEventoA(importador, ruta))
            {
                case Resultado.Agregado: agregados.Add(nombre); break;
                case Resultado.YaLoTenia: yaTenian.Add(nombre); break;
                default: noEncontrados.Add(nombre); break;
            }
        }

        if (agregados.Count > 0) AssetDatabase.Refresh();

        string resumen = "EventoGolpeSetup:";
        if (agregados.Count > 0) resumen += $"\n  evento '{NombreDelEvento}' agregado a: {string.Join(", ", agregados)}";
        if (yaTenian.Count > 0) resumen += $"\n  ya lo tenian: {string.Join(", ", yaTenian)}";
        if (noEncontrados.Count > 0) resumen += $"\n  sin clip importable: {string.Join(", ", noEncontrados)}";

        if (agregados.Count > 0)
        {
            resumen += $"\n\nEl evento quedo al {MomentoDelImpacto:P0} del clip, que es una estimacion. " +
                "Abri el clip en la ventana de Animation y movelo al frame donde la garra toca de verdad " +
                "si querés afinarlo.";
        }

        Debug.Log(resumen);
    }

    enum Resultado { Agregado, YaLoTenia, SinClip }

    static Resultado AgregarEventoA(ModelImporter importador, string ruta)
    {
        // El largo real del clip sale del asset ya importado: hace falta para pasar la fraccion de
        // impacto a segundos, que es la unidad en la que Unity guarda AnimationEvent.time.
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ruta)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null) return Resultado.SinClip;

        // clipAnimations esta vacio mientras nadie toco los clips a mano; en ese caso se parte de
        // defaultClipAnimations, que es la lista que Unity genera sola. Asignar clipAnimations deja
        // el importador en modo explicito, igual que hace AnimacionControllerBuilder con los loops.
        ModelImporterClipAnimation[] clips = importador.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importador.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return Resultado.SinClip;

        bool cambio = false;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationEvent[] eventos = clips[i].events ?? new AnimationEvent[0];

            // Idempotente: correr el menu dos veces no deja el evento duplicado (y con el duplicado
            // el enemigo pegaria dos veces por animacion, porque cada llamada resuelve el golpe
            // pendiente... en realidad la segunda la ignora, pero igual ensucia).
            if (eventos.Any(e => e != null && e.functionName == NombreDelEvento)) continue;

            var golpe = new AnimationEvent
            {
                functionName = NombreDelEvento,
                time = clip.length * Mathf.Clamp01(MomentoDelImpacto),
            };

            clips[i].events = eventos.Concat(new[] { golpe }).ToArray();
            cambio = true;
        }

        if (!cambio) return Resultado.YaLoTenia;

        importador.clipAnimations = clips;
        importador.SaveAndReimport();
        return Resultado.Agregado;
    }

    /// <summary>
    /// Verdadero si ese clip de ataque ya tiene el evento. Lo usan los self-tests y
    /// EnemyCombatSelfTest para avisar si falta el paso de Editor.
    /// </summary>
    public static bool TieneEventoDeGolpe(string nombreDelClip)
    {
        string ruta = CarpetaAnimaciones + "/" + nombreDelClip + ".fbx";
        var importador = AssetImporter.GetAtPath(ruta) as ModelImporter;
        if (importador == null) return false;

        ModelImporterClipAnimation[] clips = importador.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importador.defaultClipAnimations;
        if (clips == null) return false;

        return clips.Any(c => c.events != null
            && c.events.Any(e => e != null && e.functionName == NombreDelEvento));
    }

    /// <summary>Los clips que este tool considera ataques. Para los self-tests.</summary>
    public static IEnumerable<string> ClipsConsiderados => ClipsDeAtaque;
}
