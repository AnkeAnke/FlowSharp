﻿using SlimDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    interface GeneralUnstructurdGrid
    {
        VectorData Vertices { get; }
        int NumCells { get; }
        Tuple<VectorData, IndexArray> AssembleIndexList();
        PointSet<Point> GetVertices();
    }

    class UnstructuredGeometry : GeneralUnstructurdGrid
    {
        public VectorBuffer _vertices;
        public VectorData Vertices { get { return _vertices; } set { _vertices = value as VectorBuffer; } }
        public IndexArray Primitives;

        public UnstructuredGeometry(VectorBuffer vec, IndexArray ind)
        {
            _vertices = vec;
            Primitives = ind;
        }
        public int NumCells { get { return Primitives.Length; } }
        /// <summary>
        /// Assemble all inidces to a buffer. Do this here for general Tet grids.
        /// </summary>
        /// <returns></returns>
        public Tuple<VectorData, IndexArray> AssembleIndexList()
        {
            // Simply triangles. Index list is given as is.
            if (Primitives.IndexLength == 3)
                return new Tuple<VectorData, IndexArray> (Vertices, Primitives);

            // Tets. Make 4 triangles of every element.
            if (Primitives.IndexLength == 4)
            {
                //IndexArray indices = new IndexArray(100*4, 3);
                IndexArray indices = new IndexArray(Primitives.Length * 4, 3);
                for (int t = 0; t < indices.Length / 4; ++t)
                {
                    Index tet = Primitives[t];
                    indices.SetBlock(t*3*4, new int[] 
                    { tet[2], tet[0], tet[1],
                      tet[0], tet[2], tet[3],
                      tet[2], tet[1], tet[3],
                      tet[1], tet[0], tet[3]});
                }
                return new Tuple<VectorData, IndexArray>(Vertices, indices);
            }

            // Make axis aligned boxes.
            if (Primitives.IndexLength == 2)
            {
                Debug.Assert(Vertices.VectorLength == 3);
                IndexArray indices = new IndexArray(Primitives.Length * 12, 3);
                VectorBuffer verts = new VectorBuffer(Primitives.Length * 8, 3);

                for (int i = 0; i < Primitives.Length; ++i)
                {
                    VectorRef min = Vertices[Primitives[i][0]];
                    VectorRef max = Vertices[Primitives[i][1]];

                    int idx = i * 8;

                    verts[idx + 0] = min;
                    verts[idx + 1] = new Vec3(max[0], min[1], min[2]);
                    verts[idx + 2] = new Vec3(min[0], max[1], min[2]);
                    verts[idx + 3] = new Vec3(max[0], max[1], min[2]);
                    verts[idx + 4] = new Vec3(min[0], min[1], max[2]);
                    verts[idx + 5] = new Vec3(max[0], min[1], max[2]);
                    verts[idx + 6] = new Vec3(min[0], max[1], max[2]);
                    verts[idx + 7] = max;

                    indices.SetBlock(i * 3 * 12, new int[]
                    { idx + 0, idx + 1, idx + 2, // bottom
                      idx + 1, idx + 3, idx + 2,

                      idx + 0, idx + 4, idx + 1, // front
                      idx + 1, idx + 4, idx + 5,

                      idx + 0, idx + 6, idx + 4, // left
                      idx + 0, idx + 2, idx + 6,

                      idx + 4, idx + 6, idx + 7, // top
                      idx + 4, idx + 7, idx + 5,

                      idx + 2, idx + 3, idx + 6, // back
                      idx + 3, idx + 7, idx + 6,

                      idx + 1, idx + 5, idx + 3, // right
                      idx + 3, idx + 5, idx + 7 });
                }

                return new Tuple<VectorData, IndexArray>(verts, indices);
            }

            Debug.Assert(false, "Each primitive consists of " + Primitives.IndexLength + " vertices, don't know what to do.");
            return null;
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