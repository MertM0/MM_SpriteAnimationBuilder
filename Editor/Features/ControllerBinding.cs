using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MM.SpriteAnimation
{
    internal sealed class ControllerBinding
    {
        private readonly RuntimeAnimatorController asset;
        private readonly AnimatorController editable;
        private readonly AnimatorOverrideController overrides;
        private readonly AnimatorStateMachine machine;
        private readonly Dictionary<string, AnimatorState> states = new Dictionary<string, AnimatorState>(StringComparer.Ordinal);
        private readonly Dictionary<Motion, List<(AnimatorState State, string Label)>> motionUsers = new Dictionary<Motion, List<(AnimatorState State, string Label)>>();

        private ControllerBinding(RuntimeAnimatorController asset, AnimatorStateMachine machine, bool canCreateStates)
        {
            this.asset = asset;
            editable = asset as AnimatorController;
            overrides = asset as AnimatorOverrideController;
            this.machine = machine;
            CanCreateStates = canCreateStates;
        }

        internal bool RequiresState => asset != null;
        internal bool CanCreateStates { get; }
        internal IEnumerable<string> StateNames => states.Keys;

        internal bool HasState(string animation) => states.ContainsKey(animation);

        internal static AnimatorController BaseOf(RuntimeAnimatorController controller)
        {
            return controller is AnimatorOverrideController overrideController ? overrideController.runtimeAnimatorController as AnimatorController : controller as AnimatorController;
        }

        internal static int LayerIndex(AnimatorController controller, string layer)
        {
            return string.IsNullOrEmpty(layer) ? 0 : Array.FindIndex(controller.layers, item => item.name == layer);
        }

        internal static void Check(SpriteAnimationProfile profile, SpriteAnimationScan scan)
        {
            RuntimeAnimatorController controller = profile.Controller;
            if (controller == null) return;

            if (controller is AnimatorOverrideController overrideController && overrideController.runtimeAnimatorController == null)
            {
                scan.AddError($"Override controller '{controller.name}' has no base controller.");
                return;
            }

            AnimatorController baseController = BaseOf(controller);
            if (baseController == null)
            {
                scan.AddError($"Controller '{controller.name}' must be an Animator Controller or an Animator Override Controller.");
                return;
            }

            AnimatorControllerLayer[] layers = baseController.layers;
            if (layers.Length == 0)
            {
                scan.AddError($"Controller '{baseController.name}' has no layers.");
                return;
            }

            int index = LayerIndex(baseController, profile.Layer);
            if (index < 0) scan.AddError($"Layer '{profile.Layer}' was not found in '{baseController.name}'.");
            else if (layers[index].syncedLayerIndex >= 0) scan.AddError($"Layer '{layers[index].name}' is a synced layer; choose the layer it copies.");
        }

        internal static ControllerBinding Read(SpriteAnimationProfile profile, Action<string> warn)
        {
            RuntimeAnimatorController controller = profile.Controller;
            if (controller == null) return new ControllerBinding(null, null, false);

            AnimatorController baseController = BaseOf(controller);
            AnimatorControllerLayer[] layers = baseController.layers;
            int index = LayerIndex(baseController, profile.Layer);
            var binding = new ControllerBinding(controller, layers[index].stateMachine, controller is AnimatorController && profile.CreateMissingStates);
            binding.CollectStates(layers[index].stateMachine, warn);
            if (binding.overrides != null)
            {
                for (int i = 0; i < layers.Length; i++) binding.CollectUsers(layers[i].stateMachine, i == index ? null : layers[i].name);
            }
            return binding;
        }

        internal void CheckRow(AnimationSource source)
        {
            if (overrides == null || !states.TryGetValue(source.Name, out AnimatorState state)) return;

            if (state.motion == null)
            {
                source.AddProblem($"State '{state.name}' has no motion in the base controller; give it its own placeholder clip to override.");
                return;
            }
            if (!(state.motion is AnimationClip))
            {
                source.AddProblem($"State '{state.name}' uses a blend tree and cannot be overridden.");
                return;
            }

            List<string> others = motionUsers[state.motion].Where(user => user.State != state).Select(user => user.Label).ToList();
            if (others.Count > 0) source.AddWarning($"Shares its original clip with '{string.Join("', '", others)}'; the override changes both.");
        }

        internal bool IsAssigned(string animation, string clipPath)
        {
            if (asset == null || !states.TryGetValue(animation, out AnimatorState state)) return true;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) return true;
            if (overrides == null) return state.motion == clip;
            if (!(state.motion is AnimationClip original)) return true;
            if (motionUsers.TryGetValue(original, out List<(AnimatorState State, string Label)> users) && users.Count > 1) return true;
            return overrides[original] == clip;
        }

        internal AnimationClip BaseClipPlayed(string animation)
        {
            if (overrides == null || !states.TryGetValue(animation, out AnimatorState state) || !(state.motion is AnimationClip original)) return null;
            AnimationClip mapped = overrides[original];
            return mapped == null || mapped == original ? original : null;
        }

        internal bool Assign(string animation, AnimationClip clip)
        {
            if (asset == null) return false;
            return overrides != null ? AssignOverride(animation, clip) : AssignState(animation, clip);
        }

        internal void Save()
        {
            if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
        }

        private bool AssignOverride(string animation, AnimationClip clip)
        {
            if (!states.TryGetValue(animation, out AnimatorState state) || !(state.motion is AnimationClip original)) return false;
            if (overrides[original] == clip) return false;

            overrides[original] = clip;
            EditorUtility.SetDirty(overrides);
            return true;
        }

        private bool AssignState(string animation, AnimationClip clip)
        {
            if (states.TryGetValue(animation, out AnimatorState state))
            {
                if (state.motion == clip) return false;
                state.motion = clip;
                EditorUtility.SetDirty(state);
                EditorUtility.SetDirty(editable);
                return true;
            }

            AnimatorState created = machine.AddState(animation);
            created.motion = clip;
            states.Add(animation, created);
            EditorUtility.SetDirty(editable);
            return true;
        }

        private void CollectStates(AnimatorStateMachine stateMachine, Action<string> warn)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (states.ContainsKey(child.state.name)) warn?.Invoke($"State '{child.state.name}' exists more than once; the first one is used.");
                else states.Add(child.state.name, child.state);
            }
            foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines) CollectStates(child.stateMachine, warn);
        }

        private void CollectUsers(AnimatorStateMachine stateMachine, string layerName)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                AnimatorState state = child.state;
                if (state.motion == null) continue;
                if (!motionUsers.TryGetValue(state.motion, out List<(AnimatorState State, string Label)> users)) motionUsers.Add(state.motion, users = new List<(AnimatorState State, string Label)>());
                users.Add((state, layerName == null ? state.name : $"{layerName}/{state.name}"));
            }
            foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines) CollectUsers(child.stateMachine, layerName);
        }
    }
}
