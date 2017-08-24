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
        public class IntegratorEuler : Integrator
        {
            public IntegratorEuler(VectorField field)
            {
                Field = field;
            }
            int counter = 0;
            public override Status Step(Vector point, Vector sample, out Vector next, out Vector nextSample, out float stepLength)
            {
                ++counter;
                next = new Vector(point);
                stepLength = 0;
                nextSample = sample;

                if (!ScaleAndCheckVector(sample, out sample))
                    return Status.CP;

                if (float.IsNaN(sample[0]))
                    Console.WriteLine("NaN NaN NaN NaN WATMAN!");

                next += sample;
                stepLength += sample.LengthEuclidean();

                //Console.WriteLine($"===2 Point {point}, sample {sample}, next {next}");
                return CheckPosition(next, out nextSample);
            }

            public override bool StepBorder(Vector position, Vector dir, out Vector nextPos, out float stepLength)
            {
                nextPos = new Vector(position);
                stepLength = 0;
                dir *= (int)Direction;
                if (NormalizeField)
                    dir.Normalize();

                nextPos = Field.Grid.CutToBorder(Field, position, dir);

                stepLength = (nextPos - position).LengthEuclidean();
                //// How big is the smallest possible scale to hit a maximum border?
                //float scale = (((Vector)Field.Size - new Vector(1, Field.Size.Length) - position) / dir).MinPos();
                //scale = Math.Min(scale, (position / dir).MinPos());

                //if (scale >= StepSize)
                //    return false;

                //nextPos += dir * scale;
                //stepLength = dir.LengthEuclidean() * scale;
                return true;
            }

            protected bool ScaleAndCheckVector(Vector vec, out Vector sample)
            {
                sample = new Vector(vec);
                float length = sample.LengthEuclidean();
                if (NormalizeField)
                    sample = sample / length;
                sample *= StepSize * (int)Direction;

                if (length < EpsCriticalPoint)
                    return false;

                return true;
            }

            public override bool StepBorderTime(Vector position, Vector dir, float timeBorder, out Vector stepped, out float stepLength)
            {
                stepped = new Vector(position);
                stepLength = 0;
                dir *= (int)Direction;
                if (NormalizeField)
                    dir.Normalize();

                // How big is the smallest possible scale to hit a maximum border?
                Vector timeSize = (Vector)Field.Size - new Vector(1, Field.Size.Length);
                timeSize.T = timeBorder - 1;
                float scale = ((timeSize - position) / dir).MinPos();
                scale = Math.Min(scale, (position / dir).MinPos());

                if (scale >= StepSize)
                    return false;

                stepped += dir * scale;
                stepLength = dir.LengthEuclidean() * scale;
                return true;
            }
        }
    }
}
