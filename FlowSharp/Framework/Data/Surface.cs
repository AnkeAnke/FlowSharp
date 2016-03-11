using SlimDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace FlowSharp
{
    class Surface
    {
        public virtual Vector4[] Vertices { get; }
        public virtual int[] Indices { get; }
    }
    class TileSurface : Surface
    {
        public Vector3[,] Positions;
        private float[,] _attribute;
        public float[,] Attribute
        {
            get { return _attribute; }
            set { Debug.Assert(Positions.GetLength(0) == value.GetLength(0) && Positions.GetLength(1) == value.GetLength(1)); _attribute = value; }
        }
        public Vector3 Color = Vector3.UnitZ;
        public int SizeX { get { return Positions.GetLength(0); } }
        public int SizeY { get { return Positions.GetLength(1); } }

        public TileSurface()
        { Positions = new Vector3[0, 0]; }

        public TileSurface(LineSet lines)
        {
            // Assert "full" lineset.
            int length = lines[0].Length;
#if DEBUG
            foreach (Line l in lines.Lines)
            {
                Debug.Assert(l.Length == length);
            }
#endif
            Positions = new Vector3[length, lines.Length];

            for(int l = 0; l < lines.Length; ++l)
            {
                Array.Copy(lines[l].Positions, 0, Positions, length * l, length);

            }

        }

        public override Vector4[] Vertices
        {
            get
            {
                Vector4[] verts = new Vector4[Positions.Length];

                for(int i = 0; i < verts.Length; ++i)
                {
                    Vector3 pos = Positions[i % SizeX, (int)(i / SizeX)];
                    verts[i] = new Vector4(pos, Attribute?[i%SizeX, (int)(i/ SizeX)] ?? pos.Z);
                }
                return verts;
            }
        }

        public override int[] Indices
        {
            get
            {
                int[] idxs = new int[(SizeX - 1) * (SizeY - 1) * 6];
                for(int y = 0; y < SizeY -1; ++y)
                {
                    for(int x = 0; x < SizeX -1;++x)
                    {
                        int idx = x + y * (SizeX - 1);
                        int idxPos = x + y * SizeX;
                        idxs[idx + 0] = idxPos + 0;
                        idxs[idx + 1] = idxPos + 1;
                        idxs[idx + 2] = idxPos + SizeX + 1;

                        idxs[idx + 3] = idxPos + 0;
                        idxs[idx + 4] = idxPos + SizeX + 1;
                        idxs[idx + 5] = idxPos + SizeX;
                    }
                }

                return idxs;
            }
        }
    }
}
