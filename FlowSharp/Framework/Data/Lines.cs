using SlimDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace FlowSharp
{
    class Line
    {
        public Vector3[] Positions;
        public int Length { get { return Positions.Length; } }
        public VectorField.Integrator.Status Status;
        public float LineLength = 0;

        public float DistanceToPointInZ(Vector3 position)
        {
            // Slow linear search. Khalas.
            int i = 0;
            for(; i < Positions.Length - 1; ++i)
            {
                if ((Positions[i].Z - position.Z) * (Positions[i + 1].Z - position.Z) <= 0)
                    break;
            }
            if (i == Length - 2)
                return float.MaxValue;

            Vector3 p0 = Positions[i];
            Vector3 p1 = Positions[i + 1];
            float t = (position.Z - p0.Z) / (p1.Z - p0.Z);
            Vector3 zNearest = (1 - t) * p0 + t * p1;
            Debug.Assert(Math.Abs(position.Z - zNearest.Z) < 0.00000001f);

            return (position - zNearest).Length();
        }
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

        public PointSet<EndPoint> GetEndPoints()
        {
            EndPoint[] points = new EndPoint[Lines.Length];
            for (int idx = 0; idx < points.Length; ++idx)
                points[idx] = new EndPoint() { Position = Lines[idx].Positions.Last(), LengthLine = Lines[idx].LineLength, Status = Lines[idx].Status };
            return new PointSet<EndPoint>(points);
        }
        public PointSet<EndPoint> GetEndPoints(VectorField.Integrator.Status select)
        {
            List<EndPoint> points = new List<EndPoint>(Lines.Length);
            for (int idx = 0; idx < Lines.Length; ++idx)
            {
                Line line = Lines[idx];
                if(line.Length > 0 && line.Status == select)
                    points.Add(new EndPoint() { Position = line.Positions.Last(), LengthLine = line.LineLength, Status = line.Status });
            }
            return new PointSet<EndPoint>(points.ToArray());
        }

        public void FlattenLines(float level = 0)
        {
            foreach (Line line in Lines)
                for (int p = 0; p < line.Positions.Length; ++p)
                    line.Positions[p].Z = level;
        }
    }
}
