using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX;

namespace FlowSharp
{
    class TetNeighborGrid : FieldGrid, GeneralUnstructurdGrid
    {
        public Vector[] Vertices { get; set; }
        //public Index[] Indices;
        public TetNeighbor[] Cells;
        private const int NumCorners = 4;
        public int NumCells { get { return Cells.Length; } }
        /// <summary>
        /// Assemble all inidces to a buffer. Do this here for general Tet grids.
        /// </summary>
        /// <returns></returns>
        public Index[] AssembleIndexList()
        {
            Index[] cells = new Index[Cells.Length];
            for (int c = 0; c < Cells.Length; ++c)
                cells[c] = Cells[c].VertexIndices;
            return cells;
        }

        /// <summary>
        /// Create a new tetraeder grid descriptor.
        /// </summary>
        public TetNeighborGrid(Vector[] vertices, TetNeighbor[] indices, Vector origin = null, float? timeOrigin = null)
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
            foreach (TetNeighbor i in indices)
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

        public Index[] GetOutsides()
        {
            List<Index> sides = new List<Index>(Cells.Length / 1000);

            foreach (TetNeighbor tet in Cells)
            {
                if (tet.NeighborIndices == null)
                    continue;
                //Debug.Assert(tet.VertexIndices != null, "Should not contain invalid tetrahedrons.");
                for (int s = 0; s < 4; ++s)
                    if (tet.NeighborIndices[s] < 0)
                    {
                        sides.Add(new Index(3));
                        int idx = 0;
                        for (int i = 0; i < 4; ++i)
                            if (i != s)
                                sides[sides.Count-1][idx++] = tet.VertexIndices[i];
                    }
            }

            return sides.ToArray();
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

        #region FromHexGrid

        private TetNeighborGrid(Vector origin = null, float? timeOrigin = null)
        {
            // For Dimensionality.
            Size = new Index(4);

            Origin = origin ?? new Vector(0, 4);
            TimeDependant = timeOrigin != null;
            Origin.T = timeOrigin ?? Origin.T;
        }

        /// <summary>
        /// Split the hexahedron into 5 terahedrons. Save neighborhoods as far as known.
        /// </summary>
        /// <param name="idx">Index of the original hexahedron.</param>
        /// <param name="cuboid">Vertex indices of hexahedron.</param>
        /// <param name="split0257">Is the middle tetrahedron connecting indices 0-2-5-7 (or 1-3-4-6)?</param>
        private void SplitHexAndAppend(int idx, ref Index cuboid, bool split0257 = true)
        {
            Debug.Assert(cuboid.Length == 8);
            if (!split0257)
            {
                cuboid = new Index(new int[] { cuboid[1], cuboid[2], cuboid[3], cuboid[0], cuboid[5], cuboid[6], cuboid[7], cuboid[4] });
            }

            Cells[idx * 5] = new TetNeighbor(
                new FlowSharp.Index(new int[] { cuboid[0], cuboid[2], cuboid[5], cuboid[7] }),
                new Index(new int[] { idx * 5 + 1, idx * 5 + 2, idx * 5 + 3, idx * 5 + 4 }));

            Cells[idx * 5 + 1] = new TetNeighbor(
                new FlowSharp.Index(new int[] { cuboid[1], cuboid[5], cuboid[0], cuboid[2] }),
                new Index(new int[] { idx * 5, -1, -1, -1 }));

            Cells[idx * 5 + 2] = new TetNeighbor(
                new FlowSharp.Index(new int[] { cuboid[3], cuboid[0], cuboid[7], cuboid[2] }),
                new Index(new int[] { idx * 5, -1, -1, -1 }));

            Cells[idx * 5 + 3] = new TetNeighbor(
                new FlowSharp.Index(new int[] { cuboid[4], cuboid[0], cuboid[7], cuboid[5] }),
                new Index(new int[] { idx * 5, -1, -1, -1 }));

            Cells[idx * 5 + 4] = new TetNeighbor(
                new FlowSharp.Index(new int[] { cuboid[6], cuboid[2], cuboid[7], cuboid[5] }),
                new Index(new int[] { idx * 5, -1, -1, -1 }));
        }

        /// <summary>
        /// Take existing terahedrons and assign neighborhood indices via shared side.
        /// </summary>
        /// <param name="hexIndex">Index of the original hexahedron.</param>
        /// <param name="side">Indices of the shared side. 1-2 is the split across the </param>
        /// <param name="neighborTet0">Index of the tetrahedron at that side, 0-1-2.</param>
        /// <param name="neighborTet1">Index of the tetrahedron at that side, 3-2-1.</param>
        private void GenerateNeighborhood(int hexIndex, Index side, int neighborTet0, int neighborTet1)
        {
            // 2---3
            // | \ |
            // 0---1
            // Side vertices are aligned like this.

            // Go through the 4 outer tetrahedrons - the central ones neighborhood is clear.
            int numTetsAtThisSide = 0;
            for (int tet = 1; tet < 5; ++tet)
            {
                TetNeighbor cell = Cells[hexIndex * 5 + tet];
                int notContained = -1;
                for (int vert = 1; vert < 4; ++vert)
                {
                    // We are looking for a tet with exactly one corner not contained in the side.
                    // This is also the index we will write the neighborhood to, as it is opposite the side.
                    if (!side.Contains(cell.VertexIndices[vert]))
                    {
                        if (notContained >= 0)
                        {
                            notContained = -42;
                            break;
                        }
                        else
                            notContained = vert;
                    }
                }

                // Exactly one corner not at side?
                if (notContained >= 0)
                {
                    Debug.Assert(cell.NeighborIndices[notContained] < 0, "This side should not have a neighbor yet.");
                    numTetsAtThisSide++;

                    Debug.Assert(cell.VertexIndices.Contains(side[1]) && cell.VertexIndices.Contains(side[2]), "Side diagonal has to be contained. OR are there caases when this cannot work?");

                    // Sharing a side with the first triangle 0-1-2.
                    if (!cell.VertexIndices.Contains(side[0]))
                    {
                        Debug.Assert(cell.VertexIndices.Contains(side[3]), "Has to contain exactly one of the outer corner vertices.");
                        Cells[hexIndex * 5 + tet].NeighborIndices[notContained] = neighborTet0;
                    }
                    // Sharing a side with the second triangle 3-2-1.
                    else
                    {
                        Debug.Assert(cell.VertexIndices.Contains(side[0]) && !cell.VertexIndices.Contains(side[3]), "Has to contain exactly one of the outer corner vertices.");
                        Cells[hexIndex * 5 + tet].NeighborIndices[notContained] = neighborTet1;
                    }

                    int neighborTet = Cells[hexIndex * 5 + tet].NeighborIndices[notContained];
                    // Doing the same for the now referenced side is easy, so do it.
                    for (int s = 1; s < 4; ++s)
                    {
                        if (!side.Contains(Cells[neighborTet].VertexIndices[s]))
                        {
                            Debug.Assert(Cells[neighborTet].NeighborIndices[s] < 0, "Should not have a neighbor yet.");
                            Cells[neighborTet].NeighborIndices[s] = hexIndex * 5 + tet;
                            break;
                        }
                    }
                }
            }

            Debug.Assert(numTetsAtThisSide == 2 || numTetsAtThisSide == 0, "Should have found exactly 2 matching tetrahedrons or be at the outside.");
        }

        private void AppendTetrahedrons(int hexIndex, ref Index hex, Index side, int neighborTet0, int neighborTet1)
        {
            // 2---3
            // | \ |
            // 0---1
            // Side vertices are aligned like this.
            Debug.Assert(hex.Length == 8, "Hexaedron should have 8 vertices.");
            Debug.Assert(side.Length == 4, "Hexaedron side should have 4 vertices.");

            // Split how?
            bool split0257 = (hex[0] == side[1] || hex[2] == side[1] || hex[5] == side[1] || hex[7] == side[1]);

            // Split.
            SplitHexAndAppend(hexIndex, ref hex, split0257);

            // Extract neighborhood.
            GenerateNeighborhood(hexIndex, side, neighborTet0, neighborTet1);
        }

        private void AppendSides(int hexIndex, ref Index hex, List<Tuple<Index, int, int, int>> sideStack)
        {
            //-{-cuboid[0], cuboid[2], cuboid[5], cuboid[7]-}-
            // { cuboid[0], cuboid[2], cuboid 1 , cuboid[5] } +1
            // { cuboid[0], cuboid[7], cuboid[2], cuboid 3  } +2
            // { cuboid[0], cuboid[7], cuboid[5], cuboid 4  } +3
            // { cuboid[2], cuboid[7], cuboid[5], cuboid 6  } +4
            Debug.Assert(hex[0] == Cells[hexIndex * 5].VertexIndices[0], "Hex and tets are not mapping as expected.");
            sideStack.Add(new Tuple<Index, int, int, int>( new FlowSharp.Index(new int[] { hex[1], hex[2], hex[0], hex[3] } ), hexIndex * 5 + 1, hexIndex * 5 + 2, hexIndex) );
            sideStack.Add(new Tuple<Index, int, int, int>( new FlowSharp.Index(new int[] { hex[1], hex[0], hex[5], hex[4] } ), hexIndex * 5 + 1, hexIndex * 5 + 3, hexIndex) );
            sideStack.Add(new Tuple<Index, int, int, int>( new FlowSharp.Index(new int[] { hex[1], hex[5], hex[2], hex[6] } ), hexIndex * 5 + 1, hexIndex * 5 + 4, hexIndex) );

            sideStack.Add(new Tuple<Index, int, int, int>( new FlowSharp.Index(new int[] { hex[3], hex[7], hex[0], hex[4] } ), hexIndex * 5 + 2, hexIndex * 5 + 3, hexIndex) );
            sideStack.Add(new Tuple<Index, int, int, int>( new FlowSharp.Index(new int[] { hex[3], hex[2], hex[7], hex[6] } ), hexIndex * 5 + 2, hexIndex * 5 + 4, hexIndex) );
            sideStack.Add(new Tuple<Index, int, int, int>( new FlowSharp.Index(new int[] { hex[4], hex[7], hex[5], hex[6] } ), hexIndex * 5 + 3, hexIndex * 5 + 4, hexIndex) );
        }

        public static TetNeighborGrid BuildFromHexGrid(Vector[] vertices, Index[] hexIndices, Vector origin = null, float? timeOrigin = null)
        {
            Console.WriteLine("Converting HexGrid to TetGrid");

            TetNeighborGrid grid = new TetNeighborGrid(origin, timeOrigin);
            grid.Cells = new TetNeighbor[hexIndices.Length * 5];
            grid.Vertices = vertices;

            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            // Link from vertices to cells.
            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            List<int>[] hexsPerVertex = new List<int>[grid.Vertices.Length];
            for (int h = 0; h < hexsPerVertex.Length; ++h)
                hexsPerVertex[h] = new List<int>();

            // For each hexaeder, store its index at all corner vertices.
            for (int hex = 0; hex < hexIndices.Length; ++hex)
                foreach (int idx in hexIndices[hex].Data)
                    hexsPerVertex[idx].Add(hex);

            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            // Collapse close vertices.
            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            //int[] indicesXsorted = new int[vertices.Length];
            //for (int i = 0; i < indicesXsorted.Length; ++i)
            //    indicesXsorted[i] = i;
            //// Sort 
            //Array.Sort(indicesXsorted, (x, y) => { return vertices[x][0] > vertices[y][0] ? 1 : vertices[x][0] == vertices[y][0]? 0 : -1; });
            //float smallDist = (vertices[0] - vertices[1]).LengthSquared() * 0.01f;
            //for (int i = 0; i < indicesXsorted.Length - 1; ++i)
            //{
            //    // Very close? Collapse. Always assign to higher index (to enable chains).
            //    float vertDist = (vertices[indicesXsorted[i + 1]] - vertices[indicesXsorted[i]]).LengthSquared();
            //    if (vertDist < smallDist)
            //    {
            //        foreach (int cellIdx in hexsPerVertex[indicesXsorted[i]])
            //            for (int c = 0; c < 8; ++c)
            //                if (hexIndices[cellIdx][c] == indicesXsorted[i])
            //                    hexIndices[cellIdx][c] = indicesXsorted[i + 1];
            //        hexsPerVertex[indicesXsorted[i + 1]].AddRange(hexsPerVertex[indicesXsorted[i]]);
            //        hexsPerVertex[indicesXsorted[i]].Clear();
            //    }
            //}
            //indicesXsorted = null; // Make memory releaseable if needed.

            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            // Begin at one hexader. Split into tetraeders.
            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            grid.SplitHexAndAppend(0, ref hexIndices[0], true);

            // Save the sides that need to be connected here.
            List<Tuple<Index, int, int, int>> sideStack = new List<Tuple<Index, int, int, int>>(hexIndices.Length);
            grid.AppendSides(0, ref hexIndices[0], sideStack);

            int numHexesSplit = 1;


            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            // Continue as long as hexs remain.
            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            while (sideStack.Count > 0)
            {
                // Pop one side out.
                var side = sideStack.Last();
                sideStack.RemoveAt(sideStack.Count - 1);

                Index sideVerts = side.Item1;
                int neighbor = -1;

                // Neighboring hex needs to share every vertex.
                // Test all hexahedrons in the first vertices list.
                foreach (int hex in hexsPerVertex[sideVerts[0]])
                {
                    if (hexsPerVertex[sideVerts[1]].Contains(hex) && hexsPerVertex[sideVerts[2]].Contains(hex) && hexsPerVertex[sideVerts[3]].Contains(hex) && hex != side.Item4)
                    {
                        // Found the neighbor.
                        neighbor = hex;
                        break;
                    }
                }
                //if (neighbor == -1 && hexsPerVertex[sideVerts[0]].Count > 4)
                //    Console.WriteLine("Banana");
                // Split and integrate the hex.
                if (neighbor >= 0 && grid.Cells[neighbor * 5].VertexIndices == null)
                {
                    grid.AppendTetrahedrons(neighbor, ref hexIndices[neighbor], sideVerts, side.Item2, side.Item3);
                    grid.AppendSides(neighbor, ref hexIndices[neighbor], sideStack);
                    numHexesSplit++;
                    if (numHexesSplit % (hexIndices.Length / 10) == 0)
                        Console.WriteLine(" - " + (numHexesSplit * 100) / (hexIndices.Length) + "% Converted.");
                }
            }

            // Finally, return result.
            return grid;
        }

#endregion FromHexGrid

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

    struct TetNeighbor
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
        public TetNeighbor(Index verts, Index neighs = null)
        {
            Debug.Assert(verts.Length == 4, "Tetraeders have exactly 4 corners.");
            Debug.Assert(neighs == null || neighs.Length == 4, "Tetraeders have exactly 4 neighbors. -1 if invalid.");
            VertexIndices = verts;
            NeighborIndices = neighs ?? new Index(-1,4);
        }

        //public void SetCoord(TetGrid grid)
        //{
        //    for (int col = 0; col < 4; ++col)
        //    {
        //        Vector pos = grid.Vertices[VertexIndices[col]];
        //        for (int row = 0; row < 3; ++row)
        //            CoordTransform[row, col] = pos[row];
        //        SetCoord
        //    }
        //    CoordTransform.Invert();
        //}

        public Vector ToBaryCoord(TetNeighborGrid grid, Vector worldPos)
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
