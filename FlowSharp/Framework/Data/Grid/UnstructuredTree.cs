using SlimDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    interface GeneralUnstructurdGrid
    {
        VectorData Vertices { get; }
        int NumCells { get; }
        IndexArray AssembleIndexList();
        PointSet<Point> GetVertices();
    }

    class UnstructuredTree : GeneralUnstructurdGrid
    {
        public VectorBuffer _vertices;
        public VectorData Vertices { get { return _vertices; } set { _vertices = value as VectorBuffer; } }
        public IndexArray Primitives;

        public UnstructuredTree(VectorBuffer vec, IndexArray ind)
        {
            _vertices = vec;
            Primitives = ind;
        }
        public int NumCells { get { return Primitives.Length; } }
        /// <summary>
        /// Assemble all inidces to a buffer. Do this here for general Tet grids.
        /// </summary>
        /// <returns></returns>
        public IndexArray AssembleIndexList()
        {
            return Primitives;
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
    }
}
