using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class MathHelper
    {
        public static SlimDX.Vector3 Mult(SlimDX.Vector3 a, SlimDX.Vector3 b)
        {
            return new SlimDX.Vector3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
        }


    }
}
