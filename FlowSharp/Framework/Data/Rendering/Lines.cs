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
        public Vector4[] Positions;
        public float[] Attribute;
        public int Length { get { return Positions.Length; } }
        public VectorField.Integrator.Status Status;
        public float LineLength = 0;
        public Vector EndPoint;

        public Vector4 Last { get { return Positions[Length - 1]; } }

        public float DistanceToPointInZ(Vector4 position, out Vector4 zNearest)
        {
            zNearest = Vector4.Zero;

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

            Vector4 p0 = Positions[i];
            Vector4 p1 = Positions[i + 1];
            float t = (position.Z - p0.Z) / (p1.Z - p0.Z);
            zNearest = (1 - t) * p0 + t * p1;
            Debug.Assert(Math.Abs(position.Z - zNearest.Z) < 0.0001f);

            return (position - zNearest).Length();
        }

        public float DistanceToPointInZ(Vector4 position)
        {
            Vector4 tmp;
            return DistanceToPointInZ(position, out tmp);
        }

        public int GetLastBelowZ(float z)
        {
            if (Length < 2)
                return -1;
            // Slow linear search. Khalas.
            int i = 0;
            for (; i < Positions.Length - 1; ++i)
            {
                // One point above, one below.
                if ((Positions[i].Z - z) * (Positions[i + 1].Z - z) <= 0)
                    break;
            }

            // Did not find any?
            if (i == Length - 1)
                return -1;

            return i;
        }

        public Vector4? SampleZ(float z)
        {
            int i = GetLastBelowZ(z);
            if (i < 0)
                return null;
            Vector4 p0 = Positions[i];
            Vector4 p1 = Positions[i + 1];
            float t = (z - p0.Z) / (p1.Z - p0.Z);
            Vector4 integrated = (1 - t) * p0 + t * p1;
            Debug.Assert(Math.Abs(z - integrated.Z) < 0.0001f);

            return integrated;
        }

        public Line(Line cpy)
        {
            Positions = new Vector4[cpy.Length];
            Array.Copy(cpy.Positions, Positions, cpy.Length);
            if(cpy.Attribute != null)
            {
                Attribute = new float[cpy.Length];
                Array.Copy(cpy.Attribute, Attribute, cpy.Length);
            }
            Status = cpy.Status;
            LineLength = cpy.LineLength;
            EndPoint = new Vector(cpy.EndPoint);
        }
        public Line() { }
        public Line(int size) { Positions = new Vector4[size]; }
        public Vector4 this[int index] { get { return Positions[index]; } set { Positions[index] = value; } }
        public Vector4 Value(float index)
        {
            Vector4 p0 = this[(int)index];
            Vector4 p1 = this[(int)index + 1];
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

        public Line Append(Line other)
        {
            Line app = new Line(Length + other.Length);
            Array.Copy(Positions, 0, app.Positions, 0, Length);
            Array.Copy(other.Positions, 0, app.Positions, Length, other.Length);
            if (Attribute != null && other.Attribute != null)
            {
                app.Attribute = new float[Length + other.Length];
                Array.Copy(Attribute, 0, app.Attribute, 0, Length);
                Array.Copy(other.Attribute, 0, app.Attribute, Length, other.Length);
            }

            app.LineLength = LineLength + other.LineLength;
            app.EndPoint = other.EndPoint;
            app.Status = other.Status;
            return app;
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

        public PointSet<InertialPoint> GetValidEndPoints()
        {
            InertialPoint[] points = new InertialPoint[Lines.Length];
            int currentWriteIdx = 0;
            for (int idx = 0; idx < points.Length; ++idx)
                if (Lines[idx].Positions.Length > 0)
                {
                    Vector3 inertia = (Lines[idx].Positions.Length >= 2) ?
                        Util.Convert(Lines[idx].Positions.Last() - Lines[idx].Positions[Lines[idx].Length - 2]) : Vector3.Zero;
                    points[currentWriteIdx++] = new InertialPoint()
                    {
                        Position = Lines[idx].Positions.Last(),
                        Inertia = inertia,
                        //LengthLine = Lines[idx].LineLength,
                        Status = Lines[idx].Status
                    };
                }
            Array.Resize(ref points, currentWriteIdx);
            return new PointSet<InertialPoint>(points);
        }

        public PointSet<InertialPoint> GetAllEndPoints()
        {
            List<InertialPoint> points = new List<InertialPoint>(Lines.Length);
            for (int idx = 0; idx < Lines.Length; ++idx)
                if (Lines[idx].Positions.Length > 0)
                {
                    Vector3 inertia = (Lines[idx].Positions.Length >= 2) ?
                        Util.Convert(Lines[idx].Positions.Last() - Lines[idx].Positions[Lines[idx].Length - 2]) : Vector3.Zero;
                    points.Add(new InertialPoint()
                    {
                        Position = Lines[idx].Positions.Last(),
                        Inertia = inertia,
                        //LengthLine = Lines[idx].LineLength,
                        Status = Lines[idx].Status,
                        Radius = Thickness * 2f
                    });
                }
            
            return new PointSet<InertialPoint>(points.ToArray());
        }

        public List<Vector> GetEndPoints(VectorField.Integrator.Status select)
        {
            List<Vector> points = new List<Vector>(Lines.Length);
            for (int idx = 0; idx < Lines.Length; ++idx)
            {
                Line line = Lines[idx];

                //if (line.Length > 0)
                //    Console.WriteLine($"Line {idx} ends with {line.Status}\n\tPoint {line.EndPoint}, ie {line.Last}");
                //Vector3 inertia = (Lines[idx].Positions.Length >= 2) ?
                //        Util.Convert(Lines[idx].Positions.Last() - Lines[idx].Positions[Lines[idx].Length - 2]) : Vector3.Zero;
                if (line.Length > 0 && line.Status == select)
                    points.Add(line.EndPoint ?? new Vector(line.Positions.Last()));
            }
            return points;
        }

        //public void TimeComponentToAttribute()
        //{
        //    for 
        //}

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
                Vector4[] longline = new Vector4[add[i].Length + this[i].Length - 1];
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
