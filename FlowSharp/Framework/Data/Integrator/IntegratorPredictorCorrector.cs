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

            public override Status Step(Vector pos, Vector sample, Vector inertial, out Vector nextPos, out Vector nextSample, out float stepLength)
            {
                // One predictor step.
                Status status = Predictor.Step(pos, sample, inertial, out nextPos, out nextSample, out stepLength);
                if (status != Status.OK)
                    return status;
                // Now, step until the corrector reaches a critical point.
                Vector point;
                Vector next = nextPos;
                if (CheckPosition(next, inertial, out sample) != Status.OK)
                {
                    StepBorder(pos, sample, out nextPos, out stepLength);
                    return CheckPosition(nextPos, inertial, out sample);
                }
                int step = -1;
                do
                {
                    step++;
                    point = next;
                    status = Corrector.Step(point, sample, inertial, out next, out nextSample, out stepLength);
                } while (status == Status.OK && step < Corrector.MaxNumSteps);

                if (status == Status.CP)
                    return Status.OK;
                return status;
            }

            //TODO: Correct.
            public override bool StepBorder(Vector position, Vector sample, out Vector stepped, out float stepLength)
            {
                return Predictor.StepBorder(position, sample, out stepped, out stepLength);
            }

            //TODO: Correct.
            public override bool StepBorderTime(Vector position, Vector sample, float timeBorder, out Vector stepped, out float stepLength)
            {
                return Predictor.StepBorderTime(position, sample, timeBorder, out stepped, out stepLength);
            }
        }
    }
}
