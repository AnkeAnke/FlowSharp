﻿using System;
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
        #region VF
        private Field[] _scalars;
        public virtual Field[] Scalars { get { return _scalars; } /*protected set { _scalars = value; }*/ }
        public Field this[int index]
        {
            get { return Scalars[index]; }
        }
        public virtual FieldGrid Grid { get { return Scalars[0].Grid; } protected set { Scalars[0].Grid = value; } }
        public Index Size { get { return Grid.Size; } }

        /// <summary>
        /// Number of dimensions per vector.
        /// </summary>
        public virtual int NumVectorDimensions { get { return Scalars.Length; } }

        public virtual float? InvalidValue
        {
            get { return Scalars[0].InvalidValue; }
            set { Scalars[0].InvalidValue = value; }
        }
        public virtual float? TimeSlice {
            get { return Scalars[0].TimeOrigin; }
            set { foreach(ScalarField field in Scalars) field.TimeOrigin = value; }
        }
        public virtual bool IsValid(Vector pos)
        {
            return _scalars[0].IsValid(pos);
        }

        /// <summary>
        /// Pun. TODO: Better.
        /// </summary>
        /// <param name="fields"></param>
        public VectorField(Field[] fields)
        {
            _scalars = fields;
            SpreadInvalidValue();
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
            {
                vec[dim] = Scalars[dim][index];
            }
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

        public virtual Vector Sample(Vector position)
        {
            return Grid.Sample(this, position);
        }


        /// <summary>
        /// Function to compute a new field based on an old one, point wise.
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public delegate Vector VFFunction(Vector v);

        public delegate Vector VFJFunction(Vector v, SquareMatrix J);

        public VectorField(VectorField field, VFJFunction function, int outputDim, bool needJacobian = true)
        {
            int scalars = outputDim;/*function(field.Sample(0), field.SampleDerivative(new Vector(0, field.Size.Length))).Length;*/
            _scalars = new ScalarField[scalars];
            FieldGrid gridCopy = field.Grid.Copy();

            // In case the input field was time dependant, this one is not. Still, we keep the size and origin of the time as new dimension!
            gridCopy.TimeDependant = false;

            for (int dim = 0; dim < scalars; ++dim)
            {
                Scalars[dim] = new ScalarField(gridCopy);
            }
            this.InvalidValue = field.InvalidValue;

            GridIndex indexIterator = new GridIndex(field.Size);
            foreach (GridIndex index in indexIterator)
            {
                Vector v = field.Sample((int)index);

                if (v[0] == InvalidValue)
                {
                    for (int dim = 0; dim < Scalars.Length; ++dim)
                        Scalars[dim][(int)index] = (float)InvalidValue;
                    continue;
                }

                SquareMatrix J = needJacobian? field.SampleDerivative(index) : null;
                Vector funcValue = function(v, J);

                for (int dim = 0; dim < Scalars.Length; ++dim)
                {
                    var vec = Scalars[dim];
                    Scalars[dim][(int)index] = funcValue[dim];
                }
            }
        }

        /// <summary>
        /// Make sure the invalid value is in the first scalar field if it is anywhere.
        /// </summary>
        protected void SpreadInvalidValue()
        {
            for(int pos = 0; pos < Size.Product(); ++pos)
            {
                for (int dim = 1; dim < Scalars.Length; ++dim)
                    if (Scalars[dim][pos] == InvalidValue)
                        Scalars[0][pos] = (float)InvalidValue;
            }
        }

        public SquareMatrix SampleDerivative(Vector position)
        {
            //Debug.Assert(NumVectorDimensions == Size.Length);
            SquareMatrix jacobian = new SquareMatrix(NumVectorDimensions);

            for (int dim = 0; dim < Size.Length; ++dim)
            {
                float stepPos = Math.Min(Size[dim]-1, position[dim] + 0.5f) - position[dim];
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
                        jacobian[dim] = Sample(samplePos).ToVec(size);
                        samplePos[dim] -= 2;
                        jacobian[dim] -= Sample(samplePos).ToVec(size);
                        jacobian[dim] *= 0.5f;
                        samplePos[dim]++;
                    }
                    else
                    {
                        // Left border.
                        samplePos[dim]++;
                        jacobian[dim] = Sample(samplePos).ToVec(size);
                        samplePos[dim]--;
                        jacobian[dim] -= Sample(samplePos).ToVec(size);
                    }
                }
                else
                {
                    if (leftValid)
                    {
                        // Right border.
                        jacobian[dim] = Sample(samplePos).ToVec(size);
                        samplePos[dim]--;
                        jacobian[dim] -= Sample(samplePos).ToVec(size);
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

        public virtual VectorField GetSlice(int posInLastDimension)
        {
            ScalarField[] slices = new ScalarField[this.NumVectorDimensions];

            // Copy the grid - one dimension smaller!
            RectlinearGrid grid = Grid as RectlinearGrid;
            Index newSize = new Index(Size.Length - 1);
            Array.Copy(Size.Data, newSize.Data, newSize.Length);

            FieldGrid sliceGrid = new RectlinearGrid(newSize);
            for(int i = 0; i < slices.Length; ++i)
            {

                slices[i] = new ScalarField(sliceGrid);
                Array.Copy(((ScalarField)this.Scalars[i]).Data, newSize.Product() * posInLastDimension, slices[i].Data, 0, newSize.Product());
                slices[i].TimeOrigin = posInLastDimension;
            }
            return new VectorField(slices);
        }

        public virtual VectorField GetSlicePlanarVelocity(int posInLastDimension)
        {
            ScalarField[] slices = new ScalarField[Size.Length - 1];

            // Copy the grid - one dimension smaller!
            RectlinearGrid grid = Grid as RectlinearGrid;
            Index newSize = new Index(Size.Length - 1);
            Array.Copy(Size.Data, newSize.Data, newSize.Length);

            FieldGrid sliceGrid = new RectlinearGrid(newSize);
            for (int i = 0; i < Size.Length-1; ++i)
            {

                slices[i] = new ScalarField(sliceGrid);
                Array.Copy(((ScalarField)this.Scalars[i]).Data, newSize.Product() * posInLastDimension, slices[i].Data, 0, newSize.Product());
                slices[i].TimeOrigin = posInLastDimension;
            }
            return new VectorField(slices);
        }

        public delegate Vector3 PositionToColor(VectorField field, Vector3 position);
        public PointSet<Point> ColorCodeArbitrary(LineSet lines, PositionToColor func)
        {
            Point[] points;
            points = new Point[lines.NumExistentPoints];
            int idx = 0;
            foreach(Line line in lines.Lines)
            {
                foreach(Vector3 pos in line.Positions)
                {
                    points[idx] = new Point() { Position = pos, Color = func(this, pos), Radius = lines.Thickness };
                    ++idx;
                }
            }

            return new PointSet<Point>(points);
        }
        #endregion VF

        #region Integrators
        public abstract class Integrator
        {
            public enum Status
            {
                OK,
                CP,
                BORDER,
                TIME_BORDER,
                INVALID
            };

            protected VectorField _field;
            /// <summary>
            /// Based on CellSize equals (StepSize = 1).
            /// </summary>
            protected float _stepSize = 0.2f;
            protected Sign _direction = Sign.POSITIVE;
            protected bool _normalizeField = false;
            protected float _epsCriticalPoint = 0.00000001f;


            public virtual VectorField Field { get { return _field; } set { _field = value; } }
            /// <summary>
            /// Based on CellSize equals (StepSize = 1).
            /// </summary>
            public virtual float StepSize { get { return _stepSize; } set { _stepSize = value; } }
            public virtual Sign Direction { get { return _direction; } set { _direction = value; } }
            public virtual bool NormalizeField { get { return _normalizeField; } set { _normalizeField = value; } }
            public virtual float EpsCriticalPoint { get { return _epsCriticalPoint; } set { _epsCriticalPoint = value; } }
            public int MaxNumSteps = 2000;

            public abstract Status Step(Vector pos, out Vector stepped, out float stepLength);
            /// <summary>
            /// Perform one step, knowing that the border is nearby.
            /// </summary>
            /// <param name="pos"></param>
            /// <param name="stepped"></param>
            public abstract bool StepBorder(Vector pos, out Vector stepped, out float stepLength);
            public abstract bool StepBorderTime(Vector pos, float timeBorder, out Vector stepped, out float stepLength);
            public virtual StreamLine<Vector3> IntegrateLineForRendering(Vector pos, float? maxTime = null)
            {
                StreamLine<Vector3> line = new StreamLine<Vector3>((int)(Field.Size.Max() * 1.5f / StepSize)); // Rough guess.
                //line.Points.Add((Vector3)pos);
                float timeBorder = maxTime ?? (((Field as VectorFieldUnsteady) == null) ? float.MaxValue : (Field.Grid.TimeOrigin??0) +  Field.Size.T);

                Vector point;
                Vector next = pos;
                if (CheckPosition(next) != Status.OK)
                {
                    return line;
                }
                Status status;
                int step = -1;
                bool attachTimeZ = Field.NumVectorDimensions == 2 && Field.TimeSlice != 0;
                float stepLength;
                do
                {
                    step++;
                    point = next;
                    Vector3 posP = (Vector3)point;
                    if (attachTimeZ)
                        posP.Z = (float)Field.TimeSlice;
                    line.Points.Add(posP);
                    status = Step(point, out next, out stepLength);
                    if (status == Status.OK)
                        line.LineLength += stepLength;
                } while (status == Status.OK && step < MaxNumSteps && next.T <= timeBorder);

                // If the time was exceeded, take a small step at the end.
                if (next.T > timeBorder && (status == Status.OK || status == Status.BORDER))
                {

                    if (StepBorderTime(point, timeBorder, out next, out stepLength))
                    {
                        line.Points.Add((Vector3)next);
                        line.LineLength += stepLength;
                        status = Status.TIME_BORDER;
                    }

                    if (next[1] < 0)
                        Console.WriteLine("Wut?");
                }

                // If a border was hit, take a small step at the end.
                if (status == Status.BORDER)
                {
                    if (StepBorder(point, out next, out stepLength))
                    {
                        line.Points.Add((Vector3)next);
                        line.LineLength += stepLength;
                    }
                }

                // Single points are confusing for everybody.
                if (line.Points.Count < 2)
                {
                    line.Points.Clear();
                    line.LineLength = 0;
                }
                line.Status = status;
                return line;
            }

            public LineSet[] Integrate<P>(PointSet<P> positions, bool forwardAndBackward = false, float? maxTime = null) where P : Point
            {
                Debug.Assert(Field.NumVectorDimensions <= 3);

                Line[] lines = new Line[positions.Length];
                Line[] linesReverse = new Line[forwardAndBackward? positions.Length : 0];

                LineSet[] result = new LineSet[forwardAndBackward ? 2 : 1];

                for (int index = 0; index < positions.Length; ++index)
                {

                    StreamLine<Vector3> streamline = IntegrateLineForRendering(((Vec3)positions.Points[index].Position).ToVec(Field.NumVectorDimensions), maxTime);
                    lines[index] = new Line();
                    lines[index].Positions = streamline.Points.ToArray();
                    lines[index].Status = streamline.Status;
                    lines[index].LineLength = streamline.LineLength;

                    //if ((index) % (positions.Length / 10) == 0)
                    //    Console.WriteLine("Integrated {0}/{1} lines. {2}%", index, positions.Length, ((float)index * 100) / positions.Length);
                }
                result[0] = new LineSet(lines) { Color = (Vector3)Direction };

                if (forwardAndBackward)
                {
                    Direction = !Direction;
                    for (int index = 0; index < positions.Length; ++index)
                    {
                        StreamLine<Vector3> streamline = IntegrateLineForRendering((Vec3)positions.Points[index].Position, maxTime);
                        linesReverse[index] = new Line();
                        linesReverse[index].Positions = streamline.Points.ToArray();
                    }
                    result[1] = new LineSet(linesReverse) { Color = (Vector3)Direction };
                    Direction = !Direction;                   
                }
                return result;
            }

            public void IntegrateFurther(LineSet positions, float? maxTime = null)
            {
                try {
                    Debug.Assert(Field.NumVectorDimensions <= 3);

                    LineSet result;
                    PointSet<EndPoint> ends = positions.GetAllEndPoints();
                    if (ends.Length == 0)
                        return;

                    //int validPoints = 0;
                    for (int index = 0; index < positions.Length; ++index)
                    {
                        if (positions[index].Length == 0 || ends[index] == null || (ends[index].Status != Status.BORDER && ends[index].Status != Status.TIME_BORDER && ends[index].Status != Status.OK))
                            continue;
                        StreamLine<Vector3> streamline = IntegrateLineForRendering(((Vec3)ends.Points[index].Position).ToVec(Field.NumVectorDimensions), maxTime);
                        positions[index].Positions = positions.Lines[index].Positions.Concat(streamline.Points).ToArray();
                        positions[index].Status = streamline.Status;
                        positions[index].LineLength += streamline.LineLength;

                        //if ((index) % (positions.Length / 10) == 0)
                        //    Console.WriteLine("Further integrated {0}/{1} lines. {2}%", index, positions.Length, ((float)index*100) / positions.Length);
                        //validPoints++;
                    }
                    //return new LineSet(lines) { Color = (Vector3)Direction };
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            protected Status CheckPosition(Vector pos)
            {
                if (!Field.Grid.InGrid(pos))
                    return Status.BORDER;
                if (Field.Scalars[0].Sample(pos) == Field.InvalidValue)
                    return Status.INVALID;
                return Status.OK;
            }

            public enum Type
            {
                EULER,
                RUNGE_KUTTA_4,
                REPELLING_RUNGE_KUTTA
            }

            public static Integrator CreateIntegrator(VectorField field, Type type, Line core = null, float force = 0.1f)
            {
                switch(type)
                {
                    case Type.RUNGE_KUTTA_4:
                        return new IntegratorRK4(field);
                    case Type.REPELLING_RUNGE_KUTTA:
                        return new IntegratorRK4Repelling(field, core, force);
                    default:
                        return new IntegratorEuler(field);
                }
            }
        }

        public class StreamLine<T>
        {
            public List<T> Points;
            public float LineLength = 0;
            public Integrator.Status Status;

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
            int counter = 0;
            public override Status Step(Vector pos, out Vector stepped, out float stepLength)
            {
                ++counter;
                stepped = new Vector(pos);
                stepLength = 0;
                Vector dir = Field.Sample(pos);

                if (!ScaleAndCheckVector(dir, out dir))
                    return Status.CP;

                if (float.IsNaN(dir[0]))
                    Console.WriteLine("NaN NaN NaN NaN WATMAN!");

                stepped += dir;
                stepLength += dir.LengthEuclidean();

                return CheckPosition(stepped);
            }

            public override bool StepBorder(Vector position, out Vector stepped, out float stepLength)
            {
                stepped = new Vector(position);
                stepLength = 0;
                Vector dir = Field.Sample(position) * (int)Direction;
                if (NormalizeField)
                    dir.Normalize();

                // How big is the smallest possible scale to hit a maximum border?
                float scale = (((Vector)Field.Size - new Vector(1, Field.Size.Length) - position) / dir).MinPos();
                scale = Math.Min(scale, (position / dir).MinPos());

                if (scale >= StepSize)
                    return false;

                stepped += dir * scale;
                stepLength = dir.LengthEuclidean() * scale;
                return true;
            }

            protected bool ScaleAndCheckVector(Vector vec, out Vector scaled)
            {
                scaled = vec;
                float length = vec.LengthEuclidean();
                if (NormalizeField)
                    scaled = scaled / length;
                scaled *= StepSize * (int)Direction;

                if (length < EpsCriticalPoint)
                    return false;

                return true;
            }

            public override bool StepBorderTime(Vector position, float timeBorder, out Vector stepped, out float stepLength)
            {
                stepped = new Vector(position);
                stepLength = 0;
                Vector dir = Field.Sample(position) * (int)Direction;
                if (NormalizeField)
                    dir.Normalize();

                // How big is the smallest possible scale to hit a maximum border?
                //Vector timeSize = (Vector)Field.Size + - new Vector(1, Field.Size.Length);
                float timeSize = timeBorder;
                float scale = (timeSize - position.T) / dir.T; ;
                scale = Math.Min(scale, (position / dir).MinPos());

                if (scale >= StepSize)
                    return false;

                stepped += dir * scale;
                stepped.T = timeSize;
                if (CheckPosition(stepped) != Status.OK)
                    return false;
                stepLength = dir.LengthEuclidean() * scale;
                return true;
            }
        } 

        public class IntegratorRK4 : IntegratorEuler
        {
            public IntegratorRK4(VectorField field) : base(field)
            { }

            public override Status Step(Vector pos, out Vector stepped, out float stepLength)
            {
                stepped = new Vector(pos);
                stepLength = 0;
                Status status;

                // v0
                Vector v0 = Field.Sample(pos);
                if (!ScaleAndCheckVector(v0, out v0))
                    return Status.CP;
                stepped = pos + v0 / 2;
                status = CheckPosition(stepped);
                if (status != Status.OK)
                    return status;

                // v1
                Vector v1 = Field.Sample(stepped);
                if (!ScaleAndCheckVector(v1, out v1))
                    return Status.CP;
                stepped = pos + v1 / 2;
                status = CheckPosition(stepped);
                if (status != Status.OK)
                    return status;

                // v2
                Vector v2 = Field.Sample(stepped);
                if (!ScaleAndCheckVector(v2, out v2))
                    return Status.CP;
                stepped = pos + v2;
                status = CheckPosition(stepped);
                if (status != Status.OK)
                    return status;

                // v3
                Vector v3 = Field.Sample(stepped);
                if (!ScaleAndCheckVector(v3, out v3))
                    return Status.CP;
                stepped = pos + v3;
                status = CheckPosition(stepped);
                if (status != Status.OK)
                    return status;

                Vector dir = (v0 + (v1 + v2) * 2 + v3) / 6;
                stepped = new Vector(pos) + dir;
                stepLength = dir.LengthEuclidean();

                return CheckPosition(stepped);
            }
        }

        public class IntegratorRK4Repelling : IntegratorEuler
        {
            public Line Core { get; set; }
            public float Force { get; set; }
            public IntegratorRK4Repelling(VectorField field, Line core, float outwardForce) : base(field)
            {
                Core = core;
                Force = outwardForce;
            }

            protected Vector Repell(Vector pos)
            {
                Vector3 dir;
                float dist = Core.DistanceToPointInZ((Vector3)pos, out dir);
                dir = (Vector3)pos - dir;
                dir /= dist;
                return ((Vec3)(dir * Force)).ToVec(pos.Length);
            }

            public override Status Step(Vector pos, out Vector stepped, out float stepLength)
            {
                stepped = new Vector(pos);
                stepLength = 0;
                Status status;

                // v0
                Vector v0 = Field.Sample(pos) + Repell(pos);
                if (!ScaleAndCheckVector(v0, out v0))
                    return Status.CP;
                status = CheckPosition(pos + v0 / 2);
                if (status != Status.OK)
                    return status;

                // v1
                Vector v1 = Field.Sample(pos + v0 / 2) + Repell(pos + v0 /2);
                if (!ScaleAndCheckVector(v1, out v1))
                    return Status.CP;
                status = CheckPosition(pos + v1 / 2);
                if (status != Status.OK)
                    return status;

                // v2
                Vector v2 = Field.Sample(pos + v1 / 2) + Repell(pos + v1 / 2);
                if (!ScaleAndCheckVector(v2, out v2))
                    return Status.CP;
                status = CheckPosition(pos + v2);
                if (status != Status.OK)
                    return status;

                // v3
                Vector v3 = Field.Sample(pos + v2) + Repell(pos + v2);
                if (!ScaleAndCheckVector(v3, out v3))
                    return Status.CP;
                status = CheckPosition(pos + v2);
                if (status != Status.OK)
                    return status;

                Vector dir = (v0 + (v1 + v2) * 2 + v3) / 6;
                stepped += dir;
                stepLength = dir.LengthEuclidean();

                return CheckPosition(stepped);
            }
        }


        //public class IntegratorPredictorCorrector : IntegratorEuler
        //{
        //    public float EpsCorrector = 0.00001f;
        //    public int MaxNumCorrectorSteps = 20;

        //    public delegate Vector Predictor(Vector v, SquareMatrix J);
        //    /// <summary>
        //    /// Check and correct a vector.
        //    /// </summary>
        //    /// <param name="position">Velocity.</param>
        //    /// <param name="J">Jacobian.</param>
        //    /// <param name="correction">Direction to correct to.</param>
        //    /// <returns>Error. Will be tested to check result.</returns>
        //    public delegate float Corrector(Vector v, SquareMatrix J, out Vector correction);

        //    protected Predictor _predict;
        //    protected Corrector _correct;
        //    protected bool _computeJ;
        //    public IntegratorPredictorCorrector(VectorField field, Predictor predictor, Corrector corrector, bool needJ = true) : base(field)
        //    {
        //        _predict = predictor;
        //        _correct = corrector;
        //        _computeJ = needJ;
        //    }

        //    public Status StepPredictor(Vector pos, out Vector stepped, out float stepLength)
        //    {
        //        stepped = new Vector(pos);
        //        stepLength = 0;
        //        // Sample field.
        //        Vector v = Field.Sample(pos);
        //        SquareMatrix J = null;
        //        if (_computeJ)
        //            J = Field.SampleDerivative(pos);
        //        // Feed sampled data to predictor.
        //        Vector dir = _predict(v, J);

        //        if (!ScaleAndCheckVector(dir, out dir))
        //            return Status.CP;

        //        if (float.IsNaN(dir[0]))
        //            Console.WriteLine("NaN NaN NaN NaN WATMAN!");

        //        stepped += dir;
        //        stepLength += dir.LengthEuclidean();

        //        return CheckPosition(stepped);
        //    }

        //    public override Status Step(Vector pos, out Vector stepped, out float stepLength)
        //    {
        //        Status status = StepPredictor(pos, out stepped, out stepLength);
        //        // Predictor ran into cp / border.
        //        if (status != Status.OK)
        //            return status;

        //        // Check against corrector.
        //        Vector next = new Vector(stepped);
        //        Vector correction;
        //        int i = MaxNumCorrectorSteps;
        //        for(; i >= 0; ++i)
        //        {
        //            Vector v = Field.Sample(next);
        //            SquareMatrix J = null;
        //            if (_computeJ)
        //                J = Field.SampleDerivative(next);
        //            float value = _correct(v, J, out correction);

        //            // Scale with step size.
        //            ScaleAndCheckVector(correction, out correction);

        //            // Test end point for validity.
        //            next += correction;
        //            Status check = base.CheckPosition(next);
        //            if (check != Status.OK)
        //                return status;

        //            // We are close enough to the truth. Break.
        //            if (Math.Abs(value) < EpsCorrector)
        //            {
        //                break;
        //            }
        //        }
        //        // Is out new value good enough?
        //        if(i == 0)
        //        {
        //            stepped = pos;
        //            stepLength = 0;
        //            return Status.CP;
        //        }

        //        stepped = next;
        //        stepLength = (next - pos).LengthEuclidean();
        //        return Status.OK;

        //    }

        //    //TODO: Correct.
        //    public override bool StepBorder(Vector position, out Vector stepped, out float stepLength)
        //    {
        //        return StepPredictor(position, out stepped, out stepLength) == Status.OK;
        //    }

        //    //TODO: Correct.
        //    public override bool StepBorderTime(Vector position, float timeBorder, out Vector stepped, out float stepLength)
        //    {
        //        return StepPredictor(position, out stepped, out stepLength) == Status.OK;
        //    }
        //}

        public class IntegratorPredictorCorrector : Integrator
        {
            public float EpsCorrector = 0.00001f;

            // Two integrators. THis way, step size, integration type, field etc can be set individually.
            public Integrator Predictor, Corrector;
            public IntegratorPredictorCorrector(Integrator predictor, Integrator corrector) : base()
            {
                Predictor = predictor;
                Corrector = corrector;
                Debug.Assert(Predictor.Field.NumVectorDimensions >= Corrector.Field.NumVectorDimensions, "Predictor is " + Predictor.Field.NumVectorDimensions + "D, Corrector is " + Corrector.Field.NumVectorDimensions +"D!");

                Field = Predictor.Field;
            }

            public override Status Step(Vector pos, out Vector stepped, out float stepLength)
            {
                // One predictor step.
                Status status = Predictor.Step(pos, out stepped, out stepLength);
                if (status != Status.OK)
                    return status;
                // Now, step until the corrector reaches a critical point.
                Vector point;
                Vector next = stepped;
                if (CheckPosition(next) != Status.OK)
                {
                    StepBorder(pos, out stepped, out stepLength);
                    return CheckPosition(stepped);
                }
                int step = -1;
                do
                {
                    step++;
                    point = next;
                    status = Corrector.Step(point, out next, out stepLength);
                } while (status == Status.OK && step < Corrector.MaxNumSteps);

                if (status == Status.CP)
                    return Status.OK;
                return status;
            }

            //TODO: Correct.
            public override bool StepBorder(Vector position, out Vector stepped, out float stepLength)
            {
                return Predictor.StepBorder(position, out stepped, out stepLength);
            }

            //TODO: Correct.
            public override bool StepBorderTime(Vector position, float timeBorder, out Vector stepped, out float stepLength)
            {
                return Predictor.StepBorderTime(position, timeBorder, out stepped, out stepLength);
            }
        }
        #endregion Integrators
    }
}

// &PARM04
// ygOrigin = 9.0,
// xgOrigin = 32.0,
// delY   =  210*0.1,
// delX   =  450*0.1,