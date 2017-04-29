using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX;

namespace FlowSharp
{
    class TetGrid : FieldGrid
    {
        public Vector[] Vertices;
        //public Index[] Indices;
        public Tet[] Cells;
        private const int NumCorners = 4;

        /// <summary>
        /// Create a new tetraeder grid descriptor.
        /// </summary>
        public TetGrid(Vector[] vertices, Index[] indices, Vector origin = null, float? timeOrigin = null)
        {
            // For Dimensionality.
            Size = new Index(4);
            Cells = new Tet[indices.Length * 4];
            Vertices = vertices;

            Debug.Assert(vertices.Length > 0 && indices.Length > 0, "No data given.");
            int dim = vertices[0].Length;
#if false//DEBUG
            foreach (Vector v in vertices)
            {
                Debug.Assert(v.Length == dim, "Varying vertex dimensions.");
            }
            foreach(Index i in indices)
            {
                Debug.Assert(i.Length == NumCorners, "Cells should have " + NumCorners + " corners each.");
                foreach (int idx in i.Data)
                    Debug.Assert(idx >= 0 && idx < vertices.Length, "Invalid index, out of vertex list bounds.");
            }
#endif

            Origin = origin ?? new Vector(0, dim);
            TimeDependant = timeOrigin != null;
            Origin.T = timeOrigin ?? Origin.T;

            BuildGrid(indices);
        }

        private void BuildGrid(Index[] indices)
        {
            List<int>[] tetsPerVertex = new List<int>[Cells.Length];

            for (int c = 0; c < Cells.Length; ++c)
            {

            }
            //TODO
        }

        public override FieldGrid Copy()
        {
            throw new NotImplementedException("Grid is so big I don't want to copy it.");
            return this;
            //var cpy = tetraGrid.DeepCopyData();
            //Vertices = cpy.Item1;
            //Indices = cpy.Item2;
            //Origin = tetraGrid.Origin;
            //TimeOrigin = tetraGrid.TimeOrigin;
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

        //private Tuple<Vector[],Tet[]> DeepCopyData()
        //{
        //    // Deep copy of index list.
        //    Index[] iCpy = new Index[Cells.Length];
        //    for (int i = 0; i < Cells.Length; ++i)
        //        iCpy[i] = new Tet(Cells[i]);

        //    // Deep copy of vertex list.
        //    Vector[] vCpy = new Vector[Indices.Length];
        //    for (int v = 0; v < Vertices.Length; ++v)
        //        vCpy[v] = new Vector(Vertices[v]);

        //    return new Tuple<Vector[], Index[]>(vCpy, iCpy);
        //}

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
        /// Indices referencing the neighboring tetraeders in the containing grid. Side across from the respective corner. -1 if outside.
        /// </summary>
        public Index NeighborIndices;

        /// <summary>
        /// Create a Tetraeder with given neighborhood.
        /// </summary>
        /// <param name="verts">Vertex indices [4]</param>
        /// <param name="neighs">Neighbor indives [4] or null. If null, supply this information later.</param>
        public Tet(Index verts, Index neighs = null)
        {
            Debug.Assert(verts.Length == 4, "Tetraeders have exactly 4 corners.");
            Debug.Assert(neighs == null || neighs.Length == 4, "Tetraeders have exactly 4 neighbors. -1 if invalid.");
            VertexIndices = verts;
            NeighborIndices = neighs ?? new Index(-1,4);
        }

        public Vector ToBaryCoord(TetGrid grid, Vector worldPos)
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
