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
            Debug.Assert(index >= 0 && index < data.NumVectorDimensions);
            float[] raw = new float[data.Length];
            for (int e = 0; e < data.Length; ++e)
                raw[e] = data[e][index];
            return raw;
        }
    }
}
