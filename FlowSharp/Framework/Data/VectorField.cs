using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SlimDX;

namespace FlowSharp
{
    //abstract class AnyVectorField
    //{
    //        #region VF
    //        public FieldGrid Grid { get; protected set; }
    //        public Index Size { get { return Grid.Size; } }

    //        #region Delegates
    //        /// <summary>
    //        /// Function to compute a new field based on an old one, point wise.
    //        /// </summary>
    //        /// <param name="v"></param>
    //        /// <returns></returns>
    //        public delegate Vector VFFunction(Vector v);

    //        public delegate Vector VFJFunction(Vector v, SquareMatrix J);

    //        public delegate Vector3 PositionToColor(AnyVectorField field, Vector3 position);
    //        #endregion Delegates

    //        #region Access
    //        private VectorData Data { get; set; }
    //        /// <summary>
    //        /// Number of dimensions per vector.
    //        /// </summary>
    //        public virtual int NumVectorDimensions { get { return _data.VectorLength; } }
    //        public int NumDimensions { get { return Size.Length; } }

    //        public virtual float? InvalidValue { get; set; }
    //        public virtual float? TimeSlice { get; set; }

    //        /// <summary>
    //        /// Access field by scalar index.
    //        /// </summary>
    //        public virtual Vector this[int index] { get { return _data[index]; } protected set { _data[index] = value; } }
    //        /// <summary>
    //        /// Access field by N-dimensional index.
    //        /// </summary>
    //        public Vector this[Index gridPosition]
    //        {
    //            get
    //            {
    //                Debug.Assert(gridPosition < Size && gridPosition.IsPositive());

    //                int offsetScale = 1;
    //                int index = 0;

    //                // Have last dimension running fastest.
    //                for (int dim = 0; dim < NumVectorDimensions; ++dim)
    //                {
    //                    index += offsetScale * gridPosition[dim];
    //                    offsetScale *= Size[dim];
    //                }

    //                return this[index];
    //            }
    //            protected set
    //            {
    //                Debug.Assert(gridPosition < Size && gridPosition.IsPositive());

    //                int offsetScale = 1;
    //                int index = 0;

    //                // Have last dimension running fastest.
    //                for (int dim = 0; dim < NumVectorDimensions; ++dim)
    //                {
    //                    index += offsetScale * gridPosition[dim];
    //                    offsetScale *= Size[dim];
    //                }

    //                this[index] = value;
    //            }
    //        }
    //        public bool IsValid(Index pos)
    //        {
    //            return Sample(pos)[0] != InvalidValue;
    //        }

    //        public bool IsValid(int pos)
    //        {
    //            return Sample(pos)[0] != InvalidValue;
    //        }

    //        public VectorField(DataType data, FieldGrid grid)
    //        {
    //            _data = data;
    //            Grid = grid;
    //            SpreadInvalidValue();
    //        }

    //        protected VectorField() { }

    //        ///// <summary>
    //        ///// Access field by scalar index.
    //        ///// </summary>
    //        //public override Vector Sample(int index)
    //        //{
    //        //    return _data[index];
    //        //}

    //        public VectorField(VectorField<DataType> field, VFJFunction function, int outputDim, bool needJacobian = true)
    //        {
    //            int scalars = outputDim;/*function(field.Sample(0), field.SampleDerivative(new Vector(0, field.Size.Length))).Length;*/
    //            _data = new DataType();
    //            _data.SetSize(field.NumVectorDimensions, field.Size.Product());
    //            FieldGrid gridCopy = field.Grid.Copy();

    //            // In case the input field was time dependant, this one is not. Still, we keep the size and origin of the time as new dimension!
    //            gridCopy.TimeDependant = false;

    //            this.InvalidValue = field.InvalidValue;

    //            GridIndex indexIterator = new GridIndex(field.Size);
    //            foreach (GridIndex index in indexIterator)
    //            {
    //                Vector v = field[(int)index];

    //                if (v[0] == InvalidValue)
    //                {
    //                    for (int dim = 0; dim < NumVectorDimensions; ++dim)
    //                        _data[(int)index][dim] = (float)InvalidValue;
    //                    continue;
    //                }

    //                SquareMatrix J = needJacobian ? field.SampleDerivative(index) : null;
    //                Vector funcValue = function(v, J);

    //                _data[(int)index] = funcValue;
    //                //for (int dim = 0; dim < NumVectorDimensions; ++dim)
    //                //{
    //                //    var vec = Scalars[dim];
    //                //    _data[(int)index][dim] = funcValue[dim];
    //                //}
    //            }
    //        }

    //        public bool IsValid(Vector pos)
    //        {
    //            float[] weights;
    //            int[] neighbors = Grid.FindAdjacentIndices(pos, out weights);
    //            foreach (int neighbor in neighbors)
    //                for (int n = 0; n < NumVectorDimensions; ++n)
    //                    if (_data[neighbor][n] == InvalidValue)
    //                        return false;
    //            return true;
    //        }
    //        /// <summary>
    //        /// Access field by scalar index.
    //        /// </summary>
    //        public Vector Sample(int index) { return this[index]; }

    //        /// <summary>
    //        /// Access field by N-dimensional index.
    //        /// </summary>
    //        public virtual Vector Sample(Index gridPosition)
    //        {
    //            return this[gridPosition];
    //        }

    //        public virtual Vector Sample(Vector position)
    //        {
    //            return Grid.Sample(this, position);
    //        }

    //        /// <summary>
    //        /// Get the derivative at a data point. Not checking for InvalidValue.
    //        /// </summary>
    //        /// <param name="pos"></param>
    //        /// <returns></returns>
    //        public SquareMatrix SampleDerivative(Index pos)
    //        {
    //            //Debug.Assert(NumVectorDimensions == Size.Length);
    //            int size = Math.Max(Size.Length, NumVectorDimensions);
    //            SquareMatrix jacobian = new SquareMatrix(size);

    //            // For all dimensions, so please reset each time.
    //            Index samplePos = new Index(pos);

    //            for (int dim = 0; dim < Size.Length; ++dim)
    //            {
    //                // Just to be sure, check thst no value was overwritten.
    //                int posCpy = samplePos[dim];

    //                // See whether a step to the right/left is possible.
    //                samplePos[dim]++;
    //                bool rightValid = (samplePos[dim] < Size[dim]) && IsValid(samplePos); //Scalars[0].Sample(samplePos) != InvalidValue;
    //                samplePos[dim] -= 2;
    //                bool leftValid = (samplePos[dim] >= 0) && IsValid(samplePos); //Scalars[0].Sample(samplePos) != InvalidValue;
    //                samplePos[dim]++;

    //                if (rightValid)
    //                {
    //                    if (leftValid)
    //                    {
    //                        // Regular case. Interpolate.
    //                        samplePos[dim]++;
    //                        jacobian[dim] = this[samplePos].ToVec(size);
    //                        samplePos[dim] -= 2;
    //                        jacobian[dim] -= this[samplePos].ToVec(size);
    //                        jacobian[dim] *= 0.5f;
    //                        samplePos[dim]++;
    //                    }
    //                    else
    //                    {
    //                        // Left border.
    //                        samplePos[dim]++;
    //                        jacobian[dim] = this[samplePos].ToVec(size);
    //                        samplePos[dim]--;
    //                        jacobian[dim] -= this[samplePos].ToVec(size);
    //                    }
    //                }
    //                else
    //                {
    //                    if (leftValid)
    //                    {
    //                        // Right border.
    //                        jacobian[dim] = this[samplePos].ToVec(size);
    //                        samplePos[dim]--;
    //                        jacobian[dim] -= this[samplePos].ToVec(size);
    //                        samplePos[dim]++;
    //                    }
    //                    else
    //                    {
    //                        // Weird case. 
    //                        jacobian[dim] = new Vector(0, size);
    //                    }
    //                }
    //                Debug.Assert(posCpy == samplePos[dim]);
    //            }

    //            return jacobian;
    //        }

    //        public SquareMatrix SampleDerivative(Vector position)
    //        {
    //            //Debug.Assert(NumVectorDimensions == Size.Length);
    //            SquareMatrix jacobian = new SquareMatrix(NumVectorDimensions);

    //            for (int dim = 0; dim < Size.Length; ++dim)
    //            {
    //                float stepPos = Math.Min(Size[dim] - 1, position[dim] + 0.5f) - position[dim];
    //                float stepMin = Math.Max(0, position[dim] - 0.5f) - position[dim];
    //                Vector samplePos = new Vector(position);
    //                samplePos[dim] += stepPos;
    //                jacobian[dim] = Sample(samplePos);

    //                samplePos[dim] += stepMin - stepPos;
    //                jacobian[dim] -= Sample(samplePos);

    //                jacobian[dim] /= (stepPos - stepMin);
    //            }

    //            return jacobian;
    //        }

    //        #endregion Access
    //        /// <summary>
    //        /// Make sure the invalid value is in the first scalar field if it is anywhere.
    //        /// </summary>
    //        protected void SpreadInvalidValue()
    //        {
    //            for (int pos = 0; pos < Size.Product(); ++pos)
    //            {
    //                for (int dim = 1; dim < NumVectorDimensions; ++dim)
    //                    if (this[pos][dim] == InvalidValue)
    //                        this[pos][0] = (float)InvalidValue;
    //            }
    //        }

    //        public VectorField<DataType> GetSlice(int posInLastDimension)
    //        {
    //            // Copy the grid - one dimension smaller!
    //            RectlinearGrid grid = Grid as RectlinearGrid;
    //            Index newSize = new Index(Size.Length - 1);
    //            Array.Copy(Size.Data, newSize.Data, newSize.Length);

    //            FieldGrid sliceGrid = new RectlinearGrid(newSize);

    //            DataType newData = (DataType)_data.GetSliceInLastDimension(posInLastDimension, Size);
    //            return new VectorField<DataType>(newData, sliceGrid);
    //        }

    //        public PointSet<Point> ColorCodeArbitrary(LineSet lines, PositionToColor func)
    //        {
    //            Point[] points;
    //            points = new Point[lines.NumExistentPoints];
    //            int idx = 0;
    //            foreach (Line line in lines.Lines)
    //            {
    //                foreach (Vector3 pos in line.Positions)
    //                {
    //                    points[idx] = new Point() { Position = pos, Color = func(this, pos), Radius = lines.Thickness };
    //                    ++idx;
    //                }
    //            }

    //            return new PointSet<Point>(points);
    //        }

    //        public DataStream GetDataStream() { throw new NotImplementedException(); }
    //        public virtual bool IsUnsteady() { return this.TimeSlice != null; }

    //        public void ChangeEndian() { _data.ChangeEndian(); }

    //        public void ScaleToGrid(float dimwiseScale)
    //        {
    //            for (int n = 0; n < _data.Length; ++n)
    //                if (IsValid(n))
    //                    for (int dim = 0; dim < _data.VectorLength; ++dim)
    //                        _data[n][dim] *= dimwiseScale;
    //        }

    //        #endregion VF

    //        #region Integrators
    //        public abstract class Integrator
    //        {
    //            public enum Status
    //            {
    //                OK,
    //                CP,
    //                BORDER,
    //                TIME_BORDER,
    //                INVALID
    //            };

    //            protected VectorField<DataType> _field;
    //            /// <summary>
    //            /// Based on CellSize equals (StepSize = 1).
    //            /// </summary>
    //            protected float _stepSize = 0.2f;
    //            protected Sign _direction = Sign.POSITIVE;
    //            protected bool _normalizeField = false;
    //            protected float _epsCriticalPoint = 0.00000001f;


    //            public virtual VectorField<DataType> Field { get { return _field; } set { _field = value; } }
    //            /// <summary>
    //            /// Based on CellSize equals (StepSize = 1).
    //            /// </summary>
    //            public virtual float StepSize { get { return _stepSize; } set { _stepSize = value; } }
    //            public virtual Sign Direction { get { return _direction; } set { _direction = value; } }
    //            public virtual bool NormalizeField { get { return _normalizeField; } set { _normalizeField = value; } }
    //            public virtual float EpsCriticalPoint { get { return _epsCriticalPoint; } set { _epsCriticalPoint = value; } }
    //            public int MaxNumSteps = 2000;

    //            public abstract Status Step(Vector pos, out Vector stepped, out float stepLength);
    //            /// <summary>
    //            /// Perform one step, knowing that the border is nearby.
    //            /// </summary>
    //            /// <param name="pos"></param>
    //            /// <param name="stepped"></param>
    //            public abstract bool StepBorder(Vector pos, out Vector stepped, out float stepLength);
    //            public abstract bool StepBorderTime(Vector pos, float timeBorder, out Vector stepped, out float stepLength);
    //            public virtual StreamLine<Vector3> IntegrateLineForRendering(Vector pos, float? maxTime = null)
    //            {
    //                StreamLine<Vector3> line = new StreamLine<Vector3>((int)(Field.Size.Max() * 1.5f / StepSize)); // Rough guess.
    //                                                                                                               //line.Points.Add((Vector3)pos);
    //                float timeBorder = maxTime ?? (((Field as VectorFieldBlockUnsteady) == null) ? float.MaxValue : (Field.Grid.TimeOrigin ?? 0) + Field.Size.T);

    //                Vector point;
    //                Vector next = pos;
    //                if (CheckPosition(next) != Status.OK)
    //                {
    //                    //line.Points.Add((Vector3)pos);
    //                    return line;
    //                }
    //                Status status;
    //                int step = -1;
    //                bool attachTimeZ = Field.NumVectorDimensions == 2 && Field.TimeSlice != 0;
    //                float stepLength;
    //                do
    //                {
    //                    step++;
    //                    point = next;
    //                    Vector3 posP = (Vector3)point;
    //                    if (attachTimeZ)
    //                        posP.Z = (float)Field.TimeSlice;
    //                    line.Points.Add(posP);
    //                    status = Step(point, out next, out stepLength);
    //                    if (status == Status.OK)
    //                        line.LineLength += stepLength;
    //                } while (status == Status.OK && step < MaxNumSteps && next.T <= timeBorder);

    //                // If a border was hit, take a small step at the end.
    //                if (status == Status.BORDER)
    //                {
    //                    if (StepBorder(point, out next, out stepLength))
    //                    {
    //                        line.Points.Add((Vector3)next);
    //                        line.LineLength += stepLength;
    //                    }
    //                }

    //                // If the time was exceeded, take a small step at the end.
    //                if (status == Status.OK && next.T > timeBorder)
    //                {
    //                    status = Status.TIME_BORDER;

    //                    if (StepBorderTime(point, timeBorder, out next, out stepLength))
    //                    {
    //                        line.Points.Add((Vector3)next);
    //                        line.LineLength += stepLength;
    //                    }

    //                    if (next[1] < 0)
    //                        Console.WriteLine("Wut?");
    //                }
    //                // Single points are confusing for everybody.
    //                if (line.Points.Count < 2)
    //                {
    //                    line.Points.Clear();
    //                    line.LineLength = 0;
    //                }
    //                line.Status = status;
    //                return line;
    //            }

    //            public LineSet[] Integrate<P>(PointSet<P> positions, bool forwardAndBackward = false, float? maxTime = null) where P : Point
    //            {
    //                Debug.Assert(Field.NumVectorDimensions <= 3);

    //                Line[] lines = new Line[positions.Length];
    //                Line[] linesReverse = new Line[forwardAndBackward ? positions.Length : 0];

    //                LineSet[] result = new LineSet[forwardAndBackward ? 2 : 1];

    //                for (int index = 0; index < positions.Length; ++index)
    //                {

    //                    StreamLine<Vector3> streamline = IntegrateLineForRendering(((Vec3)positions.Points[index].Position).ToVec(Field.NumVectorDimensions), maxTime);
    //                    lines[index] = new Line();
    //                    lines[index].Positions = streamline.Points.ToArray();
    //                    lines[index].Status = streamline.Status;
    //                    lines[index].LineLength = streamline.LineLength;

    //                    if ((index) % (positions.Length / 10) == 0)
    //                        Console.WriteLine("Integrated {0}/{1} lines. {2}%", index, positions.Length, ((float)index * 100) / positions.Length);
    //                }
    //                result[0] = new LineSet(lines) { Color = (Vector3)Direction };

    //                if (forwardAndBackward)
    //                {
    //                    Direction = !Direction;
    //                    for (int index = 0; index < positions.Length; ++index)
    //                    {
    //                        StreamLine<Vector3> streamline = IntegrateLineForRendering((Vec3)positions.Points[index].Position, maxTime);
    //                        linesReverse[index] = new Line();
    //                        linesReverse[index].Positions = streamline.Points.ToArray();
    //                    }
    //                    result[1] = new LineSet(linesReverse) { Color = (Vector3)Direction };
    //                    Direction = !Direction;
    //                }
    //                return result;
    //            }

    //            public void IntegrateFurther(LineSet positions, float? maxTime = null)
    //            {
    //                try
    //                {
    //                    Debug.Assert(Field.NumVectorDimensions <= 3);
    //                    PointSet<EndPoint> ends = positions.GetAllEndPoints();
    //                    if (ends.Length == 0)
    //                        return;

    //                    //int validPoints = 0;
    //                    for (int index = 0; index < positions.Length; ++index)
    //                    {
    //                        if (positions[index].Length == 0 || ends[index] == null || (ends[index].Status != Status.BORDER && ends[index].Status != Status.TIME_BORDER && ends[index].Status != Status.OK))
    //                            continue;
    //                        StreamLine<Vector3> streamline = IntegrateLineForRendering(((Vec3)ends.Points[index].Position).ToVec(Field.NumVectorDimensions), maxTime);
    //                        positions[index].Positions = positions.Lines[index].Positions.Concat(streamline.Points).ToArray();
    //                        positions[index].Status = streamline.Status;
    //                        positions[index].LineLength += streamline.LineLength;

    //                        if ((index) % (positions.Length / 10) == 0)
    //                            Console.WriteLine("Further integrated {0}/{1} lines. {2}%", index, positions.Length, ((float)index * 100) / positions.Length);
    //                        //validPoints++;
    //                    }
    //                    //return new LineSet(lines) { Color = (Vector3)Direction };
    //                }
    //                catch (Exception e)
    //                {
    //                    Console.WriteLine(e.Message);
    //                }
    //            }

    //            protected Status CheckPosition(Vector pos)
    //            {
    //                if (!Field.Grid.InGrid(pos))
    //                    return Status.BORDER;
    //                if (!Field.IsValid(pos))
    //                    return Status.INVALID;
    //                return Status.OK;
    //            }

    //            public enum Type
    //            {
    //                EULER,
    //                RUNGE_KUTTA_4,
    //                REPELLING_RUNGE_KUTTA
    //            }

    //            public static Integrator CreateIntegrator(VectorField<DataType> field, Type type, Line core = null, float force = 0.1f)
    //            {
    //                switch (type)
    //                {
    //                    case Type.RUNGE_KUTTA_4:
    //                        return new IntegratorRK4(field);
    //                    case Type.REPELLING_RUNGE_KUTTA:
    //                        return new IntegratorRK4Repelling(field, core, force);
    //                    default:
    //                        return new IntegratorEuler(field);
    //                }
    //            }
    //        }

    //        public class StreamLine<T>
    //        {
    //            public List<T> Points;
    //            public float LineLength = 0;
    //            public Integrator.Status Status;

    //            public StreamLine(int startCapacity = 100)
    //            {
    //                Points = new List<T>(startCapacity);
    //            }
    //        }

    //        public class StreamLine : StreamLine<Vector>
    //        {
    //            public StreamLine(int startCapacity = 100) : base(startCapacity) { }
    //        }

    //        public class IntegratorEuler : Integrator
    //        {
    //            public IntegratorEuler(VectorField<DataType> field)
    //            {
    //                Field = field;
    //            }
    //            int counter = 0;
    //            public override Status Step(Vector pos, out Vector stepped, out float stepLength)
    //            {
    //                ++counter;
    //                stepped = new Vector(pos);
    //                stepLength = 0;
    //                Vector dir = Field.Sample(pos);

    //                if (!ScaleAndCheckVector(dir, out dir))
    //                    return Status.CP;

    //                if (float.IsNaN(dir[0]))
    //                    Console.WriteLine("NaN NaN NaN NaN WATMAN!");

    //                stepped += dir;
    //                stepLength += dir.LengthEuclidean();

    //                return CheckPosition(stepped);
    //            }

    //            public override bool StepBorder(Vector position, out Vector stepped, out float stepLength)
    //            {
    //                stepped = new Vector(position);
    //                stepLength = 0;
    //                Vector dir = Field.Sample(position) * (int)Direction;
    //                if (NormalizeField)
    //                    dir.Normalize();

    //                // How big is the smallest possible scale to hit a maximum border?
    //                float scale = (((Vector)Field.Size - new Vector(1, Field.Size.Length) - position) / dir).MinPos();
    //                scale = Math.Min(scale, (position / dir).MinPos());

    //                if (scale >= StepSize)
    //                    return false;

    //                stepped += dir * scale;
    //                stepLength = dir.LengthEuclidean() * scale;
    //                return true;
    //            }

    //            protected bool ScaleAndCheckVector(Vector vec, out Vector scaled)
    //            {
    //                scaled = vec;
    //                float length = vec.LengthEuclidean();
    //                if (NormalizeField)
    //                    scaled = scaled / length;
    //                scaled *= StepSize * (int)Direction;

    //                if (length < EpsCriticalPoint)
    //                    return false;

    //                return true;
    //            }

    //            public override bool StepBorderTime(Vector position, float timeBorder, out Vector stepped, out float stepLength)
    //            {
    //                stepped = new Vector(position);
    //                stepLength = 0;
    //                Vector dir = Field.Sample(position) * (int)Direction;
    //                if (NormalizeField)
    //                    dir.Normalize();

    //                // How big is the smallest possible scale to hit a maximum border?
    //                Vector timeSize = (Vector)Field.Size - new Vector(1, Field.Size.Length);
    //                timeSize.T = timeBorder - 1;
    //                float scale = ((timeSize - position) / dir).MinPos();
    //                scale = Math.Min(scale, (position / dir).MinPos());

    //                if (scale >= StepSize)
    //                    return false;

    //                stepped += dir * scale;
    //                stepLength = dir.LengthEuclidean() * scale;
    //                return true;
    //            }
    //        }

    //        public class IntegratorRK4 : IntegratorEuler
    //        {
    //            public IntegratorRK4(VectorField<DataType> field) : base(field)
    //            { }

    //            public override Status Step(Vector pos, out Vector stepped, out float stepLength)
    //            {
    //                stepped = new Vector(pos);
    //                stepLength = 0;
    //                Status status;

    //                // v0
    //                Vector v0 = Field.Sample(pos);
    //                if (!ScaleAndCheckVector(v0, out v0))
    //                    return Status.CP;
    //                status = CheckPosition(pos + v0 / 2);
    //                if (status != Status.OK)
    //                    return status;

    //                // v1
    //                Vector v1 = Field.Sample(pos + v0 / 2);
    //                if (!ScaleAndCheckVector(v1, out v1))
    //                    return Status.CP;
    //                status = CheckPosition(pos + v1 / 2);
    //                if (status != Status.OK)
    //                    return status;

    //                // v2
    //                Vector v2 = Field.Sample(pos + v1 / 2);
    //                if (!ScaleAndCheckVector(v2, out v2))
    //                    return Status.CP;
    //                status = CheckPosition(pos + v2);
    //                if (status != Status.OK)
    //                    return status;

    //                // v3
    //                Vector v3 = Field.Sample(pos + v2);
    //                if (!ScaleAndCheckVector(v3, out v3))
    //                    return Status.CP;
    //                status = CheckPosition(pos + v2);
    //                if (status != Status.OK)
    //                    return status;

    //                Vector dir = (v0 + (v1 + v2) * 2 + v3) / 6;
    //                stepped += dir;
    //                stepLength = dir.LengthEuclidean();

    //                return CheckPosition(stepped);
    //            }
    //        }

    //        public class IntegratorRK4Repelling : IntegratorEuler
    //        {
    //            public Line Core { get; set; }
    //            public float Force { get; set; }
    //            public IntegratorRK4Repelling(VectorField<DataType> field, Line core, float outwardForce) : base(field)
    //            {
    //                Core = core;
    //                Force = outwardForce;
    //            }

    //            protected Vector Repell(Vector pos)
    //            {
    //                Vector3 dir;
    //                float dist = Core.DistanceToPointInZ((Vector3)pos, out dir);
    //                dir = (Vector3)pos - dir;
    //                dir /= dist;
    //                return ((Vec3)(dir * Force)).ToVec(pos.Length);
    //            }

    //            public override Status Step(Vector pos, out Vector stepped, out float stepLength)
    //            {
    //                stepped = new Vector(pos);
    //                stepLength = 0;
    //                Status status;

    //                // v0
    //                Vector v0 = Field.Sample(pos) + Repell(pos);
    //                if (!ScaleAndCheckVector(v0, out v0))
    //                    return Status.CP;
    //                status = CheckPosition(pos + v0 / 2);
    //                if (status != Status.OK)
    //                    return status;

    //                // v1
    //                Vector v1 = Field.Sample(pos + v0 / 2) + Repell(pos + v0 / 2);
    //                if (!ScaleAndCheckVector(v1, out v1))
    //                    return Status.CP;
    //                status = CheckPosition(pos + v1 / 2);
    //                if (status != Status.OK)
    //                    return status;

    //                // v2
    //                Vector v2 = Field.Sample(pos + v1 / 2) + Repell(pos + v1 / 2);
    //                if (!ScaleAndCheckVector(v2, out v2))
    //                    return Status.CP;
    //                status = CheckPosition(pos + v2);
    //                if (status != Status.OK)
    //                    return status;

    //                // v3
    //                Vector v3 = Field.Sample(pos + v2) + Repell(pos + v2);
    //                if (!ScaleAndCheckVector(v3, out v3))
    //                    return Status.CP;
    //                status = CheckPosition(pos + v2);
    //                if (status != Status.OK)
    //                    return status;

    //                Vector dir = (v0 + (v1 + v2) * 2 + v3) / 6;
    //                stepped += dir;
    //                stepLength = dir.LengthEuclidean();

    //                return CheckPosition(stepped);
    //            }
    //        }

    //        public class IntegratorPredictorCorrector : Integrator
    //        {
    //            public float EpsCorrector = 0.00001f;

    //            // Two integrators. THis way, step size, integration type, field etc can be set individually.
    //            public Integrator Predictor, Corrector;
    //            public IntegratorPredictorCorrector(Integrator predictor, Integrator corrector) : base()
    //            {
    //                Predictor = predictor;
    //                Corrector = corrector;
    //                Debug.Assert(Predictor.Field.NumVectorDimensions >= Corrector.Field.NumVectorDimensions, "Predictor is " + Predictor.Field.NumVectorDimensions + "D, Corrector is " + Corrector.Field.NumVectorDimensions + "D!");

    //                Field = Predictor.Field;
    //            }

    //            public override Status Step(Vector pos, out Vector stepped, out float stepLength)
    //            {
    //                // One predictor step.
    //                Status status = Predictor.Step(pos, out stepped, out stepLength);
    //                if (status != Status.OK)
    //                    return status;
    //                // Now, step until the corrector reaches a critical point.
    //                Vector point;
    //                Vector next = stepped;
    //                if (CheckPosition(next) != Status.OK)
    //                {
    //                    StepBorder(pos, out stepped, out stepLength);
    //                    return CheckPosition(stepped);
    //                }
    //                int step = -1;
    //                do
    //                {
    //                    step++;
    //                    point = next;
    //                    status = Corrector.Step(point, out next, out stepLength);
    //                } while (status == Status.OK && step < Corrector.MaxNumSteps);

    //                if (status == Status.CP)
    //                    return Status.OK;
    //                return status;
    //            }

    //            //TODO: Correct.
    //            public override bool StepBorder(Vector position, out Vector stepped, out float stepLength)
    //            {
    //                return Predictor.StepBorder(position, out stepped, out stepLength);
    //            }

    //            //TODO: Correct.
    //            public override bool StepBorderTime(Vector position, float timeBorder, out Vector stepped, out float stepLength)
    //            {
    //                return Predictor.StepBorderTime(position, timeBorder, out stepped, out stepLength);
    //            }
    //        }
    //        #endregion Integrators
    //}
    /// <summary>
    /// Class for acessing vector fields. Memory management will differ.
    /// </summary>
    partial class VectorField ///<DataType> where DataType : VectorData, new()
    {
        #region VF
        public VectorData Data { get; set; }
        public FieldGrid Grid { get; protected set; }
        public Index Size { get { return Grid.Size; } }

        #region Delegates
        /// <summary>
        /// Function to compute a new field based on an old one, point wise.
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public delegate Vector VFFunction(VectorRef v);

        public delegate Vector VFJFunction(VectorRef v, SquareMatrix J);

        public delegate Vector3 PositionToColor(VectorField field, Vector3 position);
        #endregion Delegates

        #region Access
        /// <summary>
        /// Number of dimensions per vector.
        /// </summary>
        public virtual int NumVectorDimensions { get { return Data.VectorLength; } }
        public int NumDimensions { get { return Size.Length; } }

        public virtual float? InvalidValue { get; set; }
        public virtual float? TimeSlice { get; set; }

        /// <summary>
        /// Access field by scalar index.
        /// </summary>
        public virtual VectorRef this[int index] { get { return Data[index]; } protected set { Data[index] = value; } }
        /// <summary>
        /// Access field by N-dimensional index.
        /// </summary>
        public VectorRef this[Index gridPosition]
        {
            get
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

                return this[index];
            }
            protected set
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

                this[index] = value;
            }
        }
        public bool IsValid(Index pos)
        {
            return Sample(pos)[0] != InvalidValue;
        }

        public bool IsValid(int pos)
        {
            return Sample(pos)[0] != InvalidValue;
        }

        public VectorField(VectorData data, FieldGrid grid)
        {
            Data = data;
            Grid = grid;
            InvalidValue = null;
            //         SpreadInvalidValue();
        }

        protected VectorField() { }

        public VectorField(ScalarField[] scalars)
        {
            VectorBuffer[] raw = new VectorBuffer[scalars.Length];
            for (int d = 0; d < scalars.Length; ++d)
                raw[d] = scalars[d].BufferData;
            Data = new VectorDataArray<VectorBuffer>(raw);
        }
        ///// <summary>
        ///// Access field by scalar index.
        ///// </summary>
        //public override Vector Sample(int index)
        //{
        //    return _data[index];
        //}
        //public VectorField(VectorField field, VFJFunction function, int outputDim, bool needJacobian = true)
        //{
        //    return ConstructVectorField<VectorBuffer>(field, function, outputDim, needJacobian);
        //}

        public VectorField(VectorField field, VFJFunction function, int outputDim, bool needJacobian = true)
        {
            int scalars = outputDim;/*function(field.Sample(0), field.SampleDerivative(new Vector(0, field.Size.Length))).Length;*/
            Data = new VectorBuffer();
            Data.SetSize(field.NumVectorDimensions, field.Size.Product());
            FieldGrid gridCopy = field.Grid.Copy();

            // In case the input field was time dependant, this one is not. Still, we keep the size and origin of the time as new dimension!
            gridCopy.TimeDependant = false;

            InvalidValue = field.InvalidValue;

            GridIndex indexIterator = new GridIndex(field.Size);
            foreach (GridIndex index in indexIterator)
            {
                VectorRef v = field[(int)index];

                if (v[0] == InvalidValue)
                {
                    for (int dim = 0; dim < NumVectorDimensions; ++dim)
                        Data[(int)index][dim] = (float)InvalidValue;
                    continue;
                }

                SquareMatrix J = needJacobian ? field.SampleDerivative(index) : null;
                Vector funcValue = function(v, J);

                Data[(int)index] = funcValue;
            }
        }

        public ScalarField GetChannel(int slice)
        {
            return new ScalarField(new VectorBuffer(Data.GetChannel(slice), 1), Grid.Copy());
        }
        //public static VectorField ConstructVectorField(VectorField field, VFJFunction function, int outputDim, bool needJacobian = true)
        //{
        //    return ConstructVectorField<VectorBuffer>(field, function, outputDim, needJacobian);
        //}

        //public VectorField ConstructVectorField<DataType>(VectorField field, VFJFunction function, int outputDim, bool needJacobian = true) where DataType : VectorData, new()
        //{
        //    int scalars = outputDim;/*function(field.Sample(0), field.SampleDerivative(new Vector(0, field.Size.Length))).Length;*/
        //    VectorField newField = new VectorField();
        //    newField._data = new DataType();
        //    newField._data.SetSize(field.NumVectorDimensions, field.Size.Product());
        //    FieldGrid gridCopy = field.Grid.Copy();

        //    // In case the input field was time dependant, this one is not. Still, we keep the size and origin of the time as new dimension!
        //    gridCopy.TimeDependant = false;

        //    newField.InvalidValue = field.InvalidValue;

        //    GridIndex indexIterator = new GridIndex(field.Size);
        //    foreach (GridIndex index in indexIterator)
        //    {
        //        VectorRef v = field[(int)index];

        //        if (v[0] == newField.InvalidValue)
        //        {
        //            for (int dim = 0; dim < newField.NumVectorDimensions; ++dim)
        //                newField._data[(int)index][dim] = (float)newField.InvalidValue;
        //            continue;
        //        }

        //        SquareMatrix J = needJacobian ? field.SampleDerivative(index) : null;
        //        Vector funcValue = function(v, J);

        //        newField._data[(int)index] = funcValue;
        //        //for (int dim = 0; dim < NumVectorDimensions; ++dim)
        //        //{
        //        //    var vec = Scalars[dim];
        //        //    _data[(int)index][dim] = funcValue[dim];
        //        //}
        //    }
        //    return newField;
        //}

        public bool IsValid(Vector pos, out Vector sample)
        {
            sample = Sample(pos);
            return sample != null && sample[0] != InvalidValue;
            //VectorRef weights;
            //Index neighbors = Grid.FindAdjacentIndices(pos, out weights);
            //return neighbors != null;
            //foreach (int neighbor in neighbors)
            //    for (int n = 0; n < NumVectorDimensions; ++n)
            //        if (Data[neighbor][n] == InvalidValue)
            //            return false;
            //return true;
        }
        /// <summary>
        /// Access field by scalar index.
        /// </summary>
        public VectorRef Sample(int index) { return this[index]; }

        /// <summary>
        /// Access field by N-dimensional index.
        /// </summary>
        public virtual VectorRef Sample(Index gridPosition)
        {
            return this[gridPosition];
        }

        public virtual Vector Sample(Vector position)
        {
            return Grid.Sample(this, position);
        }

        /// <summary>
        /// Get the derivative at a data point. Not checking for InvalidValue.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public SquareMatrix SampleDerivative(Index pos)
        {
            //Debug.Assert(NumVectorDimensions == Size.Length);
            int size = Math.Max(Size.Length, NumVectorDimensions);
            SquareMatrix jacobian = new SquareMatrix(size);

            // For all dimensions, so please reset each time.
            Index samplePos = new Index(pos);

            for (int dim = 0; dim < Size.Length; ++dim)
            {
                // Just to be sure, check thst no value was overwritten.
                int posCpy = samplePos[dim];

                // See whether a step to the right/left is possible.
                samplePos[dim]++;
                bool rightValid = (samplePos[dim] < Size[dim]) && IsValid(samplePos); //Scalars[0].Sample(samplePos) != InvalidValue;
                samplePos[dim] -= 2;
                bool leftValid = (samplePos[dim] >= 0) && IsValid(samplePos); //Scalars[0].Sample(samplePos) != InvalidValue;
                samplePos[dim]++;

                if (rightValid)
                {
                    if (leftValid)
                    {
                        // Regular case. Interpolate.
                        samplePos[dim]++;
                        jacobian[dim] = this[samplePos].ToVec(size);
                        samplePos[dim] -= 2;
                        jacobian[dim] -= this[samplePos].ToVec(size);
                        jacobian[dim] *= 0.5f;
                        samplePos[dim]++;
                    }
                    else
                    {
                        // Left border.
                        samplePos[dim]++;
                        jacobian[dim] = this[samplePos].ToVec(size);
                        samplePos[dim]--;
                        jacobian[dim] -= this[samplePos].ToVec(size);
                    }
                }
                else
                {
                    if (leftValid)
                    {
                        // Right border.
                        jacobian[dim] = this[samplePos].ToVec(size);
                        samplePos[dim]--;
                        jacobian[dim] -= this[samplePos].ToVec(size);
                        samplePos[dim]++;
                    }
                    else
                    {
                        // Weird case. 
                        jacobian[dim] = new Vector(0, size);
                    }
                }
                Debug.Assert(posCpy == samplePos[dim]);
            }

            return jacobian;
        }

        public SquareMatrix SampleDerivative(Vector position)
        {
            //Debug.Assert(NumVectorDimensions == Size.Length);
            SquareMatrix jacobian = new SquareMatrix(NumVectorDimensions);

            for (int dim = 0; dim < Size.Length; ++dim)
            {
                float stepPos = Math.Min(Size[dim] - 1, position[dim] + 0.5f) - position[dim];
                float stepMin = Math.Max(0, position[dim] - 0.5f) - position[dim];
                Vector samplePos = new Vector(position);
                samplePos[dim] += stepPos;
                jacobian[dim] = Sample(samplePos);

                samplePos[dim] += stepMin - stepPos;
                jacobian[dim] -= Sample(samplePos);

                jacobian[dim] /= (stepPos - stepMin);
            }

            return jacobian;
        }

        #endregion Access
        /// <summary>
        /// Make sure the invalid value is in the first scalar field if it is anywhere.
        /// </summary>
        protected void SpreadInvalidValue()
        {
            for (int pos = 0; pos < Size.Product(); ++pos)
            {
                for (int dim = 1; dim < NumVectorDimensions; ++dim)
                    if (this[pos][dim] == InvalidValue)
                        this[pos][0] = (float)InvalidValue;
            }
        }

        public VectorField GetSlice(int posInLastDimension)
        {
            // Copy the grid - one dimension smaller!
            RectlinearGrid grid = Grid as RectlinearGrid;
            Index newSize = new Index(Size.Length - 1);
            Array.Copy(Size.Data, newSize.Data, newSize.Length);

            FieldGrid sliceGrid = new RectlinearGrid(newSize);

            VectorData newData = Data.GetSliceInLastDimension(posInLastDimension, Size);
            return new VectorField(newData, sliceGrid);
        }

        public VectorField GetSlicePlanarVelocity(int posInLastDimension)
        {
            // Copy the grid - one dimension smaller!
            int lastSize = Size.T;
            RectlinearGrid grid = Grid as RectlinearGrid;
            Index newSize = new Index(Size.Length - 1);

            FieldGrid sliceGrid = new RectlinearGrid(newSize);

            float[] raw = new float[NumVectorDimensions * newSize.Product()];
            foreach (Index idx in new GridIndex(newSize))
            {
                VectorRef vec = Data[(int)idx + posInLastDimension * newSize.Length];
                for (int v = 0; v < newSize.Length; ++v)
                    raw[(int)idx * newSize.Length + v] = vec[v];
            }

            return new VectorField(new VectorBuffer(raw, newSize.Length), sliceGrid);
        }

        public PointSet<Point> ColorCodeArbitrary(LineSet lines, PositionToColor func)
        {
            Point[] points;
            points = new Point[lines.NumExistentPoints];
            int idx = 0;
            foreach (Line line in lines.Lines)
            {
                foreach (Vector3 pos in line.Positions)
                {
                    points[idx] = new Point() { Position = pos, Color = func(this, pos), Radius = lines.Thickness };
                    ++idx;
                }
            }

            return new PointSet<Point>(points);
        }

        public DataStream GetDataStream() { throw new NotImplementedException(); }
        public virtual bool IsUnsteady() { return this.TimeSlice != null; }

        public void ChangeEndian() { Data.ChangeEndian(); }

        public virtual void ScaleToGrid(float dimwiseScale)
        {
            for (int n = 0; n < Data.Length; ++n)
                if (IsValid(n))
                    for (int dim = 0; dim < Data.VectorLength; ++dim)
                        Data[n][dim] *= dimwiseScale;
        }

        public virtual void ScaleToGrid(Vector scale)
        {
            Debug.Assert(Data.VectorLength == scale.Length);
            for (int dim = 0; dim < Data.VectorLength; ++dim)
            {
                for (int n = 0; n < Data.Length; ++n)
                    Data[n][dim] *= scale[dim];
            }
        }

        #endregion VF
    }
}