
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
    class HexTetGridMapper : DataMapper
    {
        //LineSet _wireframe;
        //PointSet<Point> _vertices;
        Mesh _cubes;
        TetNeighborGrid _grid;
        //Index[] _indices;
        bool update = true;
        PointCloud _vertices;

        public HexTetGridMapper(Plane plane) : base()
        {
            Mapping = ShowSide;
            BasePlane = plane;

            LoaderEnsight loader = new LoaderEnsight(Aneurysm.GeometryPart.Wall);
            var hexGrid = loader.LoadGrid();

            _grid = TetNeighborGrid.BuildFromHexGrid(hexGrid.Vertices, hexGrid.Indices);
            hexGrid.Indices = null;
            hexGrid = null;


            this.BasePlane = Plane.FitToPoints(Vector3.Zero, 10, _grid.Vertices);
            BasePlane.PointSize = 1.0f;

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
            var wire = new List<Renderable>(5);
            //if (update)
            //{
            //    update = false;
            //    _cubes = new Mesh(BasePlane, _grid);
            //}
            //if (_lastSetting == null ||
            //    WindowWidthChanged ||
            //    WindowStartChanged ||
            //    ColormapChanged)
            //{
            //    _cubes.LowerBound = WindowStart;
            //    _cubes.UpperBound = WindowStart + WindowWidth;
            //    _cubes.UsedMap = Colormap;
            //}
            //wire.Add(_cubes);
            if (_vertices == null)
                _vertices = new PointCloud(BasePlane, _grid.GetVertices());
            wire.Add(_vertices);

            var axes = BasePlane.GenerateAxisGlyph();
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
                    return true;
                default:
                    return false;
            }
        }
    }
}
