using SlimDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class CoreDistanceMapper : DataMapper
    {
        private VectorFieldUnsteady _velocity;
        //private Plane _plane;
        private Vector2 _selection;

        private LineBall[] _pathlines;
        private Renderable[] _graph;
        private FieldPlane _timeSlice;

        private LineSet _cores;
        private LineBall _coreBall;
        private CriticalPointSet2D _coreOrigins;
        private PointCloud _coreCloud;

        private VectorField _debugCore;

        private int _everyNthTimestep;
        private int _velocityEndTime;

        private bool _selectionChanged = false;
        public CoreDistanceMapper(/*VectorFieldUnsteady velocity,*/ int everyNthField, Plane plane)// : base(plane, velocity.Size.ToInt2())
        {
            _everyNthTimestep = everyNthField;
            Plane = plane;
            _intersectionPlane = plane;

            Mapping = AdvectLines;

            ComputeCoreOrigins(0);
        }
        /// <summary>
        /// Load a stack of 30 field. This should be small enough to have memory free for other operations.
        /// </summary>
        /// <param name="startStep">The start step. Running continuously. 0 1 2 3 ...</param>
        protected void LoadField(int startStep, int member = 0, int? numTimeSteps = null)
        {
            int numSteps = numTimeSteps??30;

            // Fields to build unsteady vector field from.
            ScalarField[] U = new ScalarField[numSteps];
            ScalarField[] V = new ScalarField[numSteps];

            LoaderRaw file = (RedSea.Singleton.GetLoader(0, 0, 0, RedSea.Variable.VELOCITY_X) as LoaderRaw);
            file.Range.SetMember(RedSea.Dimension.GRID_Z, 0);

            for (int field = 0; field < numSteps; ++field)
            {
                int step = field + startStep;
                int stepN = step / RedSea.Singleton.NumSubsteps;
                int substepN = step % RedSea.Singleton.NumSubsteps;

                if(stepN >= RedSea.Singleton.NumSteps)
                {
                    // Less scalar fields. Crop arrays.
                    Array.Resize(ref U, field);
                    Array.Resize(ref V, field);
                    break;
                }

                // Load field.
                file.Range.SetMember(RedSea.Dimension.TIME, stepN);
                file.Range.SetMember(RedSea.Dimension.SUBTIME, substepN);
                file.Range.SetVariable(RedSea.Variable.VELOCITY_X);

                U[field] = file.LoadFieldSlice();
                file.Range.SetVariable(RedSea.Variable.VELOCITY_Y);
                V[field] = file.LoadFieldSlice();
            }

            _velocity = new VectorFieldUnsteady(new ScalarFieldUnsteady[] 
              { new ScalarFieldUnsteady(U),
                new ScalarFieldUnsteady(V) });
            _velocity.ScaleToGrid(new Vec2((RedSea.Singleton.TimeScale * _everyNthTimestep) / RedSea.Singleton.NumSubsteps));
            _velocityEndTime = startStep + 1;
        }

        protected void ComputeCoreOrigins(int member)
        {
            // Load 2 slices only for computing core origins.
            LoadField(0, member, 2);
            LoaderRaw file = (RedSea.Singleton.GetLoader(0, 0, member, RedSea.Variable.SURFACE_HEIGHT) as LoaderRaw);
            file.Range.SetMember(RedSea.Dimension.GRID_Z, 0);
            ScalarField height = file.LoadFieldSlice();
            // Find core lines in first time step.
            VectorFieldUnsteady pathlineCores = new VectorFieldUnsteady(_velocity, FieldAnalysis.PathlineCore, 3);
            _debugCore = pathlineCores.GetTimeSlice(0);
            _coreOrigins = FieldAnalysis.ComputeCriticalPointsRegularSubdivision2D(pathlineCores.GetTimeSlice(0), 4, 0.1f, 0.001f);
            //_debugCore = pathlineCores.GetSlicePlanarVelocity(0);

            _coreCloud = new PointCloud(Plane, _coreOrigins.ToBasicSet());
        }

        public override void ClickSelection(Vector2 pos)
        {
            _selection = pos;
            _selectionChanged = true;

            Console.WriteLine("Pos: {0}, Value: {1}", pos, _debugCore.Sample((Vec2)pos));
        }

        public List<Renderable> AdvectLines()
        {
            List<Renderable> renderables = new List<Renderable>(3 + _currentSetting.LineX);
            int numLines = _currentSetting.LineX;

            // Update / create underlying plane.
            if (_lastSetting == null ||
                SliceTimeMainChanged)
            {
                _timeSlice = new FieldPlane(Plane, _debugCore /*_velocity.GetTimeSlice(_currentSetting.SliceTimeMain)*/, _currentSetting.Shader, _currentSetting.Colormap);
                _intersectionPlane = new Plane(_intersectionPlane, new Vector3(0, 0, _currentSetting.SliceTimeMain - (_lastSetting?.SliceTimeMain) ?? 0));
            }
            else if (ColormapChanged ||
                ShaderChanged ||
                WindowStartChanged ||
                WindowWidthChanged)
            {
                _timeSlice.SetRenderEffect(_currentSetting.Shader);
                _timeSlice.UsedMap = _currentSetting.Colormap;

                _timeSlice.LowerBound = _currentSetting.WindowStart;
                _timeSlice.UpperBound = _currentSetting.WindowWidth + _currentSetting.WindowStart;
            }
            // First item in list: plane.
            renderables.Add(_timeSlice);
            if(_coreCloud != null)
                renderables.Add(_coreCloud);

            // Add Point to indicate clicked position.
            renderables.Add(new PointCloud(Plane, new PointSet<Point>(new Point[] { new Point() { Position = new Vector3(_selection, _currentSetting.SliceTimeMain + _velocity.Grid.TimeOrigin ?? 0), Color = new Vector3(1, 0, 1), Radius = 0.1f } })));
            bool rebuilt = false;

            // Recompute lines if necessary.
            if (_lastSetting == null ||
                LineXChanged ||
                AlphaStableChanged ||
                StepSizeChanged ||
                IntegrationTypeChanged ||
                IntegrationTimeChanged ||
                FlatChanged ||
                _selectionChanged)
            {
                if (_velocity.IsValid((Vec2)_selection))
                {
                    // Compute starting positions.
                    Point[] circle = new Point[numLines];
                    float offset = _currentSetting.AlphaStable;
                    float angleDiff = 2 * (float)(Math.PI / numLines);
                    for (int dir = 0; dir < numLines; ++dir)
                    {
                        float x = (float)(Math.Sin(angleDiff * dir + Math.PI / 2));
                        float y = (float)(Math.Cos(angleDiff * dir + Math.PI / 2));
                        circle[dir] = new Point() { Position = new Vector3(_selection.X + x * offset, _selection.Y + y * offset, _currentSetting.SliceTimeMain + (_velocity.Grid.TimeOrigin ?? 0)) };
                    }

                    VectorField.Integrator integrator = VectorField.Integrator.CreateIntegrator(_velocity, _currentSetting.IntegrationType);
                    integrator.StepSize = _currentSetting.StepSize;
                    //bool pos = _velocity.SampleDerivative(new Vec3((Vec2)_selection, _currentSetting.SliceTimeMain)).EigenvaluesReal()[0] > 0;
                    //integrator.Direction = pos ? Sign.POSITIVE : Sign.NEGATIVE;

                    LineSet[] lineSets = integrator.Integrate<Point>(new PointSet<Point>(circle), true); /*, _currentSetting.AlphaStable * 10);
                    PointSet<EndPoint> ends = lineSets[0].GetEndPoints();
                    lineSets[0] = integrator.Integrate(ends, false)[0];
                    ends = lineSets[1].GetEndPoints();
                    lineSets[1] = integrator.Integrate(ends, false)[0];*/

                    // COmpute and show statistics.
                    if (_currentSetting.Flat)
                    {
                        lineSets[0].FlattenLines(_currentSetting.SliceTimeMain);
                        lineSets[1].FlattenLines(_currentSetting.SliceTimeMain);
                        _graph = new Renderable[0];
                        // Compute values (distance to start point).
                        foreach (LineSet lines in lineSets)
                        {
                            Line[] starLines = new Line[lines.Lines.Length];

                            int count = 0;
                            float[] values = new float[lines.NumExistentPoints];

                            for (int l = 0; l < lines.Lines.Length; ++l)
                            {
                                Line line = lines.Lines[l];
                                // Outgoing direction.
                                Vector3 start = line.Positions[0];
                                Vector3 dir = line.Positions[0] - new Vector3(_selection, line.Positions[0].Z); ; dir.Normalize();

                                // Scale such that step size does not scale.
                                dir *= _currentSetting.StepSize / _velocity.Size.T * 40;

                                // Write star coordinates here.
                                Vector3[] starPos = new Vector3[line.Length];

                                for (int p = 0; p < line.Length; ++p)
                                {
                                    values[count++] = new Vector2(line.Positions[p].X - _selection.X, line.Positions[p].Y - _selection.Y).Length();
                                    starPos[p] = start + p * dir;
                                }
                                starLines[l] = new Line() { Positions = starPos };
                            }
                            var graph = FieldAnalysis.BuildGraph(Plane, new LineSet(starLines), values, _currentSetting.IntegrationTime, _currentSetting.LineSetting, _currentSetting.Colormap);
                            _graph = graph.Concat(_graph).ToArray();
                        }
                    }
                    else
                        _graph = null;
                    _pathlines = new LineBall[] { new LineBall(Plane, lineSets[0]), new LineBall(Plane, lineSets[1]) };
                }
                _selectionChanged = false;
                rebuilt = true;
            }

            if (_graph != null &&
                _currentSetting.LineSetting == RedSea.DisplayLines.LINE && (
                _lastSetting == null || rebuilt ||
                WindowWidthChanged ||
                WindowStartChanged ||
                ColormapChanged))
            {
                foreach (Renderable ball in _graph)
                {
                    (ball as LineBall).LowerBound = _currentSetting.WindowStart + _currentSetting.AlphaStable * _currentSetting.IntegrationTime + _currentSetting.SliceTimeMain;
                    (ball as LineBall).UpperBound = _currentSetting.WindowStart + _currentSetting.AlphaStable * _currentSetting.IntegrationTime + _currentSetting.SliceTimeMain + _currentSetting.WindowWidth;
                    (ball as LineBall).UsedMap = _currentSetting.Colormap;
                }
            }

            // Add the lineball.
            if (_pathlines != null)
                renderables.AddRange(_pathlines);
            if (_graph != null && _currentSetting.Graph)
                renderables = renderables.Concat(_graph.ToList()).ToList();
            return renderables;
        }

        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.DiffusionMeasure:
                //case Setting.Element.Measure:
                case Setting.Element.MemberReference:
                case Setting.Element.SliceHeight:
                case Setting.Element.SliceTimeReference:
                case Setting.Element.Tracking:
                    return false;
                default:
                    return true;
            }
        }

        public override string GetName(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.AlphaStable:
                    return "Offset Start Point";
                case Setting.Element.LineX:
                    return "Number of Lines";
                case Setting.Element.IntegrationTime:
                    return "Height Scale Statistics";
                default:
                    return base.GetName(element);
            }
        }
    }

}
