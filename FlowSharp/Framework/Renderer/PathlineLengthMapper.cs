
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
    class PathlineLengthMapper : SelectionMapper
    {
        protected PointSet<EndPoint>[] _intersectTimeSlices;
        protected LineSet[] _pathlineSegments;
        protected ScalarFieldUnsteady _pathLengths;
        public VectorFieldUnsteady Velocity;
        //public VectorField[] SlicesToRender
        //{
        //    get;
        //    set;
        //}

        //private VectorField[] _sliceFields;

        //protected List<Renderable> _slice0;
        //protected List<Renderable> _slice1;
        protected LineSet[] _rawLines;
        protected Renderable[] _lines;
        protected PointCloud[] _points = new PointCloud[2];
        protected FieldPlane _plane;
        protected float[] _minLength, _maxLength;
        protected int[] _fieldPositionOfValidCell;

        public PathlineLengthMapper(VectorFieldUnsteady velocity, Plane plane) : base(plane, velocity.Size.ToInt2())
        {
            Velocity = velocity;
            
            Mapping = ShowPaths;
            Plane = plane;

            int time = velocity.Size.T;
            _intersectTimeSlices = new PointSet<EndPoint>[time];
            _pathlineSegments = new LineSet[time - 1];
            _intersectTimeSlices[0] = FieldAnalysis.ValidDataPoints<EndPoint>(velocity.GetTimeSlice(0));//FieldAnalysis.SomePoints2D<EndPoint>(velocity, 100);//
            _points = new PointCloud[velocity.Size.T];
            _points[0] = new PointCloud(Plane, _intersectTimeSlices[0].ToBasicSet());
            _fieldPositionOfValidCell = new int[_intersectTimeSlices[0].Length];
            for(int i = 0; i < _fieldPositionOfValidCell.Length; ++i)
            {
                Vector3 pos = _intersectTimeSlices[0].Points[i].Position;
                _fieldPositionOfValidCell[i] = (int)(pos.X + 0.5) + (int)(pos.Y + 0.5) * Velocity.Size[0];
            }
        }

        public List<Renderable> ShowPaths()
        {

            bool mapLines = false;

            // Setup an integrator.
            VectorField.Integrator intVF = VectorField.Integrator.CreateIntegrator(Velocity, _currentSetting.IntegrationType);
            intVF.MaxNumSteps = 10000;
            intVF.StepSize = _currentSetting.StepSize;

            if (_lastSetting == null ||
                _currentSetting.IntegrationType != _lastSetting.IntegrationType ||
                _currentSetting.StepSize != _lastSetting.StepSize)
            {
                // ~~~~~~~~~~~ Line Integration ~~~~~~~~~~~ \\
                // Clear the raw lines.
                int timeLength = Velocity.Size.T;
                _rawLines = new LineSet[Velocity.Size.T];

                // Initialize the firth 
                _rawLines[0] = new LineSet(new Line[0]);
                ScalarField[] lengths = new ScalarField[timeLength];
                lengths[0] = new ScalarField(Velocity.ScalarsAsSFU[0].TimeSlices[0], (v, J) => 0);

                _minLength = new float[timeLength];
                _maxLength = new float[timeLength];

                // Integrate the path line segments between each neighboring pair of time slices.
                for (int time = 1; time < timeLength; ++time)
                {
                    _minLength[time] = float.MaxValue;
                    _maxLength[time] = float.MinValue;

                    // Integrate last points until next time slice.
                    _pathlineSegments[time-1] = intVF.Integrate(_intersectTimeSlices[time - 1], false, time);
                    _pathlineSegments[time - 1].Color = Vector3.UnitZ * (float)time / timeLength;

                    //                    if(time == timeLength - 1)
                    _intersectTimeSlices[time] = _pathlineSegments[time - 1].GetEndPoints();//VectorField.Integrator.Status.BORDER);
                    //else
                    //    _intersectTimeSlices[time] = _pathlineSegments[time - 1].GetEndPoints(VectorField.Integrator.Status.TIME_BORDER);
                    _points[time] = new PointCloud(Plane, _intersectTimeSlices[time].ToBasicSet());

                    // Set all positions to 0, or invalid value.
                    lengths[time] = new ScalarField(lengths[time-1], (s, g) => s, false);
                    int i = 0;
                    for (int p = 0; p < _intersectTimeSlices[time].Points.Length; ++p)
                    {
                        EndPoint pP = _intersectTimeSlices[time].Points[p];
                        ++i;
                        // Map floating position to int position.
                        int iPos = _fieldPositionOfValidCell[p];
                        float timeStepped = (pP.Position.Z - (time-1));
                        lengths[time][iPos] += timeStepped > 0 ? pP.LengthLine / timeStepped : 0;
                        float tmp = lengths[time][iPos];
                        _minLength[time] = Math.Min(lengths[time][iPos], _minLength[time]);
                        _maxLength[time] = Math.Max(lengths[time][iPos], _maxLength[time]);


                        if (_minLength[time] < 0 || pP.Status != VectorField.Integrator.Status.TIME_BORDER)
                            i += 0;
                        //Console.WriteLine(lengths[time][iPos]);
                    }
                    Console.WriteLine("Integrated lines until time " + time);
                }

                lengths[0] = new VectorField(Velocity.GetTimeSlice(0), FieldAnalysis.VFLength, 1, false).Scalars[0] as ScalarField;
                _minLength[0] = 0;
                _maxLength[0] = RedSea.Singleton.NumTimeSlices;
                _pathLengths = new ScalarFieldUnsteady(lengths);
                mapLines = true;
            }

            if (_lastSetting == null ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain||
                _currentSetting.Shader != _lastSetting.Shader)
            {
                ScalarField f = _pathLengths.GetTimeSlice(_currentSetting.SliceTimeMain);
                f.TimeSlice = 0;
                VectorField vecField;
                switch(_currentSetting.Shader)
                {
                    case FieldPlane.RenderEffect.LIC:
                        VectorField slice = Velocity.GetTimeSlice(0);
                        slice.TimeSlice = 0;
                        vecField = new VectorField(new Field[] { slice.Scalars[0], slice.Scalars[1], f });
                        break;
                    case FieldPlane.RenderEffect.LIC_LENGTH:
                        vecField = Velocity.GetTimeSlice(_currentSetting.SliceTimeMain);
                        vecField.TimeSlice = 0;
                        break;
                    default:
                    case FieldPlane.RenderEffect.COLORMAP:
                    case FieldPlane.RenderEffect.DEFAULT:
                        vecField = new VectorField(new Field[] { f });
                        break;
                }
                _plane = new FieldPlane(Plane, vecField /*Velocity.GetSlice(_currentSetting.SliceTimeReference)*/, _currentSetting.Shader);
            }

            // The line settings have changed. Create new renderables from the lines.
            if (mapLines || _currentSetting.LineSetting != _lastSetting.LineSetting)
            {
                _lines = new Renderable[_pathlineSegments.Length];

                switch (_currentSetting.LineSetting)
                {
                    // Map the vertices to colored points.
                    case RedSea.DisplayLines.POINTS_2D_LENGTH:
                        for (int i = 0; i < _pathlineSegments.Length; ++i)
                        {
                            PointSet<Point> linePoints = Velocity.ColorCodeArbitrary(_pathlineSegments[i], RedSea.DisplayLineFunctions[(int)_currentSetting.LineSetting]);
                            _lines[i] = new PointCloud(Plane, linePoints);
                        }
                        break;

                    // Render as line.
                    default:
                    case RedSea.DisplayLines.LINE:
                        for (int i = 0; i < _pathlineSegments.Length; ++i)
                        {
                            _lines[i] = new LineBall(Plane, _pathlineSegments[i]);
                        }
                        break;
                }
            }

            // Set mapping values.
            //_plane.UpperBound = 0; //= (1 + _currentSetting.WindowWidth) * (_maxLength[_currentSetting.SliceTimeMain] - _minLength[_currentSetting.SliceTimeMain]) /2 + _minLength[_currentSetting.SliceTimeMain];
            _plane.UpperBound = _currentSetting.WindowWidth + _currentSetting.WindowStart; ///= _currentSetting.SliceTimeMain;
            //_plane.LowerBound = 0; //= (1 - _currentSetting.WindowWidth) * (_maxLength[_currentSetting.SliceTimeMain] - _minLength[_currentSetting.SliceTimeMain]) /2 + _minLength[_currentSetting.SliceTimeMain];
            _plane.LowerBound = _currentSetting.WindowStart; ///= _currentSetting.SliceTimeMain;
            _plane.UsedMap = _currentSetting.Colormap;
            _plane.SetRenderEffect(_currentSetting.Shader);

            List<Renderable> result = new List<Renderable>(50);
            result.Add(_plane);
            switch(_currentSetting.Tracking)
            {
                case RedSea.DisplayTracking.LINE:
                case RedSea.DisplayTracking.LINE_POINTS:
                    Renderable[] lines = new Renderable[_currentSetting.SliceTimeMain];
                    Array.Copy(_lines, lines, _currentSetting.SliceTimeMain);
                    result = result.Concat(lines).ToList();
                    break;
                case RedSea.DisplayTracking.POINTS:
                    result.Add(_points[_currentSetting.SliceTimeMain]);
                    break;
                case RedSea.DisplayTracking.LINE_SELECTION:
                    VectorField.StreamLine<Vector3> line = intVF.IntegrateLineForRendering(new Vec3(_startPoint.X, _startPoint.Y, 0));
                    LineSet set = new LineSet(new Line[] { new Line() { Positions = line.Points.ToArray() } });
                    if(_currentSetting.Flat)
                        set.FlattenLines(_currentSetting.SliceTimeMain);
                    result.Add(new LineBall(Plane, set));
                    break;
                default:
                    break;
            }

            return result;
        }
        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.Colormap:
                case Setting.Element.WindowWidth:
                    return true;
                case Setting.Element.LineX:
                case Setting.Element.MemberMain:
                case Setting.Element.MemberReference:
                case Setting.Element.AlphaStable:
                    return false;
                default:
                    return true;
            }
        }
    }
}
