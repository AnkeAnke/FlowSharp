using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using System.Management;
using Microsoft.VisualBasic.Devices;

namespace FlowSharp
{
    abstract class IntegrationMapper : DataMapper
    {
        protected static int STEPS_IN_MEMORY = 50;
        protected float RESPONSE_TIME = 0.25f;
        protected int TIMESTEP = 0;

        Stopwatch timeLoad = new Stopwatch();
        Stopwatch timeIntegrate = new Stopwatch();

        protected VectorField LoadToVectorField(FieldGrid grid, int startStep, int stepOffset)
        {
            timeLoad.Start();
            LoaderEnsight attribLoader = new LoaderEnsight(Aneurysm.GeometryPart.Solid);

            int numSteps = Math.Min(Aneurysm.Singleton.NumSteps - startStep, STEPS_IN_MEMORY);
            VectorChannels[] buffers = new VectorChannels[numSteps];
            for (int step = 0; step < numSteps; ++step)
            {
                buffers[step] = attribLoader.LoadAttribute(Aneurysm.Variable.velocity, startStep + step);
            }

            //var fieldInertial = new VectorFieldInertial(new VectorDataArray<VectorChannels>(buffers), _grid, INERTIA);
            VectorFieldInertialUnsteady field = new VectorFieldInertialUnsteady(buffers, RESPONSE_TIME, grid, (stepOffset + startStep) * Aneurysm.Singleton.TimeScale, Aneurysm.Singleton.TimeScale);

            //Console.WriteLine($"==== Loaded Time Range [{field.TimeOrigin}, {field.TimeEnd}) ====\n");
            timeLoad.Stop();
            return field;
        }

        protected LineSet IntegratePoints<P>(VectorField.Integrator integrator, FieldGrid grid, PointSet<P> points) where P : Point
        {
            int startStep = 0;

            // Load n time steps.
            // 0.005 is the timestep of the data. Overall they add up to 1 second.
            integrator.Field = LoadToVectorField(grid, startStep, 0);
            startStep += integrator.Field.Size.T - 1;
            timeIntegrate.Start();
            LineSet set = integrator.Integrate(points, false)[0];
            timeIntegrate.Stop();

            List<Vector> validPoints = set.GetEndPoints(VectorField.Integrator.Status.TIME_BORDER);

            Console.WriteLine($"=== {validPoints.Count} Remaining Lines");
            int numIteration = 0;
            while (validPoints.Count > 0)
            {
                while (validPoints.Count > 0 && startStep < Aneurysm.Singleton.NumSteps - 1)
                {
                    integrator.Field = LoadToVectorField(grid, startStep, numIteration * Aneurysm.Singleton.NumSteps);
                    startStep += integrator.Field.Size.T - 1;
                    timeIntegrate.Start();
                    integrator.IntegrateFurther(set);
                    timeIntegrate.Stop();

                    validPoints = set.GetEndPoints(VectorField.Integrator.Status.TIME_BORDER);
                    Console.WriteLine($"=== {validPoints.Count} Remaining Lines");
                }

                startStep = 0;
                numIteration++;
            }

            Console.WriteLine($"Total loading time: {timeLoad.Elapsed}\nTotal integaration time: {timeIntegrate.Elapsed}");
            return set;
        }

        public static void ComputeChunkSizeFromMemory()
        {
            ComputerInfo CI = new ComputerInfo();
            ulong totalMemory = ulong.Parse(CI.TotalPhysicalMemory.ToString());

            int filesize = Aneurysm.Singleton.StepInBytes;
            ulong fourGB = 4ul * 1024 * 1024 * 1024;
            Console.WriteLine($"Total available memory is {string.Format("{0:0.##}", (double)totalMemory / 1024 / 1024 / 1024)} GB");
            STEPS_IN_MEMORY = (int)((totalMemory - fourGB) / (ulong)filesize); // Leave some space for integration.
            Console.WriteLine($"Fitting {STEPS_IN_MEMORY} time steps into memory at once");
        }
    }
}
