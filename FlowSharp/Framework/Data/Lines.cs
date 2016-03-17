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
        public float[] Attribute;
        public int Length { get { return Positions.Length; } }
        public VectorField.Integrator.Status Status;
        public float LineLength = 0;

        public float DistanceToPointInZ(Vector3 position, out Vector3 zNearest)
        {
            zNearest = Vector3.Zero;

            if (Length < 2)
                return float.MaxValue;
            // Slow linear search. Khalas.
            int i = 0;
            for(; i < Positions.Length - 1; ++i)
            {
                // One point above, one below.
                if ((Positions[i].Z - position.Z) * (Positions[i + 1].Z - position.Z) <= 0)
                    break;
            }

            // Did not find any?
            if (i == Length - 1)
                return float.MaxValue;

            Vector3 p0 = Positions[i];
            Vector3 p1 = Positions[i + 1];
            float t = (position.Z - p0.Z) / (p1.Z - p0.Z);
            zNearest = (1 - t) * p0 + t * p1;
            Debug.Assert(Math.Abs(position.Z - zNearest.Z) < 0.0001f);

            return (position - zNearest).Length();
        }

        public float DistanceToPointInZ(Vector3 position)
        {
            Vector3 tmp;
            return DistanceToPointInZ(position, out tmp);
        }
        //public Vector3 GetPointInZ(float z)
        //{
        //    // Slow linear search. Khalas.
        //    int i = 0;
        //    for (; i < Positions.Length - 1; ++i)
        //    {
        //        // One point above, one below.
        //        if ((Positions[i].Z - z) * (Positions[i + 1].Z - z) <= 0)
        //            break;
        //    }
        //    if (i == Length - 2)
        //        return float.MaxValue;

        //    Vector3 p0 = Positions[i];
        //    Vector3 p1 = Positions[i + 1];
        //    float t = (position.Z - p0.Z) / (p1.Z - p0.Z);
        //    Vector3 zNearest = (1 - t) * p0 + t * p1;
        //    Debug.Assert(Math.Abs(position.Z - zNearest.Z) < 0.00000001f);

        //    return (position - zNearest).Length();
        //}
        public Line(Line cpy)
        {
            Positions = new Vector3[cpy.Length];
            Array.Copy(cpy.Positions, Positions, cpy.Length);
            if(cpy.Attribute != null)
            {
                Attribute = new float[cpy.Length];
                Array.Copy(cpy.Attribute, Attribute, cpy.Length);
            }
            Status = cpy.Status;
            LineLength = cpy.LineLength;
        }
        public Line() { }
        public Vector3 this[int index] { get { return Positions[index]; } set { Positions[index] = value; } }
        public Vector3 Value(float index)
        {
            Vector3 p0 = this[(int)index];
            Vector3 p1 = this[(int)index + 1];
            float t = index - (int)index;
            return (1.0f - t) * p0 + t * p1;
        }
        public void CutHeight(float z)
        {
            for (int p = 0; p < Length; ++p)
            {
                // if(Attribute?[p] > z)
                if ((Attribute != null && Attribute[p] > z) || (Attribute == null && this[p].Z > z))
                {
                    Array.Resize(ref Positions, p);
                    return;
                }
            }
        }

        public void Resize(int length)
        {
            Array.Resize(ref Positions, length);
            if (Attribute != null)
                Array.Resize(ref Attribute, length);
        }
    }

    class LineSet
    {
        protected Line[] _lines;
        public Line this[int index] { get { return _lines[index]; } set { _lines[index] = value; } }
        public int Length { get { return _lines.Length; } }
        
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
                    sum += Math.Max(line.Length, 1);
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
                    sum += line.Length;
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

        public LineSet(LineSet cpy, int start = 0, int length = -1) : base()
        {
            int numLines = length > 0 ? length : cpy.Length - start;
            this.Color = cpy.Color;
            this.Lines = new Line[numLines];
            this.Thickness = cpy.Thickness;
            for(int line = 0; line < numLines; ++line)
            {
                Lines[line] = new Line(cpy.Lines[start + line]);
            }
        }

        public PointSet<EndPoint> GetValidEndPoints()
        {
            EndPoint[] points = new EndPoint[Lines.Length];
            int currentWriteIdx = 0;
            for (int idx = 0; idx < points.Length; ++idx)
                if(Lines[idx].Positions.Length > 0)
                    points[currentWriteIdx++] = new EndPoint() { Position = Lines[idx].Positions.Last(), LengthLine = Lines[idx].LineLength, Status = Lines[idx].Status };
            Array.Resize(ref points, currentWriteIdx);
            return new PointSet<EndPoint>(points);
        }

        public PointSet<EndPoint> GetAllEndPoints()
        {
            EndPoint[] points = new EndPoint[Lines.Length];
            for (int idx = 0; idx < points.Length; ++idx)
                if (Lines[idx].Positions.Length > 0)
                    points[idx] = new EndPoint() { Position = Lines[idx].Positions.Last(), LengthLine = Lines[idx].LineLength, Status = Lines[idx].Status };
                else
                    points[idx] = null;
            
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

        public void Reverse()
        {
            foreach(Line l in Lines)
            {
                Array.Reverse(l.Positions);
            }
        }

        public void Append(LineSet add)
        {
            Debug.Assert(add.Length == Length, "Can only concat two equal sized line sets!");
            for (int i = 0; i < Length; i++)
            {
                Debug.Assert(add[i][0] == this[i].Positions.Last());
                Vector3[] longline = new Vector3[add[i].Length + this[i].Length - 1];
                Array.Copy(this[i].Positions, longline, this[i].Length);
                Array.Copy(add[i].Positions, 1, longline, this[i].Length, add[i].Length - 1);
                this[i].Positions = longline;
            }
        }

        public void Cut(int index)
        {
            Array.Resize(ref _lines, index);
        }

        public void CutAllHeight(float maxZ)
        {
            foreach(Line l in _lines)
            {
                    l.CutHeight(maxZ);
            }
        }
    }
}
