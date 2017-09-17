
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SlimDX;
using System.Runtime.InteropServices;
using System.IO;

namespace FlowSharp
{
    class ParticleMapper : IntegrationMapper
    {
        GeneralUnstructurdGrid _geometry;
        PointSet<DirectionPoint> _points;
        TetTreeGrid _grid;

        VectorField _vectorField;
        LineSet _streamLines;

        public ParticleMapper()
        {
            Mapping = null;
            TIMESTEP = -1;

            // Load Geometry
            LoaderVTU geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Solid);
            var hexGrid = geomLoader.LoadGeometry();
            geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Wall);
            var wallGrid = geomLoader.LoadGeometry();
            Octree attributeTree = Octree.LoadOrComputeWrite(wallGrid.Vertices, 10, 10, Aneurysm.GeometryPart.Wall, float.MaxValue);


            // Load grid.
            LoaderEnsight attribLoader = new LoaderEnsight(Aneurysm.GeometryPart.Solid);
            _grid = new TetTreeGrid(hexGrid, Aneurysm.GeometryPart.Solid, 1, 10);

            // Load inlet for seeding.
            LoaderVTU inletLoader = new LoaderVTU(Aneurysm.GeometryPart.Inlet);
            var inletGrid = inletLoader.LoadGeometry();


            // Setup integrator.
            VectorField.Integrator integrator = new VectorField.IntegratorEuler(_vectorField);
            integrator.StepSize = _grid.CellSizeReference / 2;
            integrator.NormalizeField = true;
            integrator.MaxNumSteps = 1000000;
            integrator.EpsCriticalPoint = 0;

            // Load all inlet velocity time steps to sample initial speed.
            LoaderEnsight inletAttributeLoader = new LoaderEnsight(Aneurysm.GeometryPart.Inlet);
            VectorData vel = inletAttributeLoader.LoadFieldTimeBatch(Aneurysm.Variable.velocity, 0, Aneurysm.Singleton.NumSteps);

            Stopwatch timeWrite = new Stopwatch();
            timeWrite.Start();
            PointSet<DirectionPoint> points = inletGrid.SampleRandom(100, vel);
            points.RandomizeTimes(0, Aneurysm.Singleton.NumSteps);
            Console.WriteLine($"=====\n===== Sampling {points.Length} points =====\n=====");

            Stopwatch watch = new Stopwatch();
            watch.Start();


            // Inertial
            RESPONSE_TIME = 0.000001821f;
            integrator.StepSize = 0.5f;

            _streamLines = this.IntegratePoints(integrator, _grid, points);
            _streamLines.Color = Vector3.UnitZ;

            watch.Stop();
            Console.WriteLine($"==== Integrating {points.Length} points took {watch.Elapsed}. ");


            VectorBuffer ends = _streamLines.GetEndPointBuffer();
            _streamLines.Thickness *= 0.1f;

            string filenameHits = Aneurysm.Singleton.CustomAttributeFilename("ParticleHits", Aneurysm.GeometryPart.Wall);
            if (!File.Exists(filenameHits))
                File.Create(filenameHits);

            BinaryFile.WriteFile(filenameHits, ends, FileMode.Append);

            Console.WriteLine($"Time to splat: {timeWrite.Elapsed}");

            _points = null;
        }

        public override bool IsUsed(Setting.Element element)
        {
            return false;
        }
    }
}
