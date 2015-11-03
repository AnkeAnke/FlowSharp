﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX.Direct3D11;

namespace FlowSharp
{
    interface IRenderable
    {
        void Render(SlimDX.Direct3D11.Device device);
        void Update(TimeSpan totalTime);
    }

    /// <summary>
    /// Base class for any object that can be rendered by the renderer.
    /// </summary>
    abstract class Renderable
    {
        protected static Device _device;
        public static void Initialize(Device device)
        {
            _device = device;
        }
        /// <summary>
        /// The buffer containing the vertices to be drawn.
        /// </summary>
        protected SlimDX.Direct3D11.Buffer _vertices;

        /// <summary>
        /// Layout of the vertex buffer.
        /// </summary>
        protected InputLayout _vertexLayout;

        /// <summary>
        /// Distance between two adjacent vertices in memory.
        /// </summary>
        protected int _vertexSizeBytes;

        /// <summary>
        /// Number of vertices (to be drawn).
        /// </summary>
        protected int _numVertices;

        /// <summary>
        /// Technique that is to be used.
        /// </summary>
        protected EffectTechnique _technique;

        /// <summary>
        /// Primitives to be used (defaults to triangles).
        /// </summary>
        protected PrimitiveTopology _topology = PrimitiveTopology.TriangleList;

        public bool Active = true;

        private int _ID;

        protected Renderable()
        {
            _ID = GlobalIndex++;
        }

        public override bool Equals(object objRaw)
        {
            Renderable obj = objRaw as Renderable;
            if (obj == null)
                return false;
            return obj._ID == _ID;
        }

        /// <summary>
        /// Draw the object (in immediate mode).
        /// </summary>
        /// <param name="device"></param>
        public virtual void Render(Device device)
        {
            // Rendering immediate.
            DeviceContext context = device.ImmediateContext;
            context.InputAssembler.InputLayout = _vertexLayout;
            context.InputAssembler.PrimitiveTopology = _topology;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(_vertices, _vertexSizeBytes, 0));

            // Applying all passes of the first technique.
            for (int i = 0; i < _technique.Description.PassCount; ++i)
            {
                _technique.GetPassByIndex(i).Apply(device.ImmediateContext);
                device.ImmediateContext.VertexShader.SetConstantBuffer(Renderer.Singleton.Camera.ConstantBuffer, 0);
                device.ImmediateContext.GeometryShader.SetConstantBuffer(Renderer.Singleton.Camera.ConstantBuffer, 0);

                context.Draw(_numVertices, 0);
            }
        }

        public abstract void Update(TimeSpan totalTime);

        private static int GlobalIndex = 0;
    }

    abstract class ColormapRenderable : Renderable
    {
        protected Effect _effect;
        public Colormap? UsedMap { get; set; }

        public float LowerBound = 0;
        public float UpperBound = 1;
        public float MidValue { get { return (UpperBound-LowerBound) *0.5f; } }

        /// <summary>
        /// When a colormap is set, set the associated texture as colormap.
        /// </summary>
        protected void SetColormapResources()
        {
            if (UsedMap == null)
                return;
            _effect.GetVariableByName("colormap").AsResource().SetResource(ColorMapping.GetColormapTexture((Colormap)UsedMap));
            _effect.GetVariableByName("minMap").AsScalar().Set(LowerBound);
            _effect.GetVariableByName("maxMap").AsScalar().Set(UpperBound);
        }

        public override void Render(Device device)
        {
            SetColormapResources();
            base.Render(device);
        }
    }

    class DummyRenderable : Renderable
    {
        public DummyRenderable()
        {
            Active = false;
        }

        public override void Update(TimeSpan totalTime)
        {
        }
    }
}
