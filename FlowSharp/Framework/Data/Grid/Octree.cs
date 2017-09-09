using SlimDX;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class Octree
    {
        public Vector3 Minimum { get { return (Vector3)Vertices.MinValue; } }
        public Vector3 Maximum { get { return (Vector3)Vertices.MaxValue; } }
        public Vector3 Extent { get { return (Vector3)Vertices.Extent; } }
        public int VectorLength { get { return 3; } }
        private Node _root { get { return _nodes[0]; } }
        private List<Node> _nodes;
        public CellData AllCells { get { return _root.GetData(this); } }

        public VectorData Vertices;
        private int[] _vertexPermutation;
        public Vector3 MaxLeafSize { get { return (Vector3)Vertices.Extent / (1 << _maxDepth); } }
        public float MaxCellDistance { get; private set; }
        private int _maxDepth, _maxVerts;
        private int _gridSize { get { return 1 << _maxDepth; } }
        


        public bool OUTPUT_DEBUG = false;


        public Vector3 ToGridPosition(Vector3 pos)
        {
            return (pos - Minimum).Divide(Maximum - Minimum) * (1 << _maxDepth);
        }

        public Vector3 ToWorldPosition(Vector3 pos)
        {
            return pos.Multiply(MaxLeafSize) + Minimum;
        }

        public Octree(VectorData data, int maxVertices, int maxDepth, float maxCellDistance)
        {
            if (maxDepth < 1)
                throw new NotImplementedException("Nope. Sorry.");

            _maxDepth = maxDepth;
            _maxVerts = maxVertices;
            MaxCellDistance = maxCellDistance;

            Vertices = data;
            _vertexPermutation = Enumerable.Range(0, Vertices.Length).ToArray();

            Vertices.ExtractMinMax();
            _nodes = new List<Node>(20000);
            _nodes.Add(new Node(0, 0, _vertexPermutation.Length, new Vector3(0), new Vector3(_gridSize)));


            Stopwatch watch = new Stopwatch();
            watch.Start();

            Split(maxVertices);

            float maxRadius = (1 << _maxDepth) / maxCellDistance;
            Console.WriteLine("Max {0} Vertices per Cell\nMax {1} Levels deep (Restrained to {3})\nMax {2} Cells from Cell Center", maxVertices, _maxDepth, maxRadius, maxDepth);

            watch.Stop();
            Console.WriteLine($"Octree buildup took {watch.Elapsed}");
        }

        private void Split(int maxVertices)
        {
            _root.SplitRecursively(this, maxVertices, _maxDepth);
        }

        #region FileReadWrite
        public static Octree ReadOctree(int maxVerts, int maxDepth, Aneurysm.GeometryPart part, float maxCellDistance)
        {
            string filename = Aneurysm.Singleton.OctreeFilename(maxVerts, maxDepth, part);
            Console.WriteLine(filename);
            if (!File.Exists(@filename))
                return null;
            var tree =  new Octree(filename);
            tree._maxDepth = maxDepth;
            tree._maxVerts = maxVerts;
            tree.MaxCellDistance = maxCellDistance;
            return tree;
        }

        public static Octree LoadOrComputeWrite(VectorData data, int maxNumVertices, int maxDepth, Aneurysm.GeometryPart part, float maxdist, string customPraefix = "")
        {
            Octree tree = ReadOctree(maxNumVertices, maxDepth, part, maxdist);
            if (tree == null)
            {
                tree = new Octree(data, maxNumVertices, maxDepth, maxdist);
                tree.WriteToFile(Aneurysm.Singleton.OctreeFilename(maxNumVertices, maxDepth, part));
            }
            else
            {
                tree.Vertices = data;
                tree.Vertices.ExtractMinMax();
            }

            return tree;
        }

        private Octree(string filename)
        { 
            using (FileStream fs = File.Open(@filename, FileMode.Open))
            {
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    // Read Permutations.
                    int numPerms = reader.ReadInt32();
                    _vertexPermutation = new int[numPerms];
                    byte[] perms = reader.ReadBytes(numPerms * sizeof(int));
                    Buffer.BlockCopy(perms, 0, _vertexPermutation, 0, numPerms * sizeof(int));

                    // Load Nodes.
                    int numNodes = reader.ReadInt32();
                    _nodes = new List<Node>(numNodes);
                    for (int n = 0; n < numNodes; ++n)
                        _nodes.Add(new Node());

                    // Read nodes iteratively.
                    for (int n = 0; n < numNodes; ++n)
                    {
                        // Reading children. Reading 8 (-1) means Children == null.
                        for (int c = 0; c < 8; ++c)
                        {
                            _nodes[n].Children[c] = reader.ReadInt32();
                            //Console.WriteLine("Child " + node.Children[c]);
                        }
                        if (_nodes[n].Children[0] == -1)
                            _nodes[n].Children = null;

                        // Write int values.
                        _nodes[n].MinIdx = reader.ReadInt32();
                        _nodes[n].MaxIdx = reader.ReadInt32();
                        _nodes[n].Level = reader.ReadInt32();

                        // Read vectors component-wise.
                        //node.MinPos = Vector3.Zero;
                        for (int m = 0; m < 3; ++m)
                            _nodes[n].MinPos[m] = reader.ReadSingle();

                       // node.MaxPos = Vector3.Zero;
                        for (int m = 0; m < 3; ++m)
                            _nodes[n].MaxPos[m] = reader.ReadSingle();

                        _nodes[n].MidPos = (_nodes[n].MinPos + _nodes[n].MaxPos) * 0.5f;
                    }
                }
            }
        }

        public void WriteToFile(string filename)
        {
            using (FileStream fs = File.Open(@filename, FileMode.Create))
            {
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    // Write Permutations.
                    writer.Write(_vertexPermutation.Length);
                    byte[] perms = new byte[_vertexPermutation.Length * sizeof(int)];
                    Buffer.BlockCopy(_vertexPermutation, 0, perms, 0, _vertexPermutation.Length * sizeof(int));
                    writer.Write(perms);

                    // Write Nodes.
                    writer.Write(_nodes.Count);
                    foreach (Node node in _nodes)
                    {
                        // Writing children. Write 8 (-1) so each block is the same length.
                        for (int c = 0; c < 8; ++c)
                            writer.Write(node.Children?[c] ?? -1);

                        // Write int values.
                        writer.Write(node.MinIdx);
                        writer.Write(node.MaxIdx);
                        writer.Write(node.Level);

                        // Write vectors component-wise.
                        writer.Write(node.MinPos.X);
                        writer.Write(node.MinPos.Y);
                        writer.Write(node.MinPos.Z);

                        writer.Write(node.MaxPos.X);
                        writer.Write(node.MaxPos.Y);
                        writer.Write(node.MaxPos.Z);
                    }
                }
            }
        }
        #endregion FileReadWrite

        /// <summary>
        /// Stab the octree. Returns the lowest node containing the position. Maximal level can be set.
        /// </summary>
        /// <param name="pos">Sample position.</param>
        /// <param name="leafNode">Output node.</param>
        /// <param name="maxLevel">Maximal traversal depth. Negative means no condition.</param>
        /// <returns>Node containing the position. Null if outside of octree bounding box.</returns>
        public bool StabCell(Vector3 pos, out Node leafNode)
        {
            return StabCellGridPos(ToGridPosition(pos), out leafNode);
        }

        // public static Stopwatch PROF_WATCH = new Stopwatch();
        /// <summary>
        /// Stab the octree. Returns the lowest node containing the position. Maximal level can be set.
        /// </summary>
        /// <param name="pos">Sample position.</param>
        /// <param name="leafNode">Output node.</param>
        /// <param name="maxLevel">Maximal traversal depth. Negative means no condition.</param>
        /// <returns>Node containing the position. Null if outside of octree bounding box.</returns>
        private bool StabCellGridPos(Vector3 gridPos, out Node leafNode)
        {
            //Console.WriteLine($"= Position {gridPos} =");
            //cellExtent = null;
            if (!(gridPos.IsLess(_gridSize)) || !gridPos.IsPositive())
            {
                leafNode = null;
                return false;
            }
            
            leafNode = _root;
            //PROF_WATCH.Start();
            //leafNode = _root.Stab(this, gridPos);
            //PROF_WATCH.Stop();
            for (int l = 0; l <= _maxDepth; ++l)
            {
                //Console.WriteLine(leafNode);

                if (leafNode.IsLeaf)
                    return true;

                int comp = Vector3Extensions.CompareForIndex(gridPos, leafNode.MidPos);
                //if (l != 0 || comp != 0)
                //{
                //    Console.WriteLine("MidPos " + leafNode.MidPos);
                //    Console.WriteLine("Children[0] {0}", leafNode.Children?[0] ?? -42);
                //    Console.WriteLine("Going to Children[{0}] = {1}", comp, leafNode.Children[comp]);
                //}
                //Console.WriteLine($"=== Going to {leafNode.Children[comp]} ===");
                leafNode =  _nodes[leafNode.Children[comp]];
            }

            return false;
        }

        public int FindNeighborNodes(TetTreeGrid grid, Vector3 pos, Node leaf, out Vector4 bary)
        {
            List<CellData> neighbors = new List<CellData>(6);

            Vector3 gridPos = ToGridPosition(pos);
           
            Node range;

            // How many cells can we go maximally before there is definitely nothing there.
            // 1.73... = sqrt(3)
            int maxCellSum = (int)Math.Ceiling(MaxCellDistance * 1.732050807568877);
            float maxEuclideanDistSquared = MaxCellDistance * MaxCellDistance;

            // From close to inner cell (1), we move outwards.
            for (int dist = 1; dist <= maxCellSum; ++dist)
            {
                for (int x = 0; x <= dist; ++x)
                    for (int y = 0; y <= dist - x; ++y)
                    {
                        int z = dist - x - y;
                        Index offset = new Index(new int[] { x, y, dist - x - y });

                        // As we grow in a <> shape, we can discard some cells early.
                        if (offset.LengthSquared() > maxEuclideanDistSquared)
                            continue;

                        // Test in both directions.
                        for (int signX = -1; signX <= 1; signX += 2)
                            for (int signY = -1; signY <= 1; signY += 2)
                                for (int signZ = -1; signZ <= 1; signZ += 2)
                                {
                                    // Check: is it redundant to stab again?
                                    Index sign = new Index(new int[] { signX, signY, signZ });
                                    Index offsetVec = offset * sign;
                                    Vector3 stab = gridPos + (Vector3)offsetVec;
                                    //Console.WriteLine("Stab pos: {0}\n\tOffset {1}", stab, offset * sign);
                                    //if (offsetVec[0] == 1 && offsetVec[1] == -1 && offsetVec[2] == -2)
                                    //    Console.WriteLine("Here here! Position " + stab);

                                    if (!(stab.IsLess(_gridSize)) || !stab.IsPositive())
                                        continue;

                                    // New stabbing query.
                                    bool worked = StabCellGridPos(stab, out range);
                                    //if (offsetVec[0] == 1 && offsetVec[1] == -1 && offsetVec[2] == -2)
                                    //    Console.WriteLine("\tIndex Range [{0},{1}]\n\tGrid Range [{2},{3}]", range.MinIdx, range.MaxIdx, range.MinPos, range.MaxPos);
                                    if (!worked)
                                        continue;
                                    int tet = grid.FindInNode(range.GetData(this), pos, out bary);
                                    if (tet >= 0)
                                        return tet;
                                }
                    }
            }
            bary = Vector4.Zero;
            return -1;
        }

        public struct IndexDistance : IComparable<IndexDistance>
        {
            public int VertexIndex;
            public float Distance;

            //public static  bool operator > (IndexDistance a, IndexDistance b)
            //{
            //    return a.Distance > b.Distance;
            //}

            //public static bool operator <(IndexDistance a, IndexDistance b)
            //{
            //    return a.Distance < b.Distance;
            //}

            public int CompareTo(IndexDistance other)
            {
                return Distance.CompareTo(other.Distance);
            }
        }
        public Dictionary<int, float> FindWithinRadius(Vector3 pos, float radius)
        {
            Node range;
            HashSet<Node> nodes = new HashSet<Node>();
            Dictionary<int, float> vertices = new Dictionary<int, float>();
            Vector3 gridPos = ToGridPosition(pos);
            
            // How many cells can we go maximally before there is definitely nothing there.
            // 1.73... = sqrt(3)
            int maxCellSum = (int)Math.Ceiling(radius / MaxLeafSize.Min() * 1.732050807568877 + 1);
            float radiusSquared = radius * radius;
            float maxEuclideanDistSquared = (radius / MaxLeafSize.Min()) + 1;
            maxEuclideanDistSquared *= maxEuclideanDistSquared;

            GridIndex extent = new GridIndex(new Index(2*maxCellSum + 1, 3));
            foreach (GridIndex gi in extent)
            {
                Index offset = (Index)extent - maxCellSum;

                // As we grow in a <> shape, we can discard some cells early.
                if (offset.LengthSquared() > maxEuclideanDistSquared)
                    continue;
                
                Vector3 stab = gridPos + (Vector3)offset;
                //Console.WriteLine("Stab pos: {0}\n\tOffset {1}", stab, offset * sign);
                //if (offsetVec[0] == 1 && offsetVec[1] == -1 && offsetVec[2] == -2)
                //    Console.WriteLine("Here here! Position " + stab);

                if (!(stab.IsLess(_gridSize)) || !stab.IsPositive())
                    continue;

                // New stabbing query.
                bool worked = StabCellGridPos(stab, out range);
                if (!worked)
                    continue;

                if (nodes.Contains(range))
                    continue;

                foreach (int vert in range.GetData(this))
                {
                    float dist = Vector3.Distance((Vector3)Vertices[vert], pos);
                    if (dist < radius)
                        vertices[vert] = dist;
                        //verts.Add(new IndexDistance() { VertexIndex = vert, Distance = dist });
                }
            }
            //return verts;
            return vertices;
        }

        public UnstructuredGeometry LeafGeometry()
        {
            // Assemble leaf nodes.
            List<Node> leafs = new List<Node>();
            _root.AssembleLeafs(this, leafs);

            // Create vertex and index buffer.
            VectorBuffer verts = new VectorBuffer(leafs.Count * 2, VectorLength);
            IndexArray inds = new IndexArray(leafs.Count, 2);

            for (int l = 0; l < leafs.Count; ++l)
            {
                verts[l * 2] =     new Vector(ToWorldPosition(leafs[l].MinPos));
                verts[l * 2 + 1] = new Vector(ToWorldPosition(leafs[l].MaxPos));

                inds[l] = new Index(new int[] { l * 2, l * 2 + 1 });
            }

            return new UnstructuredGeometry(verts, inds);
        }

        public int GetTetPermutationPosition(int tet)
        {
            for (int i = 0; i < _vertexPermutation.Length; ++i)
                if (_vertexPermutation[i] == tet)
                    return i;
            return -1;
        }

        public class Node
        {
            public int[] Children;
            //public Node Parent;
            public int Level { get; set; }
            public Vector3 MinPos, MidPos, MaxPos;
            public int MinIdx;
            public int MaxIdx;

            public Node()
            {
                Children = new int[8];
            }

            public Node(int level, int minIdx, int maxIdx, Vector3 min, Vector3 max)
            {
                Level = level;
                //Parent = parent;
                MinIdx = minIdx;
                MaxIdx = maxIdx;

                MinPos = min;
                MaxPos = max;
//%                MidPos = MinPos + (MaxPos - MinPos) * 0.5f;

                Children = null;
            }

            public Node(int level, int minIdx, int maxIdx)
            {
                Level = level;
                //Parent = parent;
                MinIdx = minIdx;
                MaxIdx = maxIdx;

                Children = null;
            }

            public bool IsLeaf { get { return Children == null; } }
            public bool IsEmpty { get { return IsLeaf && MaxIdx == MinIdx; } }

            static int NUM_LEAFS = 0;
            static int NUM_NODES = 0;

            public void SplitRecursively(Octree tree, int minElements = 100, int maxDepth = -1)
            {
                //tree._maxDepth = Math.Max(Level, tree._maxDepth);

                int numDims = tree.VectorLength;
                MidPos = (MaxPos + MinPos) * 0.5f;

                if (++NUM_NODES % 50000 == 0)
                    Console.WriteLine("Nodes: " + NUM_NODES);

                if (Level == maxDepth || MaxIdx - MinIdx <= minElements)
                {
                    // Console.WriteLine("Level {0}({1}), {2}({3}) Elements", Level, maxDepth, MaxIdx - MinIdx, minElements);
                    if (++NUM_LEAFS % 50000 == 0)
                        Console.WriteLine("Leaves: " + NUM_LEAFS);

                    return;
                }

                // 2^n children.
                Children = new int[1 << numDims];
                for (int c = 0; c < Children.Length; ++c)
                    Children[c] = tree._nodes.Count + c;

                // ============= Repeated Sort =============== \\
                Debug.Assert(numDims == 3, "Only implemented for 3 dims so far.");

                // 1st dim
                int cutX = SortSubPermuationByDimension(tree, MinIdx, MaxIdx, 0);
                cutX = GetCut(tree, MinIdx, MaxIdx, MidPos, 0);

                // 2nd dim
                int[] cutY = new int[2];
                SortSubPermuationByDimension(tree, MinIdx, cutX, 1);
                SortSubPermuationByDimension(tree, cutX, MaxIdx, 1);

                cutY[0] = GetCut(tree, MinIdx, cutX, MidPos, 1);
                cutY[1] = GetCut(tree, cutX, MaxIdx, MidPos, 1);

                // 3rd dim
                int[] cutZ = new int[4];
                SortSubPermuationByDimension(tree, MinIdx, cutY[0], 2);
                SortSubPermuationByDimension(tree, cutY[0], cutX, 2);
                SortSubPermuationByDimension(tree, cutX, cutY[1], 2);
                SortSubPermuationByDimension(tree, cutY[1], MaxIdx, 2);

                cutZ[0] = GetCut(tree, MinIdx, cutY[0], MidPos, 2);
                cutZ[1] = GetCut(tree, cutY[0], cutX, MidPos, 2);
                cutZ[2] = GetCut(tree, cutX, cutY[1], MidPos, 2);
                cutZ[3] = GetCut(tree, cutY[1], MaxIdx, MidPos, 2);

                // Assign index ranges.
                tree._nodes.Add( new Node(Level + 1, MinIdx,  cutZ[0]));
                tree._nodes.Add( new Node(Level + 1, cutX,    cutZ[2]));

                tree._nodes.Add( new Node(Level + 1, cutY[0], cutZ[1]));
                tree._nodes.Add( new Node(Level + 1, cutY[1], cutZ[3]));

                tree._nodes.Add( new Node(Level + 1, cutZ[0], cutY[0]));
                tree._nodes.Add( new Node(Level + 1, cutZ[2], cutY[1]));

                tree._nodes.Add( new Node(Level + 1, cutZ[1],    cutX));
                tree._nodes.Add( new Node(Level + 1, cutZ[3],  MaxIdx));

                for (int childIdx = 0; childIdx < Children.Length; ++childIdx)
                {
                    Sign[] signs = SignsByIndex(childIdx, numDims);
                    Vector3 childMin = MinPos;
                    Vector3 childMax = MidPos;

                    // Collect the position range.
                    for (int i = 0; i < numDims; ++i)
                        if (signs[i])
                        {
                            childMin[i] = MidPos[i];
                            childMax[i] = MaxPos[i];
                        }

                    tree._nodes[Children[childIdx]].MinPos = childMin;
                    tree._nodes[Children[childIdx]].MaxPos = childMax;
                    tree._nodes[Children[childIdx]].SplitRecursively(tree, minElements, maxDepth);
                    //                foreach (Node child in Children)
                    //                    child.SplitUntilMinElementsint(tree, minElements,maxDepth, depth+1);
                }
            }

            private static int SortSubPermuationByDimension(Octree tree, int minIdx, int maxIdx, int dim)
            {
                Array.Sort<int>(tree._vertexPermutation, minIdx, maxIdx - minIdx, Comparer<int>.Create((x, y) => tree.Vertices[x][dim].CompareTo(tree.Vertices[y][dim])));
                return (int)((minIdx + maxIdx) * 0.5f);
            }

            private static int GetCut(Octree tree, int minIdx, int maxIdx, Vector3 middle, int dim)
            {
                Vector3 worldCut = tree.ToWorldPosition(middle);
                int cutIdx = Array.BinarySearch<int>(tree._vertexPermutation, minIdx, maxIdx - minIdx, -42, Comparer<int>.Create((x, y) => tree.Vertices[x][dim].CompareTo(worldCut[dim])));
                int ret = cutIdx >= 0 ? cutIdx : ~cutIdx;
                return ret;
            }

            public bool IsGridPosInside(Vector3 vec)
            {
                return vec.IsLargerEqual(MinPos) && vec.IsLess(MaxPos);
            }

            public void AssembleLeafs(Octree tree, List<Node> leafs)
            {
                if (IsLeaf)
                {
                    if (!IsEmpty)
                        leafs.Add(this);
                    return;
                }

                for (int childIdx = 0; childIdx < Children.Length; ++childIdx)
                {
                    tree._nodes[Children[childIdx]].AssembleLeafs(tree, leafs);
                }
            }

            public CellData GetData(Octree tree)
            {
                return new CellData(this, tree);
            }

            private Sign[] SignsByIndex(int idx, int numDimensions)
            {
                Debug.Assert(idx < Math.Pow(2, numDimensions) && idx >= 0);

                Sign[] signs = new Sign[numDimensions];
                for (int s = 0; s < signs.Length; ++s)
                {
                    signs[s] = (idx % 2) == 1 ? Sign.POSITIVE : Sign.NEGATIVE;
                    idx /= 2;
                }
                return signs;
            }

            public static bool operator ==(Node a, Node b)
            {
                return a?.MinIdx == b?.MinIdx && a?.MaxIdx == b?.MaxIdx;
            }

            public static bool operator !=(Node a, Node b)
            {
                return a?.MinIdx != b?.MinIdx || a?.MaxIdx != b?.MaxIdx;
            }

            public override bool Equals(object obj)
            {
                if ((obj as Node) == null)
                    return false;
                return (obj as Node) == this;
            }

            public override string ToString()
            {
                return $"Indices in [{MinIdx}, {MaxIdx})\nPositions in [{MinPos}, {MaxPos})\nLevel {Level}\n{(Children == null? "Leaf":$"Children [{Children[0]}, {Children[1]}, {Children[2]}, {Children[3]}, {Children[4]}, {Children[5]}, {Children[6]}, {Children[7]}]")}";
            }
        }

        public class CellData : IEnumerable<int>
        {
            public int MinIdx { get; protected set; }
            public int MaxIdx { get; protected set; }
            protected Octree _tree;

            public CellData(Node node, Octree tree)
            {
                MinIdx = node.MinIdx;
                MaxIdx = node.MaxIdx;
                _tree = tree;
            }
            public CellData(CellData copy)
            {
                MinIdx = copy.MinIdx;
                MaxIdx = copy.MaxIdx;
                _tree = copy._tree;
            }

            public CellData()
            {
                MinIdx = 0;
                MaxIdx = 0;
                _tree = null;
            }

            public int Length { get { return MaxIdx - MinIdx; } }

            public static bool operator == (CellData a, CellData b)
            {
                return a?.MinIdx == b?.MinIdx && a?.MaxIdx == b?.MaxIdx;
            }

            public static bool operator !=(CellData a, CellData b)
            {
                return !(a == b);
            }

            public static bool operator ==(CellData a, Node b)
            {
                return a?.MinIdx == b?.MinIdx && a?.MaxIdx == b?.MaxIdx;
            }

            public static bool operator !=(CellData a, Node b)
            {
                return !(a == b);
            }

            public IEnumerator<int> GetEnumerator()
            {
                return new CellDataEnumerator(this);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new CellDataEnumerator(this);
            }
        }
        class CellDataEnumerator : CellData, IEnumerator<int>
        {
            int _current;
            public CellDataEnumerator(CellData parent) : base(parent)
            {
                Reset();
            }
            public int Current
            {
                get
                {
                    return _tree._vertexPermutation[_current];
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return this;
                }
            }

            public void Dispose() { }

            public bool MoveNext()
            {
                _current++;
                return _current < MaxIdx;
            }

            public void Reset()
            {
                _current = MinIdx - 1;
            }
        }
    }

}
