using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Device = SlimDX.Direct3D11.Device;
using Buffer = SlimDX.Direct3D11.Buffer;
using Debug = System.Diagnostics.Debug;
using SlimDX;
using SlimDX.D3DCompiler;
using System.IO;
using System.Runtime.InteropServices;


namespace FlowSharp
{
    class Mesh : ColormapRenderable
    {
        protected Vector3 _color;
        protected float _thickness;
        protected Vector3 _planeNormal;
        public LineBall.RenderEffect Effect
        {
            get; protected set;
        }
        
        //private Buffer _indices;
        /// <summary>
        /// Set of lines in 3D space.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="xAxis"></param>
        /// <param name="yAxis"></param>
        /// <param name="scale">Scaling the field extent.</param>
        /// <param name="field"></param>
        public Mesh(Plane plane, TileSurface surf, RenderEffect effect = RenderEffect.DEFAULT, Colormap colormap = Colormap.Parula)
        {
            _color = surf.Color;
            this._vertexSizeBytes = Marshal.SizeOf(typeof(Vector4));
            this._topology = PrimitiveTopology.TriangleList;

            // Setting up the vertex buffer. 
            
            UsedMap = colormap;
            _planeNormal = plane.ZAxis;
            _planeNormal.Normalize();
            _effect = _meshEffect;
            SetRenderEffect(effect);

            this._vertexLayout = new InputLayout(_device, _technique.GetPassByIndex(0).Description.Signature, new[] {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0, 0),
                new InputElement("SCALAR", 0, Format.R32_Float, 12, 0)
            });
        }

        /// <summary>
        /// Setting up the vertex buffer. Vertex size and number has to be known.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="xAxis"></param>
        /// <param name="yAxis"></param>
        /// <param name="scale"></param>
        protected void GenerateGeometry(Plane plane, TileSurface surf)
        {
            Vector4[] verts = surf.Vertices;
            int[] idxs = surf.Indices;

            _numVertices = verts.Length;
            _numindices = idxs.Length;

            // Write poition and UV-map data.
            var stream = new DataStream(_numVertices * _vertexSizeBytes, true, true);

            stream.Position = 0;

            // Create and fill buffer.
            _vertices = new Buffer(_device, stream, new BufferDescription()
            {
                BindFlags = BindFlags.VertexBuffer,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = _numVertices * _vertexSizeBytes,
                Usage = ResourceUsage.Default
            });
            stream.Dispose();

            stream.WriteRange(surf.Indices);

            _indices = new Buffer(_device, stream, new BufferDescription()
            {
                BindFlags = BindFlags.IndexBuffer,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = (int)stream.Length,
                Usage = ResourceUsage.Default
            });
        }

        public void SetRenderEffect(RenderEffect effect)
        {
            switch (effect)
            {
                default:
                    this._technique = _meshEffect.GetTechniqueByName("RenderHeight");
                    break;

            }
        }

        public override void Render(Device device)
        {
            _meshEffect.GetVariableByName("color").AsVector().Set(_color);
            _meshEffect.GetVariableByName("worldNormal").AsVector().Set(_planeNormal);
            _meshEffect.GetVariableByName("thickness").AsScalar().Set(_thickness);
            base.Render(device);
        }

        public override void Update(TimeSpan totalTime)
        {
        }

        /// <summary>
        /// Initialize the static components.
        /// </summary>
        /// <param name="device"></param>
        public static void Initialize()
        {
            try
            {
                var bytecode = ShaderBytecode.CompileFromFile("Framework/Renderer/Data/DataEffects/Mesh.fx", "fx_5_0", ShaderFlags.None, EffectFlags.None);
                _meshEffect = new Effect(_device, bytecode);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }


        }

        public enum RenderEffect
        {
            DEFAULT
        }
        /// <summary>
        /// The effects that will be used by meshes.
        /// </summary>
        protected static Effect _meshEffect;

        ///// <summary>
        ///// Device for creating resources.
        ///// </summary>
        //protected static Device _device;
    }
}
