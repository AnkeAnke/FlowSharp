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
        //public void AddOffsetZ(float offset) { Origin = Origin + Vector3.UnitZ * offset; }
        /// <summary>
        /// Scaled x axis.
        /// </summary>
        public Vector3 XAxis { get { return _xAxis * Scale.X; } protected set { _xAxis = value; _xAxis.Normalize(); } }
        private Vector3 _xAxis;
        /// <summary>
        /// Scaled y axis.
        /// </summary>
        public Vector3 YAxis { get { return _yAxis * Scale.Y; } protected set { _yAxis = value; _yAxis.Normalize(); } }
        private Vector3 _yAxis;
        /// <summary>
        /// Scaled y axis.
        /// </summary>
        public Vector3 ZAxis { get { return _zAxis * Scale.Z; } protected set { _zAxis = value; _zAxis.Normalize(); } }
        private Vector3 _zAxis;

        public Vector3 Scale;// { get { return Math.Min(XAxis.Length(), YAxis.Length()); } }

        public float PointSize { get; set; }

        public Plane(Vector3 origin, Vector3 xAxis, Vector3 yAxis, float scale, float pointSize = 0.1f)
        {
            Origin = origin;
            XAxis = xAxis;// * scale;
            YAxis = yAxis;// * scale;
            ZAxis = Vector3.Cross(YAxis, XAxis);
            Scale = new Vector3(scale);
            PointSize = pointSize;
        }

        public Plane(Vector3 origin, Vector3 xAxis, Vector3 yAxis, Vector3 scale, float pointSize = 0.1f)
        {
            Origin = origin;
            XAxis = xAxis;// * scale;
            YAxis = yAxis;// * scale;
            ZAxis = Vector3.Cross(YAxis, XAxis);
            Scale = scale;
            PointSize = pointSize;
        }

        public Plane(Vector3 origin, Vector3 xAxis, Vector3 yAxis, Vector3 zAxis, float scale, float pointSize = 0.1f)
        {
            Origin = origin;
            XAxis = xAxis;// * scale;
            YAxis = yAxis;// * scale;
            ZAxis = zAxis;// * scale;
            Scale = new Vector3(scale);
            PointSize = pointSize;
        }

        public Plane(Vector3 origin, Vector3 xAxis, Vector3 yAxis, Vector3 zAxis, Vector3 scale, float pointSize = 0.1f)
        {
            Origin = origin;
            XAxis = xAxis;// * scale;
            YAxis = yAxis;// * scale;
            ZAxis = zAxis;// * scale;
            Scale = scale;
            PointSize = pointSize;
        }

        public Plane(Plane cpy, Vector3 offset)
        {
            Origin = cpy.Origin + offset;
                //+ offset.X * cpy.XAxis
                //+ offset.Y * cpy.YAxis
                //+ offset.Z * cpy.ZAxis;
            XAxis = cpy.XAxis;
            YAxis = cpy.YAxis;
            ZAxis = cpy.ZAxis;
            Scale = cpy.Scale;
            PointSize = cpy.PointSize;
        }

        public Plane(Plane cpy, float zScale)
        {
            Origin = cpy.Origin;
            XAxis  = cpy.XAxis;
            YAxis  = cpy.YAxis;
            ZAxis = cpy.ZAxis;// * zScale;
            Scale.Z = zScale;
            PointSize = cpy.PointSize;
        }

        public Plane(Plane cpy)
        {
            Origin = cpy.Origin;
            XAxis = cpy.XAxis;
            YAxis = cpy.YAxis;
            ZAxis = cpy.ZAxis;
            Scale = cpy.Scale;
            PointSize = cpy.PointSize;
        }

        public static Plane FitToPoints<P>(Vector4 origin, float maximalExtent, PointSet<P> points) where P : Point
        {
            // Extent.
            Vector4 extent = points.MaxPosition - points.MinPosition;
            float maxEx = Math.Max(Math.Max(extent[0], extent[1]), extent[2]);

            //foreach (P p in points.Points)
            //{
            //    p.Position = (p.Position - minPos) / maxEx;
            //}
            float scale = maximalExtent / maxEx;
            Vector4 scaledOrigin = origin - points.MinPosition * scale;
            //Vector3 newOrigin = origin - minPos;

            return new Plane(Util.Convert(scaledOrigin), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, scale, scale * 0.01f);

//          return new Plane(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ, 1);
        }

        public static Plane FitToPoints(Vector3 origin, float maximalExtent, VectorData verts)
        {
            verts.ExtractMinMax();
            //Vector3 minPos = (Vector3)verts[0];
            //Vector3 maxPos = (Vector3)verts[0];

            //// Find min and max in each dimension.
            //foreach (VectorRef p in verts)
            //{
            //    for (int v = 0; v < 3; ++v)
            //    {
            //        minPos[v] = Math.Min(minPos[v], p[v]);
            //        maxPos[v] = Math.Max(maxPos[v], p[v]);
            //    }
            //}

            // Extent.
            Vector3 extent = (Vector3)(verts.MaxValue - verts.MinValue);
            float maxEx = Math.Max(Math.Max(extent[0], extent[1]), extent[2]);

            ////foreach (P p in points.Points)
            ////{
            ////    p.Position = (p.Position - minPos) / maxEx;
            ////}
            //float scale = maximalExtent / maxEx;
            //Vector3 scaledOrigin = origin - (Vector3)verts.MinValue * scale;

            //return new Plane(scaledOrigin, Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ, scale, scale * 0.01f);
            float scale = maximalExtent / maxEx;
            Vector3 scaledOrigin = origin - (Vector3)verts.MinValue * scale;
            //Vector3 newOrigin = origin - minPos;

            return new Plane(scaledOrigin, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, scale, scale * 0.01f);
        }

        public List<Renderable> GenerateAxisGlyph()
        {
            List<Renderable> result = new List<Renderable>(4);

            Point[] ends = new Point[3];
            for (int a = 0; a < 3; ++a)
            {
                Vector3 vec = Vector3.Zero;
                vec[a] = 1;

                Line line = new Line(2);
                result.Add(new LineBall(this, new LineSet(new Line[] { new Line()
                {
                    Positions = new Vector4[] { Vector4.Zero, Util.Convert(vec) } } }) {
                    Color = vec, Thickness = 0.01f
                }));

                ends[a] = new Point(vec) { Color = vec, Radius = 0.02f };
            }
            result.Add(new PointCloud(this, new PointSet<Point>(ends)));

            return result;
        }

        public List<Renderable> GenerateOriginAxisGlyph()
        {
            List<Renderable> result = new List<Renderable>(4);
            Plane noOffset = new Plane(this, -Origin);
            noOffset.Scale = new Vector3(1);
            Point[] ends = new Point[3];
            for (int a = 0; a < 3; ++a)
            {
                Vector3 vec = Vector3.Zero;
                vec[a] = 1;

                Line line = new Line(2);
                result.Add(new LineBall(noOffset, new LineSet(new Line[] { new Line() {
                    Positions = new Vector4[] { Vector4.Zero, Util.Convert(vec) } } }) {
                    Color = vec, Thickness = 0.01f }));

                ends[a] = new Point(vec) { Color = vec, Radius = 0.02f };
            }
            result.Add(new PointCloud(noOffset, new PointSet<Point>(ends)));

            return result;
        }



    }
    class FieldPlane : ColormapRenderable
    {
        /// <summary>
        /// A number of equally sized textures. Depending on the number, a differnet mapping to color will be applied.
        /// </summary>
        protected ShaderResourceView[] _fieldTextures;
        protected VectorField _field;
        protected int _width, _height;
        protected float _invalid;
        protected Plane _timePlane;
        public FieldPlane.RenderEffect Effect
        {
            get; protected set;
        }
        public int NumTextures { get { return _fieldTextures.Length; } }


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
//#if DEBUG
//            // Assert that the fields are 2 dimensional.
//            foreach(Field field in fields.Scalars)
//                System.Diagnostics.Debug.Assert(field.Size.Length >= 2);
//#endif
            this._effect = _planeEffect;
            this._vertexSizeBytes = 32;
            this._numVertices = 6;
            this.UsedMap = map;
            this._width  = fields.Size[0];
            this._height = fields.Size[1];
            this._invalid = fields.InvalidValue ?? float.MaxValue;
            this._field = fields;
            
            // Setting up the vertex buffer. 
            GenerateGeometry(plane, fields.Size.ToInt2(), fields.TimeSlice??0);


            // Generating Textures from the fields.
            _fieldTextures = new ShaderResourceView[fields.NumVectorDimensions];
            for(int f = 0; f < _field.NumVectorDimensions; ++f)
            {
                Texture2D tex = ColorMapping.GenerateTextureFromField(_device, fields); // Was fields[0]...
                _fieldTextures[f] = new ShaderResourceView(_device, tex);
            }

            this.SetRenderEffect(effect);
            this._vertexLayout = new InputLayout(_device, _technique.GetPassByIndex(0).Description.Signature, new[] {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("TEXTURE", 0, Format.R32G32B32A32_Float, 16, 0)
            });
        }

        public void SetRenderEffect(RenderEffect effect)
        {
            // Ad length texture.
            if(effect == RenderEffect.LIC_LENGTH && Effect != RenderEffect.LIC_LENGTH && _field != null)
            {
                VectorField length = new VectorField(_field, FieldAnalysis.VFLength, 1, false);
                this.AddScalar(length.GetChannel(0));
            }
            // Remove length texture?
            else if (effect != RenderEffect.LIC_LENGTH && Effect == RenderEffect.LIC_LENGTH && _field != null)
            {
                ShaderResourceView[] cpy = _fieldTextures;
                _fieldTextures = new ShaderResourceView[cpy.Length - 1];
                Array.Copy(cpy, _fieldTextures, _fieldTextures.Length);
            }
            switch (effect)
            {
                case RenderEffect.LIC:
                case RenderEffect.LIC_LENGTH:
                    //Debug.Assert(_fields.Length >= 2);
                    this._technique = _planeEffect.GetTechniqueByName("RenderLIC" + _fieldTextures.Length);
                    break;
                case RenderEffect.CHECKERBOARD:
                    this._technique = _planeEffect.GetTechniqueByName("RenderChecker");
                    break;
                case RenderEffect.CHECKERBOARD_COLORMAP:
                    this._technique = _planeEffect.GetTechniqueByName("RenderCheckerTex" + _fieldTextures.Length);
                    break;
                case RenderEffect.OVERLAY:
                    this._technique = _planeEffect.GetTechniqueByName("Overlay" + _fieldTextures.Length);
                    break;
                case RenderEffect.LAPLACE:
                    this._technique = _planeEffect.GetTechniqueByName("Laplace" + _fieldTextures.Length);
                    break;
                case RenderEffect.GRADIENT:
                    this._technique = _planeEffect.GetTechniqueByName("Gradient" + _fieldTextures.Length);
                    break;
                case RenderEffect.COLORMAP:
                default:
                    this._technique = _planeEffect.GetTechniqueByName("RenderTex" + _fieldTextures.Length);
                    break;
                
            }
            Effect = effect;

        }

        public FieldPlane(Plane plane, Texture2D field, Int2 texSize, float timeSlice = 0, float invalidValue = float.MaxValue, RenderEffect effect = RenderEffect.DEFAULT, Colormap map = Colormap.Parula)
        {
            this._effect = _planeEffect;
            this._vertexSizeBytes = 32;
            this._numVertices = 6;
            this.UsedMap = map;
            this._width = texSize.X;
            this._height = texSize.Y;
            this._invalid = invalidValue;

            // Setting up the vertex buffer. 
            GenerateGeometry(plane, texSize, timeSlice);


            // Generating Textures from the fields.
            _fieldTextures = new ShaderResourceView[1];
            _fieldTextures[0] = new ShaderResourceView(_device, field);

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
            ShaderResourceView[] cpy = _fieldTextures;
            _fieldTextures = new ShaderResourceView[_fieldTextures.Length + 1];
            Array.Copy(cpy, _fieldTextures, cpy.Length);

            _fieldTextures[cpy.Length] = new ShaderResourceView(_device, field);

            SetRenderEffect(Effect);
        }

        /// <summary>
        /// Add one field given as texture.
        /// </summary>
        /// <param name="field"></param>
        public void AddScalar(ScalarField field)
        {
            ShaderResourceView[] cpy = _fieldTextures;
            _fieldTextures = new ShaderResourceView[_fieldTextures.Length + 1];
            Array.Copy(cpy, _fieldTextures, cpy.Length);

            Texture2D tex = ColorMapping.GenerateTextureFromField(_device, field);
            _fieldTextures[cpy.Length] = new ShaderResourceView(_device, tex);

            SetRenderEffect(Effect);
        }

        public void ChangeScalar(int pos, Texture2D field)
        {
            Debug.Assert(pos < _fieldTextures.Length && pos > 0);
            _fieldTextures[pos] = new ShaderResourceView(_device, field);
        }

        /// <summary>
        /// Setting up the vertex buffer. Vertex size and number has to be known.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="xAxis"></param>
        /// <param name="yAxis"></param>
        /// <param name="scale"></param>
        protected void GenerateGeometry(Plane plane, Int2 size, float timeOffset)
        {
            SetToSubrange(plane, size, Vector3.Zero, (Vector3)(Vector)size, timeOffset);
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

        public void SetToSubrangeFloat(Plane plane, Int2 size, Vector2 minBox, Vector2 extent, float timeOffset = 0)
        {
            SetToSubrange(plane, size, new Vector3(minBox[0] * size[0], minBox[1] * size[1], 0), new Vector3(size[0] * extent[0], size[1] * extent[1], 0), timeOffset);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="plane"></param>
        /// <param name="grid"></param>
        /// <param name="minBox">Minimal values of the box. Minimum: (0,0)</param>
        /// <param name="extentBox">Extent of the subplane. Maximum: Size of the Grid.</param>
        /// <param name="timeOffset"></param>
        public void SetToSubrange(Plane plane, Int2 size, Vector3 minBox, Vector3 extentBox, float timeOffset = 0)
        {
            Vector extent = (Vector)size;
            Vector3 origin = plane.Origin + plane.ZAxis * timeOffset + minBox[0] * plane.XAxis + minBox[1] * plane.YAxis;
            _timePlane = new Plane(plane, timeOffset * Vector3.UnitZ);
            Vector3 maximum = origin 
                + extentBox[0] * plane.XAxis 
                + extentBox[1] * plane.YAxis 
                + extentBox[2] * plane.ZAxis;

            // Offset, becaue the grid data points shall be OUTSIDE of the grid cells. [x]
            float uMin = 1.0f / (size[0] - 1) * 0.5f;
            float vMin = 1.0f / (size[1] - 1) * 0.5f;
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

        public Plane GetIntersectionPlane()
        {
            return new Plane(_timePlane);
        }

        public override void Render(Device device)
        {
            for(int fieldNr = 0; fieldNr < _fieldTextures.Length; ++fieldNr)
                _planeEffect.GetVariableByName("field" + fieldNr).AsResource().SetResource(_fieldTextures[fieldNr]);

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
            COLORMAP,
            OVERLAY,
            LAPLACE,
            GRADIENT,
            LIC,
            LIC_LENGTH,
            CHECKERBOARD,
            CHECKERBOARD_COLORMAP
        }
    }
}
