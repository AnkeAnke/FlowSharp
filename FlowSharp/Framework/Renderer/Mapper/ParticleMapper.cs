
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
        GeneralUnstructurdGrid _geometryWall;
        Mesh _meshWall;
        PointSet<DirectionPoint> _points;
        TetTreeGrid _grid;

        VectorField _vectorField;
        LineSet _streamLines, _streamLinesSelected;
        LineBall _streamBall, _streamBallSelected;

        public ParticleMapper(Plane basePlane)
        {
            Mapping = ShowSides;
            BasePlane = basePlane;
            TIMESTEP = -1;

            // Load Geometry
            LoaderVTU geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Solid);
            var hexGrid = geomLoader.LoadGeometry();
            geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Wall);
            _geometryWall = geomLoader.LoadGeometry();
            //Octree attributeTree = Octree.LoadOrComputeWrite(wallGrid.Vertices, 10, 10, Aneurysm.GeometryPart.Wall, float.MaxValue);


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
            integrator.EpsCriticalPoint = 0.00000f;

            // Load all inlet velocity time steps to sample initial speed.
            LoaderEnsight inletAttributeLoader = new LoaderEnsight(Aneurysm.GeometryPart.Inlet);
            VectorData vel = inletAttributeLoader.LoadFieldTimeBatch(Aneurysm.Variable.velocity, 0, Aneurysm.Singleton.NumSteps);

            Stopwatch timeWrite = new Stopwatch();
            timeWrite.Start();
            //
            //     !BEWARE! Fixed Random right now !BEWARE!
            //
            PointSet<DirectionPoint> points = inletGrid.SampleRandom(100, vel);
            points.RandomizeTimes(0, Aneurysm.Singleton.NumSteps * Aneurysm.Singleton.TimeScale);
            //for (int i = 0; i < points.Length; ++i)
            //    Console.WriteLine($"{i}: {points[i].Position.W} - {points[i].Position} -> {points[i].Direction}");
            Console.WriteLine($"=====\n===== Sampling {points.Length} points =====\n=====");

            Stopwatch watch = new Stopwatch();
            watch.Start();

            // Inertial
            RESPONSE_TIME = 0.000001821f;
            integrator.StepSize = 0.5f;

            _streamLines = this.IntegratePoints(integrator, _grid, points);

            watch.Stop();
            Console.WriteLine($"==== Integrating {points.Length} points took {watch.Elapsed}. ");

            _streamLines.Color = Vector3.UnitZ;

            List<Line> specialLines = new List<Line>();
            foreach (Line line in _streamLines.Lines)
                if (line.Length > 2000000)
                    specialLines.Add(line);
            _streamLinesSelected = new LineSet(specialLines.ToArray());
            _streamLines.Color = Vector3.UnitZ;
            _streamLinesSelected.Color = new Vector3(0, 1, 1);


            VectorBuffer ends = _streamLines.GetEndPointBuffer();
            _points = _streamLines.GetAllEndPoints();
            _streamLines.Thickness *= 0.05f;
            _streamLinesSelected.Thickness *= 0.1f;

            string filenameHits = Aneurysm.Singleton.CustomAttributeFilename("ParticleHits", Aneurysm.GeometryPart.Wall);
            if (!File.Exists(filenameHits))
                File.Create(filenameHits);

            BinaryFile.WriteFile(filenameHits, ends, FileMode.Append);

            Console.WriteLine($"Time to splat: {timeWrite.Elapsed}");

            _points = null;
        }

        protected List<Renderable> ShowSides()
        {
            List<Renderable> renderables = new List<Renderable>();

            if (_lastSetting == null)
            {
                _meshWall = new Mesh(BasePlane, _geometryWall);

                _streamBall = new LineBall(BasePlane, _streamLines, LineBall.RenderEffect.THIN);
                _streamBallSelected = new LineBall(BasePlane, _streamLinesSelected, LineBall.RenderEffect.DEFAULT);
            }

            if (_lastSetting == null || ColormapChanged)
            {
                _meshWall.UsedMap = Colormap;
            }


            renderables.Add(_streamBall);
            renderables.Add(_streamBallSelected);
            renderables.Add(_meshWall);

            return renderables;
        }

        public override bool IsUsed(Setting.Element element)
        {
            switch(element)
            {
                case Setting.Element.Colormap:
                    return true;
                default:
                    return false;
            }
        }
    }
}
