using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    /// <summary>
    /// Find all points within a certein maximal radius. Setting this radius to the maximal tet side length, we get all possible tets.
    /// </summary>
    class FixedRadiusLattice
    {
        public VectorData Vertices;
        private List<int>[,,] _lattice;
        public List<int>[,,] Lattice
        {
            get { return _lattice; }
            set
            {
                Size = value == null? null : Util.GetSize(value);
                _lattice = value;
            }
        }
        public Index Size { get; protected set; }
        public float EdgeLength { get; protected set; }

        public List<int> this[Index index]
        {
            get
            {
                Debug.Assert(index.IsPositive() && index < Size);
                Debug.Assert(index.Length == 3);
                return Lattice[index[0], index[1], index[2]];
            }
            set
            {
                Debug.Assert(index.IsPositive() && index < Size);
                Debug.Assert(index.Length == 3);
                Lattice[index[0], index[1], index[2]] = value;
            }
        }

        public List<int> this[VectorRef pos]
        {
            get
            {
                Index idx = SnapToGrid(pos);
                return (idx == null) ? null : this[idx];
            }
        }

        public FixedRadiusLattice(VectorData data, float latticeDist)
        {
            EdgeLength = latticeDist;
            data.ExtractMinMax();
            Vertices = data;

            // Lets say we want about 1000x1000x1000 Cells...
            EdgeLength = (data.Extent / 100).Max();


            // Setup grid.
            Index size = (Index)(data.Extent / EdgeLength + 1);
            Lattice = new List<int>[size[0], size[1], size[2]];

            for (int v = 0; v < Vertices.Length; ++v)
            {
                VectorRef pos = Vertices[v];
                Index index = SnapToGrid(pos);
                if (this[index] == null)
                    this[index] = new List<int>(8);
                this[index].Add(v);
            }
        }

        public Index SnapToGrid(VectorRef pos)
        {
            // Outside the vertex bounding box. Return null.
            if (!(pos >= Vertices.MinValue) && !(pos <= Vertices.MaxValue))
                return null;

            Vector relativPos = (pos - Vertices.MinValue) / EdgeLength/* + 0.5f*/;
            return (Index)relativPos;
        }

        private Vector CellPos(Index idx)
        {
            return Vertices.MinValue + ((Vector)idx + 0.5f) * EdgeLength;
        }

        public List<List<int>> GetNeighborData(VectorRef pos)
        {
            List<List<int>> data = new List<List<int>>(26);
            Index posIndex = SnapToGrid(pos);

            if (posIndex == null)
                return data;

            Vector posFloatIndex = (pos - Vertices.MinValue) / EdgeLength;
            Sign[] comp = VectorRef.Compare(posFloatIndex, (Vector)posIndex);



            //var maxOffset = new GridIndex(new Index(3, 3));
            //foreach (GridIndex offset in maxOffset)
            //{
            //    if (offset == null)
            //        Console.Write("What.");
            for (int dim = 0; dim < 3; ++dim)
            {
                Index neighIndex = new Index(posIndex);
                neighIndex[dim] += (int)comp[dim];

                //Outside of grid ?
                if (!neighIndex.IsPositive() || neighIndex >= Size)
                    continue;

                // Add if not null.
                var list = this[neighIndex];
                if (list != null)
                    data.Add(list);
            }

            return data;
        }

        /// <summary>
        /// Gather and display some statistics.
        /// </summary>
        public void ShowStatistics()
        {
            int maxVerts = 0;
            int nonEmptyCells = 0;

            foreach (var list in Lattice)
            {
                if (list == null)
                    continue;

                nonEmptyCells++;
                maxVerts = Math.Max(maxVerts, list.Count);
            }

            Console.WriteLine("Lattice Statistics\n=================\n\t{0} Data Cells for {1} Vertices\n\tMax. {2} Vertices per Cell\n\tAvg. {3} Vertices per Cell\n\t{4}% Cells not empty",
                Size, Vertices.Length, maxVerts, ((float)Vertices.Length) / nonEmptyCells, ((float)nonEmptyCells)/Vertices.Length * 100);
        }
    }
}
