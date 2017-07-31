
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
        Mesh _cubes;
        //TetTreeGrid _grid;'
        GeneralUnstructurdGrid _geometry;
        //Index[] _indices;
        //bool update = true;
        PointCloud _vertices;
        VectorBuffer _attribute;

        public TetGridMapper(Plane plane) : base()
        {
            Mapping = ShowSide;
            BasePlane = plane;

            //Aneurysm.GeometryPart part = Aneurysm.GeometryPart.Wall;
            
            //hexGrid = null;

            //// TMP
            //tmp = new PointSet<Point>(new Point[]
            //{
            //    new Point(new Vector3(-3, 0, -3)),
            //    new Point(new Vector3(-6, -3, -6)),
            //    new Point(new Vector3(-6, -3, 0))
            //});
            //// \TMP




            //int[] selection = new int[_grid.Indices.Length / 100];
            //for (int s = 0; s < selection.Length; ++s)
            //    selection[s] = s*100;

            //try
            //{
            //    _indices = _grid.GetAllSides();
            //}
            //catch (Exception e)
            //{
            //    Console.Write(e);
            //    Debug.Assert(false);
            //}


            //Console.WriteLine("Num sides: {0}", _indices.Length);
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

                _geometry = geomLoader.Grid;

                // Fit plane to data.
                this.BasePlane = Plane.FitToPoints(Vector3.Zero, 4, _geometry.GetVertices());
                BasePlane.PointSize = 1.0f;

                //update = false;
                updateCubes = true;
            }

            if (_lastSetting == null ||
                GeometryPartChanged ||
                MeasureChanged)
            {
                LoaderEnsight attribLoader = new LoaderEnsight(GeometryPart);
                _attribute = attribLoader.LoadAttribute((Aneurysm.Variable)(int)Measure, 0);
                updateCubes = true;
            }

            if (updateCubes)
                _cubes = new Mesh(BasePlane, _geometry, _attribute);


            if (_lastSetting == null ||
                GeometryPartChanged ||
                WindowWidthChanged ||
                WindowStartChanged ||
                ColormapChanged ||
                updateCubes)
            {
                _cubes.LowerBound = WindowStart;
                _cubes.UpperBound = WindowStart + WindowWidth;
                _cubes.UsedMap = Colormap;
            }

            wire.Add(_cubes);
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

        public override int? GetLength(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.WindowWidth:
                    return 1000;
                case Setting.Element.WindowStart:
                    return 500;
                default:
                    return base.GetLength(element);
            }
        }

        public override int? GetStart(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.WindowWidth:
                    return 0;
                case Setting.Element.WindowStart:
                    return -500;
                default:
                    return base.GetLength(element);
            }
        }
    }
}
