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

            public override Status Step(ref Vector state, out float stepLength)
            {
                stepLength = 0;
                Vector step;

                Status stat = CheckPosition(state, out step);
                //Console.WriteLine($"State {stat}\nStep {step}");
                if (stat != Status.OK)
                    return stat;

                if (!ScaleAndCheckVector(step))
                    return Status.CP;

                state += step;

                stepLength += step.LengthEuclidean();
                return Status.OK;
            }

            public override bool StepBorder(Vector state, ref Vector nextState, out float stepLength)
            {
                stepLength = 0;

                nextState = Field.Grid.CutToBorder(Field, state, nextState-state);

                stepLength = (nextState - state).LengthEuclidean();

                return true;
            }

            protected bool ScaleAndCheckVector(Vector sample)
            {
                Vector timeless = new Vector(sample);
                if (Field.IsUnsteady())
                    timeless.T = 0;
                float length = Field.ToPosition(timeless).LengthEuclidean();
                if (NormalizeField)
                    sample = sample / length;
                sample *= StepSize * (int)Direction;

                if (length <= EpsCriticalPoint)
                    return false;

                return true;
            }

            public override bool StepBorderTime(Vector state, ref Vector nextState, float timeBorder, out float stepLength)
            {
                stepLength = 0;
                //// Derive direction from failed next position.
                //Vector dir = nextState - state;
                //Console.WriteLine($"Direction: {dir}");
                //stepLength = 0;

                //// How big is the smallest possible scale to hit a maximum border?
                //float scale = ((timeBorder - state.T) / dir.T);
                //Console.WriteLine($"Scale: {(timeBorder - state.T)} / {dir.T} = {scale}");
                //if (scale >= StepSize)
                //    return false;

                //nextState = state + dir * scale;
                //nextState.T = timeBorder + dir.T * 0.001f;
                //stepLength = Field.ToPosition(dir).LengthEuclidean() * scale;
                return true;
            }
        }
    }
}
