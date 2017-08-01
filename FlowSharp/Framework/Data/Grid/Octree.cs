using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class Octree
    {
        public Vector Minimum { get { return _root.MinPos;}}
        public Vector Maximum { get { return _root.MaxPos; } }
        public int VectorLength { get { return Minimum.Length; } }
        private Node _root;

        public GeneralUnstructurdGrid Grid;
        private int[] _vertexPermutation;


        public Octree(GeneralUnstructurdGrid grid, int minElements = 100)
        {
            Grid = grid;
            _vertexPermutation = Enumerable.Range(0, Grid.Vertices.Length).ToArray();

            grid.Vertices.ExtractMinMax();
            _root = new Node(0, _vertexPermutation.Length, grid.Vertices.MinValue, grid.Vertices.MaxValue);

            _root.SplitUntilMinElementsint(this, minElements);
        }

        public CellData StabCell(Vector pos)
        {
            Debug.Assert(pos.Length == Minimum.Length);
            if (!(pos <= Maximum) || !(pos > Minimum))
            {
                return new CellData();
            }

            Node leaf = _root.Stab(pos);
            return leaf.GetData(this);
        }

        public UnstructuredGeometry LeafGeometry()
        {
            // Assemble leaf nodes.
            List<Node> leafs = new List<Node>();
            _root.AssembleLeafs(leafs);

            // Create vertex and index buffer.
            VectorBuffer verts = new VectorBuffer(leafs.Count * 2, VectorLength);
            IndexArray inds = new IndexArray(leafs.Count, 2);

            for (int l = 0; l < leafs.Count; ++l)
            {
                verts[l * 2]        = leafs[l].MinPos;
                verts[l * 2 + 1]    = leafs[l].MaxPos;

                inds[l] = new Index(l * 2, l * 2 + 1);
            }

            return new UnstructuredGeometry(verts, inds);
        }

        public struct Node
        {
            public Node[] Children;
            public Vector MinPos, MaxPos;
            public int MinIdx;
            public int MaxIdx;

            public Node(int minIdx, int maxIdx, Vector min, Vector max)
            {
                Debug.Assert(min?.Length == max?.Length);
                MinIdx = minIdx;
                MaxIdx = maxIdx;
                MinPos = min;
                MaxPos = max;
                Children = null;
            }

            public bool IsLeaf { get { return Children == null; } }

            static int NUM_LEAFS = 0;
            static int NUM_NODES = 0;
            public void SplitUntilMinElementsint(Octree tree, int minElements = 100)
            {
                if (++NUM_NODES % 10 == 0)
                    Console.WriteLine("Nodes: " + NUM_NODES);

                int numDims = MinPos.Length;
                if (MaxIdx - MinIdx <= minElements)
                {
                    if (++NUM_LEAFS % 10 == 0)
                        Console.WriteLine("Leaves: " + NUM_LEAFS);

                    return;
                }

                // Split in the middle in all dimensions.
                Vector middle = MinPos + (MaxPos - MinPos) * 0.5f;

                // 2^n children.
                Children = new Node[1 << numDims];

#if false
                // ============= Check each index =============== \\
                // Before this index, we will already have assigned them to lower nodes.
                int beginUnordered = MinIdx;
                int lastMinIdx = MinIdx;

                for (int childIdx = 0; childIdx < Children.Length; ++childIdx)
                {
                    Sign[] signs = SignsByIndex(childIdx, numDims);
                    Vector childMin = new Vector(MinPos);
                    Vector childMax = middle;

                    // Collect the position range.
                    for (int i = 0; i < numDims; ++i)
                        if (signs[i])
                        {
                            childMin[i] = middle[i];
                            childMax[i] = MaxPos[i];
                        }

                        // Sort all points that are within that range to the beginning.
                    for (int vertIdx = lastMinIdx; vertIdx < MaxIdx; ++vertIdx)
                    {
                        int currentIdx = tree._vertexPermutation[vertIdx];
                        if (tree.Grid.Vertices[currentIdx] <= childMax)
                        {
                            tree._vertexPermutation[vertIdx] = tree._vertexPermutation[beginUnordered];
                            tree._vertexPermutation[beginUnordered] = currentIdx;
                            beginUnordered++;
                        }
                    }

                    Children[childIdx] = new Node(lastMinIdx, beginUnordered, childMin, childMax);
                    lastMinIdx = beginUnordered;
                }
                Debug.Assert(beginUnordered == MaxIdx);
#endif
                // ============= Repeated Sort =============== \\
                Debug.Assert(numDims == 3, "Only implemented for 3 dims so far.");

                int cutX = SortSubPermuationByDimension(tree, MinIdx, MaxIdx, middle, 0);

                int cutYlow = SortSubPermuationByDimension(tree, MinIdx, cutX, middle, 1);
                int cutYhigh = SortSubPermuationByDimension(tree, cutX, MaxIdx, middle, 1);

                int cutZlowYlow = SortSubPermuationByDimension(tree, MinIdx, cutYlow, middle, 2);
                int cutZhighYlow = SortSubPermuationByDimension(tree, cutYlow, cutX, middle, 2);
                int cutZlowYhigh = SortSubPermuationByDimension(tree, cutX, cutYhigh, middle, 2);
                int cutZhighYhigh = SortSubPermuationByDimension(tree, cutYhigh, MaxIdx, middle, 2);

                Children[0] = new Node(MinIdx, cutZlowYlow, null, null);
                Children[1] = new Node(cutX, cutZlowYhigh, null, null);

                Children[2] = new Node(cutYlow, cutZhighYlow, null, null);
                Children[3] = new Node(cutYhigh, cutZhighYhigh, null, null);

                Children[4] = new Node(cutZlowYlow, cutYlow, null, null);
                Children[5] = new Node(cutZlowYhigh, cutYhigh, null, null);

                Children[6] = new Node(cutZhighYlow, cutX, null, null);
                Children[7] = new Node(cutZhighYhigh, MaxIdx, null, null);

                for (int childIdx = 0; childIdx < Children.Length; ++childIdx)
                {
                    Sign[] signs = SignsByIndex(childIdx, numDims);
                    Vector childMin = new Vector(MinPos);
                    Vector childMax = middle;

                    // Collect the position range.
                    for (int i = 0; i < numDims; ++i)
                        if (signs[i])
                        {
                            childMin[i] = middle[i];
                            childMax[i] = MaxPos[i];
                        }

                    Children[childIdx].MinPos = childMin;
                    Children[childIdx].MaxPos = childMax;
                }

                    foreach (Node child in Children)
                    child.SplitUntilMinElementsint(tree, minElements);
            }

            private static int SortSubPermuationByDimension(Octree tree, int minIdx, int maxIdx, Vector middle, int dim)
            {
                Array.Sort<int>(tree._vertexPermutation, minIdx, maxIdx - minIdx, Comparer<int>.Create((x, y) => tree.Grid.Vertices[x][dim].CompareTo(tree.Grid.Vertices[y][dim])));
                VectorRef min = tree.Grid.Vertices[tree._vertexPermutation[0]];
                VectorRef max = tree.Grid.Vertices[tree._vertexPermutation.Last()];

                int cutIdx = Array.BinarySearch<int>(tree._vertexPermutation, minIdx, maxIdx - minIdx, -42, Comparer<int>.Create((x, y) => tree.Grid.Vertices[x][dim].CompareTo(middle[dim])));
                int ret = cutIdx >= 0 ? cutIdx : ~cutIdx;
                return ret;
            }

            public Node Stab(Vector pos)
            {
                Debug.Assert(pos.Length == MinPos.Length, "Dimensions don't agree.");
                Debug.Assert(pos >= MinPos && pos < MaxPos, "Position not within range.");
                if (IsLeaf)
                    return this;

                Sign[] comp = VectorRef.Compare(pos, MinPos + (MaxPos - MinPos) * 0.5f);
                return Children[IndexBySigns(comp)].Stab(pos);
            }

            public void AssembleLeafs(List<Node> leafs)
            {
                if (IsLeaf)
                {
                    leafs.Add(this);
                    return;
                }

                foreach (Node child in Children)
                    AssembleLeafs(leafs);
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
                    idx += signs[s]? factor : 0;
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
        }

        public class CellData : IEnumerable<VectorRef>
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

            public IEnumerator<VectorRef> GetEnumerator()
            {
                return new CellDataEnumerator(this);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new CellDataEnumerator(this);
            }
        }
        class CellDataEnumerator : CellData, IEnumerator<VectorRef>
        {
            int _current;
            public CellDataEnumerator(CellData parent) : base(parent)
            {
                Reset();
            }
            public VectorRef Current
            {
                get
                {
                    return _tree.Grid.Vertices[_tree._vertexPermutation[_minIdx]];
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
