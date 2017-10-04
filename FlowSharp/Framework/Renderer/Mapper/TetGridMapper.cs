
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
        PointSet<DirectionPoint> _points;
        PointCloud _vertices;
        VectorData _attribute;
        TetTreeGrid _grid;

        VectorField _vectorField;
        List<LineSet> _streamLines;
        List<LineBall> _streamBall;

        VectorData _canvasQuant, _canvasAnglePerp, _canvasAngleShear;

        public TetGridMapper(Plane plane) : base()
        {
            Mapping = ShowSide;
            BasePlane = plane;

            // Load Geometry
            LoaderVTU geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Solid);
            var hexGrid = geomLoader.LoadGeometry();
            geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Wall);
            var wallGrid = geomLoader.LoadGeometry();
            Octree attributeTree = Octree.LoadOrComputeWrite(wallGrid.Vertices, 10, 10, Aneurysm.GeometryPart.Wall, float.MaxValue);

            // Fit plane to data.
            this.BasePlane = Plane.FitToPoints(Vector3.Zero, 4, hexGrid.Vertices);
            BasePlane.PointSize = 0.1f;
            
            // Load grid.
            LoaderEnsight attribLoader = new LoaderEnsight(Aneurysm.GeometryPart.Solid);
            _grid = new TetTreeGrid(hexGrid, Aneurysm.GeometryPart.Solid, 1, 10);

            // Load inlet for seeding.
            LoaderVTU inletLoader = new LoaderVTU(Aneurysm.GeometryPart.Inlet);
            var inlet = inletLoader.LoadGeometry();


            // Setup integrator.
            VectorField.Integrator integrator = new VectorField.IntegratorEuler(_vectorField);
            integrator.StepSize = _grid.CellSizeReference / 2;
            integrator.NormalizeField = true;
            integrator.MaxNumSteps = 1000000;
            integrator.EpsCriticalPoint = 0;

            //for (int offset = 0; offset < 10; ++offset)
            //    for (TIMESTEP = 0; TIMESTEP < 200; TIMESTEP += 10)
            int offset = 0;
            TIMESTEP = 0;
                {

                    //if (TIMESTEP == 0 && offset == 0)
                    //        continue;

                    LoaderEnsight inletAttributeLoader = new LoaderEnsight(Aneurysm.GeometryPart.Inlet);
                    VectorData vel = inletAttributeLoader.LoadAttribute(Aneurysm.Variable.velocity, TIMESTEP);

                    // Load one vector field for comparison.
                    //_vectorField = new VectorField(attribLoader.LoadAttribute(Aneurysm.Variable.velocity, TIMESTEP), _grid);


                    _canvasQuant = Util.LoadOrCreateEmptyWallCanvas($"SplatQuantity", TIMESTEP);
                    _canvasAnglePerp = Util.LoadOrCreateEmptyWallCanvas($"SplatPerpendicular", TIMESTEP);
                    _canvasAngleShear = Util.LoadOrCreateEmptyWallCanvas($"SplatShear", TIMESTEP);

                    //for (int offset = 0; offset < 10; offset++)
                    //int offset = 0;
                    //if (false)

                    {
                        //PointSet<DirectionPoint> points = inlet.SampleRandom(100, vel);
                        PointSet<DirectionPoint> points = inlet.SampleRegular(offset, 10, vel);
                        Console.WriteLine($"=====\n===== Sampling {offset}/10 at time {TIMESTEP} =====\n=====");
                        //PointSet<DirectionPoint> points2 = inlet.SampleAllVertices(vel);
                        //for (int i = 0; i < 10; ++i)
                        //    Console.WriteLine($"Regular: {points[i].Position} -> {points[i].Direction}\nVertex: {points2[i].Position} -> {points2[i].Direction}\n");
                        //PointSet<DirectionPoint> points = inlet.SampleAllVertices(vel);
                        //Console.WriteLine($"Integrating {points.Length} Positions");
                        Stopwatch watch = new Stopwatch();
                        watch.Start();
                        _streamLines = new List<LineSet>(3);


                        // Inertial
                        RESPONSE_TIME = 0.000001821f;
                        integrator.StepSize = 0.5f;

                        _streamLines.Add(this.IntegratePoints(integrator, _grid, points));
                        _streamLines.Last().Color = Vector3.UnitZ;

                        watch.Stop();
                        Console.WriteLine($"==== Integrating {points.Length} points took {watch.Elapsed}. ");


                        _points = _streamLines?[0].GetAllEndPoints() ?? _points;
                        foreach (LineSet lines in _streamLines)
                            lines.Thickness *= 0.1f;




                        VectorBuffer normals = wallGrid.ComputeNormals();

                        this.SplatToAttribute(attributeTree, normals, _points, attributeTree.Extent.Max() * 0.02f);

                        Stopwatch timeWrite = new Stopwatch();
                        timeWrite.Start();
                        BinaryFile.WriteFile(
                            Aneurysm.Singleton.CustomAttributeFilename($"SplatQuantity_{TIMESTEP}", Aneurysm.GeometryPart.Wall),
                            _canvasQuant);
                        BinaryFile.WriteFile(
                            Aneurysm.Singleton.CustomAttributeFilename($"SplatPerpendicular_{TIMESTEP}", Aneurysm.GeometryPart.Wall),
                            _canvasAnglePerp);
                        BinaryFile.WriteFile(
                            Aneurysm.Singleton.CustomAttributeFilename($"SplatShear_{TIMESTEP}", Aneurysm.GeometryPart.Wall),
                            _canvasAngleShear);
                        timeWrite.Stop();
                        Console.WriteLine($"Time to splat: {timeWrite.Elapsed}");

                    }
                }
            _points = null;
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
                //if (GeometryPart == Aneurysm.GeometryPart.Wall)
                //{
                //    _attribute = BinaryFile.ReadFile(Aneurysm.Singleton.CustomAttributeFilename("SplatQuant", Aneurysm.GeometryPart.Wall), 1);
                //}
                //if (GeometryPart != Aneurysm.GeometryPart.Wall || _attribute == null)
                //{
                _attribute = null;

                LoaderEnsight attribLoader = new LoaderEnsight(GeometryPart);

                if (Measure == Aneurysm.Measure.x_wall_shear)
                    _attribute = _canvasQuant;
                if (Measure == Aneurysm.Measure.y_wall_shear)
                    _attribute = _canvasAnglePerp;
                if (Measure == Aneurysm.Measure.z_wall_shear)
                    _attribute = _canvasAngleShear;

                if (_attribute == null)
                    _attribute = attribLoader.LoadAttribute((Aneurysm.Variable)(int)Measure, 0);
                //}
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
                    if (_streamBall.Count > 0)
                        _streamBall[0].UsedMap = Colormap.Red;
                    if (_streamBall.Count > 1)
                        _streamBall[1].UsedMap = Colormap.Green;
                }
            }

            wire.Add(_viewGeom);

            if (_test != null)
                wire.Add(_test);



            if (_points != null && _points.Length > 0 && (_vertices == null || updateCubes))
            {
                _vertices = new PointCloud(BasePlane, _points.ToBasicSet());
            }
            if (_vertices != null)
                wire.Add(_vertices);

            Plane cpy = new Plane(BasePlane);
            cpy.PointSize *= 10;
            var axes = cpy.GenerateOriginAxisGlyph();
            wire.AddRange(axes);

            return wire;
        }

        private void SplatToAttribute(Octree attributeTree, VectorData normals, PointSet<DirectionPoint> points, float radius) 
        {
            Parallel.ForEach(points.Points, p =>
            //foreach (DirectionPoint p in )
            {
                /*List<Octree.IndexDistance>*/
                Dictionary<int, float> verts = attributeTree.FindWithinRadius(Util.Convert(p.Position), radius);
                foreach (var v in verts)
                {
                    float weight = radius / v.Value - 1;
                    _canvasQuant[v.Key] += (Vector)weight;

                    Vector incident = new Vector(p.Direction);
                    incident.Normalize();

                    float angle = VectorRef.Dot(normals[v.Key], incident);
                    angle = (float)Math.Cosh(Math.Abs(angle));
                    _canvasAnglePerp[v.Key] += weight / angle;
                    _canvasAngleShear[v.Key] += angle * weight;
                }
            });
            _canvasQuant.MaxValue = null; //(Vector)50;
            _canvasQuant.MinValue = (Vector)0;
            _canvasQuant.ExtractMinMax();
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
