
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
//        GeneralUnstructurdGrid _geometryWall;
//        Mesh _meshWall;
        PointSet<DirectionPoint> _points;
        TetTreeGrid _grid;

//        VectorField _vectorField;
        LineSet _streamLines;//, _streamLinesSelected;
                             //        LineBall _streamBall;//, _streamBallSelected;

        public ParticleMapper(Plane basePlane, int numSamples)
        {
            Mapping = ShowSides;
            BasePlane = basePlane;
            TIMESTEP = -1;

            //// Load Geometry
            LoaderVTU geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Solid);
            var hexGrid = geomLoader.LoadGeometry();
            //geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Wall);
            //_geometryWall = geomLoader.LoadGeometry();

            //this.BasePlane = Plane.FitToPoints(Vector3.Zero, 4, hexGrid.Vertices);
            BasePlane.PointSize = 0.1f;
            //Octree attributeTree = Octree.LoadOrComputeWrite(wallGrid.Vertices, 10, 10, Aneurysm.GeometryPart.Wall, float.MaxValue);


            //// Load grid.
            LoaderEnsight attribLoader = new LoaderEnsight(Aneurysm.GeometryPart.Solid);
            _grid = new TetTreeGrid(hexGrid, Aneurysm.GeometryPart.Solid, 1, 10);

            //// Load inlet for seeding.
            LoaderVTU inletLoader = new LoaderVTU(Aneurysm.GeometryPart.Inlet);
            var inletGrid = inletLoader.LoadGeometry();


            //// Setup integrator.
            VectorField.Integrator integrator = new VectorField.IntegratorEuler(null);
            integrator.StepSize = _grid.CellSizeReference * 4;
            integrator.NormalizeField = true;
            integrator.MaxNumSteps = 1000000;
            integrator.EpsCriticalPoint = 0.00000f;

            //// Load all inlet velocity time steps to sample initial speed.
            LoaderEnsight inletAttributeLoader = new LoaderEnsight(Aneurysm.GeometryPart.Inlet);
            VectorData vel = inletAttributeLoader.LoadFieldTimeBatch(Aneurysm.Variable.velocity, 0, Aneurysm.Singleton.NumSteps);

            Stopwatch timeWrite = new Stopwatch();

            int run = 0;

            while(true)
            {
                run++;
                timeWrite.Start();

                PointSet<DirectionPoint> points = inletGrid.SampleRandom(numSamples, vel);
                points.RandomizeTimes(0, Aneurysm.Singleton.NumSteps * Aneurysm.Singleton.TimeScale);
                Console.WriteLine($"=====\n===== Run {run}: Sampling {points.Length} points =====\n=====");

                Stopwatch watch = new Stopwatch();
                watch.Start();

                //// Inertial
                RESPONSE_TIME = 0.000001821f;
                integrator.StepSize = 0.5f;

                _streamLines = this.IntegratePoints(integrator, _grid, points);

                watch.Stop();
                Console.WriteLine($"==== Integrating {points.Length} points took {watch.Elapsed}. ");

                _streamLines.Color = Vector3.UnitZ;

                //List<Line> specialLines = new List<Line>();
                //foreach (Line line in _streamLines.Lines)
                //    if (line.EndPoint.T > 5)
                //        specialLines.Add(line);
                //_streamLinesSelected = new LineSet(specialLines.ToArray());
                _streamLines.Color = Vector3.UnitZ;
                //_streamLinesSelected.Color = Vector3.UnitX;


                VectorBuffer ends = _streamLines.GetEndPointBuffer();
                _points = _streamLines.GetAllEndPoints();
                _streamLines.Thickness *= 0.2f;
                //_streamLinesSelected.Thickness *= 0.4f;

                string filenameHits = Aneurysm.Singleton.CustomAttributeFilename("ParticleHits", Aneurysm.GeometryPart.Wall);
                //if (!File.Exists(filenameHits))
                //    File.Create(filenameHits);

                BinaryFile.WriteFile(filenameHits, ends, FileMode.Append);

                Console.WriteLine($"Time to write: {timeWrite.Elapsed}");
            }

            _points = null;
        }

        protected List<Renderable> ShowSides()
        {
            List<Renderable> renderables = new List<Renderable>();

            //if (_lastSetting == null)
            //{
            //    LoaderEnsight loader = new LoaderEnsight(Aneurysm.GeometryPart.Wall);
            //    VectorChannels pressure = loader.LoadAttribute(Aneurysm.Variable.pressure, 0);

            //    pressure.ExtractMinMax();

            //    _meshWall = new Mesh(BasePlane, _geometryWall, pressure);

            //    _meshWall.LowerBound = pressure.MinValue[0];
            //    _meshWall.UpperBound = pressure.MaxValue[0];
            //    _meshWall.UsedMap = Colormap.Gray;

            //    _streamBall = new LineBall(BasePlane, _streamLines, LineBall.RenderEffect.HEIGHT);
            //    //_streamBallSelected = new LineBall(BasePlane, _streamLinesSelected, LineBall.RenderEffect.DEFAULT);
            //}

            //if (_lastSetting == null || ColormapChanged)
            //{
            //    _streamBall.UsedMap = Colormap;
            //    //_streamBallSelected.UsedMap = Colormap;
            //}
            //if (_lastSetting == null || WindowStartChanged || WindowWidthChanged)
            //{
            //    _streamBall.LowerBound = WindowStart;
            //    _streamBall.UpperBound = WindowStart + WindowWidth;
            //    _streamBall.UsedMap = Colormap;
            //    Console.WriteLine($"{WindowStart} - {WindowWidth}");
            //}


            //renderables.Add(_streamBall);
            ////renderables.Add(_streamBallSelected);
            //renderables.Add(_meshWall);

            return renderables;
        }

        public override bool IsUsed(Setting.Element element)
        {
            switch(element)
            {
                case Setting.Element.Colormap:
                case Setting.Element.WindowStart:
                case Setting.Element.WindowWidth:
                    return true;
                default:
                    return false;
            }
        }

        public override float? GetMin(Setting.Element element)
        {
            switch(element)
            {
                case Setting.Element.WindowStart:
                    return 0;
                case Setting.Element.WindowWidth:
                    return 0;
            }
            return base.GetMin(element);
        }

        public override float? GetMax(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.WindowStart:
                    return 3;
                case Setting.Element.WindowWidth:
                    return 3;
            }
            return base.GetMin(element);
        }
    }
}
