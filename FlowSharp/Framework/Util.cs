using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class Util
    {
        public static SlimDX.Vector3 Mult(SlimDX.Vector3 a, SlimDX.Vector3 b)
        {
            return new SlimDX.Vector3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
        }

        public static SlimDX.Vector3 Div(SlimDX.Vector3 a, SlimDX.Vector3 b)
        {
            return new SlimDX.Vector3(a.X / b.X, a.Y / b.Y, a.Z / b.Z);
        }

        public static SlimDX.Vector3 Convert(SlimDX.Vector4 vec)
        {
            return new SlimDX.Vector3(vec.X, vec.Y, vec.Z);
        }

        public static SlimDX.Vector4 Convert(SlimDX.Vector3 vec)
        {
            return new SlimDX.Vector4(vec.X, vec.Y, vec.Z, 0);
        }

        //public static SlimDX.Matrix MatrixFromColumns(SlimDX.Vector3)
        public static void FlipEndian(float[] data)
        {
            int length = data.Length;
            // Rewrite the data in memory.
            unsafe
            {
                fixed (float* floatPtr = data)
                {
                    byte* byteData = (byte*)floatPtr;

                    // Swap beginning and ending float after multiplication.
                    for (int i = 0; i < length; ++i)
                    {
                        byte tmp = byteData[i * 4];
                        byteData[i * 4] = byteData[i * 4 + 3];
                        byteData[i * 4 + 3] = tmp;

                        tmp = byteData[i * 4 + 1];
                        byteData[i * 4 + 1] = byteData[i * 4 + 2];
                        byteData[i * 4 + 2] = tmp;

                    }
                }
            }
        }

        public static float[] GetChannel(VectorData data, int index)
        {
            Debug.Assert(index >= 0 && index < data.VectorLength);
            float[] raw = new float[data.Length];
            for (int e = 0; e < data.Length; ++e)
                raw[e] = data[e][index];
            return raw;
        }

        public static Index GetSize<T>(T[,,] data)
        {
            return new Index(new int[] { data.GetLength(0), data.GetLength(1), data.GetLength(2) });
        }

        public static void FloodFill(bool[,,] data, Index start)
        {
            HashSet<Index> todo = new HashSet<Index>();
            todo.Add(start);
            bool predicate = data[start[0], start[1], start[2]];
            Index size = GetSize(data);


            while (todo.Count > 0)
            {
                // Work on the stack.
                Index pos = todo.First();
                todo.Remove(pos);

                // Possibly add neighbors.
                for (int dim = 0; dim < 3; ++dim)
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        Index neigh = new Index(pos);
                        neigh[dim] += sign;
                        if (neigh[dim] < 0 || neigh[dim] >= size[dim])
                            continue;
                        if (data[neigh[0], neigh[1], neigh[2]] == predicate)
                        {
                            todo.Add(neigh);
                            data[neigh[0], neigh[1], neigh[2]] = !predicate;
                        }
                    }
            }
        }
    }
}
