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
        protected static bool LOAD_STEPS = true;
        protected float RESPONSE_TIME = 0.25f;
        protected int TIMESTEP = 0;

        Stopwatch timeLoad = new Stopwatch();
        Stopwatch timeIntegrate = new Stopwatch();


        protected VectorField LoadToVectorField(FieldGrid grid, int startStep, VectorFieldUnsteady last = null)
        {
            if (last != null && STEPS_IN_MEMORY >= Aneurysm.Singleton.NumSteps - 1)
            {
                last.TimeOrigin = startStep * Aneurysm.Singleton.TimeScale;
                return last;
            }

            timeLoad.Start();
            LoaderEnsight attribLoader = new LoaderEnsight(Aneurysm.GeometryPart.Solid);
            
            VectorChannels[] buffers = new VectorChannels[STEPS_IN_MEMORY];
            for (int step = 0; step < STEPS_IN_MEMORY; ++step)
            {
                buffers[step] = attribLoader.LoadAttribute(Aneurysm.Variable.velocity, (startStep + step) % Aneurysm.Singleton.NumSteps);
            }

            //var fieldInertial = new VectorFieldInertial(new VectorDataArray<VectorChannels>(buffers), _grid, INERTIA);
            VectorFieldInertialUnsteady field = new VectorFieldInertialUnsteady(buffers, RESPONSE_TIME, grid, startStep * Aneurysm.Singleton.TimeScale, Aneurysm.Singleton.TimeScale);

            //Console.WriteLine($"==== Loaded Time Range [{field.TimeOrigin}, {field.TimeEnd}) ====\n");
            timeLoad.Stop();
            Console.WriteLine($"Loaded steps {startStep}-{startStep + STEPS_IN_MEMORY} = [{field.TimeOrigin},{field.TimeEnd}]\n\tTook {timeLoad.Elapsed} so far");
            return field;
        }

        protected LineSet IntegratePoints<P>(VectorField.Integrator integrator, FieldGrid grid, PointSet<P> points) where P : Point
        {
            int startStep = 0;

            // Load n time steps.
            // 0.005 is the timestep of the data. Overall they add up to 1 second.
            integrator.Field = LoadToVectorField(grid, startStep, integrator.Field as VectorFieldUnsteady);
            startStep += integrator.Field.Size.T - 2;
            timeIntegrate.Start();
            LineSet set = integrator.Integrate(points, false)[0];
            timeIntegrate.Stop();

            List<Vector> validPoints = set.GetEndPoints(VectorField.Integrator.Status.TIME_BORDER);

            //Console.WriteLine($"=== {validPoints.Count} Remaining Lines");
            while (validPoints.Count > 0 && startStep < 1500)
            {
                Console.WriteLine($"=== {validPoints.Count} Remaining Lines\n\tTook {timeIntegrate.Elapsed} so far");

                //for (int l = 0; l < set.Lines.Length; ++l)
                //{
                //    if (set.Lines[l].Status == VectorField.Integrator.Status.TIME_BORDER)
                //        Console.WriteLine($"\t\t{l}: Time {set[l].EndPoint?.T.ToString() ?? "None"}, length so far {set[l].Length}, pos {set[l].EndPoint}");
                //}

                integrator.Field = LoadToVectorField(grid, startStep, integrator.Field as VectorFieldUnsteady);
                startStep += integrator.Field.Size.T - 2;
                timeIntegrate.Start();
                integrator.IntegrateFurther(set);
                timeIntegrate.Stop();

                validPoints = set.GetEndPoints(VectorField.Integrator.Status.TIME_BORDER);
            }

            Console.WriteLine($"Total loading time: {timeLoad.Elapsed}\nTotal integaration time: {timeIntegrate.Elapsed}");
            return set;
        }

        public static void ComputeChunkSizeFromMemory()
        {
            ComputerInfo CI = new ComputerInfo();
            ulong totalMemory = ulong.Parse(CI.TotalPhysicalMemory.ToString());

            int filesize = Aneurysm.Singleton.StepInBytes;
            ulong reservedSpace = 3ul * 1024 * 1024 * 1024;
            Console.WriteLine($"Total available memory is {string.Format("{0:0.##}", (double)totalMemory / 1024 / 1024 / 1024)} GB");
            STEPS_IN_MEMORY = (int)((totalMemory - reservedSpace) / (ulong)filesize); // Leave some space for integration.

            // If there is enough space, only load the field once.
            if (STEPS_IN_MEMORY >= Aneurysm.Singleton.NumSteps)
                STEPS_IN_MEMORY = Aneurysm.Singleton.NumSteps + 2;

            //if (STEPS_IN_MEMORY < Aneurysm.Singleton.NumSteps - 1)
            //    STEPS_IN_MEMORY = 20;
            Console.WriteLine($"Fitting {STEPS_IN_MEMORY} time steps into memory at once");
        }
    }
}
