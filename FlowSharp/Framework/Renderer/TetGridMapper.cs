
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

        float INERTIA = 0.1f;
        //LineSet _wireframe;
        //PointSet<Point> _vertices;
        Mesh _viewGeom;

        ColormapRenderable _test;
        //TetTreeGrid _grid;'
        GeneralUnstructurdGrid _geometry;
        //Index[] _indices;
        //bool update = true;
        PointSet<Point> _points;
        PointCloud _vertices;
        VectorData _attribute;

        VectorField _vectorField;
        List<LineSet> _streamlines;
        List<LineBall> _streamBall;

        VectorData _canvas;

        TetTreeGrid _grid;
        //KDTree _tree;
        Mesh _octreeLeafs;

        IndexData _tmpTest;

        public TetGridMapper(Plane plane) : base()
        {
            Mapping = ShowSide;
            BasePlane = plane;

            int timestep = 0;

            // Load Geometry
            LoaderVTU geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Solid);
            var hexGrid = geomLoader.LoadGeometry();
            geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Wall);
            var wallGrid = geomLoader.LoadGeometry();

            // Fit plane to data.
            this.BasePlane = Plane.FitToPoints(Vector3.Zero, 4, hexGrid.Vertices);
            BasePlane.PointSize = 0.1f;

            // Load some attribute.
            LoaderEnsight attribLoader = new LoaderEnsight(Aneurysm.GeometryPart.Solid);
            _grid = new TetTreeGrid(hexGrid, Aneurysm.GeometryPart.Solid, 1, 10);
            _vectorField = new VectorField(attribLoader.LoadAttribute(Aneurysm.Variable.velocity, 0), _grid);

            // Load inlet for seeding.
            LoaderVTU inletLoader = new LoaderVTU(Aneurysm.GeometryPart.Inlet);
            var inlet = inletLoader.LoadGeometry();
            LoaderEnsight inletAttributeLoader = new LoaderEnsight(Aneurysm.GeometryPart.Inlet);
            VectorData vel = inletAttributeLoader.LoadAttribute(Aneurysm.Variable.velocity, timestep);

            VectorField.Integrator integrator = new VectorField.IntegratorRK4(new VectorFieldInertial(_vectorField, INERTIA));
            integrator.StepSize = _grid.CellSizeReference / 2;

            //            while (true)
            {
                _points = inlet.SampleRandom(10, vel);
//                _points.SetTime(timestep);

                Stopwatch watch = new Stopwatch();
                watch.Start();
                _streamlines = new List<LineSet>(3);


                // Inertial
                _streamlines.Add(integrator.Integrate(_points)[0]);
                _streamlines.Last().Color = Vector3.UnitZ;

                watch.Stop();
                Console.WriteLine($"==== Integrating {_points.Length} points took {watch.Elapsed}. ");

                foreach (LineSet lines in _streamlines)
                    lines.Thickness *= 0.2f;
                _points = _streamlines?[0].GetAllEndPoints().ToBasicSet() ?? _points;


                Octree attributeTree = Octree.LoadOrComputeWrite(wallGrid.Vertices, 10, 10, Aneurysm.GeometryPart.Wall, float.MaxValue);
                _canvas = BinaryFile.ReadFile(Aneurysm.Singleton.CustomAttributeFilename($"SplatInt_{INERTIA}", Aneurysm.GeometryPart.Wall), 1);
                if (_canvas == null)
                    _canvas = new VectorBuffer(wallGrid.Vertices.Length, 1);
                this.SplatToAttribute(attributeTree, _canvas, _points, attributeTree.Extent.Max() * 0.02f);
                BinaryFile.WriteFile(Aneurysm.Singleton.CustomAttributeFilename($"SplatInt_{INERTIA}", Aneurysm.GeometryPart.Wall), _canvas);
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
                    _attribute = BinaryFile.ReadFile(Aneurysm.Singleton.CustomAttributeFilename($"SplatInt_{INERTIA}", Aneurysm.GeometryPart.Wall), 1);
                }
                else
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

            //if (_streamlines != null && 
            //    _streamlines.Count > 0 &&
            //    (_lastSetting == null ||
            //    _streamBall == null))
            //{
            //    _streamBall = new List<LineBall>(_streamlines.Count);
            //    foreach (LineSet lines in _streamlines)
            //        _streamBall.Add( new LineBall(BasePlane, lines));
            //}

            //if (_streamBall != null)
            //    wire.AddRange(_streamBall);

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

                if (_test != null)
                {
                    _test.LowerBound = WindowStart;
                    _test.UpperBound = WindowStart + WindowWidth;
                    _test.UsedMap = Colormap;
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
