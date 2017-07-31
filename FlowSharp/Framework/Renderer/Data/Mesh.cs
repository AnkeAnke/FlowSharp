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
        protected float _thickness = 0;
        protected Vector3 _planeNormal;
        public LineBall.RenderEffect Effect
        {
            get; protected set;
        }

        //private Buffer _indices;

#region TiledSurface
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

            GenerateGeometry(plane, surf);

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
            Vector4[] verts = surf.GetVertices(plane);
            uint[] idxs = surf.Indices;

            _numVertices = verts.Length;
            _numindices = idxs.Length;

            // Write poition and UV-map data.
            var stream = new DataStream(_numVertices * _vertexSizeBytes, true, true);
            stream.WriteRange(verts);
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

            stream = new DataStream(idxs.Length * sizeof(uint), true, true);
            stream.WriteRange(idxs);
            stream.Position = 0;

            _indices = new Buffer(_device, stream, new BufferDescription()
            {
                BindFlags = BindFlags.IndexBuffer,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = idxs.Length * sizeof(int),
                Usage = ResourceUsage.Default
            });
        }
        #endregion TiledSurface
#region VertexIndexList
        /// <summary>
        /// Set of lines in 3D space.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="xAxis"></param>
        /// <param name="yAxis"></param>
        /// <param name="scale">Scaling the field extent.</param>
        /// <param name="field"></param>
        public Mesh(Plane plane, Vector[] verts, Index[] sides, RenderEffect effect = RenderEffect.DEFAULT, Colormap colormap = Colormap.Parula)
        {
            _color = Vector3.UnitZ;
            this._vertexSizeBytes = Marshal.SizeOf(typeof(Vector4));
            this._topology = PrimitiveTopology.TriangleList;

            // Setting up the vertex buffer.             
            UsedMap = colormap;
            _planeNormal = plane.ZAxis;
            _planeNormal.Normalize();
            _effect = _meshEffect;
            SetRenderEffect(effect);

            GenerateGeometry(plane, verts, sides);

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
        protected void GenerateGeometry(Plane plane, Vector[] verts, Index[] sides)
        {
            // COnvert from Vector to SlimDX.Vector4.
            Vector4[] vertices = new Vector4[verts.Length];
            for (int v = 0; v < vertices.Length; ++v)
            {
                int vertexDim = verts[v].Length;
                Debug.Assert(vertexDim >= 3, "Need at least 3D objects.");
                vertices[v] = new Vector4(plane.Origin + verts[v][0] * plane.XAxis + verts[v][1] * plane.YAxis + verts[v][2] * plane.ZAxis, vertexDim > 3? verts[v][3] : verts[v][2]);
            }

            // Assume all indices are all triangles.
            int numIndicesPerSide = 3 * (sides[0].Length - 2);
            uint[] idxs = new uint[sides.Length * numIndicesPerSide];
            for (int i = 0; i < sides.Length; ++i)
            {
                Debug.Assert(sides[i].Length == sides[0].Length, "Assuming same size everywhere.");
                for (int t = 0; t < 3; ++t)
                    idxs[numIndicesPerSide * i + t] = (uint)sides[i][t];

                // For each vertex above the first three, add a triangle (strip-style).
                for (int tri = 3; tri < sides[0].Length; ++tri)
                {
                    idxs[numIndicesPerSide * i + (tri - 2) * 3] = (uint)sides[i][tri];
                    idxs[numIndicesPerSide * i + (tri - 2) * 3 + 2] = (uint)sides[i][0];
                    idxs[numIndicesPerSide * i + (tri - 2) * 3 + 1] = idxs[numIndicesPerSide * i + (tri - 2) * 3 - 1];
                }

            }

            _numVertices = verts.Length;
            _numindices = idxs.Length;

            // Write poition and UV-map data.
            var stream = new DataStream(_numVertices * _vertexSizeBytes, true, true);
            stream.WriteRange(vertices);
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

            stream = new DataStream(idxs.Length * sizeof(uint), true, true);
            stream.WriteRange(idxs);
            stream.Position = 0;

            _indices = new Buffer(_device, stream, new BufferDescription()
            {
                BindFlags = BindFlags.IndexBuffer,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = idxs.Length * sizeof(int),
                Usage = ResourceUsage.Default
            });
        }
        #endregion VertexIndexList
#region TetGrid
        /// <summary>
        /// Set of lines in 3D space.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="xAxis"></param>
        /// <param name="yAxis"></param>
        /// <param name="scale">Scaling the field extent.</param>
        /// <param name="field"></param>
        public Mesh(Plane plane, GeneralUnstructurdGrid grid, VectorData data = null, RenderEffect effect = RenderEffect.DEFAULT, Colormap colormap = Colormap.Parula)
        {
            _color = Vector3.UnitZ;
            this._vertexSizeBytes = Marshal.SizeOf(typeof(Vector4));
            this._topology = PrimitiveTopology.TriangleList;

            // Setting up the vertex buffer.             
            UsedMap = colormap;
            _planeNormal = plane.ZAxis;
            _planeNormal.Normalize();
            _effect = _meshEffect;
            SetRenderEffect(effect);

            GenerateGeometry(plane, grid, data);

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
        protected void GenerateGeometry(Plane plane, GeneralUnstructurdGrid grid, VectorData data)
        {
            int vertexDim = grid.Vertices.NumVectorDimensions;

            _numVertices = grid.Vertices.Length;

            // Write position and UV-map data.
            var stream = new DataStream(_numVertices * _vertexSizeBytes, true, true);
            for (int v = 0; v < grid.Vertices.Length; ++v)
                stream.Write(new Vector4(plane.Origin + grid.Vertices[v][0] * plane.XAxis + grid.Vertices[v][1] * plane.YAxis + grid.Vertices[v][2] * plane.ZAxis, vertexDim > 3 ? grid.Vertices[v][3] : (data?[v][0] ?? grid.Vertices[v][2])));
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

            IndexArray indices = grid.AssembleIndexList();

            stream.Position = 0;
            _numindices = indices.Length * 3;


            stream = new DataStream(_numindices * sizeof(uint), true, true);
            for (int c = 0; c < indices.Length; c++)
            {
                //Debug.Assert(indices[c].Length == 3, "Expected triangles, vertex count: " + indices[c].Length);
                for (int v = 0; v < 3; ++v)
                    stream.Write(indices[c][v]);
            }
            stream.Position = 0;

            _indices = new Buffer(_device, stream, new BufferDescription()
            {
                BindFlags = BindFlags.IndexBuffer,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = _numindices * sizeof(int),
                Usage = ResourceUsage.Default
            });
        }
#endregion TetGrid

        public void SetRenderEffect(RenderEffect effect)
        {
            switch (effect)
            {
                default:
                    this._technique = _meshEffect.GetTechniqueByName("Height");
                    break;

            }
        }

        public override void Render(Device device)
        {
            //_meshEffect.GetVariableByName("color").AsVector().Set(_color);
            //_meshEffect.GetVariableByName("worldNormal").AsVector().Set(_planeNormal);
            //_meshEffect.GetVariableByName("thickness").AsScalar().Set(_thickness);
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
