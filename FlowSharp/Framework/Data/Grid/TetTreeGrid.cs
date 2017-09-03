﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX;
using static FlowSharp.Octree;
using System.Collections.Concurrent;

namespace FlowSharp
{
    class TetTreeGrid : FieldGrid, GeneralUnstructurdGrid
    {
        //public VectorList _vertices;
        public VectorData Vertices { get; set; }// { get { return _vertices; } set { _vertices = value as VectorList; } }

        public IndexArray Cells;
        private VectorBuffer _cellCenters;
        public int NumCells { get { return Cells.Length; } }

        public Octree Tree;
        public float CellSizeReference { get; protected set; }

        private TetTreeGrid(TetTreeGrid other)
        {
            Vertices = other.Vertices;
            Cells = other.Cells;
            _cellCenters = other._cellCenters;
            Tree = other.Tree;
            CellSizeReference = other.CellSizeReference;

            // For Dimensionality.
            Size = new Index(1, 3);
            Size[0] = Vertices.Length;

            // Space position.
            Origin = other.Origin ?? new Vector(0, 4);
            TimeDependant = other.TimeOrigin != null;
            Origin.T = other.TimeOrigin ?? Origin.T;
        }
        public TetTreeGrid(UnstructuredGeometry geom, Aneurysm.GeometryPart part, int maxNumVertices = 100, int maxLevel = 10, Vector origin = null, float? timeOrigin = null) : this(geom.Vertices, geom.Primitives, part, maxNumVertices, maxLevel, origin, timeOrigin) { }

        /// <summary>
        /// Create a new tetraeder grid descriptor.
        /// </summary>
        public TetTreeGrid(VectorData vertices, IndexArray indices, Aneurysm.GeometryPart part, int maxNumVertices = 10, int maxDepth = 10, Vector origin = null, float? timeOrigin = null)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();

            Vertices = vertices;
            Vertices.ExtractMinMax();

            // For Dimensionality.
            Size = new Index(1, 3);
            Size[0] = Vertices.Length;
            Cells = indices;


            Debug.Assert(vertices.Length > 0 && indices.Length > 0, "No data given.");
            Debug.Assert(indices.IndexLength == 4, "Not tets.");
            int dim = vertices[0].Length;

            // Space position.
            Origin = origin ?? new Vector(0, 4);
            TimeDependant = timeOrigin != null;
            Origin.T = timeOrigin ?? Origin.T;

            CellSizeReference = 0;

            _cellCenters = new VectorBuffer(Cells.Length, Vertices.VectorLength);
            for (int c = 0; c < Cells.Length; ++c)
            {
                _cellCenters[c] = (Vertices[Cells[c][0]] + Vertices[Cells[c][1]] + Vertices[Cells[c][2]] + Vertices[Cells[c][3]]) * 0.25f;
                CellSizeReference = Math.Max(CellSizeReference, (_cellCenters[c] - Vertices[Cells[c][0]]).LengthSquared());
            }

            CellSizeReference = (float)Math.Sqrt(CellSizeReference);

            _cellCenters.MinValue = new Vector(Vertices.MinValue);
            _cellCenters.MaxValue = new Vector(Vertices.MaxValue);

            Vector howOftenDoesCellSizeFitThere = _cellCenters.Extent / CellSizeReference * 0.5f;

            // Compute maximal level.
            // Setup Octree for fast access.
            Tree = Octree.LoadOrComputeWrite(_cellCenters, maxNumVertices, maxDepth, part, howOftenDoesCellSizeFitThere.Min());
            

            watch.Stop();
            Console.WriteLine("===== Grid buildup with {1} Levels of maximal {2} took \n========= {0}", watch.Elapsed, maxDepth, maxNumVertices);
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
        public override FieldGrid GetAsTimeGrid(int numTimeSlices, float timeStart)
        {
            //TetTreeGrid cpy = new TetTreeGrid(this);
            TetTreeGrid other = new TetTreeGrid(this);
            other.Size = new FlowSharp.Index(1, 4);
            other.Size[0] = Vertices.Length;
            other.Size.T = numTimeSlices;
            other.TimeOrigin = timeStart;
            return other;
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
            Vector4 weightsTmp;
            int cell = FindCell((Vector3)pos, out weightsTmp);
            weights = new Vector(weightsTmp);
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
        static uint NUM_SAMPLE_OUTSIDE_LEAF = 0;
        static uint NUM_SAMPLES_NO_NEW_NEIGHBORS = 0;
        static uint NUM_SLIGHTLY_NEGATIVE = 0;
        static uint NUM_OUTSIDE = 0;

        public static void ShowSampleStatistics()
        {
            Console.WriteLine("{0} \tsamples.\n{1}% \tsampled neighbor nodes.\n\t{2}% \tof those did not actually sample neighbors\n\t{3}% of them are slightly negative\n\t{4}% \t of them not found at all\n{5} outside of bounding box.\n===============",
                NUM_SAMPLES, ((float)NUM_SAMPLE_OUTSIDE_LEAF) / NUM_SAMPLES * 100, ((float)NUM_SAMPLES_NO_NEW_NEIGHBORS) / NUM_SAMPLE_OUTSIDE_LEAF * 100, ((float)NUM_SLIGHTLY_NEGATIVE) / NUM_SAMPLE_OUTSIDE_LEAF * 100, ((float)NUM_UNSUCCESSFULL_SAMPLES) / NUM_SAMPLE_OUTSIDE_LEAF * 100, NUM_OUTSIDE
                /*Octree.PROF_WATCH.Elapsed, PROF_BARY.Elapsed*/);
        }

        public static float MAX_NEGATIVE_BARY = 0.002f;

        private int FindCell(Vector3 pos, out Vector4 bary)
        {
            // Stab the tree.
            // Search through all cells that have a vertex in the stabbed leaf.
            Node leaf;
            bool worked = Tree.StabCell(pos, out leaf);
            NUM_SAMPLES++;

            bary = Vector4.Zero;
            //if (NUM_SAMPLES % 10 == 0)
            //    Console.WriteLine(NUM_SAMPLES);

            // Outside the octrees bounding box?
            if (!worked)
            {
                NUM_OUTSIDE++;
                return -1;
            }

            int tet = FindInNode(leaf.GetData(Tree), pos, out bary);

            if (tet >= 0)
                return tet;

            NUM_SAMPLE_OUTSIDE_LEAF++;

            // Test the neighbor cells in a maximal radius around the center.
            tet = Tree.FindNeighborNodes(this, pos, leaf, out bary);

            if (tet >= 0)
                return tet;

            //if (bestNegativeTet >= 0 || bestNegativeNeighborTet >= 0)
            //{
            //    NUM_SLIGHTLY_NEGATIVE++;
            //    if (neighborBary.AbsSumNegatives() < bary.AbsSumNegatives())
            //    {
            //        bary = neighborBary;
            //        Debug.Assert(bary.AbsSumNegatives() < MAX_NEGATIVE_BARY);
            //        return bestNegativeTet;
            //    }

            //    Debug.Assert(bary.AbsSumNegatives() < MAX_NEGATIVE_BARY);
            //    return bestNegativeNeighborTet;
            //}

            NUM_UNSUCCESSFULL_SAMPLES++;
            return -1;
        }

        public int FindInNode(CellData tets, Vector3 pos,/* float maxNegativeBary,*/ out Vector4 bary/*, out int negativeTet*/)
        {
            // Squared distance maximal 3 times as high as a random tet edge length.
            //            float distEps = CellSizeReference * CellSizeReference * 9;

            //Vector bestNegBary = null;
            //negativeTet = -1;
            //float bestBaryDist = 1;
            bary = Vector4.Zero;

            //Console.WriteLine($"Going through Indices [{tets.MinIdx}, {tets.MaxIdx}]");
            // Test whether inside.
            foreach (int tet in tets)
            {
                //if ((_cellCenters[tet] - pos).LengthSquared() > distEps)
                //    continue;
                //if (tet == 13630235)
                //{
                //    Console.WriteLine($"Tet {tet}: {Cells[tet]}");
                //    Console.WriteLine($"Sampling pos: {pos}");
                //    Console.WriteLine($"Matrix:\n\t{Vertices[Cells[tet][0]]}\n\t{Vertices[Cells[tet][1]]}\n\t{Vertices[Cells[tet][2]]}\n\t{Vertices[Cells[tet][3]]}");
                //}
                bool baryGood = ToBaryCoord(tet, pos, out bary);

                if (baryGood)
                {
                    return tet;
                }
                //float baryDiff = bary.AbsSumNegatives();

                //if (baryDiff < bestBaryDist)
                //{
                //    bestBaryDist = baryDiff;
                //    bestNegBary = new Vector(bary);
                //    negativeTet = tet;
                //}

            }

            //if (bestBaryDist < maxNegativeBary)
            //{
            //    bary = bestNegBary;
            //}
            return -1;
        }
        //private int FindInNode(List<CellData> data, Vector3 pos,/* float maxNegativeBary, */out VectorRef bary/*, out int negativeTet*/)
        //{
        //    // Squared distance maximal 3 times as high as a random tet edge length.
        //    float distEps = CellSizeReference * CellSizeReference * 9;
        //    bary = null;
        //    //bary = new Vector(-1, 4);
        //    //Vector bestNegBary = null;
        //    //negativeTet = -1;
        //    //float bestBaryDist = 1;

        //    // Test whether inside.
        //    foreach (CellData tets in data)
        //        foreach (int tet in tets)
        //        {
        //            if ((_cellCenters[tet] - pos).LengthSquared() > distEps)
        //                continue;

        //            bary = ToBaryCoord(tet, pos);
        //            if (bary.IsPositive())
        //            {
        //                return tet;
        //            }

        //            //float baryDiff = bary.AbsSumNegatives();

        //            //if (baryDiff < bestBaryDist)
        //            //{
        //            //    bestBaryDist = baryDiff;
        //            //    bestNegBary = new Vector(bary);
        //            //    negativeTet = tet;
        //            //}
        //        }

        //    //if (bestBaryDist < maxNegativeBary)
        //    //{
        //    //    bary = bestNegBary;
        //    //}
        //    return -1;
        //}

        public Vector OldToBaryCoord(int cell, VectorRef worldPos)
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
        //static Stopwatch PROF_BARY = new Stopwatch();
        public bool ToBaryCoord(int cell, Vector3 worldPos, out Vector4 bary)
        {
            //PROF_BARY.Start();
            Matrix tet = new Matrix();
            for (int i = 0; i < 4; ++i)
            {
                //tet[i] = VectorRef.ToUnsteady(Vertices[Cells[cell][i]]);
                tet.set_Columns(i, new Vector4(Vertices[Cells[cell][i]][0], Vertices[Cells[cell][i]][1], Vertices[Cells[cell][i]][2], 1));
            }

            //Console.WriteLine("===========\nTetrahedron matrix {0}", tet);
            bary = Vector4.Zero;
            float d0 = tet.Determinant();

            // Go over all corner points and exchange them with the sample position.
            // If sign of determinant is the same as of the original, cube, the point is on the same side.
            for (int i = 0; i < 4; ++i)
            {
                Matrix mi = tet;
                mi.set_Columns(i, new Vector4((Vector3)worldPos, 1));
                bary[i] = mi.Determinant() / d0;
                if (bary[i] <= 0)
                {
                    //PROF_BARY.Stop();
                    return false;
                }
            }
            float barySum = bary.Sum();
            //float eps = 0.01f;
            //Console.WriteLine("===========\nPosition {0}\nBarycentric Coordinate {1}", worldPos, bary);
            //PROF_BARY.Stop();
            return true;
        }


        /// <summary>
        /// Binary search for the last point inside the domain.
        /// </summary>
        /// <param name="pos">The last valid position on the inside.</param>
        /// <param name="outsidePos">First position found outside.</param>
        /// <returns></returns>
        public override Vector CutToBorder(VectorField field, VectorRef pos, VectorRef outsidePos)
        {
            float eps = CellSizeReference / 4;
            Vector dir = outsidePos - pos;
            float dirLength = dir.LengthEuclidean();
            float dirPercentage = 0.5f;
            float step = 0.25f;

            Vector4 bary;
            int lastWorkingCell = -1;

            while (dirLength * step > eps)
            {
                int cell = lastWorkingCell;
                Vector3 samplePos = (Vector3)(pos + dir * dirPercentage);

                if (cell < 0 || !ToBaryCoord(cell, samplePos, out bary))
                    cell = FindCell(samplePos, out bary);
                lastWorkingCell = (cell >= 0) ? cell : lastWorkingCell;
                dirPercentage += (cell >= 0) ? step : -step;
                step *= 0.5f;
            }

            return pos + dir * dirPercentage;
        }

        #region DebugRendering

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

        public PointSet<Point> SampleAll()
        {
            Point[] midpoints = new Point[Cells.Length];
            for (int p = 0; p < midpoints.Length; ++p)
            {
                Vector3 pos = Vector3.Zero;
                foreach (int i in Cells[p].Data)
                    pos += (Vector3)Vertices[i];
                midpoints[p] = new Point(pos / Cells.IndexLength);
            }

            return new PointSet<Point>(midpoints);
        }

        public IndexArray BorderGeometry()
        {
            // ========== Setup Vertex to Side List ========== \\
            // Assemble all sides at assigned vertices. Delete them later.
            List<Index>[] sidesPerVertex = new List<Index>[Vertices.Length];
            for (int v = 0; v < Vertices.Length; ++v)
                sidesPerVertex[v] = new List<Index>();

            for (int c = 0; c < Cells.Length; ++c)
            {

                if (c % (Cells.Length / 100) == 0)
                    Console.WriteLine("Buildup done for {0}% cells.", c / (Cells.Length / 100));
                // Save at all connected vertices.
                for (int s = 0; s < 4; ++s)
                {
                    // Remove s'ths index to get a side.
                    Index side = new Index(Cells[c]);
                    side[s] = side[3];
                    side = side.ToIntX(3);

                    // Sort indices to make comparable.
                    Array.Sort(side.Data);

                    // Add to all connected vertex side lists.
                    //for (int v = 0; v < 3; ++v)
                    sidesPerVertex[side[0]].Add(side);
                }
            }

            List<Index> remainingSides = new List<Index>();

            // ========== Remove All Sides that Appear Multiple Times ========== \\
            for (int v = 0; v < Vertices.Length; ++v)
            {
                var sides = sidesPerVertex[v].ToArray();
                Array.Sort(sides, Index.Compare);

                if (v % (Vertices.Length / 100) == 0)
                    Console.WriteLine("Finished sorting through {0}% triangles.\n\t{1} border triangles found.", v / (Vertices.Length / 100), remainingSides.Count);
                // sidesPerVertex[v] = sides.ToList();

                for (int s = sides.Length - 1; s >= 0; s--)
                {
                    if (s > 0 && sides[s] == sides[s - 1])
                    {
                        //sidesPerVertex[v].RemoveRange(s - 1, 2);
                        s--;
                    }
                    else
                    {
                        remainingSides.Add(sides[s]);

                        //bool exists = sidesPerVertex[sides[s][1]].Remove(sides[s]);
                        //Debug.Assert(exists);
                        //exists = sidesPerVertex[sides[s][2]].Remove(sides[s]);
                        //Debug.Assert(exists);
                    }


                    //// Not the first side that should have seen this side. Should be deleted then.
                    //if (side[0] != v)
                    //    continue;

                    //if (sidesPerVertex[side[1]].Remove(side))
                    //{
                    //    bool test = sidesPerVertex[side[2]].Remove(side);
                    //    Debug.Assert(test, "Side should be contained ")
                    //}
                }
            }

            return new IndexArray(remainingSides.ToArray(), 3);
        }

        private void SamplePosition(VectorField data, int posIdx, int samplesPerSide, ConcurrentBag<Point> verts)
        {
            Vector pos = Vertices.MinValue +
                            new Vector(new float[] {
                                       (float)(posIdx % samplesPerSide) / samplesPerSide * Tree.Extent[0],
                                       (float)((posIdx / samplesPerSide) % samplesPerSide) / samplesPerSide * Tree.Extent[1],
                                       (float)(posIdx / samplesPerSide / samplesPerSide) / samplesPerSide * Tree.Extent[2] });

            Vector sample = Sample(data, pos);
            if (sample != null)
            {
                //Console.WriteLine($"Successfull Position at {pos}");
                // Console.WriteLine($"Sample {posIdx} is inside");
                float color = (float)posIdx / (samplesPerSide * samplesPerSide * samplesPerSide); // ((sample - data.Data.MinValue) / (data.Data.MaxValue - data.Data.MinValue))[0];
                verts.Add( new Point((Vector3)pos) { Radius = 0.01f, Color = new Vector3(color) });
            }

            //if (posIdx % (samplesPerSide * samplesPerSide) == 0)
            //    Console.WriteLine("Sample {0}%", posIdx / (samplesPerSide * samplesPerSide * samplesPerSide));
        }

        public PointSet<Point> SampleTest(VectorField data, int vertsPerSide = 100)
        {
            data.Data.ExtractMinMax();

            Stopwatch watch = new Stopwatch();
            watch.Start();

            ConcurrentBag<Point> verts = new ConcurrentBag<Point>();
            Vector extent = Vertices.MaxValue - Vertices.MinValue;

            int numNotFound = 0;

            Parallel.For(0, vertsPerSide * vertsPerSide * vertsPerSide, s => { SamplePosition(data, s, vertsPerSide, verts); });
            //for (int s = 0; s < vertsPerSide * vertsPerSide * vertsPerSide; ++s)
            //    SamplePosition(data, s, vertsPerSide, verts);

            watch.Stop();
            Console.WriteLine("===== Grid stabbing with {0} successfull samples took\n========= {1}", verts.Count, watch.Elapsed);
            //ShowSampleStatistics();

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
        #endregion DebugRendering
    }
}
