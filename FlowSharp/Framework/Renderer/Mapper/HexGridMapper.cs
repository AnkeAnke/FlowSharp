
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
    class HexGridMapper : DataMapper
    {
        //LineSet _wireframe;
        //PointSet<Point> _vertices;
        Mesh _cubes;
        //HexGrid _grid;
        IndexArray _indices;
        bool update = true;
        //PointSet<Point> _vertices;

        public HexGridMapper(Plane plane) : base()
        {
            Mapping = ShowSide;
            BasePlane = plane;

            //LoaderEnsight loader = new LoaderEnsight(Aneurysm.GeometryPart.Wall);
            //_grid = loader.LoadGrid();


            //this.BasePlane = Plane.FitToPoints(Vector3.Zero, 10, _grid.Vertices);
            BasePlane.PointSize = 1.0f;

            //int[] selection = new int[_grid.Indices.Length / 100];
            //for (int s = 0; s < selection.Length; ++s)
            //    selection[s] = s*100;

//            _indices = _grid.GetCubes();
           // _vertices = _grid.GetVertices();
            

            Console.WriteLine("Num sides: {0}", _indices.Length);
        }

        public List<Renderable> ShowSide()
        {
            var wire = new List<Renderable>(3);
            //if (update)
            //{
            //    update = false;
            //    _cubes = new Mesh(BasePlane, new UnstructuredGeometry(_grid.Vertices, _indices));
            //}
            //if (_lastSetting == null ||
            //    WindowWidthChanged ||
            //    WindowStartChanged ||
            //    ColormapChanged)
            //    {
            //        _cubes.LowerBound = WindowStart;
            //        _cubes.UpperBound = WindowStart + WindowWidth;
            //        _cubes.UsedMap = Colormap;
            //    }
            //wire.Add(_cubes);
            ////wire.Add(new PointCloud(Plane, _vertices));

            //var axes = BasePlane.GenerateAxisGlyph();
            //wire.AddRange(axes);
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
