using SlimDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.IO;

namespace FlowSharp
{

    class HitSampleMapper : DataMapper
    {
        UnstructuredGeometry _wallGrid;
        Mesh _wall;
        PointSet<DirectionPoint> _hits;
        PointCloud _hitCloud;

        public enum HitMeasure
        {
            Hits,
            Shear,
            Perpendicular
        }


        public HitSampleMapper(Plane plane/*, HitMeasure splatMeasure*/) : base()
        {
            Mapping = ShowWall;
            BasePlane = plane;

            LoaderVTU geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Wall);
            _wallGrid = geomLoader.LoadGeometry();

            this.BasePlane = Plane.FitToPoints(Vector3.Zero, 4, _wallGrid.Vertices);
            BasePlane.PointSize = 0.1f;

            string filenameHits = Aneurysm.Singleton.CustomAttributeFilename("ParticleHits", Aneurysm.GeometryPart.Wall);
            if (!File.Exists(filenameHits))
                return;
            float[] pointData = BinaryFile.ReadAllFileArrays<float>(filenameHits);
            VectorBuffer pointsBuffer = new VectorBuffer(pointData, 7);
            DirectionPoint[] points = new DirectionPoint[pointsBuffer.Length];
            for (int p = 0; p < points.Length; ++p)
                points[p] = new DirectionPoint(pointsBuffer[p]);

            _hits = new PointSet<DirectionPoint>(points);

            Console.WriteLine($"===== # End Points {_hits.Length}");
        }

        public List<Renderable> ShowWall()
        {
            List<Renderable> renderables = new List<Renderable>(16);

            if (_lastSetting == null)
            {
                _wall = new Mesh(BasePlane, _wallGrid);
               // _hitCloud = new PointCloud(BasePlane, _hits.ToBasicSet());
            }

            if (_lastSetting == null || LineXChanged)
            {
                List<Point> selected = new List<Point>(_hits.Length);
                //float timeX = Aneurysm.Singleton.TimeScale * LineX;
                foreach (DirectionPoint p in _hits.Points)
                //for (int pIdx = 0; pIdx < 100; ++pIdx)
                {
                    if (Math.Abs((p.Position.W / 0.05f) + 200 - LineX) % 200 <= 4)
                        selected.Add(p);
                }

                _hitCloud = new PointCloud(BasePlane, new PointSet<Point>(selected.ToArray()));
            }
            

            renderables.Add(_wall);
            renderables.Add(_hitCloud);

            Plane cpy = new Plane(BasePlane);
            cpy.PointSize *= 10;
            var axes = cpy.GenerateOriginAxisGlyph();
            renderables.AddRange(axes);

            return renderables;
        }

        //private void SplatToAttribute(Octree attributeTree, VectorData normals, PointSet<DirectionPoint> points, float radius)
        //{
        //    Parallel.ForEach(points.Points, p =>
        //    //foreach (DirectionPoint p in )
        //    {
        //        /*List<Octree.IndexDistance>*/
        //        Dictionary<int, float> verts = attributeTree.FindWithinRadius(Util.Convert(p.Position), radius);
        //        foreach (var v in verts)
        //        {
        //            float weight = radius / v.Value - 1;
        //            _canvasQuant[v.Key] += (Vector)weight;

        //            Vector incident = new Vector(p.Direction);
        //            incident.Normalize();

        //            float angle = VectorRef.Dot(normals[v.Key], incident);
        //            angle = (float)Math.Cosh(Math.Abs(angle));
        //            _canvasAnglePerp[v.Key] += weight / angle;
        //            _canvasAngleShear[v.Key] += angle * weight;
        //        }
        //    });
        //    _canvasQuant.MaxValue = null; //(Vector)50;
        //    _canvasQuant.MinValue = (Vector)0;
        //    _canvasQuant.ExtractMinMax();
        //}

        #region GUI
        public override bool IsUsed(Setting.Element element)
        {
            if (element == Setting.Element.LineX)
                return true;
            return false;
        }


        public override IEnumerable GetCustomAttribute()
        {
            return Enum.GetValues(typeof(HitMeasure)).Cast<HitMeasure>();
        }
        #endregion
    }
}
