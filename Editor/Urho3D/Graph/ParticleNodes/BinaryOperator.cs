﻿namespace UnityToCustomEngineExporter.Editor.Urho3D.Graph.ParticleNodes
{
    public class BinaryOperator : GraphNode
    {
        public BinaryOperator(string name) : base(name)
        {
            In.Add(X);
            In.Add(Y);
            base.Out.Add(Result);
        }

        public GraphInPin X { get; } = new GraphInPin("x");
        public GraphInPin Y { get; } = new GraphInPin("y");
        public GraphOutPin Result { get; } = new GraphOutPin("out");
    }
}