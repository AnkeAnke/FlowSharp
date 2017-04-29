
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
        LineSet _wireframe;
        PointSet<Point> _vertices;
        TetGrid _grid;

        public HexGridMapper(Plane plane) : base()
        {
            Mapping = ShowWireframe;
            Plane = plane;

            LoaderEnsight loader = new LoaderEnsight(Aneurysm.Variable.velocity);
            _grid = loader.LoadGrid();
            _vertices = _grid.GetVertices();

            this.Plane = Plane.FitToPoints(Vector3.Zero, 10, _vertices);
            Plane.PointSize = 1.0f;

            Console.WriteLine("Num verts: {0}", _vertices.Length);
        }

        public List<Renderable> ShowWireframe()
        {
            
            
            var wire = new List<Renderable>(2);
            //wire.Add(new LineBall(Plane, _wireframe));
            wire.Add(new PointCloud(Plane, _vertices));
            //PointSet<Point> points = new PointSet<Point>(new Point[]
            //{
            //    //new Point(Vector3.Zero) { Radius = 0.01f},
            //    //new Point(Vector3.UnitX){ Radius = 0.01f},
            //    //new Point(Vector3.UnitY){ Radius = 0.01f},
            //    //new Point(new Vector3(1,1,0)){ Radius = 0.01f},
            //    //new Point(Vector3.UnitZ){ Radius = 0.01f},
            //    //new Point(new Vector3(1,0,1)){ Radius = 0.01f},
            //    //new Point(new Vector3(0,1,1)){ Radius = 0.01f},
            //    //new Point(new Vector3(1,1,1)) { Radius = 0.01f},
            //    _vertices.Points[0],
            //    _vertices.Points[1],
            //    _vertices.Points[2],
            //    _vertices.Points[3],
            //    _vertices.Points[4],
            //    _vertices.Points[5]
            //});
            //wire.Add(new PointCloud(Plane, points));

            var axes = Plane.GenerateAxisGlyph();
            wire.AddRange(axes);
            return wire;
            
        }
        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                default:
                    return false;
            }
        }
    }
}
