using SlimDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class HexGrid : FieldGrid
    {
        public VectorBuffer Vertices;
        public IndexArray Indices;
        private const int NumCorners = 8;

        /// <summary>
        /// Create a new hexahedron grid descriptor.
        /// </summary>
        public HexGrid(VectorBuffer vertices, IndexArray indices, Vector origin = null, float? timeOrigin = null)
        {
            // For Dimensionality.
            Size = new Index(8);
            Vertices = vertices;
            Indices = indices;

            Debug.Assert(vertices.Length > 0 && indices.Length > 0, "No data given.");
            int dim = vertices[0].Length;
#if DEBUG
            foreach (Vector v in vertices)
            {
                Debug.Assert(v.Length == dim, "Varying vertex dimensions.");
            }
            foreach (Index i in indices)
            {
                Debug.Assert(i.Length == NumCorners, "Cells should have " + NumCorners + " corners each.");
                foreach (int idx in i.Data)
                    Debug.Assert(idx >= 0 && idx < vertices.Length, "Invalid index, out of vertex list bounds.");
            }
#endif

            Origin = origin ?? new Vector(0, 8);
            TimeDependant = timeOrigin != null;
            Origin.T = timeOrigin ?? Origin.T;
        }

        private void AppendTetrahedrons(Index hex, Index side, int neighborTet0, int neighborTet1)
        {
            Debug.Assert(hex.Length == 8, "Hexaedron should have 8 vertices.");
            Debug.Assert(side.Length == 4, "Hexaedron side should have 4 vertices.");


        }

        public override FieldGrid Copy()
        {
            throw new NotImplementedException("Grid is so big I don't want to copy it.");
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
        public override Index FindAdjacentIndices(VectorRef pos, out VectorRef weights)
        {
            int numPoints = NumAdjacentPoints();
            Index indices = new Index(numPoints);
            weights = new Vector(numPoints);


            return indices;
        }

        public override bool InGrid(Vector position)
        {
            Debug.Assert(position.Length == Size.Length, "Trying to access " + Size.Length + "D field with " + position.Length + "D index.");
            return false;
        }

        #region DebugRendering

        public Index[] GetCubes(int[] selection)
        {
            int numSides = 6;
            Index[] cubes = new Index[selection.Length * numSides];

            for (int s = 0; s < selection.Length; ++s)
            {
                Debug.Assert(selection[s] >= 0 && selection[s] < Indices.Length, "Selected index out of range.");
                Index idx = Indices[selection[s]];
                // Down/Up
                cubes[s * numSides + 0] = new Index(new int[] { idx[0], idx[1], idx[2], idx[3] });
                cubes[s * numSides + 1] = new Index(new int[] { idx[7], idx[6], idx[5], idx[4] });
                // Left/Right
                cubes[s * numSides + 2] = new Index(new int[] { idx[1], idx[5], idx[6], idx[2] });
                cubes[s * numSides + 3] = new Index(new int[] { idx[0], idx[3], idx[7], idx[4] });
                // Front/Back
                cubes[s * numSides + 4] = new Index(new int[] { idx[2], idx[6], idx[7], idx[3] });
                cubes[s * numSides + 5] = new Index(new int[] { idx[0], idx[4], idx[5], idx[1] });
            }

            return cubes;
        }

        public IndexArray GetCubes()
        {
            int numSides = 1;
            IndexArray cubes = new IndexArray(Indices.Length * numSides, 4);
            for (int i = 0; i < Indices.Length; ++i)
            {
                Index idx = Indices[i];

                // Down/Up
                cubes[i * numSides + 0] = new Index(new int[] { idx[0], idx[1], idx[2], idx[3] });
                //cubes[i * numSides + 1] = new Index(new int[] { idx[7], idx[6], idx[5], idx[4] });
                //// Left/Right
                //cubes[i * numSides + 2] = new Index(new int[] { idx[1], idx[5], idx[6], idx[2] });
                //cubes[i * numSides + 3] = new Index(new int[] { idx[0], idx[3], idx[7], idx[4] });
                //// Front/Back
                //cubes[i * numSides + 4] = new Index(new int[] { idx[2], idx[6], idx[7], idx[3] });
                //cubes[i * numSides + 5] = new Index(new int[] { idx[0], idx[4], idx[5], idx[1] });
            }

            return cubes;
        }

        public PointSet<Point> GetVertices()
        {
            Point[] verts = new Point[Vertices.Length];
            for (int p = 0; p < Vertices.Length; ++p)
            {
                verts[p] = new Point((Vector4)Vertices[p]) { Color = (Vector3)Vertices[p], Radius = 0.001f };
            }

            return new PointSet<Point>(verts);
        }

        public override Vector CutToBorder(VectorField field, VectorRef pos, VectorRef dir)
        {
            throw new NotImplementedException();
        }
        #endregion DebugRendering
    }
}
