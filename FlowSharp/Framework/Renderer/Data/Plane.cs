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
    class Plane
    {
        public Vector3 Origin { get; protected set; }
        /// <summary>
        /// Scaled x axis.
        /// </summary>
        public Vector3 XAxis { get; protected set; }
        /// <summary>
        /// Scaled y axis.
        /// </summary>
        public Vector3 YAxis { get; protected set; }
        /// <summary>
        /// Scaled y axis.
        /// </summary>
        public Vector3 ZAxis; // { get { return Vector3.Cross(YAxis, XAxis); } }

        public float Scale { get { return Math.Min(XAxis.Length(), YAxis.Length()); } }

        public float PointSize { get; set; }

        public Plane(Vector3 origin, Vector3 xAxis, Vector3 yAxis, float scale, float pointSize = 0.1f)
        {
            Origin = origin;
            XAxis = xAxis * scale;
            YAxis = yAxis * scale;
            ZAxis = Vector3.Cross(YAxis, XAxis);
            PointSize = pointSize;
        }

        public Plane(Vector3 origin, Vector3 xAxis, Vector3 yAxis, Vector3 zAxis, float scale, float pointSize = 0.1f)
        {
            Origin = origin;
            XAxis = xAxis * scale;
            YAxis = yAxis * scale;
            ZAxis = zAxis * scale;
            PointSize = pointSize;
        }
    }
    class FieldPlane : ColormapRenderable
    {
        /// <summary>
        /// A number of equally sized textures. Depending on the number, a differnet mapping to color will be applied.
        /// </summary>
        protected ShaderResourceView[] _fields;
        protected int _width, _height;
        protected float _invalid;

        

        /// <summary>
        /// Plane to display scalar/vector field data on. Condition: Fields domain is 2D.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="xAxis"></param>
        /// <param name="yAxis"></param>
        /// <param name="scale">Scaling the field extent.</param>
        /// <param name="field"></param>
        public FieldPlane(Plane plane, VectorField fields, RenderEffect effect = RenderEffect.DEFAULT, Colormap map = Colormap.Parula)
        {
#if DEBUG
            // Assert that the fields are 2 dimensional.
            foreach(Field field in fields.Scalars)
                System.Diagnostics.Debug.Assert(field.Size.Length == 2);
#endif

            this._effect = _planeEffect;
            this._vertexSizeBytes = 32;
            this._numVertices = 6;
            this.UsedMap = map;
            this._width  = fields[0].Size[0];
            this._height = fields[0].Size[1];
            this._invalid = fields.InvalidValue ?? float.MaxValue;
            
            // Setting up the vertex buffer. 
            GenerateGeometry(plane, (RectlinearGrid)fields[0].Grid, fields.TimeSlice??0);


            // Generating Textures from the fields.
            _fields = new ShaderResourceView[effect == RenderEffect.LIC ? 2 : fields.Scalars.Length];
            for(int f = 0; f < _fields.Length; ++f)
            {
                Texture2D tex = ColorMapping.GenerateTextureFromField(_device, fields[f]);
                _fields[f] = new ShaderResourceView(_device, tex);
            }

            this.SetRenderEffect(effect);
            this._vertexLayout = new InputLayout(_device, _technique.GetPassByIndex(0).Description.Signature, new[] {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("TEXTURE", 0, Format.R32G32B32A32_Float, 16, 0)
            });
        }

        public void SetRenderEffect(RenderEffect effect)
        {
            switch (effect)
            {
                case RenderEffect.LIC:
                    //Debug.Assert(_fields.Length >= 2);
                    this._technique = _planeEffect.GetTechniqueByName("RenderLIC" + _fields.Length);
                    break;
                case RenderEffect.CHECKERBOARD:
                    this._technique = _planeEffect.GetTechniqueByName("RenderChecker");
                    break;
                case RenderEffect.COLORMAP:
                default:
                    this._technique = _planeEffect.GetTechniqueByName("RenderTex" + _fields.Length);
                    break;
                
            }

        }

        public FieldPlane(Plane plane, RectlinearGrid grid, Texture2D field, Int2 texSize, float timeSlice = 0, float invalidValue = float.MaxValue, RenderEffect effect = RenderEffect.DEFAULT, Colormap map = Colormap.Parula)
        {
            this._effect = _planeEffect;
            this._vertexSizeBytes = 32;
            this._numVertices = 6;
            this.UsedMap = map;
            this._width = texSize.X;
            this._height = texSize.Y;
            this._invalid = invalidValue;

            // Setting up the vertex buffer. 
            GenerateGeometry(plane, grid, timeSlice);


            // Generating Textures from the fields.
            _fields = new ShaderResourceView[1];
            _fields[0] = new ShaderResourceView(_device, field);

            this.SetRenderEffect(effect);
            this._vertexLayout = new InputLayout(_device, _technique.GetPassByIndex(0).Description.Signature, new[] {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("TEXTURE", 0, Format.R32G32B32A32_Float, 16, 0)
            });
        }

        /// <summary>
        /// Add one field given as texture.
        /// </summary>
        /// <param name="field"></param>
        public void AddScalar(Texture2D field)
        {
            ShaderResourceView[] cpy = _fields;
            _fields = new ShaderResourceView[_fields.Length + 1];
            Array.Copy(cpy, _fields, cpy.Length);

            _fields[cpy.Length] = new ShaderResourceView(_device, field);
        }

        public void ChangeScalar(int pos, Texture2D field)
        {
            Debug.Assert(pos < _fields.Length && pos > 0);
            _fields[pos] = new ShaderResourceView(_device, field);
        }

        /// <summary>
        /// Setting up the vertex buffer. Vertex size and number has to be known.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="xAxis"></param>
        /// <param name="yAxis"></param>
        /// <param name="scale"></param>
        protected void GenerateGeometry(Plane plane, RectlinearGrid grid, float timeOffset)
        {
            SetToSubrange(plane, grid, Vector3.Zero, (Vector3)grid.Extent, timeOffset);
            //Vector Extent = grid.Extent;
            //Vector3 maximum = plane.Origin + plane.XAxis * (Extent[0] + grid.Origin[0]) + plane.YAxis * (Extent[1] + grid.Origin[1]);
            //Vector3 origin = plane.Origin + plane.ZAxis * timeOffset + plane.XAxis * grid.Origin[0] + plane.YAxis * grid.Origin[1];

            //// Offset, becaue the grid data points shall be OUTSIDE of the grid cells.
            //float uMin = 1.0f / (grid.Size[0] - 1) * 0.5f;
            //float vMin = 1.0f / (grid.Size[1] - 1) * 0.5f;
            //float uMax = 1 - uMin;
            //float vMax = 1 - vMin;

            //// Write poition and UV-map data.
            //var stream = new DataStream(_numVertices * _vertexSizeBytes, true, true);
            //stream.WriteRange(new[] {
            //    new Vector4( origin[0], maximum[1], origin[2], 1.0f), new Vector4(uMin, vMax, 0.0f, 1.0f),
            //    new Vector4(maximum[0], maximum[1], origin[2], 1.0f), new Vector4(uMax, vMax, 0.0f, 1.0f),
            //    new Vector4(maximum[0],  origin[1], origin[2], 1.0f), new Vector4(uMax, vMin, 0.0f, 1.0f),

            //    new Vector4( origin[0], maximum[1], origin[2], 1.0f), new Vector4(uMin, vMax, 0.0f, 1.0f),
            //    new Vector4(maximum[0],  origin[1], origin[2], 1.0f), new Vector4(uMax, vMin, 0.0f, 1.0f),               
            //    new Vector4( origin[0],  origin[1], origin[2], 1.0f), new Vector4(uMin, vMin, 0.0f, 1.0f)

            //});
            //stream.Position = 0;

            //// Create and fill buffer.
            //_vertices = new Buffer(_device, stream, new BufferDescription()
            //{
            //    BindFlags = BindFlags.VertexBuffer,
            //    CpuAccessFlags = CpuAccessFlags.None,
            //    OptionFlags = ResourceOptionFlags.None,
            //    SizeInBytes = _numVertices * _vertexSizeBytes,
            //    Usage = ResourceUsage.Default
            //});
            //stream.Dispose();
        }

        public void SetToSubrangeFloat(Plane plane, RectlinearGrid grid, Vector2 minBox, Vector2 extent, float timeOffset = 0)
        {
            SetToSubrange(plane, grid, new Vector3(minBox[0] * grid.Extent[0], minBox[1] * grid.Extent[1], 0), new Vector3(grid.Extent[0] * extent[0], grid.Extent[1] * extent[1], 0), timeOffset);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="plane"></param>
        /// <param name="grid"></param>
        /// <param name="minBox">Minimal values of the box in world space.</param>
        /// <param name="extentBox">Extent of the range in world space.</param>
        /// <param name="timeOffset"></param>
        public void SetToSubrange(Plane plane, RectlinearGrid grid, Vector3 minBox, Vector3 extentBox, float timeOffset = 0)
        {
            Vector extent = grid.Extent;
            Vector3 origin = plane.Origin + plane.ZAxis * timeOffset + plane.XAxis * grid.Origin[0] + plane.YAxis * grid.Origin[1] + minBox * plane.Scale;
            Vector3 maximum = origin + extentBox * plane.Scale;

            // Offset, becaue the grid data points shall be OUTSIDE of the grid cells. [x]
            float uMin = 1.0f / (grid.Size[0] - 1) * 0.5f;
            float vMin = 1.0f / (grid.Size[1] - 1) * 0.5f;
            uMin += minBox.X / extent[0];
            vMin += minBox.Y / extent[1];
            
            float uMax = uMin + extentBox.X / (extent[0]);
            float vMax = vMin + extentBox.Y / (extent[1]);  //vMin * 2 * maxBox.Y + 1;
            

            // Write poition and UV-map data.
            var stream = new DataStream(_numVertices * _vertexSizeBytes, true, true);
            stream.WriteRange(new[] {
                new Vector4( origin[0], maximum[1], origin[2], 1.0f), new Vector4(uMin, vMax, 0.0f, 1.0f),
                new Vector4(maximum[0], maximum[1], origin[2], 1.0f), new Vector4(uMax, vMax, 0.0f, 1.0f),
                new Vector4(maximum[0],  origin[1], origin[2], 1.0f), new Vector4(uMax, vMin, 0.0f, 1.0f),

                new Vector4( origin[0], maximum[1], origin[2], 1.0f), new Vector4(uMin, vMax, 0.0f, 1.0f),
                new Vector4(maximum[0],  origin[1], origin[2], 1.0f), new Vector4(uMax, vMin, 0.0f, 1.0f),
                new Vector4( origin[0],  origin[1], origin[2], 1.0f), new Vector4(uMin, vMin, 0.0f, 1.0f)

            });
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
            for(int fieldNr = 0; fieldNr < _fields.Length; ++fieldNr)
                _planeEffect.GetVariableByName("field" + fieldNr).AsResource().SetResource(_fields[fieldNr]);

            _planeEffect.GetVariableByName("width").AsScalar().Set(_width);
            _planeEffect.GetVariableByName("height").AsScalar().Set(_height);
            _planeEffect.GetVariableByName("invalidNum").AsScalar().Set(_invalid);            

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
                var bytecode = ShaderBytecode.CompileFromFile("Framework/Renderer/Data/DataEffects/Plane.fx", "fx_5_0", ShaderFlags.None, EffectFlags.None);
                _planeEffect = new Effect(_device, bytecode);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// The effects that will be used by the plane. Use technique "RenderTex"+numTextures to get the right technique.
        /// </summary>
        protected static Effect _planeEffect;

        /// <summary>
        /// Device for creating resources.
        /// </summary>
        //protected static Device _device;

        public enum RenderEffect
        {
            DEFAULT = 0,
            LIC = 1,
            CHECKERBOARD = 2,
            COLORMAP = 3
        }
    }
}
