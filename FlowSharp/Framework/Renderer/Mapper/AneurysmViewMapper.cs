
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
    class AneurysmViewMapper : IntegrationMapper
    {


        Mesh _viewGeom;

        ColormapRenderable _test;
        
        VectorData _attribute;

        VectorField _vectorField;
        List<LineSet> _streamLines;
        List<LineBall> _streamBall;
        UnstructuredGeometry _geometry;

        VectorData _canvasQuant, _canvasAnglePerp, _canvasAngleShear;

        public AneurysmViewMapper(Plane plane) : base()
        {
            Mapping = ShowSide;
            BasePlane = plane;
            
            var geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Wall);
            var wallGrid = geomLoader.LoadGeometry();
            
            // Fit plane to data.
            this.BasePlane = Plane.FitToPoints(Vector3.Zero, 4, wallGrid.Vertices);
            BasePlane.PointSize = 0.1f;
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

            var axes = BasePlane.GenerateOriginAxisGlyph();
            wire.AddRange(axes);
            return wire;

        }

        private VectorBuffer LoadOrCreateEmptyWallCanvas(string name, int step)
        {
            VectorBuffer buff = BinaryFile.ReadFile(Aneurysm.Singleton.CustomAttributeFilename(name + $"_{step}", Aneurysm.GeometryPart.Wall), 1);
            if (buff == null)
                buff = new VectorBuffer(LoaderEnsight.NumVerticesPerPart[(int)Aneurysm.GeometryPart.Wall], 1, 0);

            return buff;
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
