using System;
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
                context.Draw(_numVertices, 0);
            }
        }

        public abstract void Update(TimeSpan totalTime);
    }

    abstract class ColormapRenderable : Renderable
    {
        protected Effect _effect;
        public Colormap? UsedMap { get; set; }

        /// <summary>
        /// When a colormap is set, set the associated texture as colormap.
        /// </summary>
        protected void SetColormapResources()
        {
            if (UsedMap == null)
                return;
            _effect.GetVariableByName("colormap").AsResource().SetResource(ColorMapping.GetColormapTexture((Colormap)UsedMap));
        }

        public override void Render(Device device)
        {
            SetColormapResources();
            base.Render(device);
        }
    }
}
