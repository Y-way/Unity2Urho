﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UnityToCustomEngineExporter.Editor.Urho3D
{
    public class AnimationControllerExporter
    {
        private readonly Urho3DEngine _engine;

        public AnimationControllerExporter(Urho3DEngine engine)
        {
            _engine = engine;
        }

        public string EvaluateAnimationControllerName(AnimatorController clip, PrefabContext prefabContext)
        {
            if (clip == null)
                return null;
            var relPath = ExportUtils.GetRelPathFromAsset(_engine.Options.Subfolder, clip);
            return ExportUtils.ReplaceExtension(relPath, ".json");
        }

        public void ExportAnimationController(AnimatorController animationController, PrefabContext prefabContext)
        {
            var animationControllerName = EvaluateAnimationControllerName(animationController, prefabContext);
            var controllerJson =
                new ControllerJson(animationController, animationControllerName, _engine, prefabContext);
            var assetGuid = animationController.GetKey();
            var sourceFileTimestampUtc = ExportUtils.GetLastWriteTimeUtc(animationController);

            SaveJson(animationControllerName, controllerJson, assetGuid, sourceFileTimestampUtc);
            for (var index = 0; index < controllerJson.layers.Length; index++)
            {
                var layer = animationController.layers[index];

                SaveJson(controllerJson.layers[index].stateMachine,
                    new StateMachineJson(layer.stateMachine, _engine, prefabContext), assetGuid,
                    sourceFileTimestampUtc);
            }
        }

        private void SaveJson(string animationControllerName, object json, AssetKey assetGuid,
            DateTime sourceFileTimestampUtc)
        {
            using (var fileStream = _engine.TryCreate(assetGuid, animationControllerName, sourceFileTimestampUtc))
            {
                if (fileStream == null)
                    return;
                using (var streamWriter = new StreamWriter(fileStream, new UTF8Encoding(false)))
                {
                    streamWriter.Write(EditorJsonUtility.ToJson(json, true));
                }
            }
        }

        [Serializable]
        public class LayerJson
        {
            [SerializeField] public string stateMachine;

            public LayerJson(AnimatorControllerLayer layer, string name, int index, Urho3DEngine engine,
                PrefabContext prefabContext)
            {
                stateMachine = ExportUtils.ReplaceExtension(name, $".SM{index}.json");
            }
        }

        [Serializable]
        public class ChildMotionJson
        {
            [SerializeField] public string animationClip;
            [SerializeField] public bool hasBlendTree;
            [SerializeField] public BlendTreeJson blendTree;
            [SerializeField] public float cycleOffset;

            public ChildMotionJson(ChildMotion childMotion, Urho3DEngine engine, PrefabContext prefabContext)
            {
                cycleOffset = childMotion.cycleOffset;
                var motion = childMotion.motion;
                if (motion is AnimationClip animationClip)
                {
                    this.animationClip = engine.EvaluateAnimationName(animationClip, prefabContext);
                    engine.ScheduleAssetExport(animationClip, prefabContext);
                }
                else if (motion is BlendTree blendTree)
                {
                    hasBlendTree = true;
                    this.blendTree = new BlendTreeJson(blendTree, engine, prefabContext);
                }
            }
        }

        [Serializable]
        public class BlendTreeJson
        {
            [SerializeField] public string name;
            [SerializeField] public ChildMotionJson[] children;
            [SerializeField] public string blendParameter;
            [SerializeField] public string blendParameterY;
            [SerializeField] public BlendTreeType blendType;
            [SerializeField] public float maxThreshold;
            [SerializeField] public float minThreshold;
            [SerializeField] public bool useAutomaticThresholds;
            [SerializeField] public float apparentSpeed;
            [SerializeField] public float averageAngularSpeed;
            [SerializeField] public float averageDuration;
            [SerializeField] public Vector3 averageSpeed;
            [SerializeField] public bool isHumanMotion;
            [SerializeField] public bool isLooping;
            [SerializeField] public bool legacy;

            public BlendTreeJson(BlendTree blendTree, Urho3DEngine engine, PrefabContext prefabContext)
            {
                name = engine.DecorateName(blendTree.name);
                blendParameter = blendTree.blendParameter;
                blendParameterY = blendTree.blendParameterY;
                blendType = blendTree.blendType;
                maxThreshold = blendTree.maxThreshold;
                minThreshold = blendTree.minThreshold;
                useAutomaticThresholds = blendTree.useAutomaticThresholds;
                apparentSpeed = blendTree.apparentSpeed;
                averageAngularSpeed = blendTree.averageAngularSpeed;
                averageDuration = blendTree.averageDuration;
                averageSpeed = blendTree.averageSpeed;
                isHumanMotion = blendTree.isHumanMotion;
                isLooping = blendTree.isLooping;
                legacy = blendTree.legacy;
                children = blendTree.children.Select(_ => new ChildMotionJson(_, engine, prefabContext)).ToArray();
            }
        }

        [Serializable]
        public class StateJson
        {
            [SerializeField] public string name;

            // [SerializeField] public float speed;
            // [SerializeField] public string animationClip;
            // [SerializeField] public bool hasBlendTree;
            // [SerializeField] public BlendTreeJson blendTree;
            [SerializeField] public TransitionJson[] transitions;

            public StateJson(AnimatorState state, Urho3DEngine engine, PrefabContext prefabContext)
            {
                name = engine.DecorateName(state.name);
                // this.speed = state.speed;
                // this.cycleOffset = state.cycleOffset;
                var motion = state.motion;
                if (motion is AnimationClip animationClip)
                {
                    // this.animationClip = engine.EvaluateAnimationName(animationClip, prefabContext);
                    engine.ScheduleAssetExport(animationClip, prefabContext);
                }
                else if (motion is BlendTree blendTree)
                {
                    // this.hasBlendTree = true;
                    // this.blendTree = new BlendTreeJson(blendTree, engine, prefabContext);
                }

                transitions = state.transitions.Select(_ => new TransitionJson(_, engine, prefabContext)).ToArray();
            }

            // [SerializeField] public float cycleOffset;
        }

        [Serializable]
        public class ConditionJson
        {
            [SerializeField] public AnimatorConditionMode mode;

            [SerializeField] public string parameter;
            // [SerializeField] public float threshold;

            public ConditionJson(AnimatorCondition animatorCondition, Urho3DEngine engine, PrefabContext prefabContext)
            {
                mode = animatorCondition.mode;
                parameter = animatorCondition.parameter;
                // this.threshold = animatorCondition.threshold;
            }
        }

        [Serializable]
        public class TransitionJson
        {
            [SerializeField] public string destinationState;

            [SerializeField] public float duration;

            // [SerializeField] public  bool hasFixedDuration;
            // [SerializeField] public  bool canTransitionToSelf;
            // [SerializeField] public  float exitTime;
            // [SerializeField] public  bool hasExitTime;
            // [SerializeField] public  float offset;
            // [SerializeField] public  bool orderedInterruption;
            // [SerializeField] public  bool isExit;
            // [SerializeField] public  bool mute;
            // [SerializeField] public  bool solo;
            [SerializeField] public ConditionJson[] conditions;

            public TransitionJson(AnimatorStateTransition transition, Urho3DEngine engine, PrefabContext prefabContext)
            {
                destinationState = engine.DecorateName(transition.destinationState.name);
                duration = transition.duration;
                // this.hasFixedDuration = transition.hasFixedDuration;
                // this.canTransitionToSelf = transition.canTransitionToSelf;
                // this.exitTime = transition.exitTime;
                // this.hasExitTime = transition.hasExitTime;
                // this.offset = transition.offset;
                // this.orderedInterruption = transition.orderedInterruption;
                conditions = transition.conditions.Select(_ => new ConditionJson(_, engine, prefabContext)).ToArray();
                // this.isExit = transition.isExit;
                // this.mute = transition.mute;
                // this.solo = transition.solo;
            }
        }

        [Serializable]
        public class StateMachineJson
        {
            [SerializeField] public StateJson[] states;
            [SerializeField] public TransitionJson[] anyStateTransitions;
            [SerializeField] public string defaultState;

            public StateMachineJson(AnimatorStateMachine stateMachine, Urho3DEngine engine, PrefabContext prefabContext)
            {
                defaultState = engine.DecorateName(stateMachine.defaultState?.name);
                anyStateTransitions = stateMachine.anyStateTransitions
                    .Select(_ => new TransitionJson(_, engine, prefabContext)).ToArray();
                states = stateMachine.states.Select(_ => new StateJson(_.state, engine, prefabContext)).ToArray();
            }
        }

        [Serializable]
        public class ControllerJson
        {
            [SerializeField] public string name;
            [SerializeField] public LayerJson[] layers;

            public ControllerJson(AnimatorController animationController, string assetName, Urho3DEngine engine,
                PrefabContext prefabContext)
            {
                name = engine.DecorateName(animationController.name);
                layers = animationController.layers
                    .Select((_, index) => new LayerJson(_, assetName, index, engine, prefabContext)).ToArray();
            }
        }
    }
}