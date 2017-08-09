
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
    class TetGridMapper : DataMapper
    {
        //LineSet _wireframe;
        //PointSet<Point> _vertices;
        Mesh _viewGeom;


        //TetTreeGrid _grid;'
        GeneralUnstructurdGrid _geometry;
        //Index[] _indices;
        //bool update = true;
        PointSet<Point> _points;
        PointCloud _vertices;
        VectorData _attribute;

        VectorField _vectorField;
        LineSet _streamlines;
        LineBall _streamBall;

        TetTreeGrid _grid;
        //KDTree _tree;
        Mesh _octreeLeafs;

        public TetGridMapper(Plane plane) : base()
        {
            Mapping = ShowSide;
            BasePlane = plane;

            LoaderVTU geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Solid);
            var hexGrid = geomLoader.LoadGeometry();

            _grid = new TetTreeGrid(hexGrid, 10);
            //_grid.Tree.WriteToFile(Aneurysm.Singleton.OctreeFilename); // Fun fact: never loaded yet.

            // Fit plane to data.
            this.BasePlane = Plane.FitToPoints(Vector3.Zero, 4, hexGrid.Vertices);
            BasePlane.PointSize = 1f;

            // Load some attribute.
            LoaderEnsight attribLoader = new LoaderEnsight(Aneurysm.GeometryPart.Solid);
            _vectorField = new VectorField(attribLoader.LoadAttribute(Aneurysm.Variable.velocity, 0), _grid);

            _points = _grid.SampleTest(_vectorField, 10);
            TetTreeGrid.ShowSampleStatistics();
            //VectorField.IntegratorEuler integrator = new VectorField.IntegratorEuler(_vectorField);
            //integrator.StepSize = _grid.CellSizeReference / 2;
            //_streamlines = integrator.Integrate(_points)[0];
            //_points = _streamlines.GetAllEndPoints().ToBasicSet();

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
                LoaderEnsight attribLoader = new LoaderEnsight(GeometryPart);
                _attribute = attribLoader.LoadAttribute((Aneurysm.Variable)(int)Measure, 0);
                _attribute.ExtractMinMax();
                updateCubes = true;
            }

            if (updateCubes)
            {
                _viewGeom = new Mesh(BasePlane, _geometry, _attribute);
            }

            //if (_lastSetting == null ||
            //    _streamBall == null)
            //{
            //    _streamBall = new LineBall(BasePlane, _streamlines);
            //}


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
            }

            wire.Add(_viewGeom);
//            wire.Add(_streamBall);

            if (_vertices == null || updateCubes)
            {
                _vertices = new PointCloud(BasePlane, _points);
            }
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
