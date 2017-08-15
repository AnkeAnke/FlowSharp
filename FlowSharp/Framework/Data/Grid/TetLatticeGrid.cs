using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX;
using static FlowSharp.Octree;

namespace FlowSharp
{
    class TetLatticeGrid : FieldGrid, GeneralUnstructurdGrid
    {
        //public VectorList _vertices;
        public VectorData Vertices { get; set; }// { get { return _vertices; } set { _vertices = value as VectorList; } }

        public IndexArray Cells;
        public int NumCells { get { return Cells.Length; } }

        public FixedRadiusLattice Lattice;

        private VectorData _cellCenters;
        // private List<int>[] _cellsPerVertex;

        public TetLatticeGrid(UnstructuredGeometry geom, Vector origin = null, float? timeOrigin = null) : this(geom.Vertices, geom.Primitives, origin, timeOrigin) { }

        /// <summary>
        /// Create a new tetraeder grid descriptor.
        /// </summary>
        public TetLatticeGrid(VectorData vertices, IndexArray indices, Vector origin = null, float? timeOrigin = null)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();

            // For Dimensionality.
            Size = new Index(4);
            Cells = indices;
            //            Cells = new Tet[indices.Length * 5];
            Vertices = vertices;

            Debug.Assert(vertices.Length > 0 && indices.Length > 4, "No data given.");
            Debug.Assert(indices.IndexLength == 4, "Not tets.");
            int dim = vertices[0].Length;

            // Space position.
            Origin = origin ?? new Vector(0, dim);
            TimeDependant = timeOrigin != null;

            // Link vertices to cells for stabbing queries.
            // Setup data structure.
            //_cellsPerVertex = new List<int>[Vertices.Length];
            //for (int v = 0; v < _cellsPerVertex.Length; ++v)
            //    _cellsPerVertex[v] = new List<int>(16);


            _cellCenters = new VectorBuffer(Cells.Length, Vertices.VectorLength);

            // Each cell registers itself with all of its vertices.
            for (int c = 0; c < Cells.Length; ++c)
            {
                _cellCenters[c] = (Vertices[Cells[c][0]] + Vertices[Cells[c][1]] + Vertices[Cells[c][2]] + Vertices[Cells[c][3]]) * 0.25f;
            }
            _cellCenters.MinValue = Vertices.MinValue;
            _cellCenters.MaxValue = Vertices.MaxValue;

            float maxLength = 0;
            int samples = 4;
            for (int s = 0; s < samples; ++s)
            {
                float dist = (Vertices[Cells[(s * Cells.Length) / samples][0]] - Vertices[Cells[(s * Cells.Length) / samples][1]]).LengthSquared();
                maxLength = Math.Max(maxLength, dist);
            }

            Lattice = new FixedRadiusLattice(_cellCenters, maxLength);

            watch.Stop();
            Console.WriteLine("Grid buildup took {0}m {1}s", (int)watch.Elapsed.TotalMinutes, watch.Elapsed.Seconds);

            Lattice.ShowStatistics();
        }

        public Index[] GetAllSides()
        {
            List<Index> sides = new List<Index>(Cells.Length);

            for (int c = 0; c < Cells.Length; ++c)
            {
                // Dirty quickfix: Duplicate the first cells multiple times, so we don't need to deal with uninitialized tets.
                Index verts = Cells[c];
                if (verts == null)
                    continue;
                sides.Add(new Index(new int[] { verts[0], verts[1], verts[2] }));
                sides.Add(new Index(new int[] { verts[0], verts[2], verts[3] }));
                sides.Add(new Index(new int[] { verts[0], verts[1], verts[3] }));
                sides.Add(new Index(new int[] { verts[1], verts[2], verts[3] }));
            }

            return sides.ToArray();
        }

        public override FieldGrid Copy()
        {
            throw new NotImplementedException("Grid is so big I don't want to copy it.");
        }

        public override int NumAdjacentPoints()
        {
            return 4; // Constant withing tetraeders, though we don't know neighborhood of vertices themselves.
        }

        /// <summary>
        /// Append a time dimension.
        /// </summary>
        /// <param name="numTimeSlices"></param>
        /// <param name="timeStart"></param>
        /// <param name="timeStep"></param>
        /// <returns></returns>
        public override FieldGrid GetAsTimeGrid(int numTimeSlices, float timeStart, float timeStep)
        {
            return this;//new TetGrid(this);
        }

        /// <summary>
        /// Returns the adjacent grid point indices.
        /// Indices in ascending order.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="indices"></param>
        /// <param name="weights"></param>
        public override Index FindAdjacentIndices(VectorRef pos, out VectorRef weights)
        {
            // Setup return values.
            int numPoints = NumAdjacentPoints();
            //Index indices = new int[numPoints];
            //weights = new float[numPoints];

            int cell = FindCell(pos, out weights);
            //Debug.Assert(cell >= 0, "Not in the grid.");
            return cell >= 0 ? new Index(Cells[cell]) : null;
        }

        public override bool InGrid(Vector position)
        {
            return true;
            //Debug.Assert(position.Length == Size.Length, "Trying to access " + Size.Length + "D field with " + position.Length + "D index.");

            //Vector bary;
            //return FindCell(position, out bary) >= 0;
        }

        static uint NUM_SAMPLES = 0;
        static uint NUM_UNSUCCESSFULL_SAMPLES = 0;
        static uint NUM_SAMPLE_OUTSIDE_NEAREST = 0;
        public static void ShowSampleStatistics()
        {
            Console.WriteLine("{0} \tsamples.\n{1}% \tsampled neighbor nodes.\n{2}% \tnot found at all.", NUM_SAMPLES, ((float)NUM_SAMPLE_OUTSIDE_NEAREST) / NUM_SAMPLES * 100, ((float)NUM_UNSUCCESSFULL_SAMPLES) / NUM_SAMPLES * 100);
        }

        private int FindCell(VectorRef pos, out VectorRef bary)
        {
            // Simply sample all within half maximal distance.
            NUM_SAMPLES++;
            var center = Lattice[pos];
            int tet = FindInNode(center, pos, out bary);
            if (tet >= 0)
                return tet;

            // Sample neighbor cells - maximal distance.
            NUM_SAMPLE_OUTSIDE_NEAREST++;
            var neighs = Lattice.GetNeighborData(pos);
            foreach (var neigh in neighs)
            {
                tet = FindInNode(neigh, pos, out bary);
                if (tet >= 0)
                    return tet;
            }

            NUM_UNSUCCESSFULL_SAMPLES++;
            return -1;
        }

        private int FindInNode(List<int> vertices, VectorRef pos, out VectorRef bary)
        {
            bary = null;
            if (vertices == null)
                return -1;

            // Test whether inside.
            foreach (int tet in vertices)
            {
                bary = ToBaryCoord(tet, pos);
                if (bary.IsPositive())
                {
                    return tet;
                }
            }
            return -1;
        }

        public Vector ToBaryCoord(int cell, VectorRef worldPos)
        {
            Debug.Assert(worldPos.Length == Vertices.VectorLength);
            SquareMatrix tet = new SquareMatrix(3);
            VectorRef origin = Vertices[Cells[cell][0]];
            for (int i = 0; i < 3; ++i)
            {
                tet[i] = Vertices[Cells[cell][i + 1]] - origin;
            }

            Vector result = tet.Inverse() * ((worldPos - origin));
            result = new Vector(new float[] { 1f - result.Sum(), result[0], result[1], result[2] });

            return result;
        }

        #region DebugRendering

        //public LineSet GetWireframe()
        //{
        //    Line[] lines = new Line[Indices.Length];

        //    for (int l = 0; l < Indices.Length; ++l)
        //    {
        //        lines[l] = new Line(NumCorners);
        //        for (int v = 0; v < NumCorners; ++v)
        //        {
        //            lines[l].Positions[v] = (SlimDX.Vector3) Vertices[Indices[l][v]];
        //        }
        //    }

        //    return new LineSet(lines);
        //}

        /// <summary>
        /// Assemble all inidices to a buffer. Do this here for general Tet grids.
        /// </summary>
        /// <returns></returns>
        public Tuple<VectorData, IndexArray> AssembleIndexList()
        {
            //Index[] cells = new Index[Cells.Length];
            //for (int c = 0; c < Cells.Length; ++c)
            //        cells[c] = Cells[c].VertexIndices;
            //return cells;
            IndexArray tris = new IndexArray(Cells.Length, 4);
            for (int c = 0; c < Cells.Length; c++)
            {
                for (int s = 0; s < 4; ++s)
                {
                    tris[c * 4 + s] = new Index(3);
                    int count = 0;
                    for (int i = 0; i < 4; ++i)
                        if (i != s)
                            tris[c * 4 + s][count++] = Cells[c][i];
                }
            }

            return new Tuple<VectorData, IndexArray>(Vertices, tris);
        }

        public PointSet<Point> SampleTest(VectorField data, int vertsPerSide = 100)
        {
            data.Data.ExtractMinMax();

            Stopwatch watch = new Stopwatch();
            watch.Start();

            List<Point> verts = new List<Point>(vertsPerSide * vertsPerSide * vertsPerSide / 5);
            Vector extent = Vertices.MaxValue - Vertices.MinValue;

            for (int x = 0; x < vertsPerSide; ++x)
                for (int y = 0; y < vertsPerSide; ++y)
                    for (int z = 0; z < vertsPerSide; ++z)
                    {
                        Vector pos = Vertices.MinValue +
                            new Vector(new float[] {
                                       ((float)x) / vertsPerSide * extent[0],
                                       ((float)y) / vertsPerSide * extent[1],
                                       ((float)z) / vertsPerSide * extent[2] });
                        //                        Index tet = this.FindAdjacentIndices(pos, out weight);
                        //                        if (tet != null)
                        Vector sample = Sample(data, pos);
                        if (sample != null)
                        {
                            float color = ((sample - data.Data.MinValue) / (data.Data.MaxValue - data.Data.MinValue))[0];
                            verts.Add(new Point((Vector3)pos) { Radius = 0.02f, Color = new Vector3(color) });
                        }
                    }


            watch.Stop();
            Console.WriteLine("Grid stabbing with {0} samples took {1}m {2}s", vertsPerSide * vertsPerSide * vertsPerSide, (int)watch.Elapsed.TotalMinutes, watch.Elapsed.Seconds);

            return new PointSet<Point>(verts.ToArray());
        }

        public PointSet<Point> GetVertices()
        {
            Point[] verts = new Point[Vertices.Length];
            for (int p = 0; p < Vertices.Length; ++p)
            {
                verts[p] = new Point((Vector3)Vertices[p]) { Color = (Vector3)Vertices[p], Radius = 0.001f };
            }

            return new PointSet<Point>(verts);
        }

        private Vector ToBaryCoord(int celLidx, Vector worldPos)
        {
            Matrix tet = new Matrix();
            for (int c = 0; c < 4; ++c)
            {
                tet.set_Columns(c, (Vector4)Vertices[Cells[celLidx][c]]);
            }

            tet.Invert();
            Vector4 result = Vector4.Transform((Vector4)worldPos, tet);
            return new Vector(result);

        }

        /// <summary>
        /// Binary search for the last point inside the domain.
        /// </summary>
        /// <param name="pos">The last valid position on the inside.</param>
        /// <param name="outsidePos">First position found outside.</param>
        /// <returns></returns>
        public override Vector CutToBorder(VectorField field, VectorRef pos, VectorRef outsidePos)
        {
            float eps = Lattice.EdgeLength / 1000;
            Vector dir = outsidePos - pos;
            float dirLength = dir.LengthEuclidean();
            float dirPercentage = 0.5f;
            float step = 0.25f;

            VectorRef outBary;
            int lastWorkingCell = -1;

            while (dirLength * step > eps)
            {
                int cell = lastWorkingCell;
                Vector samplePos = pos + dir * dirPercentage;
                if (cell < 0 || !ToBaryCoord(cell, samplePos).IsPositive())
                    cell = FindCell(samplePos, out outBary);
                lastWorkingCell = (cell >= 0) ? cell : lastWorkingCell;
                dirPercentage += (cell >= 0) ? step : -step;
                step *= 0.5f;
            }

            return pos + dir * dirPercentage;
        }
        #endregion DebugRendering
    }

    //struct Tet
    //{
    //    /// <summary>
    //    /// Indices referencing the vertices in the containing grid.
    //    /// </summary>
    //    public Index VertexIndices;

    //    /// <summary>
    //    /// Create a Tetraeder.
    //    /// </summary>
    //    /// <param name="verts">Vertex indices [4]</param>
    //    public Tet(Index verts)
    //    {
    //        Debug.Assert(verts.Length == 4, "Tetraeders have exactly 4 corners.");
    //        VertexIndices = verts;
    //    }
    //    public Vector ToBaryCoord(TetTreeGrid grid, Vector worldPos)
    //    {
    //        Matrix tet = new Matrix();
    //        for (int c = 0; c < 4; ++c)
    //        {
    //            tet.set_Columns(c, (Vector4)grid.Vertices[VertexIndices[c]]);
    //        }

    //        tet.Invert();
    //        Vector4 result = Vector4.Transform((Vector4)worldPos, tet);
    //        return new Vector(result);

    //    }
    //}
}
