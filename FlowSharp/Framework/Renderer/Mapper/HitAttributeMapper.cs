using SlimDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;

namespace FlowSharp
{
    
    class HitAttributeMapper : DataMapper
    {
        UnstructuredGeometry _wallGrid;
        Mesh _wall;
        //VectorData[] _timeSteps;
        VectorData _timeStep;
        string _splatName;



        public enum HitMeasure
        {
            Hits,
            Shear,
            Perpendicular,
            Velocity
        }


        public HitAttributeMapper(Plane plane/*, HitMeasure splatMeasure*/) : base()
        {
            Mapping = ShowWall;
            BasePlane = plane;

            LoaderVTU geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Wall);
            _wallGrid = geomLoader.LoadGeometry();

            this.BasePlane = Plane.FitToPoints(Vector3.Zero, 4, _wallGrid.Vertices);
            BasePlane.PointSize = 0.1f;
        }

        public List<Renderable> ShowWall()
        {
            List<Renderable> renderables = new List<Renderable>(16);

            if (_lastSetting == null || CustomChanged)
            {
                switch ((HitMeasure)Custom)
                {
                    case HitMeasure.Hits:
                        _splatName = "SplatQuantity";
                        break;
                    case HitMeasure.Perpendicular:
                        _splatName = "SplatPerpendicular";
                        break;
                    case HitMeasure.Shear:
                        _splatName = "SplatShear";
                        break;
                    case HitMeasure.Velocity:
                        _splatName = "SplatVelocity";
                        break;
                    default:
                        _splatName = "Error";
                        break;
                }
            }

            if (_lastSetting == null || CustomChanged)
            {
                //_timeSteps = new VectorData[20];
                //for (int s = 0; s < 20; s++)
                //{
                //    _timeSteps[s] = BinaryFile.ReadFile(Aneurysm.Singleton.CustomAttributeFilename(_splatName + $"_{s * 10}", Aneurysm.GeometryPart.Wall), 1);
                //    _timeSteps[s].ExtractMinMax();
                //}
                _timeStep = BinaryFile.ReadFile(Aneurysm.Singleton.CustomAttributeFilename(_splatName, Aneurysm.GeometryPart.Wall), 1);
                if (_timeStep == null)
                    Console.WriteLine($"Whaaat? Could not load file {_splatName}");
                _timeStep.ExtractMinMax();

                _wall = new Mesh(
                    BasePlane,
                    _wallGrid,
                    _timeStep,
                    Mesh.RenderEffect.DEFAULT,
                    Colormap);
            }

            if (_lastSetting == null ||
                ColormapChanged ||
                WindowStartChanged ||
                WindowWidthChanged ||
                CustomChanged)
            {
                _wall.LowerBound = WindowStart;
                _wall.UpperBound = WindowStart + WindowWidth;
                _wall.UsedMap = Colormap;
            }

            renderables.Add(_wall);

            var axes = BasePlane.GenerateOriginAxisGlyph();
            renderables.AddRange(axes);

            return renderables;
        }

        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.Colormap:
                //case Setting.Element.GeometryPart:
                //case Setting.Element.IntegrationTime:
                //case Setting.Element.LineX:
                case Setting.Element.WindowStart:
                case Setting.Element.WindowWidth:
                case Setting.Element.Custom:
                    return true;
                default:
                    return false;
            }
        }

        public override string GetName(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.LineX:
                    return "Time Slice";
                default:
                    return base.GetName(element);
            }
        }

        public override float? GetMin(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.LineX:
                    return 0;
                case Setting.Element.WindowStart:
                    return 0;
                default:
                    return base.GetMin(element);
            }
        }

        public override float? GetMax(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.LineX:
                    return 20;
                case Setting.Element.WindowWidth:
                    return _timeStep?.MaxValue[0] ?? 20;
                case Setting.Element.WindowStart:
                    return _timeStep?.MaxValue[0] ?? 20;
                default:
                    return base.GetMin(element);
            }
        }

        public override IEnumerable GetCustomAttribute()
        {
            return Enum.GetValues(typeof(HitMeasure)).Cast<HitMeasure>();
        }
    }
}
