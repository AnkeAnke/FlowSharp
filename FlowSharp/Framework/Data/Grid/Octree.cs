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
        public Vector Minimum { get { return Vertices.MinValue; } }
        public Vector Maximum { get { return Vertices.MaxValue; } }
        public int VectorLength { get { return Minimum.Length; } }
        private Node _root { get { return _nodes[0]; } }
        private List<Node> _nodes;

        public VectorData Vertices;
        private int[] _vertexPermutation;
        private Vector _maxLeafSize { get { return Vertices.Extent / (1 << _maxDepth); } }
        private int _maxDepth;
        private int _gridSize { get { return 1 << _maxDepth; } }


        public bool OUTPUT_DEBUG = false;


        private Vector ToGridPosition(VectorRef pos)
        {
            return (pos - Minimum) * (1 << _maxDepth) / (Maximum - Minimum);
        }

        private Vector ToWorldPosition(Vector pos)
        {
            return pos * _maxLeafSize + Minimum;
        }

        public Octree(VectorData data, int minElements = 100, int maxDepth = -1)
        {
            if (maxDepth < 1)
                throw new NotImplementedException("Nope. Sorry.");

            _maxDepth = maxDepth;

            Vertices = data;
            _vertexPermutation = Enumerable.Range(0, Vertices.Length).ToArray();

            Vertices.ExtractMinMax();
            _nodes = new List<Node>(20000);
            _nodes.Add(new Node(0, 0, _vertexPermutation.Length, new Vector(0,3), new Vector(_gridSize, 3)));


            Stopwatch watch = new Stopwatch();
            watch.Start();

            _root.SplitRecursively(this, minElements, maxDepth);

            // Use max depth for "lattice distance" computation.
            //if (maxDepth > 0)
            //{
            //    Debug.Assert(maxDepth >= _maxDepth);
            //    _maxDepth = maxDepth;
            //}

            // This is important, as neighbor queries rely on it.
//            _maxLeafSize = Vertices.Extent / (1 << _maxDepth);

            watch.Stop();
            Console.WriteLine("Octree buildup took {0}m {1}s", (int)watch.Elapsed.TotalMinutes, watch.Elapsed.Seconds);
        }

        #region FileReadWrite
        //public Octree(string filename)
        //{
        //    using (FileStream fs = File.Open(@filename, FileMode.Open))
        //    {
        //        using (BinaryReader reader = new BinaryReader(fs))
        //        {
        //            // Read Permutations.
        //            int numPerms = reader.ReadInt32();
        //            _vertexPermutation = new int[numPerms];
        //            byte[] perms = reader.ReadBytes(numPerms * sizeof(int));
        //            Buffer.BlockCopy(perms, 0, _vertexPermutation, 0, numPerms * sizeof(int));

        //            // Load Nodes.
        //            int numNodes = reader.ReadInt32();
        //            _nodes = new List<Node>(numNodes);

        //            // Read nodes iteratively.
        //            for (int n = 0; n < numNodes; ++n)
        //            {
        //                Node node = new Node();
        //                // Reading children. Reading 8 (-1) means Children == null.
        //                for (int c = 0; c < 8; ++c)
        //                    node.Children[c] = reader.ReadInt32();

        //                // Write int values.
        //                node.MinIdx = reader.ReadInt32();
        //                node.MaxIdx = reader.ReadInt32();
        //                node.Level  = reader.ReadInt32();

        //                // Read vectors component-wise.
        //                node.MinPos = new Vector(3);
        //                for (int m = 0; m < 3; ++m)
        //                    node.MinPos[m] = reader.ReadSingle();

        //                node.MaxPos = new Vector(3);
        //                for (int m = 0; m < 3; ++m)
        //                    node.MaxPos[m] = reader.ReadSingle();

        //                node.MidPos = node.MinPos + (node.MaxPos - node.MinPos) * 0.5f;
        //            }
        //        }
        //    }
        //}

        //public void WriteToFile(string filename)
        //{
        //    using (FileStream fs = File.Open(@filename, FileMode.Create))
        //    {
        //        using (BinaryWriter writer = new BinaryWriter(fs))
        //        {
        //            // Write Permutations.
        //            writer.Write(_vertexPermutation.Length);
        //            byte[] perms = new byte[_vertexPermutation.Length * sizeof(int)];
        //            Buffer.BlockCopy(_vertexPermutation, 0, perms, 0, _vertexPermutation.Length);
        //            writer.Write(perms);

        //            // Write Nodes.
        //            writer.Write(_nodes.Count);
        //            foreach (Node node in _nodes)
        //            {
        //                // Writing children. Write 8 (-1) so each block is the same length.
        //                for (int c = 0; c < 8; ++c)
        //                    writer.Write(node.Children?[c] ?? -1);

        //                // Write int values.
        //                writer.Write(node.MinIdx);
        //                writer.Write(node.MaxIdx);
        //                writer.Write(node.Level);

        //                // Write vectors component-wise.
        //                foreach (float f in node.MinPos.Data)
        //                    writer.Write(f);

        //                foreach (float f in node.MaxPos.Data)
        //                    writer.Write(f);
        //            }
        //        }
        //    }
        //}
        #endregion FileReadWrite

        /// <summary>
        /// Stab the octree. Returns the lowest node containing the position. Maximal level can be set.
        /// </summary>
        /// <param name="pos">Sample position.</param>
        /// <param name="leafNode">Output node.</param>
        /// <param name="maxLevel">Maximal traversal depth. Negative means no condition.</param>
        /// <returns>Node containing the position. Null if outside of octree bounding box.</returns>
        public CellData StabCell(VectorRef pos, out Node leafNode)
        {
            Debug.Assert(pos.Length == Minimum.Length);
            return StabCellGridPos(ToGridPosition(pos), out leafNode);
        }

        /// <summary>
        /// Stab the octree. Returns the lowest node containing the position. Maximal level can be set.
        /// </summary>
        /// <param name="pos">Sample position.</param>
        /// <param name="leafNode">Output node.</param>
        /// <param name="maxLevel">Maximal traversal depth. Negative means no condition.</param>
        /// <returns>Node containing the position. Null if outside of octree bounding box.</returns>
        private CellData StabCellGridPos(VectorRef gridPos, out Node leafNode)
        {
            //cellExtent = null;

            if (!(gridPos < new Vector(_gridSize, 3)) || !gridPos.IsPositive())
            {
                leafNode = null;
                return new CellData();
            }

            leafNode = _root.Stab(this, gridPos);

            return leafNode.GetData(this);
        }

        public List<CellData> FindNeighborNodes(VectorRef pos, Node leaf)
        {
            List<CellData> neighbors = new List<CellData>(6);

            // Get "sub cell" - the leaf node we would be in.
            //Vector midPos = Minimum + ((Index)((pos - Minimum) / _maxLeafSize)) * _maxLeafSize;
            //midPos += _maxLeafSize * 0.5f;
            //Sign[] comp = VectorRef.Compare(pos, midPos);

            Vector gridPos = ToGridPosition(pos);
            Vector refPos = gridPos - (Vector)((Index)gridPos);
            Sign[] comp = VectorRef.Compare(refPos, new Vector(0.5f, 3));

            Node stabbed;

            // Go through all dimensions. We only need to got into one direction in each dimension, the corners of a cube.
            foreach (GridIndex offsetGI in new GridIndex(new Index(2, 3)))
            {
                // Continue if we are at the center.
                Index offset = offsetGI;
                if (offset.Max() == 0)
                    continue;

                // Assemble stabbing position. New stab should be performanter/more failsafe than walking the tree up and down.
                Vector stab = new Vector(gridPos);
                for (int n = 0; n < 3; ++n)
                    stab += (int)comp[n] * offset[n];

                // If it's inside the node found, no need to stab again.
                if (leaf.IsGridPosInside(stab))
                    continue;

                CellData data = StabCellGridPos(stab, out stabbed/*, leafNode.Level*/);
                if (data?.Length != null)
                    neighbors.Add(data);
            }

            return neighbors;
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
                verts[l * 2] =     ToWorldPosition(leafs[l].MinPos);
                verts[l * 2 + 1] = ToWorldPosition(leafs[l].MaxPos);

                inds[l] = new Index(new int[] { l * 2, l * 2 + 1 });
            }

            return new UnstructuredGeometry(verts, inds);
        }

        public class Node
        {
            public int[] Children;
            //public Node Parent;
            public int Level { get; set; }
            public Vector MinPos, MidPos, MaxPos;
            public int MinIdx;
            public int MaxIdx;

            public Node()
            {
                Children = new int[8];
            }

            public Node(int level, int minIdx, int maxIdx, Vector min, Vector max)
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
                MinPos = MaxPos = null;
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
                    Vector childMin = new Vector(MinPos);
                    Vector childMax = new Vector(MidPos);

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

            private static int GetCut(Octree tree, int minIdx, int maxIdx, Vector middle, int dim)
            {
                Vector worldCut = tree.ToWorldPosition(middle);
                int cutIdx = Array.BinarySearch<int>(tree._vertexPermutation, minIdx, maxIdx - minIdx, -42, Comparer<int>.Create((x, y) => tree.Vertices[x][dim].CompareTo(worldCut[dim])));
                int ret = cutIdx >= 0 ? cutIdx : ~cutIdx;
                return ret;
            }

            public Node Stab(Octree tree, VectorRef pos) //Vector cellExtent)
            {

                if (IsLeaf)
                {
                    //if (tree.OUTPUT_DEBUG)
                    //    Console.WriteLine("============\nMin pos {0}\nMax pos {1}\nPosition {2}\nMid pos {3}\n\tLevel {4}\n\t{5} Children\n\t{6} Elements\n============", MinPos, MaxPos, pos, MidPos, Level, Children?.Length??0, MaxIdx-MinIdx);
                    return this;
                }

                Sign[] comp = VectorRef.Compare(pos, MidPos);
                Node child = tree._nodes[Children[IndexBySigns(comp)]];
                //if (child.IsEmpty)
                //    return this;
                return child.Stab(tree, pos);
            }

            public bool IsGridPosInside(VectorRef vec)
            {
                return vec >= MinPos && vec < MaxPos;
            }

            //public struct Leaf
            //{
            //    public Node Node;
            //    public Vector MinPos, MaxPos;
            //    public Leaf(Node node)
            //    {
            //        Node = node;
            //        MinPos = min;
            //        MaxPos = max;
            //    }
            //}
            public void AssembleLeafs(Octree tree, List<Node> leafs)
            {
                if (IsLeaf)
                {
                    if (!IsEmpty)
                        leafs.Add(this);
                    return;
                }

                int numDims = MinPos.Length;
                //foreach (Node child in Children)
                //    AssembleLeafs(leafs);
                for (int childIdx = 0; childIdx < Children.Length; ++childIdx)
                {
                    tree._nodes[Children[childIdx]].AssembleLeafs(tree, leafs);
                }
            }

            public CellData GetData(Octree tree)
            {
                return new CellData(this, tree);
            }

            private int IndexBySigns(Sign[] signs)
            {
                int idx = 0;
                int factor = 1;
                for (int s = 0; s < signs.Length; ++s)
                {
                    idx += signs[s] ? factor : 0;
                    factor *= 2;
                }
                return idx;
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
                if (obj as Node == null)
                    return false;
                return obj as Node == this;
            }
        }

        public class CellData : IEnumerable<int>
        {
            protected int _minIdx;
            protected int _maxIdx;
            protected Octree _tree;

            public CellData(Node node, Octree tree)
            {
                _minIdx = node.MinIdx;
                _maxIdx = node.MaxIdx;
                _tree = tree;
            }
            public CellData(CellData copy)
            {
                _minIdx = copy._minIdx;
                _maxIdx = copy._maxIdx;
                _tree = copy._tree;
            }

            public CellData()
            {
                _minIdx = 0;
                _maxIdx = 0;
                _tree = null;
            }

            public int Length { get { return _maxIdx - _minIdx; } }

            public static bool operator == (CellData a, CellData b)
            {
                return a?._minIdx == b?._minIdx && a?._maxIdx == b?._maxIdx;
            }

            public static bool operator !=(CellData a, CellData b)
            {
                return !(a == b);
            }

            public static bool operator ==(CellData a, Node b)
            {
                return a?._minIdx == b?.MinIdx && a?._maxIdx == b?.MaxIdx;
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
                return _current < _maxIdx;
            }

            public void Reset()
            {
                _current = _minIdx - 1;
            }
        }
    }

}
