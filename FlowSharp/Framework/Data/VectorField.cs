using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SlimDX;

namespace FlowSharp
{
    /// <summary>
    /// Class for N dimensional vectorV fields, consisting of V=Length dimensional scalar fields.
    /// </summary>
    class VectorField
    {
        private Field[] _scalars;
        public virtual Field[] Scalars { get { return _scalars; } /*protected set { _scalars = value; }*/ }
        public Field this[int index]
        {
            get { return Scalars[index]; }
        }
        public FieldGrid Grid { get { return Scalars[0].Grid; } protected set { Scalars[0].Grid = value; } }
        public Index Size { get { return Grid.Size; } }

        /// <summary>
        /// Number of dimensions per vector.
        /// </summary>
        public virtual int NumVectorDimensions { get { return Scalars.Length; } }

        public float? InvalidValue
        {
            get { return Scalars[0].InvalidValue; }
            set { Scalars[0].InvalidValue = value; }
        }
        public float? TimeSlice { get { return _scalars[0].TimeSlice; } set { _scalars[0].TimeSlice = value; } }
        /// <summary>
        /// Pun. TODO: Better.
        /// </summary>
        /// <param name="fields"></param>
        public VectorField(Field[] fields)
        {
            _scalars = fields;
        }

        protected VectorField() { }

        /// <summary>
        /// Access field by scalar index.
        /// </summary>
        public virtual Vector Sample(int index)
        {
            Debug.Assert(index >= 0 && index < Size.Product(), "Index out of bounds: " + index + " not within [0, " + Size.Product() + ").");
            Vector vec = new Vector(NumVectorDimensions);
            for (int dim = 0; dim < NumVectorDimensions; ++dim)
                vec[dim] = Scalars[dim][index];

            return vec;
        }

        /// <summary>
        /// Access field by N-dimensional index.
        /// </summary>
        public virtual Vector Sample(Index gridPosition)
        {
            Debug.Assert(gridPosition < Size && gridPosition.IsPositive());

            int offsetScale = 1;
            int index = 0;

            // Have last dimension running fastest.
            for (int dim = 0; dim < NumVectorDimensions; ++dim)
            {
                index += offsetScale * gridPosition[dim];
                offsetScale *= Size[dim];
            }

            return Sample(index);
        }

        public virtual Vector Sample(Vector position, bool worldPosition = true)
        {
            return Grid.Sample(this, position, worldPosition);
        }


        /// <summary>
        /// Function to compute a new field based on an old one, point wise.
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public delegate Vector VFFunction(Vector v);

        public delegate Vector VFJFunction(Vector v, SquareMatrix J);

        public VectorField(VectorField field, VFJFunction function, int outputDim)
        {
            int scalars = outputDim;/*function(field.Sample(0), field.SampleDerivative(new Vector(0, field.Size.Length))).Length;*/
            _scalars = new ScalarField[scalars];
            FieldGrid gridCopy = field.Grid.Copy();

            for (int dim = 0; dim < scalars; ++dim)
            {
                Scalars[dim] = new ScalarField(gridCopy);
            }
            this.InvalidValue = field.InvalidValue;

            // Let's assume the field is always 2D... 
            //TODO: Make nD
            //for (int y = 0; y < Size[1]; ++y)
            //    for (int x = 0; x < Size[0]; ++x)
            bool first = true;
            GridIndex indexIterator = new GridIndex(field.Size);
            foreach (GridIndex index in indexIterator)
            {
                //Vector pos = (Vector)(Index)index;
                Vector v = field.Sample((int)index);

                if (v[0] == InvalidValue)
                {
                    for (int dim = 0; dim < Scalars.Length; ++dim)
                        Scalars[dim][(int)index] = (float)InvalidValue;
                    continue;
                }

                SquareMatrix J = field.SampleDerivative(index);
                if (first)
                {
                    first = false;
                    Console.WriteLine("V: " + v);
                    Console.WriteLine("J:\n" + J[0] + '\n' + J[1] + '\n' + J[2]);
                }
                Vector funcValue = function(v, J);

                for (int dim = 0; dim < Scalars.Length; ++dim)
                {
                    var vec = Scalars[dim];
                    Scalars[dim][(int)index] = funcValue[dim];
                }
            }
        }

        public SquareMatrix SampleDerivative(Vector position, bool worldPosition = true)
        {
            Debug.Assert(NumVectorDimensions == Size.Length);
            SquareMatrix jacobian = new SquareMatrix(NumVectorDimensions);

            for (int dim = 0; dim < NumVectorDimensions; ++dim)
            {
                float stepPos = Math.Min(Size[dim]-1, position[dim] + 0.5f) - position[dim];
                float stepMin = Math.Max(0, position[dim] - 0.5f) - position[dim];
                Vector samplePos = new Vector(position);
                samplePos[dim] += stepPos;
                jacobian[dim] = Sample(samplePos, false);

                samplePos[dim] += stepMin - stepPos;
                jacobian[dim] -= Sample(samplePos, false);

                jacobian[dim] /= (stepPos - stepMin);
            }

            return jacobian;
        }

        /// <summary>
        /// Get the derivative at a data point. Not checking for InvalidValue.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public SquareMatrix SampleDerivative(Index pos)
        {
            Debug.Assert(NumVectorDimensions == Size.Length);
            SquareMatrix jacobian = new SquareMatrix(NumVectorDimensions);

            // For all dimensions, so please reset each time.
            Index samplePos = new Index(pos);

            for (int dim = 0; dim < NumVectorDimensions; ++dim)
            {
                // Just to be sure, check thst no value was overwritten.
                int posCpy = samplePos[dim];

                // See whether a step to the right/left is possible.
                samplePos[dim]++;
                bool rightValid = (samplePos[dim] < Size[dim]) && Scalars[0].Sample(samplePos) != InvalidValue;
                samplePos[dim] -= 2;
                bool leftValid = (samplePos[dim] >= 0) && Scalars[0].Sample(samplePos) != InvalidValue;
                samplePos[dim]++;

                if (rightValid)
                {
                    if (leftValid)
                    {
                        // Regular case. Interpolate.
                        samplePos[dim]++;
                        jacobian[dim] = Sample(samplePos);
                        samplePos[dim] -= 2;
                        jacobian[dim] -= Sample(samplePos);
                        jacobian[dim] *= 0.5f;
                        samplePos[dim]++;
                    }
                    else
                    {
                        // Left border.
                        samplePos[dim]++;
                        jacobian[dim] = Sample(samplePos);
                        samplePos[dim]--;
                        jacobian[dim] -= Sample(samplePos);
                    }
                }
                else
                {
                    if (leftValid)
                    {
                        // Right border.
                        jacobian[dim] = Sample(samplePos);
                        samplePos[dim]--;
                        jacobian[dim] -= Sample(samplePos);
                        samplePos[dim]++;
                    }
                    else
                    {
                        // Weird case. 
                        jacobian[dim] = new Vector(0, NumVectorDimensions);
                    }
                }
                Debug.Assert(posCpy == samplePos[dim]);
            }

            return jacobian;
        }

        public delegate Vector3 PositionToColor(VectorField field, bool worldPos, Vector3 position);
        public PointSet<Point> ColorCodeArbitrary(LineSet lines, PositionToColor func)
        {
            Point[] points;
            points = new Point[lines.NumExistentPoints];
            int idx = 0;
            foreach(Line line in lines.Lines)
            {
                foreach(Vector3 pos in line.Positions)
                {
                    points[idx] = new Point() { Position = pos, Color = func(this, lines.WorldPosition, pos), Radius = lines.Thickness };
                    ++idx;
                }
            }

            return new PointSet<Point>(points, lines);
        }

        public abstract class Integrator
        {
            public enum Status
            {
                OK,
                CP,
                BORDER,
                INVALID
            };

            protected VectorField _field;
            protected bool _worldPosition = false;
            /// <summary>
            /// Based on CellSize equals (StepSize = 1).
            /// </summary>
            protected float _stepSize = 0.5f;
            protected Sign _direction = Sign.POSITIVE;
            protected bool _normalizeField = true;
            protected float _epsCriticalPoint = 0.000001f;


            public virtual VectorField Field { get { return _field; } set { _field = value; } }
            public virtual bool WorldPosition { get { return _worldPosition; } set { _worldPosition = value; } }
            /// <summary>
            /// Based on CellSize equals (StepSize = 1).
            /// </summary>
            public virtual float StepSize { get { return _stepSize; } set { _stepSize = value; } }
            public virtual Sign Direction { get { return _direction; } set { _direction = value; } }
            public virtual bool NormalizeField { get { return _normalizeField; } set { _normalizeField = value; } }
            public virtual float EpsCriticalPoint { get { return _epsCriticalPoint; } set { _epsCriticalPoint = value; } }
            public int MaxNumSteps = 2000;

            public abstract Status Step(Vector pos, out Vector stepped);
            /// <summary>
            /// Perform one step, knowing that the border is nearby.
            /// </summary>
            /// <param name="pos"></param>
            /// <param name="stepped"></param>
            public abstract bool StepBorder(Vector pos, out Vector stepped);
            public virtual StreamLine<Vector3> IntegrateLineForRendering(Vector pos)
            {
                StreamLine<Vector3> line = new StreamLine<Vector3>((int)(Field.Size.Max() * 1.5f / StepSize)); // Rough guess.
                line.WorldPosition = WorldPosition;

                Vector point;
                Vector next = pos;
                if (CheckPosition(next) != Status.OK)
                    return line;
                Status status;
                int step = -1;
                bool attachTimeZ = Field.NumVectorDimensions == 2 && Field.TimeSlice != 0;
                do
                {
                    step++;
                    point = next;
                    Vector3 posP = (Vector3)point;
                    if (attachTimeZ)
                        posP.Z = (float)Field.TimeSlice;
                    line.Points.Add(posP);
                    status = Step(point, out next);
                } while (status == Status.OK && step < MaxNumSteps);

                // If a border was hit, take a small step at the end.
                if (status == Status.BORDER)
                {
                    if (StepBorder(point, out next))
                        line.Points.Add((Vector3)next);
                }

                return line;
            }

            public LineSet Integrate<P>(PointSet<P> positions, bool forwardAndBackward = false) where P : Point
            {
                Debug.Assert(Field.NumVectorDimensions <= 3);
                Debug.Assert(positions.WorldPosition == WorldPosition, "Point set and integrator must both be in world OR grid position.");

                Line[] lines = new Line[positions.Length * (forwardAndBackward ? 2 : 1)];

                for (int index = 0; index < positions.Length; ++index)
                {
                    StreamLine<Vector3> streamline = IntegrateLineForRendering(((Vec3)positions.Points[index].Position).ToVec(Field.NumVectorDimensions));
                    lines[index] = new Line();
                    lines[index].Positions = streamline.Points.ToArray();
                }
                Vector3 color = (Vector3)Direction;
                if (forwardAndBackward)
                {
                    Direction = !Direction;
                    for (int index = 0; index < positions.Length; ++index)
                    {
                        StreamLine<Vector3> streamline = IntegrateLineForRendering((Vec3)positions.Points[index].Position);
                        lines[positions.Length + index] = new Line();
                        lines[positions.Length + index].Positions = streamline.Points.ToArray();
                    }
                    Direction = !Direction;
                    color = new Vector3(0.5f);
                }
                return new LineSet(lines, positions) { Color = color };
            }

            protected Status CheckPosition(Vector pos)
            {
                if (!Field.Grid.InGrid(pos, WorldPosition))
                    return Status.BORDER;
                if (Field.Scalars[0].Sample(pos, WorldPosition) == Field.InvalidValue)
                    return Status.INVALID;
                return Status.OK;
            }
        }

        public class StreamLine<T>
        {
            public List<T> Points;
            public bool WorldPosition = false;

            public StreamLine(int startCapacity = 100)
            {
                Points = new List<T>(startCapacity);
            }
        }

        public class StreamLine : StreamLine<Vector>
        {
            public StreamLine(int startCapacity = 100) : base(startCapacity) { }
        }

        public class IntegratorEuler : Integrator
        {
            public IntegratorEuler(VectorField field)
            {
                Field = field;
            }

            public override Status Step(Vector pos, out Vector stepped)
            {
                stepped = new Vector(pos);
                Vector dir = Field.Sample(pos, WorldPosition);

                if (!ScaleAndCheckVector(dir, out dir))
                    return Status.CP;

                stepped += dir;

                return CheckPosition(stepped);
            }

            public override bool StepBorder(Vector position, out Vector stepped)
            {
                stepped = new Vector(position);
                Vector dir = Field.Sample(position, WorldPosition) * (int)Direction;
                if (NormalizeField)
                    dir.Normalize();

                if (!WorldPosition)
                    dir /= Field.Grid.Scale;

                Vector pos = new Vector(position);
                if (WorldPosition)
                    pos = Field.Grid.ToGridPosition(pos);

                // How big is the smallest possible scale to hit a maximum border?
                float scale = (((Vector)Field.Size - pos) / dir).MinPos();
                scale = Math.Min(scale, (pos / dir).MinPos());

                if (scale >= StepSize)
                    return false;

                stepped += dir * scale;
                return true;
            }

            protected bool ScaleAndCheckVector(Vector vec, out Vector scaled)
            {
                scaled = vec;
                float length = vec.LengthEuclidean();
                if (length < EpsCriticalPoint)
                    return false;
                if (NormalizeField)
                    scaled = scaled / length;
                if (!WorldPosition)
                    scaled = scaled / Field.Grid.Scale;
                scaled *= StepSize * (int)Direction;
                return true;
            }
        } 

        public class IntegratorRK4 : IntegratorEuler
        {
            public IntegratorRK4(VectorField field) : base(field)
            { }

            public override Status Step(Vector pos, out Vector stepped)
            {
                stepped = new Vector(pos);
                Status status;

                // v0
                Vector v0 = Field.Sample(pos, WorldPosition);
                if (!ScaleAndCheckVector(v0, out v0))
                    return Status.CP;
                status = CheckPosition(pos + v0 / 2);
                if (status != Status.OK)
                    return status;

                // v1
                Vector v1 = Field.Sample(pos + v0 / 2, WorldPosition);
                if (!ScaleAndCheckVector(v1, out v1))
                    return Status.CP;
                status = CheckPosition(pos + v1 / 2);
                if (status != Status.OK)
                    return status;

                // v2
                Vector v2 = Field.Sample(pos + v1 / 2, WorldPosition);
                if (!ScaleAndCheckVector(v2, out v2))
                    return Status.CP;
                status = CheckPosition(pos + v2);
                if (status != Status.OK)
                    return status;

                // v3
                Vector v3 = Field.Sample(pos + v2, WorldPosition);
                if (!ScaleAndCheckVector(v3, out v3))
                    return Status.CP;
                status = CheckPosition(pos + v2);
                if (status != Status.OK)
                    return status;

                stepped += (v0 + (v1 + v2)  * 2 + v3) / 6;

                return CheckPosition(stepped);
            }
        }
    }
}

// &PARM04
// ygOrigin = 9.0,
// xgOrigin = 32.0,
// delY   =  210*0.1,
// delX   =  450*0.1,