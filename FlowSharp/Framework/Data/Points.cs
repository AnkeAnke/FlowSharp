using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class Point
    {
        public SlimDX.Vector3 Position;
        public SlimDX.Vector3 Color = SlimDX.Vector3.UnitY;
        public float Radius = 0.01f;
    }

    /// <summary>
    /// Object containing multiple points.
    /// </summary>
    class PointSet
    {
        public Point[] Points;
    }
}
