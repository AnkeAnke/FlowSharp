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
        /// Set of points in 3D space.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="xAxis"></param>
        /// <param name="yAxis"></param>
        /// <param name="scale">Scaling the field extent.</param>
        /// <param name="field"></param>
        public PointCloud(Plane plane, PointSet<Point> points)
        {
            this._vertexSizeBytes = 32;
            this._numVertices = points.Points.Length;
            if (_numVertices == 0)
                return;
            this._topology = PrimitiveTopology.PointList;

            // Setting up the vertex buffer. 
            GenerateGeometry(plane, points);

            Debug.Assert(_cloudEffect != null);
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
        protected void GenerateGeometry(Plane plane, PointSet<Point> points)
        {
            if (points.Length == 0)
                return;
            // Write poition and UV-map data.
            var stream = new DataStream(_numVertices * _vertexSizeBytes, true, true);
            Vector3 zAxis = plane.ZAxis;
            foreach(Point point in points.Points)
            {
                var test = new Vector4(plane.Origin + (plane.XAxis * point.Position[0] + plane.YAxis * point.Position[1] + zAxis * point.Position[2]), 1.0f);
                stream.Write(new Vector4(plane.Origin + (plane.XAxis * point.Position[0] + plane.YAxis * point.Position[1] + zAxis * point.Position[2]), 1.0f));
                stream.Write(point.Color);
                stream.Write(point.Radius * plane.PointSize);
            }
            stream.Position = 0;

            // Create and fill buffer.
            _vertices = new Buffer(Renderable._device, stream, new BufferDescription()
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
            //Renderer.Singleton.Camera.UpdateResources(device);
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
                var bytecode = ShaderBytecode.CompileFromFile("Framework/Renderer/Data/DataEffects/Cloud.fx", "fx_5_0", ShaderFlags.None, EffectFlags.None);
                _cloudEffect = new Effect(_device, bytecode);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.InnerException);
            }
        }

        /// <summary>
        /// The effects that will be used by the points.
        /// </summary>
        protected static Effect _cloudEffect;
    }
}
