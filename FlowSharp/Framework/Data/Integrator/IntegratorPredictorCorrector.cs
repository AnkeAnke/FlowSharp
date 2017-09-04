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
        public class IntegratorPredictorCorrector : Integrator
        {
            public float EpsCorrector = 0.00001f;

            // Two integrators. THis way, step size, integration type, field etc can be set individually.
            public Integrator Predictor, Corrector;
            public IntegratorPredictorCorrector(Integrator predictor, Integrator corrector) : base()
            {
                Predictor = predictor;
                Corrector = corrector;
                Debug.Assert(Predictor.Field.NumVectorDimensions >= Corrector.Field.NumVectorDimensions, "Predictor is " + Predictor.Field.NumVectorDimensions + "D, Corrector is " + Corrector.Field.NumVectorDimensions + "D!");

                Field = Predictor.Field;
            }

            public override Status Step(ref Vector state, out float stepLength)
            {
                stepLength = 0;
                Vector step, next;
                Status status = CheckPosition(state, out step);
                if (status != Status.OK)
                    return status;

                step *= StepSize * (int)Direction;

                state += step;

                stepLength += step.LengthEuclidean();

                int stepCount = -1;
                do
                {
                    stepCount++;
                    status = Corrector.Step(ref state, out stepLength);
                } while (status == Status.OK && stepCount < Corrector.MaxNumSteps);

                if (status == Status.CP)
                    return Status.OK;
                return status;
            }

            //TODO: Correct.
            public override bool StepBorder(Vector state, ref Vector nextState, out float stepLength)
            {
                return Predictor.StepBorder(state, ref nextState, out stepLength);
            }

            //TODO: Correct.
            public override bool StepBorderTime(Vector state, ref Vector nextState, float timeBorder, out float stepLength)
            {
                return Predictor.StepBorderTime(state, ref nextState, timeBorder, out stepLength);
            }
        }
    }
}
