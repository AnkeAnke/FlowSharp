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
                //Console.WriteLine($"Position {state}");
                Status stat = CheckPosition(state, out step);
                //Console.WriteLine($"State {stat}\nStep {step}");
                if (stat != Status.OK)
                    return stat;

                if (!ScaleAndCheckVector(step))
                    return Status.CP;
                //Console.WriteLine($"Step then {step}");
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
                float length = Field.ToPosition(sample).LengthEuclidean();
                if (NormalizeField)
                    sample = sample / length;
                sample *= StepSize * (int)Direction;

                if (length < EpsCriticalPoint)
                    return false;

                return true;
            }

            public override bool StepBorderTime(Vector state, ref Vector nextState, float timeBorder, out float stepLength)
            {
                // Derive direction from failed next position.
                Vector dir = nextState - state;
                stepLength = 0;

                // How big is the smallest possible scale to hit a maximum border?
                Vector timeSize = (Vector)Field.Size - new Vector(1, Field.Size.Length);
                timeSize.T = timeBorder - 1;
                float scale = ((timeSize - state) / dir).MinPos();
                scale = Math.Min(scale, (state / dir).MinPos());

                if (scale >= StepSize)
                    return false;

                nextState = state + dir * scale;
                stepLength = dir.LengthEuclidean() * scale;
                return true;
            }
        }
    }
}
