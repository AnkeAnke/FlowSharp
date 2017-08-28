using SlimDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    partial class VectorField
    {
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

            public abstract Status Step(Vector pos, Vector sample, Vector inertial, out Vector next, out Vector nextSample, out float stepLength);
            /// <summary>
            /// Perform one step, knowing that the border is nearby.
            /// </summary>
            /// <param name="pos"></param>
            /// <param name="stepped"></param>
            public abstract bool StepBorder(Vector pos, Vector sample, out Vector stepped, out float stepLength);
            public abstract bool StepBorderTime(Vector pos, Vector sample, float timeBorder, out Vector stepped, out float stepLength);
            public virtual StreamLine<Vector4> IntegrateLineForRendering(Vector pos, Vector inertia, float? maxTime = null)
            {
                StreamLine<Vector4> line = new StreamLine<Vector4>();
                if (StepSize <= 0)
                    Console.WriteLine("StepSize is " + StepSize);

                try
                {
                    //line.Points.Add((Vector3)pos);
                    float timeBorder = maxTime ?? (((Field as VectorFieldUnsteady) == null) ? float.MaxValue : (Field.Grid.TimeOrigin ?? 0) + Field.Size.T);

                    Vector point;
                    Vector next = pos;
                    Vector inertial = inertia.ToVec(pos.Length);

                    Vector sample, nextSample;
                    line.Status = CheckPosition(next, inertial, out nextSample);
                    if (line.Status != Status.OK)
                    {
                        return line;
                    }
                    int step = -1;
                    bool attachTimeZ = Field.NumVectorDimensions == 2 && Field.TimeSlice != 0;
                    float stepLength;
                    do
                    {
                        step++;
                        sample = nextSample;
                        // Copy last point.
                        point = new Vector(next);

                        // Add 3D point to streamline list.
                        Vector4 posP = (Vector4)point;
                        if (attachTimeZ)
                            posP.Z = (float)Field.TimeSlice;
                        line.Points.Add(posP);

                        // Make one step. The step depends on the explicit integrator.
                        line.Status = Step(point, sample, inertial, out next, out nextSample, out stepLength);

                        inertial = sample;

                        if (line.Status == Status.OK)
                        {
                            line.LineLength += stepLength;
                        }
                    } while (line.Status == Status.OK && step < MaxNumSteps && next.T <= timeBorder);

                    // If a border was hit, take a small step at the end.
                    if (line.Status == Status.BORDER)
                    {
                        if (nextSample != null && StepBorder(point, nextSample, out next, out stepLength))
                        {
                            line.Points.Add((Vector4)next);
                            line.LineLength += stepLength;
                        }
                    }

                    // If the time was exceeded, take a small step at the end.
                    if (line.Status == Status.OK && next.T > timeBorder)
                    {
                        line.Status = Status.TIME_BORDER;

                        if (StepBorderTime(point, sample, timeBorder, out next, out stepLength))
                        {
                            line.Points.Add((Vector4)next);
                            line.LineLength += stepLength;
                        }

                        if (next[1] < 0)
                            Console.WriteLine("Wut?");
                    }
                    // Single points are confusing for everybody.
                    if (line.Points.Count < 2)
                    {
                        line.Points.Clear();
                        line.LineLength = 0;
                    }

                }
                catch (Exception e)
                {

                    Console.WriteLine("Caught it! For Rendering!");
                    Console.WriteLine(e.InnerException);
                }
                return line;
            }

            public LineSet[] Integrate<P>(PointSet<P> positions, bool forwardAndBackward = false, float? maxTime = null) where P : Point
            {
                Debug.Assert(Field.NumVectorDimensions <= 4);

                Line[] lines = new Line[positions.Length];
                Line[] linesReverse = new Line[forwardAndBackward ? positions.Length : 0];

                LineSet[] result = new LineSet[forwardAndBackward ? 2 : 1];

                Parallel.For(0, positions.Length, index =>
                //for (int index = 0; index < positions.Length; ++index)
                {
                    Vector inertia = (Vec3)(positions.Points[index] as InertialPoint)?.Inertia ?? new Vec3(0);
                    StreamLine<Vector4> streamline = IntegrateLineForRendering(
                        ((Vec4)positions.Points[index].Position).ToVec(Field.NumDimensions), 
                        inertia, 
                        maxTime);

                    lines[index] = new Line();
                    lines[index].Positions = streamline.Points.ToArray();
                    lines[index].Status = streamline.Status;
                    lines[index].LineLength = streamline.LineLength;
                });
                result[0] = new LineSet(lines) { Color = (Vector3)Direction };

                if (forwardAndBackward)
                {
                    Direction = !Direction;
                    Parallel.For(0, positions.Length, index =>
                    //for (int index = 0; index < positions.Length; ++index)
                    {
                        Vector inertia = (Vec3)(positions.Points[index] as InertialPoint)?.Inertia ?? new Vec3(0);
                        StreamLine<Vector4> streamline = IntegrateLineForRendering(
                            (Vec4)positions.Points[index].Position, 
                            inertia, 
                            maxTime);
                        linesReverse[index] = new Line();
                        linesReverse[index].Positions = streamline.Points.ToArray();
                    });
                    result[1] = new LineSet(linesReverse) { Color = (Vector3)Direction };
                    Direction = !Direction;
                }
                return result;
            }

            public void IntegrateFurther(LineSet positions, float? maxTime = null)
            {
                try
                {
                    Debug.Assert(Field.NumVectorDimensions <= 3);
                    PointSet<InertialPoint> ends = positions.GetAllEndPoints();
                    if (ends.Length == 0)
                        return;

                    //int validPoints = 0;
                    Parallel.For(0, positions.Length, index =>
                    //for (int index = 0; index < positions.Length; ++index)
                    {
                        if (positions[index].Length == 0 || ends[index] == null || (ends[index].Status != Status.BORDER && ends[index].Status != Status.TIME_BORDER && ends[index].Status != Status.OK))
                            return;

                        Vector inertia = (Vec3)(ends.Points[index] as InertialPoint)?.Inertia ?? new Vec3(0);
                        StreamLine<Vector4> streamline = IntegrateLineForRendering(
                            ((Vec4)ends.Points[index].Position).ToVec(Field.NumVectorDimensions),
                            inertia,
                            maxTime);
                        positions[index].Positions = positions.Lines[index].Positions.Concat(streamline.Points).ToArray();
                        positions[index].Status = streamline.Status;
                        positions[index].LineLength += streamline.LineLength;

                        if ((index) % (positions.Length / 10) == 0)
                            Console.WriteLine("Further integrated {0}/{1} lines. {2}%", index, positions.Length, ((float)index * 100) / positions.Length);
                        //validPoints++;
                    });
                    //return new LineSet(lines) { Color = (Vector3)Direction };
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.InnerException);
                }
            }

            protected virtual Status CheckPosition(Vector pos, Vector inertial, out Vector sample)
            {
                sample = null;
                if (!Field.Grid.InGrid(pos))
                    return Status.BORDER;

                // Console.WriteLine($"Checking Position at {pos}");
                sample = Field.Sample(pos, inertial);
                if (sample == null)
                    return Status.BORDER;

                if (sample[0] == Field.InvalidValue)
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
                switch (type)
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
    }
}
