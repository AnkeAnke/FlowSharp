
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
    class LineStatisticsMapper : SelectionMapper
    {
        protected PointSet<Point> _linePoints;
        protected float[] _values;
        protected Vector3 _startSelection, _endSelection;
        protected bool _initialized = false;
        protected bool _selectionChanged = false;

        public VectorFieldUnsteady Velocity;


        public LineStatisticsMapper(VectorFieldUnsteady velocity, Plane plane) : base(plane, velocity.Size.ToInt2())
        {
            Velocity = velocity;
            Mapping = ComputeStatistics;
            Plane = plane;


            int time = velocity.Size.T;

        }

        public List<Renderable> ComputeStatistics()
        {
            List<Renderable> result = new List<Renderable>(200);
            result.Add(new FieldPlane(Plane, Velocity.GetTimeSlice(_currentSetting.SliceTimeMain), _currentSetting.Shader, _currentSetting.Colormap));

            if (!_initialized)
                return result;



            result.Add(new LineBall(Plane, new LineSet(new Line[] { new Line() { Positions = new Vector3[] { _startSelection, _endSelection } } })));


            bool completelyNew = false;
            // ~~~~~~~~~~~~~~ Get new Start Points ~~~~~~~~~~~~~~ //
            if (_lastSetting == null ||
                SliceTimeMainChanged ||
                LineXChanged ||
                _selectionChanged)
            {
                int numPoints = _startSelection == null ? 0 : Math.Max(2, _currentSetting.LineX + 1);
                Point[] startPoints = new Point[numPoints];

                // Compute point positions (linear interpolation).
                for (int x = 0; x < numPoints; ++x)
                {
                    float t = (float)x / (numPoints - 1);
                    startPoints[x] = new Point()
                    {
                        Position = _startSelection * (1.0f - t) + _endSelection * t
                    };
                }
                _linePoints = new PointSet<Point>(startPoints);
                _values = new float[_linePoints.Length];

                completelyNew = true;
            }

            // ~~~~~~~~~~~~ Compute Selected Measure ~~~~~~~~~~~~ //
            if (completelyNew ||
                MeasureChanged ||
                FlatChanged ||
                IntegrationTimeChanged ||
                IntegrationTypeChanged ||
                StepSizeChanged)
            {
                // ~~~~~~~~~~~~~ Compute Scalar FIeld ~~~~~~~~~~~~~~~ //
                ScalarField measure = null;
                switch (_currentSetting.Measure)
                {
                    // Velocity Length / Pathline Length.
                    case RedSea.Measure.VELOCITY:
                        measure = new VectorField(Velocity.GetTimeSlice(_currentSetting.SliceTimeMain), FieldAnalysis.VFLength, 1).Scalars[0] as ScalarField;
                        break;
                    case RedSea.Measure.SURFACE_HEIGHT:
                        break;
                    case RedSea.Measure.SALINITY:
                        break;
                    case RedSea.Measure.TEMPERATURE:
                        break;
                    case RedSea.Measure.DIVERGENCE:
                        measure = new VectorField(Velocity.GetTimeSlice(_currentSetting.SliceTimeMain), FieldAnalysis.Divergence, 1).Scalars[0] as ScalarField;
                        break;
                    // Closeness of Pathline.
                    case RedSea.Measure.DIVERGENCE_2D:
                        break;
                    case RedSea.Measure.VORTICITY:
                        measure = new VectorField(Velocity.GetTimeSlice(_currentSetting.SliceTimeMain), FieldAnalysis.Vorticity, 1).Scalars[0] as ScalarField;
                        break;
                    case RedSea.Measure.SHEAR:
                        measure = new VectorField(Velocity.GetTimeSlice(_currentSetting.SliceTimeMain), FieldAnalysis.Shear, 1).Scalars[0] as ScalarField;
                        break;
                }

                // ~~~~~~~~~~~~~~~~ Sample Field ~~~~~~~~~~~~~~~~~~~ //
                switch (_currentSetting.Measure)
                {
                    // Velocity Length / Pathline Length.
                    case RedSea.Measure.VELOCITY:
                        if (_currentSetting.IntegrationTime == 0)
                        {
                            for (int index = 0; index < _values.Length; ++index)
                            {
                                _values[index] = measure.Sample(((Vec3)_linePoints.Points[index].Position).ToVec2());
                            }
                        }
                        else
                        {
                            VectorField.Integrator integrator = VectorField.Integrator.CreateIntegrator(Velocity, _currentSetting.IntegrationType);
                            integrator.Direction = Sign.POSITIVE;
                            integrator.StepSize = _currentSetting.StepSize;

                            LineSet line = integrator.Integrate(_linePoints, _currentSetting.Flat, _currentSetting.IntegrationTime)[0];
                            for (int index = 0; index < _values.Length; ++index)
                            {
                                _values[index] = line.Lines[index].LineLength;
                            }
                            result.Add(new LineBall(Plane, line));
                        }
                        break;
                    // Simply sample a field.
                    case RedSea.Measure.SURFACE_HEIGHT:
                    case RedSea.Measure.SALINITY:
                    case RedSea.Measure.TEMPERATURE:
                    case RedSea.Measure.DIVERGENCE:
                    case RedSea.Measure.VORTICITY:
                    case RedSea.Measure.SHEAR:
                        for (int index = 0; index < _values.Length; ++index)
                        {
                            _values[index] = measure.Sample(((Vec3)_linePoints.Points[index].Position).ToVec2());
                        }
                        break;
                    // Closeness of Pathline.
                    case RedSea.Measure.DIVERGENCE_2D:

                        break;
                }
                completelyNew = true;
            }

            //if (completelyNew ||
            //    AlphaStableChanged ||
            //    LineSettingChanged)
            //{
                // ~~~~~~~~~~~~~~~~ Display the Graph ~~~~~~~~~~~~~~~ //
                result.Add(FieldAnalysis.BuildGraph(Plane, _linePoints, _values, _currentSetting.AlphaStable, _currentSetting.LineSetting));
            //}
            _selectionChanged = false;
            return result;
        }

        public override void EndSelection(Vector2[] points)
        {
            _startSelection = new Vector3(points[0], _currentSetting.SliceTimeMain);
            _endSelection = new Vector3(points[1], _currentSetting.SliceTimeMain);
            _selectionChanged = true;
            _initialized = true;
        }
        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.LineSetting:
                case Setting.Element.SliceTimeMain:
                case Setting.Element.AlphaStable:
                case Setting.Element.StepSize:
                case Setting.Element.IntegrationType:
                case Setting.Element.LineX:
                case Setting.Element.Colormap:
                case Setting.Element.Shader:
                case Setting.Element.WindowWidth:
                case Setting.Element.WindowStart:
                case Setting.Element.Measure:
                case Setting.Element.IntegrationTime:
                case Setting.Element.Flat:
                    return true;
                default:
                    return false;
            }
        }

        public override string GetName(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.AlphaStable:
                    return "Height Scale";
                case Setting.Element.Flat:
                    return "Integrate both Directions?";
                case Setting.Element.LineX:
                    return "Number of Samples per Line";
                default:
                    return base.GetName(element);
            }

        }
    }
}
