
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SlimDX;
using System.Runtime.InteropServices;

namespace FlowSharp
{
    class TetGridMapper : IntegrationMapper
    {


        Mesh _viewGeom;

        ColormapRenderable _test;
        GeneralUnstructurdGrid _geometry;
        //Index[] _indices;
        //bool update = true;
        PointSet<Point> _points;
        PointCloud _vertices;
        VectorData _attribute;
        TetTreeGrid _grid;

        VectorField _vectorField;
        List<LineSet> _streamLines;
        List<LineBall> _streamBall;

        VectorData _canvas;

        public TetGridMapper(Plane plane) : base()
        {
            Mapping = ShowSide;
            BasePlane = plane;

            // Load Geometry
            LoaderVTU geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Solid);
            var hexGrid = geomLoader.LoadGeometry();
            geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Wall);
            var wallGrid = geomLoader.LoadGeometry();
            
            // Fit plane to data.
            this.BasePlane = Plane.FitToPoints(Vector3.Zero, 4, hexGrid.Vertices);
            BasePlane.PointSize = 0.1f;
            
            // Load grid.
            LoaderEnsight attribLoader = new LoaderEnsight(Aneurysm.GeometryPart.Solid);
            _grid = new TetTreeGrid(hexGrid, Aneurysm.GeometryPart.Solid, 1, 10);

            // Load inlet for seeding.
            LoaderVTU inletLoader = new LoaderVTU(Aneurysm.GeometryPart.Inlet);
            var inlet = inletLoader.LoadGeometry();
            LoaderEnsight inletAttributeLoader = new LoaderEnsight(Aneurysm.GeometryPart.Inlet);
            VectorData vel = inletAttributeLoader.LoadAttribute(Aneurysm.Variable.velocity, TIMESTEP);

            // Load one vector field for comparison.
            _vectorField = new VectorField(attribLoader.LoadAttribute(Aneurysm.Variable.velocity, 0), _grid);
            VectorField.Integrator integrator = new VectorField.IntegratorEuler(_vectorField);
            integrator.StepSize = _grid.CellSizeReference / 2;
            integrator.NormalizeField = true;
            integrator.MaxNumSteps = 10000;
            integrator.EpsCriticalPoint = 0;

           // while (true)
            {
                PointSet<InertialPoint> points = inlet.SampleRandom(3, vel);
//                _points.SetTime(timestep);

                Stopwatch watch = new Stopwatch();
                watch.Start();
                _streamLines = new List<LineSet>(3);


                // Inertial
                RESPONSE_TIME = 0.000001821f;
                integrator.StepSize = 1;
                //_streamLines.Add(integrator.Integrate(points)[0]);
                _streamLines.Add(this.IntegratePoints(integrator, _grid, points, 0));
                _streamLines.Last().Color = Vector3.UnitZ;

                integrator.StepSize = 0.25f;
                _streamLines.Add(this.IntegratePoints(integrator, _grid, points, 0));
                _streamLines.Last().Color = new Vector3(0, 1, 1);

                watch.Stop();
                Console.WriteLine($"==== Integrating {points.Length} points took {watch.Elapsed}. ");

                foreach (LineSet lines in _streamLines)
                    lines.Thickness *= 0.4f;
                _points = _streamLines?[0].GetAllEndPoints().ToBasicSet() ?? _points;


//                Octree attributeTree = Octree.LoadOrComputeWrite(wallGrid.Vertices, 10, 10, Aneurysm.GeometryPart.Wall, float.MaxValue);
//                _canvas = BinaryFile.ReadFile(Aneurysm.Singleton.CustomAttributeFilename($"SplatInt_{INERTIA}", Aneurysm.GeometryPart.Wall), 1);
//                if (_canvas == null)
//                    _canvas = new VectorBuffer(wallGrid.Vertices.Length, 1);
//                this.SplatToAttribute(attributeTree, _canvas, _points, attributeTree.Extent.Max() * 0.02f);
//                BinaryFile.WriteFile(Aneurysm.Singleton.CustomAttributeFilename($"SplatInt_{INERTIA}", Aneurysm.GeometryPart.Wall), _canvas);
            }
            //_tree = new KDTree(geomLoader.Grid, 100);
        }

        public List<Renderable> ShowSide()
        {
            bool updateCubes = false;
            // Assemble renderables.
            var wire = new List<Renderable>(5);
            if (_lastSetting == null || GeometryPartChanged)
            {
                LoaderVTU geomLoader = new LoaderVTU(GeometryPart);
                var hexGrid = geomLoader.LoadGeometry();
                if (hexGrid == null)
                    Console.WriteLine("What?");

                //int divFactor = 10000;
                //IndexArray subset = new IndexArray(hexGrid.Primitives.Length / divFactor, hexGrid.Primitives.IndexLength);
                //for (int s = 0; s < subset.Length; ++s)
                //    subset[s] = hexGrid.Primitives[s * divFactor];
                //hexGrid.Primitives = subset;


                _geometry = geomLoader.Grid;

                //update = false;
                updateCubes = true;
            }

            if (_lastSetting == null ||
                GeometryPartChanged ||
                MeasureChanged)
            {
                if (GeometryPart == Aneurysm.GeometryPart.Wall)
                {
                    _attribute = BinaryFile.ReadFile(Aneurysm.Singleton.CustomAttributeFilename($"SplatInt_{RESPONSE_TIME}", Aneurysm.GeometryPart.Wall), 1);
                }
                if (GeometryPart != Aneurysm.GeometryPart.Wall || _attribute == null)
                {
                    LoaderEnsight attribLoader = new LoaderEnsight(GeometryPart);
                    _attribute = attribLoader.LoadAttribute((Aneurysm.Variable)(int)Measure, 0);
                }
                _attribute.ExtractMinMax();
                updateCubes = true;
            }

            if (updateCubes)
            {
                _viewGeom = new Mesh(BasePlane, _geometry, _attribute);
            }

            if (_streamLines != null &&
                _streamLines.Count > 0 &&
                (_lastSetting == null ||
                _streamBall == null))
            {
                _streamBall = new List<LineBall>(_streamLines.Count);
                foreach (LineSet lines in _streamLines)
                {
                    _streamBall.Add(new LineBall(BasePlane, lines, LineBall.RenderEffect.HEIGHT));

                    foreach (LineBall ball in _streamBall)
                    {
                        ball.LowerBound = 0;
                    }
                }
            }

            if (_streamBall != null)
                wire.AddRange(_streamBall);

            if (_lastSetting == null ||
                GeometryPartChanged ||
                WindowWidthChanged ||
                WindowStartChanged ||
                ColormapChanged ||
                updateCubes)
            {
                _viewGeom.LowerBound = WindowStart;
                _viewGeom.UpperBound = WindowStart + WindowWidth;
                _viewGeom.UsedMap = Colormap;

                if (_streamBall != null)
                {
                    foreach (LineBall ball in _streamBall)
                    {
                        ball.UsedMap = ColorMapping.GetComplementary(Colormap);
                        ball.LowerBound = WindowStart;
                        ball.UpperBound = WindowStart + WindowWidth;
                    }
                    _streamBall[0].UsedMap = Colormap.Red;
                    _streamBall[1].UsedMap = Colormap.Green;
                }
            }

            wire.Add(_viewGeom);

            if (_test != null)
                wire.Add(_test);



            if (_points != null && _points.Length > 0 && (_vertices == null || updateCubes))
            {
                _vertices = new PointCloud(BasePlane, _points);
            }
            if (_vertices != null)
                wire.Add(_vertices);


            //if (_octreeLeafs == null)
            //    _octreeLeafs = new Mesh(BasePlane, _tree.LeafGeometry());

            //wire.Add(_octreeLeafs);

            //if (_vertices == null)
            //    _vertices = new PointCloud(BasePlane, _geometry.GetVertices());
            //wire.Add(_vertices);

            var axes = BasePlane.GenerateOriginAxisGlyph();
            wire.AddRange(axes);
            return wire;

        }

        private void SplatToAttribute<P>(Octree attributeTree, VectorData canvas, PointSet<P> points, float radius) where P : Point
        {
            foreach (P p in points.Points)
            {
                List<Octree.IndexDistance> verts = attributeTree.FindWithinRadius(Util.Convert(p.Position), radius);
                foreach (Octree.IndexDistance v in verts)
                    canvas[v.VertexIndex] += 1.0f / v.Distance;
            }
            canvas.MaxValue = null;
        }

        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.Colormap:
                case Setting.Element.WindowStart:
                case Setting.Element.WindowWidth:
                case Setting.Element.Measure:
                case Setting.Element.GeometryPart:
                    return true;
                default:
                    return false;
            }
        }

        public override float? GetMin(Setting.Element element)
        {
            _attribute.ExtractMinMax();
            float min = _attribute?.MinValue?[0] ?? -500f;

            switch (element)
            {
                case Setting.Element.WindowWidth:
                    return 0;
                case Setting.Element.WindowStart:
                    return min;
                default:
                    return base.GetMin(element);
            }
        }

        public override float? GetMax(Setting.Element element)
        {
            _attribute.ExtractMinMax();
            float min = _attribute?.MinValue?[0] ?? -500f;
            float max = _attribute?.MaxValue?[0] ?? 500;
            if (max == min)
                max = min + 0.001f;
            //Console.WriteLine("Attribute {2}:\n\tAttribute min {0}\n\tAttribute max {1}", min, max, element.ToString());
            switch (element)
            {
                case Setting.Element.WindowWidth:
                    return max - min;
                case Setting.Element.WindowStart:
                    return max;
                default:
                    return base.GetMax(element);
            }
        }
    }
}
