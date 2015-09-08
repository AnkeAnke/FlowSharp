using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Device = SlimDX.Direct3D11.Device;
using SlimDX;
using SlimDX.D3DCompiler;

namespace FlowSharp
{
    class Plane : Renderable
    {
        /// <summary>
        /// A number of equally sized textures. Depending on the number, a differnet mapping to color will be applied.
        /// </summary>
        protected Texture2D[] _fields;

        /// <summary>
        /// Plane to display scalar/vector field data on. Condition: Fields domain is 2D.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="xAxis"></param>
        /// <param name="yAxis"></param>
        /// <param name="scale">Scaling the field extent.</param>
        /// <param name="field"></param>
        public Plane(Vector3 origin, Vector3 xAxis, Vector3 yAxis, float scale, ScalarField[] fields)
        {
#if DEBUG
            // Assert that the fields are 2 dimensional.
            foreach(ScalarField field in fields)
                System.Diagnostics.Debug.Assert(field.Size.Length == 2);
#endif
            this._technique = _planeEffect.GetTechniqueByName("RenderTex" + fields.Length);
            this._vertexLayout = new InputLayout(_device, _technique.GetPassByIndex(0).Description.Signature, new[] {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 16, 0)
            });
            this._vertexSizeBytes = 32;
            this._numVertices = 6;

            // TODO: remove!
            _numVertices = 3;

            // Setting up the vertex buffer. 
            GenerateGeometry(origin, xAxis,yAxis, scale);

            
            // Generating Textures from the fields.
            //TODO

        }

        /// <summary>
        /// Setting up the vertex buffer. Vertex size and number has to be known.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="xAxis"></param>
        /// <param name="yAxis"></param>
        /// <param name="scale"></param>
        protected void GenerateGeometry(Vector3 origin, Vector3 xAxis, Vector3 yAxis, float scale)
        {
            var stream = new DataStream(3 * 32, true, true);
            stream.WriteRange(new[] {
                new Vector4(0.0f, 0.5f, 0.5f, 1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
                new Vector4(0.5f, -0.5f, 0.5f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
                new Vector4(-0.5f, -0.5f, 0.5f, 1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)
            });
            stream.Position = 0;

            _vertices = new SlimDX.Direct3D11.Buffer(_device, stream, new BufferDescription()
            {
                BindFlags = BindFlags.VertexBuffer,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = _numVertices * _vertexSizeBytes,
                Usage = ResourceUsage.Default
            });
            stream.Dispose();
        }

        public override void Render(Device device)
        {
            // TODO: Set texture.
            base.Render(device);
        }

        public override void Update(TimeSpan totalTime)
        {
        }

        /// <summary>
        /// Initialize the static components.
        /// </summary>
        /// <param name="device"></param>
        public static void Initialize(Device device)
        {
            _device = device;
            var bytecode = ShaderBytecode.CompileFromFile("Framework/Renderer/Colormap.fx", "fx_5_0", ShaderFlags.None, EffectFlags.None);
            _planeEffect = new Effect(_device, bytecode);
        }

        /// <summary>
        /// The effects that will be used by the plane. Use technique "RenderTex"+numTextures to get the right technique.
        /// </summary>
        protected static Effect _planeEffect;

        /// <summary>
        /// Device for creating resources.
        /// </summary>
        protected static Device _device;

    }
}
