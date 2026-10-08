using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AnimacionControllerBuilder
{
    static readonly string[] LoopNames = { "idle", "idle_2", "breathing_idle", "walking", "run" };

    // Umbral de Speed (m/s de NavMeshAgent) para pasar de caminar a correr. Entre patrolSpeed
    // (2.5) y chaseSpeed (4.5) de EnemyAI por defecto.
    const float UmbralCorrer = 3.5f;
    const float UmbralCaminar = 0.1f;

    [MenuItem("Animacion/Build Animator Controller")]
    public static void Build()
    {
        const string folder = "Assets/Animacion";
        const string controllerPath = "Assets/Animacion/Creature.controller";

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)
                         ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var sm = controller.layers[0].stateMachine;

        var estados = new Dictionary<string, AnimatorState>();
        foreach (var s in sm.states) estados[s.state.name.ToLower()] = s.state;

        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            var nameLower = name.ToLower();

            // Loop para idle / walk / run
            if (LoopNames.Contains(nameLower))
            {
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                var clips = imp.defaultClipAnimations;
                foreach (var c in clips) c.loopTime = true;
                imp.clipAnimations = clips;
                imp.SaveAndReimport();
            }

            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null) continue;

            if (estados.ContainsKey(nameLower)) continue;
            var state = sm.AddState(name);
            state.motion = clip;
            if (nameLower == "idle") sm.defaultState = state;
            estados[nameLower] = state;
        }

        AgregarParametroSiFalta(controller, "Speed", AnimatorControllerParameterType.Float);
        AgregarParametroSiFalta(controller, "Attack", AnimatorControllerParameterType.Trigger);
        AgregarParametroSiFalta(controller, "Die", AnimatorControllerParameterType.Trigger);

        ConectarLocomocion(estados);
        ConectarAtaqueYMuerte(sm, estados);

        AssetDatabase.SaveAssets();
        Debug.Log("Creature.controller listo en " + controllerPath);
    }

    static void AgregarParametroSiFalta(AnimatorController controller, string nombre, AnimatorControllerParameterType tipo)
    {
        if (controller.parameters.Any(p => p.name == nombre)) return;
        controller.AddParameter(nombre, tipo);
    }

    // Idle <-> Walking <-> Run segun Speed (velocidad del NavMeshAgent, ver EnemyAnimator).
    static void ConectarLocomocion(Dictionary<string, AnimatorState> estados)
    {
        if (!estados.TryGetValue("idle", out var idle) || !estados.TryGetValue("walking", out var walk))
            return;

        AgregarTransicionSiFalta(idle, walk, AnimatorConditionMode.Greater, UmbralCaminar, "Speed");
        AgregarTransicionSiFalta(walk, idle, AnimatorConditionMode.Less, UmbralCaminar, "Speed");

        if (estados.TryGetValue("run", out var run))
        {
            AgregarTransicionSiFalta(walk, run, AnimatorConditionMode.Greater, UmbralCorrer, "Speed");
            AgregarTransicionSiFalta(run, walk, AnimatorConditionMode.Less, UmbralCorrer, "Speed");
        }
    }

    // Any State -> swiping (ataque) -> vuelve a idle; Any State -> dying (no vuelve, EnemyAI se
    // apaga solo al morir via EnemyHealth.AlMorir, ver EnemyAnimator.ManejarMuerte).
    static void ConectarAtaqueYMuerte(AnimatorStateMachine sm, Dictionary<string, AnimatorState> estados)
    {
        if (estados.TryGetValue("swiping", out var ataque))
        {
            AgregarAnyStateTransitionSiFalta(sm, ataque, "Attack");

            if (estados.TryGetValue("idle", out var idle) && !ataque.transitions.Any(t => t.destinationState == idle))
            {
                var vuelta = ataque.AddTransition(idle);
                vuelta.hasExitTime = true;
                vuelta.exitTime = 0.9f;
                vuelta.duration = 0.15f;
                vuelta.hasFixedDuration = true;
            }
        }

        if (estados.TryGetValue("dying", out var muerte))
        {
            AgregarAnyStateTransitionSiFalta(sm, muerte, "Die");
        }
    }

    static void AgregarTransicionSiFalta(AnimatorState desde, AnimatorState hacia, AnimatorConditionMode modo, float umbral, string parametro)
    {
        if (desde.transitions.Any(t => t.destinationState == hacia)) return;

        var transicion = desde.AddTransition(hacia);
        transicion.hasExitTime = false;
        transicion.duration = 0.15f;
        transicion.hasFixedDuration = true;
        transicion.AddCondition(modo, umbral, parametro);
    }

    static void AgregarAnyStateTransitionSiFalta(AnimatorStateMachine sm, AnimatorState hacia, string trigger)
    {
        if (sm.anyStateTransitions.Any(t => t.destinationState == hacia)) return;

        var transicion = sm.AddAnyStateTransition(hacia);
        transicion.hasExitTime = false;
        transicion.duration = 0.1f;
        transicion.canTransitionToSelf = false;
        transicion.AddCondition(AnimatorConditionMode.If, 0, trigger);
    }
}
