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

            public abstract Status Step(ref Vector state, out float stepLength);
            /// <summary>
            /// Perform one step, knowing that the border is nearby.
            /// </summary>
            /// <param name="pos"></param>
            /// <param name="stepped"></param>
            public abstract bool StepBorder(Vector state, ref Vector nextState, out float stepLength);
            public abstract bool StepBorderTime(Vector state, ref Vector nextState, float timeBorder, out float stepLength);


            public virtual Line IntegrateLineForRendering(Vector pos, float? maxTime = null)
            {
                // Integrate.
                StreamLine<Vector> streamline = IntegrateLine(pos, maxTime);

                Line line4 = new Line(streamline.Points.Count);
                
                for (int p = 0; p < streamline.Points.Count; ++p)
                    line4.Positions[p] = (Vector4)streamline.Points[p];

                line4.Status = streamline.Status;
                line4.LineLength = streamline.LineLength;
                if (streamline.Points.Count > 0)
                    line4.EndPoint = streamline.Points.Last();
                else if(line4.Status == Status.TIME_BORDER)
                    line4.EndPoint = pos;

                return line4;

            }
            public virtual StreamLine<Vector> IntegrateLine(Vector pos, float? maxTime = null)
            {
                StreamLine<Vector> line = new StreamLine<Vector>();
                if (StepSize <= 0)
                    Console.WriteLine("StepSize is " + StepSize);

                var unsteadyField = (Field as VectorFieldUnsteady);
                float timeBorder = maxTime ?? ((unsteadyField == null) ? float.MaxValue : unsteadyField.TimeEnd);

                Vector point = new Vector(pos);
                if (!Field.InTime(point.T))
                {
                    line.Status = Status.TIME_BORDER;
                    return line;
                }

                int step = -1;
                float stepLength;
                do
                {
                    step++;

                    // Make one step. The step depends on the explicit integrator.
                    line.Status = Step(ref point, out stepLength);

                    if (point.T >= timeBorder)
                    {
                        line.Status = Status.TIME_BORDER;
                        break;
                    }

                    if (line.Status == Status.OK)
                    {
                        line.LineLength += stepLength;
                        line.Points.Add(new Vector(point));
                    }

                } while (line.Status == Status.OK && step < MaxNumSteps && point.T <= timeBorder);
                //Console.WriteLine($"Status now {line.Status}");
                if (line.Points.Count < 1)
                {
                    line.Points.Clear();
                    line.LineLength = 0;
                    return line;
                }

                // If a border was hit, take a small step at the end.
                if (line.Status == Status.BORDER)
                {
                    //Console.WriteLine($"Ended with {line.Status}\n\tPoint {line.Points.Last()}");
                    //if (StepBorder(line.Points.Last(), ref point, out stepLength))
                    //{
                    //    line.Points.Add(new Vector(point));
                    //    line.LineLength += stepLength;
                    //}
                }

                // If the time was exceeded, take a small step at the end.
                if (line.Status == Status.TIME_BORDER ||
                    (line.Status == Status.OK && point.T > timeBorder))
                {
                    line.Status = Status.TIME_BORDER;

                    if (StepBorderTime(line.Points.Last(), ref point, timeBorder, out stepLength))
                    {
                        line.Points.Add(new Vector(point));
                        line.LineLength += stepLength;
                        line.Points.Last().T = timeBorder;
                    }
                }
                // Single points are confusing for everybody.
                if (line.Points.Count < 2)
                {
                    line.Points.Clear();
                    line.LineLength = 0;
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
                    lines[index] = IntegrateLineForRendering(
                        (positions.Points[index].ToVector()).SubVec(Field.NumVectorDimensions),
                        maxTime);
                });
                result[0] = new LineSet(lines) { Color = (Vector3)Direction };

                if (forwardAndBackward)
                {
                    Direction = !Direction;
                    Parallel.For(0, positions.Length, index =>
                    //for (int index = 0; index < positions.Length; ++index)
                    {
                        Vector inertia = (Vec3)(positions.Points[index] as DirectionPoint)?.Direction ?? new Vec3(0);
                        linesReverse[index] = IntegrateLineForRendering(
                            (positions.Points[index].ToVector()).SubVec(Field.NumVectorDimensions),
                            maxTime);
                    });
                    result[1] = new LineSet(linesReverse) { Color = (Vector3)Direction };
                    Direction = !Direction;
                }
                return result;
            }

            public void IntegrateFurther(LineSet positions, float? maxTime = null)
            {
                Debug.Assert(Field.NumVectorDimensions <= 4);

                //int validPoints = 0;
                Parallel.For(0, positions.Length, index =>
                //for (int index = 0; index < positions.Length; ++index)
                {
                    if (positions[index].Status != Status.TIME_BORDER)
                        return;

                    Line append = IntegrateLineForRendering(
                        (positions[index].EndPoint),
                        maxTime);
                    positions[index] = positions[index].Append(append);
                });
            }

            protected virtual Status CheckPosition(Vector state, out Vector sample)
            {
                sample = null;
                Vector pos = Field.ToPosition(state);
                if (!Field.Grid.InGrid(pos))
                    return Status.BORDER;

                if (!Field.InTime(state.T))
                    return Status.TIME_BORDER;

                sample = Field.Sample(state);
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
