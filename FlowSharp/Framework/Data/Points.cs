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
        /// <summary>
        /// Are the points saved in world position or grid position?
        /// </summary>
        public bool WorldPosition;
        private SlimDX.Vector3 _cellSize;
        private SlimDX.Vector3 _origin;

        public PointSet(Point[] points)
        {
            Points = points;
            WorldPosition = true;
        }

        public PointSet(Point[] points, SlimDX.Vector3 cellSize, SlimDX.Vector3 origin)
        {
            Points = points;
            WorldPosition = false;
            _cellSize = cellSize;
            _origin = origin;
        }

        public Point GetWorldPoint(int index)
        {
            if (WorldPosition)
                return Points[index];

            Point worldPoint = new Point()
            {
                Position = MathHelper.Mult(Points[index].Position, _cellSize) + _origin,
                Color = Points[index].Color,
                Radius = Points[index].Radius
            };

            return worldPoint;
        }
    }
}
