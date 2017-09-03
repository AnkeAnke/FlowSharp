using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    abstract class IntegrationMapper : DataMapper
    {
        protected static int STEPS_IN_MEMORY = 10;
        protected float INERTIA = 0.25f;
        protected int TIMESTEP = 0;

        protected VectorField LoadToVectorField(FieldGrid grid, int startStep, int stepOffset)
        {
            LoaderEnsight attribLoader = new LoaderEnsight(Aneurysm.GeometryPart.Solid);

            int numSteps = Math.Min(Aneurysm.Singleton.NumSteps - startStep, STEPS_IN_MEMORY);
            VectorChannels[] buffers = new VectorChannels[numSteps];
            for (int step = 0; step < numSteps; ++step)
            {
                buffers[step] = attribLoader.LoadAttribute(Aneurysm.Variable.velocity, startStep + step);
            }

            //var fieldInertial = new VectorFieldInertial(new VectorDataArray<VectorChannels>(buffers), _grid, INERTIA);
            VectorFieldInertialUnsteady field = new VectorFieldInertialUnsteady(buffers, INERTIA, grid, (stepOffset + startStep) * Aneurysm.Singleton.TimeScale, Aneurysm.Singleton.TimeScale);

            return field;
        }

        protected LineSet IntegratePoints<P>(VectorField.Integrator integrator, FieldGrid grid, PointSet<P> points, int startStep = 0) where P : Point
        {
            if (startStep != 0)
                points.SetTime(Aneurysm.Singleton.TimeScale * startStep);

            // Load some attribute.
            // 0.005 is the timestep of the data. Overall they add up to 1 second.
            integrator.Field = LoadToVectorField(grid, startStep, 0);
            startStep += integrator.Field.Size.T;
            LineSet set = integrator.Integrate(points, false)[0];

            PointSet<InertialPoint> validPoints = set.GetEndPoints(VectorField.Integrator.Status.TIME_BORDER);
            foreach (Line l in set.Lines)
            {
                Console.Write(l.Status);
                if (l.Length > 0)
                    Console.Write(" at time " + l.Last.W);
                Console.Write('\n');
            }

            int numIteration = 0;
            while (validPoints.Length > 0)
            {
                while (startStep < Aneurysm.Singleton.NumSteps)
                {
                    integrator.Field = LoadToVectorField(grid, startStep, numIteration * Aneurysm.Singleton.NumSteps);
                    startStep += integrator.Field.Size.T;
                    integrator.IntegrateFurther(set);
                }

                validPoints = set.GetEndPoints(VectorField.Integrator.Status.TIME_BORDER);
                numIteration++;
            }
            return set;
        } 

    }
}
