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
        private static float SLA_THRESHOLD = 0.10f;
        private static int STEPS_IN_MEMORY = 30;

        private Plane _linePlane;
        private VectorFieldUnsteady _velocity;
        //private Plane _plane;
        private Vector2 _selection;

        private LineBall[] _pathlines;
        private Renderable[] _graph;
        private FieldPlane _timeSlice;
 //       private FieldPlane _baseSlice;

        private LineSet _cores;
        private LineBall _coreBall;
        private CriticalPointSet2D _coreOrigins;
        private PointCloud _coreCloud;
        //       private LoaderRaw _loader;
        private int _currentEndStep;

 //       private VectorField _debugCore;

        private int _everyNthTimestep;

        private bool _selectionChanged = false;
        public CoreDistanceMapper(int everyNthField, Plane plane)
        {
            _everyNthTimestep = everyNthField;
            Plane = new Plane(plane.Origin, plane.XAxis, plane.YAxis, (plane.ZAxis * RedSea.Singleton.NumSubsteps)/_everyNthTimestep, 1.0f, plane.PointSize);
            _linePlane = plane;
            _intersectionPlane = plane;

            Mapping = Map;

            ComputeCoreOrigins(_currentSetting.MemberMain, 0);

        //    TraceCore(_currentSetting.MemberMain, _currentSetting.SliceTimeMain);
        }
        /// <summary>
        /// Load a stack of 30 field. This should be small enough to have memory free for other operations.
        /// </summary>
        /// <param name="startStep">The start step. Running continuously. 0 1 2 3 ...</param>
        protected void LoadField(int startStep, int member = 0, int? numTimeSteps = null)
        {
            int numSteps = numTimeSteps??STEPS_IN_MEMORY;

            // Fields to build unsteady vector field from.
            ScalarField[] U = new ScalarField[numSteps];
            ScalarField[] V = new ScalarField[numSteps];

            LoaderRaw file = (RedSea.Singleton.GetLoader(0, 0, 0, RedSea.Variable.VELOCITY_X) as LoaderRaw);
            file.Range.SetMember(RedSea.Dimension.GRID_Z, 0);
            _currentEndStep = startStep + numSteps - 1;

            for (int field = 0; field < numSteps; ++field)
            {
                int step = field * _everyNthTimestep + startStep;
                int stepN = step / RedSea.Singleton.NumSubsteps;
                int substepN = step % RedSea.Singleton.NumSubsteps;

                if(stepN >= RedSea.Singleton.NumSteps)
                {
                    // Less scalar fields. Crop arrays.
                    Array.Resize(ref U, field);
                    Array.Resize(ref V, field);
                    _currentEndStep = startStep + field;
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
            _velocity.Grid.TimeOrigin = startStep;
            _velocity.ScaleToGrid(new Vec2((RedSea.Singleton.TimeScale * _everyNthTimestep) / RedSea.Singleton.NumSubsteps));
        }

        protected void ComputeCoreOrigins(int member, int startSubstep = 0)
        {
            // Load 2 slices only for computing core origins.
            LoadField(startSubstep, member, 2);
            //LoaderRaw file = (RedSea.Singleton.GetLoader(_currentSetting.SliceTimeMain, 0, member, RedSea.Variable.SURFACE_HEIGHT) as LoaderRaw);

            //ScalarField height = file.LoadFieldSlice();

            // Find core lines in first time step.
            VectorFieldUnsteady pathlineCores = new VectorFieldUnsteady(_velocity, FieldAnalysis.PathlineCore, 3);
            //_debugCore = pathlineCores.GetTimeSlice(0);
            //LoadPlane(member, (startSubstep * RedSea.Singleton.NumSubsteps) / _everyNthTimestep,  )
            _coreOrigins = FieldAnalysis.ComputeCriticalPointsRegularSubdivision2D(pathlineCores.GetTimeSlice(0), 4, 0.3f, 0.001f);

            // Take only points which are changed SLA and "unique".
            _coreOrigins = new CriticalPointSet2D( FilterCores(_coreOrigins.Points).ToArray());
            _coreOrigins = new CriticalPointSet2D(new CriticalPoint2D[] { _coreOrigins.Points[45] });
            _coreCloud = new PointCloud(Plane, _coreOrigins.ToBasicSet());
        }

        protected void TraceCore(int member = 0, int startSubstep = 0)
        {
            // How often do we have to load a VF stack? 
            int numBlocks = (int)Math.Ceiling((float)(RedSea.Singleton.NumSubstepsTotal - startSubstep) / (_everyNthTimestep * STEPS_IN_MEMORY));

            List<List<Vector3>> coreLines = new List<List<Vector3>>(_coreOrigins.Length * 5);

            // Enter first core points to list.
            for (int p = 0; p < _coreOrigins.Length; ++p)
            {
                coreLines.Add(new List<Vector3>(numBlocks * STEPS_IN_MEMORY));
                coreLines[p].Add(_coreOrigins.Points[p].Position);
            }

            // ~~~~~~~~~~~~~~~~ Split dataset into blocks to not exceed memory ~~~~~~~~~~~~~~~ \\
            for(int block = 0; block < numBlocks; ++block)
            {
                int startStep = block * _everyNthTimestep;
                int numSteps = Math.Min(RedSea.Singleton.NumSubstepsTotal - startStep, STEPS_IN_MEMORY);

                // Load the VFU.
                LoadField(startStep, member, numSteps);

                // Generate Core Field.
                VectorFieldUnsteady pathlineCores = new VectorFieldUnsteady(_velocity, FieldAnalysis.Acceleration, 3);

                // ~~~~~~~~~~~~~~~ Trace a line through time ~~~~~~~~~~~~~~~~ \\
                for (int slice = 0; slice < numSteps; ++slice)
                {
                    // Take core points with high enough surface height.
                    CriticalPointSet2D pointsT = FieldAnalysis.ComputeCriticalPointsRegularSubdivision2D(pathlineCores.GetTimeSlice(slice), 4, 0.1f, 0.000001f);
                    List<CriticalPoint2D> valid = FilterCores(pointsT.Points);

                    // Connect old lines.
                    foreach(List<Vector3> line in coreLines)
                    {
                        Vector3 end = line.Last();
                        // Break if the list already ended before.
                        if (end.Z != startStep + slice - 1)
                            continue;

                        // Look for next closest point.
                        float closestDiff = float.MaxValue;
                        int nextIdx = -1;
                        for(int i = 0; i < valid.Count; ++i)
                        {
                            if (valid[i] == null)
                                continue;

                            float dist = (valid[i].Position - end).LengthSquared();

                            if(dist < closestDiff)
                            {
                                closestDiff = dist;
                                nextIdx = i;
                            }
                        }

                        // Is that point close enough? Add it.
                        if (closestDiff < 10 * _everyNthTimestep)
                        {
                            line.Add(valid[nextIdx].Position);
                            valid[nextIdx] = null;
                        }
                    }

                    // ~~~~~~~~~~~~~ Start new lines ~~~~~~~~~~~~~ \\
                    //foreach(CriticalPoint2D p in valid)
                    //{
                    //    if(p != null)
                    //    {
                    //        coreLines.Add(new List<Vector3>(numBlocks * STEPS_IN_MEMORY - startStep));
                    //        coreLines.Last().Add(p.Position);
                    //    }
                    //}

                }
            }

            Line[] lines = new Line[coreLines.Count];
            for(int i = 0; i < coreLines.Count; ++i)
            {
                lines[i] = new Line()
                {
                    Positions = coreLines[i].ToArray()
                };
            }
            _cores = new LineSet(lines);
            _coreBall = new LineBall(_linePlane, _cores);
        }

        protected List<CriticalPoint2D> FilterCores(CriticalPoint2D[] cores)
        {
            LoaderRaw loader = new LoaderRaw();
            loader.Range.SetMember(RedSea.Dimension.TIME, _currentSetting.SliceTimeMain);
            loader.Range.SetMember(RedSea.Dimension.SUBTIME, 0);
            loader.Range.SetMember(RedSea.Dimension.GRID_Z, 0);
            ScalarField SLA = loader.LoadFieldSlice(RedSea.Variable.SURFACE_HEIGHT);

            List<CriticalPoint2D> selected = new List<CriticalPoint2D>(10);

            foreach(CriticalPoint2D cp in cores)
            {
                float height = SLA.Sample((Vec2)cp.Position);

                if(Math.Abs(height) > SLA_THRESHOLD &&
                    (cp.Type == CriticalPoint2D.TypeCP.ATTRACTING_FOCUS ||
                    cp.Type == CriticalPoint2D.TypeCP.REPELLING_FOCUS))
                {
                    selected.Add(cp);
                }
            }

            return selected;
        }

        public override void ClickSelection(Vector2 pos)
        {
            if (pos.X >= 0 && pos.Y >= 0 && pos.X < _velocity.Size[0] && pos.Y < _velocity.Size[1])
            {
                _selection = pos;
                _selectionChanged = true;

                Console.WriteLine("Pos: {0}", pos);//, _debugCore.Sample((Vec2)pos));
            }
        }
        
        protected void IntegrateLines(Line core, int time = 0)
        {
            int numLines = _currentSetting.LineX;

            // Compute starting positions.
            Point[] circle = new Point[numLines];
            float offset = _currentSetting.AlphaStable;
            float angleDiff = 2 * (float)(Math.PI / numLines);
            for (int dir = 0; dir < numLines; ++dir)
            {
                float x = (float)(Math.Sin(angleDiff * dir + Math.PI / 2));
                float y = (float)(Math.Cos(angleDiff * dir + Math.PI / 2));
                circle[dir] = new Point() { Position = new Vector3(_selection.X + x * offset, _selection.Y + y * offset, _currentSetting.SliceTimeMain) };
            }

            
            VectorField.Integrator integrator = VectorField.Integrator.CreateIntegrator(_velocity, _currentSetting.IntegrationType);
            integrator.StepSize = _currentSetting.StepSize;
            //bool pos = _velocity.SampleDerivative(new Vec3((Vec2)_selection, _currentSetting.SliceTimeMain)).EigenvaluesReal()[0] > 0;
            //integrator.Direction = pos ? Sign.POSITIVE : Sign.NEGATIVE;
            LineSet[] lineSets;
            if (_velocity.Grid.TimeOrigin != 0)
                LoadField(0, _currentSetting.MemberMain);
            integrator.Field = _velocity;
            lineSets = integrator.Integrate<Point>(new PointSet<Point>(circle)); 
            
            while(_currentEndStep < RedSea.Singleton.NumSubstepsTotal / _everyNthTimestep - 1)
            {
                LoadField(_currentEndStep, _currentSetting.MemberMain);
                integrator.Field = _velocity;
                integrator.IntegrateFurther(lineSets[0]);
                //integrator.Integrate<Point>(lineSets[0].GetEndPoints().ToBasicSet(), false);
            }
            if (_currentSetting.SliceTimeMain > (_velocity.Grid.TimeOrigin??0))
                LoadField(_currentSetting.SliceTimeMain, _currentSetting.MemberMain, 2);

            Console.WriteLine(lineSets[0].Lines[0].Positions.Last());
            /*, _currentSetting.AlphaStable * 10);
            
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
                            values[count++] = /*core.DistanceToPointInZ(line.Positions[p]);//*/new Vector2(line.Positions[p].X - _selection.X, line.Positions[p].Y - _selection.Y).Length();
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
            _pathlines = new LineBall[] { new LineBall(_linePlane, lineSets[0])/*, new LineBall(Plane, lineSets[1]) */};
        }

        public List<Renderable> Map()
        {
            List<Renderable> renderables = new List<Renderable>(3 + _currentSetting.LineX);
            int numLines = _currentSetting.LineX;


            if(_lastSetting == null ||
                MeasureChanged ||
                SliceTimeMainChanged ||
                MemberMainChanged)
            {
                int totalTime = Math.Min(RedSea.Singleton.NumSubstepsTotal, _currentSetting.SliceTimeMain);
                int time = (totalTime * _everyNthTimestep) / RedSea.Singleton.NumSubsteps;
                int subtime = (totalTime * _everyNthTimestep) % RedSea.Singleton.NumSubsteps;
                //if (_lastSetting == null || MeasureChanged || MemberMainChanged)
                //    _baseSlice = LoadPlane(_currentSetting.MemberMain, 0, 0, false);

                _timeSlice = LoadPlane(_currentSetting.MemberMain, time, subtime, true);
                _intersectionPlane = _timeSlice.GetIntersectionPlane();//new Plane(Plane, Vector3.UnitZ * ((float)totalTime * _everyNthTimestep) / (RedSea.Singleton.NumSubsteps ));
               // _debugCore = _velocity.GetTimeSlice(_currentSetting.SliceTimeMain);
            }

            //// Update / create underlying plane.
            //if (_lastSetting == null ||
            //    SliceTimeMainChanged)
            //{
            //    _intersectionPlane = new Plane(_intersectionPlane, new Vector3(0, 0, _currentSetting.SliceTimeMain - (_lastSetting?.SliceTimeMain) ?? 0));
            //}
            if (_lastSetting == null ||
                ColormapChanged ||
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
            if (_coreBall != null)
                renderables.Add(_coreBall);

            // Add Point to indicate clicked position.
            renderables.Add(new PointCloud(_linePlane, new PointSet<Point>(new Point[] { new Point() { Position = new Vector3(_selection, _currentSetting.SliceTimeMain), Color = new Vector3(1, 0, 1), Radius = 0.3f } })));
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
                if (_velocity.IsValid((Vec2)_selection) && numLines > 0)
                {
                    IntegrateLines(_cores?.Lines[0]);
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
