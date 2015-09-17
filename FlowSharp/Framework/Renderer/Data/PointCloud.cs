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


namespace FlowSharp
{
    class PointCloud : Renderable
    {
        /// <summary>
        /// Plane to display scalar/vector field data on. Condition: Fields domain is 2D.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="xAxis"></param>
        /// <param name="yAxis"></param>
        /// <param name="scale">Scaling the field extent.</param>
        /// <param name="field"></param>
        public PointCloud(Vector3 origin, Vector3 xAxis, Vector3 yAxis, Vector3 zAxis, float scale, PointSet points)
        {
            this._vertexSizeBytes = 32;
            this._numVertices = points.Points.Length;
            this._topology = PrimitiveTopology.PointList;

            // Setting up the vertex buffer. 
            GenerateGeometry(origin, xAxis, yAxis, zAxis, scale, points);

            this._technique = _cloudEffect.GetTechniqueByName("Render");
            this._vertexLayout = new InputLayout(_device, _technique.GetPassByIndex(0).Description.Signature, new[] {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("COLOR", 0, Format.R32G32B32_Float, 16, 0),
                new InputElement("RADIUS", 0, Format.R32_Float, 28, 0)
            });
        }

        /// <summary>
        /// Setting up the vertex buffer. Vertex size and number has to be known.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="xAxis"></param>
        /// <param name="yAxis"></param>
        /// <param name="scale"></param>
        protected void GenerateGeometry(Vector3 origin, Vector3 xAxis, Vector3 yAxis, Vector3 zAxis, float scale, PointSet points)
        {

            // Write poition and UV-map data.
            var stream = new DataStream(_numVertices * _vertexSizeBytes, true, true);
            foreach(Point point in points.Points)
            {
                stream.Write(new Vector4(origin + (xAxis * point.Position[0] + yAxis * point.Position[1]) * scale, 1.0f));
                stream.Write(point.Color);
                stream.Write(point.Radius);
            }
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
        }

        public override void Render(Device device)
        {
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
          
            try {
                var bytecode = ShaderBytecode.CompileFromFile("Framework/Renderer/Data/DataEffects/Cloud.fx", "fx_5_0", ShaderFlags.None, EffectFlags.None);
                _cloudEffect = new Effect(_device, bytecode);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
            
            
        }

        /// <summary>
        /// The effects that will be used by the plane. Use technique "RenderTex"+numTextures to get the right technique.
        /// </summary>
        protected static Effect _cloudEffect;

        /// <summary>
        /// Device for creating resources.
        /// </summary>
        protected static Device _device;
    }
}
