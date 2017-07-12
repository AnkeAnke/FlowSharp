using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX;

namespace FlowSharp
{
    class TetTreeGrid : FieldGrid, GeneralUnstructurdGrid
    {
        public Vector[] Vertices { get; set; }

        public Tet[] Cells;
        private const int NumCorners = 4;
        public int NumCells { get { return Cells.Length; } }
        /// <summary>
        /// Assemble all inidices to a buffer. Do this here for general Tet grids.
        /// </summary>
        /// <returns></returns>
        public Index[] AssembleIndexList()
        {
            //Index[] cells = new Index[Cells.Length];
            //for (int c = 0; c < Cells.Length; ++c)
            //        cells[c] = Cells[c].VertexIndices;
            //return cells;
            Index[] tris = new Index[Cells.Length * 4];
            for (int c = 0; c < Cells.Length; c++)
            {
                for (int s = 0; s < 4; ++s)
                {
                    tris[c * 4 + s] = new Index(3);
                    int count = 0;
                    for (int i = 0; i < 4; ++i)
                        if (i != s)
                            tris[c * 4 + s][count++] = Cells[c].VertexIndices[i];
                }
            }

            return tris;
        }

        /// <summary>
        /// Create a new tetraeder grid descriptor.
        /// </summary>
        public TetTreeGrid(Vector[] vertices, Tet[] indices, Vector origin = null, float? timeOrigin = null)
        {
            // For Dimensionality.
            Size = new Index(4);
            Cells = indices;
            //            Cells = new Tet[indices.Length * 5];
            Vertices = vertices;

            Debug.Assert(vertices.Length > 0 && indices.Length > 0, "No data given.");
            int dim = vertices[0].Length;
#if DEBUG
            foreach (Vector v in vertices)
            {
                Debug.Assert(v.Length == dim, "Varying vertex dimensions.");
            }
            foreach (Tet i in indices)
            {
                Debug.Assert(i.VertexIndices.Length == NumCorners, "Cells should have " + NumCorners + " corners each.");
                foreach (int idx in i.VertexIndices.Data)
                    Debug.Assert(idx >= 0 && idx < vertices.Length, "Invalid index, out of vertex list bounds.");
            }
#endif

            Origin = origin ?? new Vector(0, 4);
            TimeDependant = timeOrigin != null;
            Origin.T = timeOrigin ?? Origin.T;
        }

        public Index[] GetAllSides()
        {
            List<Index> sides = new List<Index>(Cells.Length);

            for (int c = 0; c < Cells.Length; ++c)
            {
                // Dirty quickfix: Duplicate the first cells multiple times, so we don't need to deal with uninitialized tets.
                Index verts = Cells[c].VertexIndices;
                if (verts == null)
                    continue;
                sides.Add( new Index( new int[] { verts[0], verts[1], verts[2] } ) );
                sides.Add( new Index( new int[] { verts[0], verts[2], verts[3] } ) );
                sides.Add( new Index( new int[] { verts[0], verts[1], verts[3] } ) );
                sides.Add( new Index( new int[] { verts[1], verts[2], verts[3] } ) );
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
        public override int[] FindAdjacentIndices(Vector pos, out float[] weights)
        {
            int numPoints = NumAdjacentPoints();
            int[] indices = new int[numPoints];
            weights = new float[numPoints];


            return indices;
        }

        public override bool InGrid(Vector position)
        {
            Debug.Assert(position.Length == Size.Length, "Trying to access " + Size.Length + "D field with " + position.Length + "D index.");
            return false;
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

        public PointSet<Point> GetVertices()
        {
            Point[] verts = new Point[Vertices.Length];
            for (int p = 0; p < Vertices.Length; ++p)
            {
                verts[p] = new Point((Vector3)Vertices[p]) { Color = (Vector3)Vertices[p], Radius = 0.001f };
            }

            return new PointSet<Point>(verts);
        }

        //public PointSet<Point> GetCellCenters()
        //{
        //    Point[] verts = new Point[Indices.Length];
        //    for (int p = 0; p < Indices.Length; ++p)
        //    {
        //        Vector center = new FlowSharp.Vector(NumCorners);
        //        for (int i = 0; i < NumCorners; ++i)
        //            center += Vertices[Indices[p][i]];

        //        verts[p] = new Point((Vector3)center) { Color = Vector3.UnitX };
        //    }

        //    return new PointSet<Point>(verts);
        //}
        #endregion DebugRendering
    }

    struct Tet
    {
        /// <summary>
        /// Indices referencing the vertices in the containing grid.
        /// </summary>
        public Index VertexIndices;

        /// <summary>
        /// Create a Tetraeder.
        /// </summary>
        /// <param name="verts">Vertex indices [4]</param>
        public Tet(Index verts)
        {
            Debug.Assert(verts.Length == 4, "Tetraeders have exactly 4 corners.");
            VertexIndices = verts;
        }
        public Vector ToBaryCoord(TetTreeGrid grid, Vector worldPos)
        {
            Matrix tet = new Matrix();
            for (int c = 0; c < 4; ++c)
            {
                tet.set_Columns(c, (Vector4)grid.Vertices[VertexIndices[c]]);
            }

            tet.Invert();
            Vector4 result = Vector4.Transform((Vector4)worldPos, tet);
            return new Vector(result);

        }
    }
}
