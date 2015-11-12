using SlimDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class Line
    {
        public Vector3[] Positions;
        public int Length { get { return Positions.Length; } }
    }

    class LineSet
    {
        protected Line[] _lines;
        
        public Line[] Lines
        {
            get { return _lines; }
            protected set
            {
                _lines = value;
                //NumPoints = 0;
                //foreach (Line line in _lines)
                //{
                //    NumPoints += Math.Max(line.Positions.Length, 2);
                //    NumExistentPoints += line.Positions.Length;
                //}
            }
        }
        public int NumPoints
        {
            get
            {
                int sum = 0;
                foreach (Line line in _lines)
                {
                    sum += Math.Max(line.Positions.Length, 1);
                }
                return sum;
            }
        }
        public int NumExistentPoints
        {
            get
            {
                int sum = 0;
                foreach (Line line in _lines)
                {
                    sum += line.Positions.Length;
                }
                return sum;
            }
        }
        public virtual Vector3 Color { get; set; } = SlimDX.Vector3.UnitY;
        public virtual float Thickness { get; set; } = 0.1f;

        public LineSet(Line[] lines)
        {
            Lines = lines;
        }
    }
}
