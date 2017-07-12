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
        Vector[] Vertices { get; }
        int NumCells { get; }
        Index[] AssembleIndexList();
        PointSet<Point> GetVertices();
    }

    class UnstructuredTree : GeneralUnstructurdGrid
    {
        public Vector[] Vertices{ get; set; }
        public Index[] Primitives;

        public int NumCells { get { return Primitives.Length; } }
        /// <summary>
        /// Assemble all inidces to a buffer. Do this here for general Tet grids.
        /// </summary>
        /// <returns></returns>
        public Index[] AssembleIndexList()
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
