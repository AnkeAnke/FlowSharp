using SlimDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace FlowSharp
{
    class CoreDistanceMapper : DataMapper
    {
        #region PropertyChanged
        private bool NumLinesChanged { get { return LineXChanged; } }
        private bool OffsetRadiusChanged { get { return AlphaStableChanged; } }
        private bool GraphScaleChanged { get { return IntegrationTimeChanged; } }
        #endregion PropertyChanged
        private static float SLA_THRESHOLD = 0.0f;
        private static float SLA_RELAXED_THRESHOLD = 0.00f;
        private int MinLengthCore
        {
            get { return (int)((float)(25 * 24) / _everyNthTimestep + 0.5f); }
        }
        private static int STEPS_IN_MEMORY = 30;

        private Plane _linePlane;
        private VectorFieldUnsteady _velocity;
        private Vector2 _selection;

        private LineBall[] _pathlines;
        private LineSet[] _pathlinesTime;
        private LineBall[] _graph;
        private Line[] _coreDistancesGraph;
        private Line[] _coreAngleGraph;
        private FieldPlane _timeSlice, _compareSlice;

        private LineSet _cores;
        private LineBall _coreBall;
        private CriticalPointSet2D _coreOrigins;
        private PointCloud _coreCloud;
        private int _currentEndStep;
        private int _selectedCore = -1;

        private int _everyNthTimestep;

        private Line[] _boundaries;
        private LineBall _boundaryBallFunction;
        private Line[] _boundariesSpacetime;
        private LineBall _boundaryBallSpacetime;

        private float _lastActiveGraphScale = -1;

        private bool _selectionChanged = false;
        public CoreDistanceMapper(int everyNthField, Plane plane)
        {
            _everyNthTimestep = everyNthField;
            Plane = new Plane(plane.Origin, plane.XAxis, plane.YAxis, (plane.ZAxis * RedSea.Singleton.NumSubsteps) / _everyNthTimestep, 1.0f, plane.PointSize);
            _linePlane = plane;
            _intersectionPlane = plane;
            Console.WriteLine("Min Core Length: {0}", MinLengthCore);
            Mapping = Map;

            //ComputeCoreOrigins(_currentSetting.MemberMain, 0);

            //TraceCore(_currentSetting.MemberMain, _currentSetting.SliceTimeMain);

            _boundaries = new Line[(RedSea.Singleton.NumSubstepsTotal * 2) / _everyNthTimestep];
            _boundariesSpacetime = new Line[_boundaries.Length];
            for (int l = 0; l < _boundaries.Length; ++l)
            {
                _boundaries[l] = new Line() { Positions = new Vector3[0] };
                _boundariesSpacetime[l] = new Line() { Positions = new Vector3[0] };
            }
        }
        /// <summary>
        /// Load a stack of 30 field. This should be small enough to have memory free for other operations.
        /// </summary>
        /// <param name="startStep">The start step. Running continuously. 0 1 2 3 ...</param>
        protected void LoadField(int startStep, int member = 0, int? numTimeSteps = null)
        {
            int numSteps = numTimeSteps ?? STEPS_IN_MEMORY;

            // Fields to build unsteady vector field from.
            ScalarField[] U = new ScalarField[numSteps];
            ScalarField[] V = new ScalarField[numSteps];

            LoaderRaw file = (RedSea.Singleton.GetLoader(0, 0, 0, RedSea.Variable.VELOCITY_X) as LoaderRaw);
            file.Range.SetMember(RedSea.Dimension.GRID_Z, 0);
            _currentEndStep = startStep + numSteps - 1;

            for (int field = 0; field < numSteps; ++field)
            {
                int step = (field + startStep) * _everyNthTimestep;
                int stepN = step / RedSea.Singleton.NumSubsteps;
                int substepN = step % RedSea.Singleton.NumSubsteps;

                if (stepN >= RedSea.Singleton.NumSteps)
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
              { new ScalarFieldUnsteady(U, startStep, 1.0f),
                new ScalarFieldUnsteady(V, startStep, 1.0f) });
            _velocity.TimeOrigin = startStep;
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
            _coreOrigins = new CriticalPointSet2D(FilterCores(_coreOrigins.Points).ToArray());
            //_coreOrigins = new CriticalPointSet2D(new CriticalPoint2D[] { _coreOrigins.Points[9] });
            _coreCloud = new PointCloud(Plane, _coreOrigins.ToBasicSet());
        }

        protected void TraceCore(int member = 0, int startSubstep = 0)
        {
            string corename = RedSea.Singleton.CoreFileName + ".line";
            if (System.IO.File.Exists(corename))
                GeometryWriter.ReadFromFile(corename, out _cores);
            else
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
                for (int block = 0; block < numBlocks; ++block)
                {
                    int startStep = block * STEPS_IN_MEMORY;
                    int numSteps = Math.Min(RedSea.Singleton.NumSubstepsTotal / _everyNthTimestep - startStep, STEPS_IN_MEMORY);

                    // Load the VFU.
                    LoadField(startStep, member, numSteps);

                    // Generate Core Field.
                    VectorFieldUnsteady pathlineCores = new VectorFieldUnsteady(_velocity, FieldAnalysis.Acceleration, 3);

                    // ~~~~~~~~~~~~~~~ Trace a line through time ~~~~~~~~~~~~~~~~ \\
                    for (int slice = 0; slice < numSteps; ++slice)
                    {
                        // Take core points with high enough surface height.
                        CriticalPointSet2D pointsT = FieldAnalysis.ComputeCriticalPointsRegularSubdivision2D(pathlineCores.GetTimeSlice(startStep + slice), 4, 0.1f, 0.000001f);
                        List<FloatCP2D> valid = FilterCores(pointsT.Points, SLA_RELAXED_THRESHOLD);

                        // Connect old lines.
                        foreach (List<Vector3> line in coreLines)
                        {
                            Vector3 end = line.Last();
                            // Break if the list already ended before.
                            if (end.Z != startStep + slice - 1)
                            {
                                //Console.WriteLine("Line ends at {0}, trying to connect {1}", end.Z, startStep + slice);
                                continue;
                            }

                            // Look for next closest point.
                            float closestDiff = float.MaxValue;
                            int nextIdx = -1;
                            for (int i = 0; i < valid.Count; ++i)
                            {
                                if (valid[i] == null)
                                    continue;

                                float dist = (valid[i].Position - end).LengthSquared();

                                if (dist < closestDiff)
                                {
                                    closestDiff = dist;
                                    nextIdx = i;
                                }
                            }

                            // Is that point close enough? Add it.
                            if (closestDiff < 50)
                            {
                                line.Add(valid[nextIdx].Position);
                                //Console.WriteLine("Extended line till {0}.", valid[nextIdx].Position.Z);
                                valid[nextIdx] = null;
                            }
                        }

                        // ~~~~~~~~~~~~~ Start new lines ~~~~~~~~~~~~~ \\
                        foreach (FloatCP2D p in valid)
                        {
                            if (p != null && p.Value > SLA_THRESHOLD)
                            {
                                coreLines.Add(new List<Vector3>(numBlocks * STEPS_IN_MEMORY - startStep));
                                coreLines.Last().Add(p.Position);
                            }
                        }

                    }
                    Console.WriteLine("Tracked cores until step {0}.", startStep + numSteps);
                }

                Line[] lines = new Line[coreLines.Count];
                int numLines = 0;
                for (int i = 0; i < coreLines.Count; ++i)
                {
                    if (coreLines[i].Count < MinLengthCore)
                        continue;

                    lines[numLines++] = new Line()
                    {
                        Positions = coreLines[i].ToArray()
                    };
                }

                Array.Resize(ref lines, numLines);
                _cores = new LineSet(lines);
                GeometryWriter.WriteToFile(corename, _cores);
            }
            _coreBall = new LineBall(_linePlane, _cores);
        }

        protected List<FloatCP2D> FilterCores(CriticalPoint2D[] cores, float? abs = null)
        {
            List<FloatCP2D> selected = new List<FloatCP2D>(20);
            if (cores.Length < 1)
                return selected;

            float threshold = abs ?? SLA_THRESHOLD;
            int substep = (int)(cores[0].Position.Z + 0.5f) * _everyNthTimestep;
            int stepN = substep / RedSea.Singleton.NumSubsteps;
            int substepN = substep % RedSea.Singleton.NumSubsteps;

            LoaderRaw loader = new LoaderRaw();
            loader.Range.SetMember(RedSea.Dimension.TIME, stepN);
            loader.Range.SetMember(RedSea.Dimension.SUBTIME, substepN);
            loader.Range.SetMember(RedSea.Dimension.GRID_Z, 0);
            ScalarField SLA = loader.LoadFieldSlice(RedSea.Variable.SURFACE_HEIGHT);



            foreach (CriticalPoint2D cp in cores)
            {
                float height = SLA.Sample((Vec2)cp.Position);

                if (Math.Abs(height) > threshold &&
                    (cp.Type == CriticalPoint2D.TypeCP.ATTRACTING_FOCUS ||
                    cp.Type == CriticalPoint2D.TypeCP.REPELLING_FOCUS))
                {
                    selected.Add(new FloatCP2D(cp, height));
                }
            }

            return selected;
        }

        public override void ClickSelection(Vector2 pos)
        {
            if (pos.X >= 0 && pos.Y >= 0 && pos.X < _velocity.Size[0] && pos.Y < _velocity.Size[1])
            {
                if (_cores == null || _cores.Length < 1)
                {
                    _selection = pos;
                    _selectionChanged = true;

                    Console.WriteLine("Pos: {0}", pos);//, _debugCore.Sample((Vec2)pos));
                    return;
                }

                Vector3 selection3D = new Vector3(pos, _currentSetting.SliceTimeMain);

                float minDist = float.MaxValue;
                _selectedCore = -1;
                // It's a struct, we have to initialize it!
                Vector3 nearest = new Vector3();
                for (int core = 0; core < _cores.Length; ++core)
                {
                    float dist = _cores.Lines[core].DistanceToPointInZ(selection3D, out nearest);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        _selectedCore = core;
                        Debug.Assert(Math.Abs(nearest.Z - selection3D.Z) < 0.00001);
                        _selection = new Vector2(nearest.X, nearest.Y);
                        _coreBall = new LineBall(_linePlane, new LineSet(new Line[] { _cores[_selectedCore] }) { Color = new Vector3(0.8f, 0.1f, 0.1f), Thickness = 0.3f});
                    }
                }
                Debug.Assert(_selectedCore >= 0 && _selectedCore < _cores.Length, "The nearest core is invalid.");

            }
            else
                Console.WriteLine("Selection not within field range.");
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

            // Do we need to load new data/is the selected time step in the currently loaded field?
            LineSet[] lineSets;
            if (_velocity.TimeOrigin > time || _velocity.Size.T < time)
                LoadField(time, _currentSetting.MemberMain);
            integrator.Field = _velocity;
            lineSets = integrator.Integrate<Point>(new PointSet<Point>(circle));

            while (_currentEndStep < RedSea.Singleton.NumSubstepsTotal / _everyNthTimestep - 1)
            {
                LoadField(_currentEndStep, _currentSetting.MemberMain);
                integrator.Field = _velocity;
                integrator.IntegrateFurther(lineSets[0]);
            }
            integrator.Field = _velocity;
            integrator.IntegrateFurther(lineSets[0]);

            if ((_currentSetting.SliceTimeMain * _everyNthTimestep) / RedSea.Singleton.NumSubsteps > (_velocity.TimeOrigin ?? 0))
                LoadField(_currentSetting.SliceTimeMain, _currentSetting.MemberMain, 2);

            //Console.WriteLine(lineSets[0].Lines[0].Positions.Last());
            _pathlinesTime = new LineSet[] { lineSets[0] };
        }

        protected void ComputeGraph(Line core, int time = 0)
        {
            bool remap = false;
            if (_lastSetting == null ||
                NumLinesChanged ||
                OffsetRadiusChanged ||
                StepSizeChanged ||
                IntegrationTypeChanged ||
                GraphScaleChanged ||
                _selectionChanged ||
                _currentSetting.IntegrationTime != _lastActiveGraphScale)
            {
                //lineSets[1].FlattenLines(_currentSetting.SliceTimeMain);
                _graph = new LineBall[0];
                // Compute values (distance to start point).
                LineSet lines = _pathlinesTime[0];

                _coreDistancesGraph = FieldAnalysis.GetGraph(_cores[_selectedCore], _pathlinesTime[0], _currentSetting.StepSize, _everyNthTimestep, true);
                _coreAngleGraph = FieldAnalysis.GetGraph(_cores[_selectedCore], _pathlinesTime[0], _currentSetting.StepSize, _everyNthTimestep, false);

                LineSet set = new LineSet(_coreAngleGraph);
                GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + ".csv", set);
                GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".donut", set);

                remap = true;
            }

            // Compute and show statistics. Since we computed the lines anew, they are not yet flat!
            if ((remap  || FlatChanged || SliceTimeReferenceChanged) &&
                _currentSetting.Graph)
            {
                LineSet graph = new LineSet(_coreAngleGraph);
                if (_currentSetting.SliceTimeReference > _currentSetting.SliceTimeMain)
                {
                    int length = _currentSetting.SliceTimeReference - _currentSetting.SliceTimeMain;
                    length =  (int)((float)length / _currentSetting.StepSize + 0.5f);
                    graph = FieldAnalysis.CutLength(graph, length);
                        }
                //var graph = //FieldAnalysis.BuildGraph(Plane, new LineSet(starLines), values, _currentSetting.IntegrationTime, _currentSetting.LineSetting, _currentSetting.Colormap);
                _graph = new LineBall[] { new LineBall(Plane, graph, LineBall.RenderEffect.HEIGHT, _currentSetting.Colormap/*, !_currentSetting.Flat*/) }; 
                //_graph = graph.Concat(_graph).ToArray();
            }
            else
                _graph = null;
        }

        protected void MapLines()
        {
            if (_currentSetting.Flat)
            {
                // Flatten after line integration. Else, the start point would always be taken.
                LineSet flat = new LineSet(_pathlinesTime[0]);
                flat.FlattenLines(_currentSetting.SliceTimeMain);
                _pathlines = new LineBall[] { new LineBall(_linePlane, flat, LineBall.RenderEffect.HEIGHT)/*, new LineBall(Plane, lineSets[1]) */};
            }
            else
                _pathlines = new LineBall[] { new LineBall(_linePlane, _pathlinesTime[0], LineBall.RenderEffect.HEIGHT) };
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
                                                                       // New Core Tracking.
                if (_lastSetting == null ||
                    MemberMainChanged)
                {
                    // Re-compute cores.
                    ComputeCoreOrigins(_currentSetting.MemberMain, 0);

                    TraceCore(_currentSetting.MemberMain, _currentSetting.SliceTimeMain);

                    _boundaries = new Line[(RedSea.Singleton.NumSubstepsTotal * 2) / _everyNthTimestep];
                    for (int l = 0; l < _boundaries.Length; ++l)
                        _boundaries[l] = new Line() { Positions = new Vector3[0] };
                }
            }

            if (_lastSetting == null || SliceTimeReferenceChanged)
            {
                int totalTime = Math.Min(RedSea.Singleton.NumSubstepsTotal, _currentSetting.SliceTimeReference);
                int time = (totalTime * _everyNthTimestep) / RedSea.Singleton.NumSubsteps;
                int subtime = (totalTime * _everyNthTimestep) % RedSea.Singleton.NumSubsteps;
                _compareSlice = LoadPlane(_currentSetting.MemberMain, time, subtime, true);
            }

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
            renderables.Add(new PointCloud(_linePlane, new PointSet<Point>(new Point[] { new Point() { Position = new Vector3(_selection, _currentSetting.SliceTimeMain), Color = new Vector3(0.7f), Radius = 0.6f } })));
            bool rebuilt = false;

            // Recompute lines if necessary.
            if (_lastSetting == null ||
                NumLinesChanged ||
                OffsetRadiusChanged ||
                StepSizeChanged ||
                IntegrationTypeChanged ||
                GraphScaleChanged ||
                FlatChanged ||
                GraphChanged ||
                _selectionChanged)
            {
                if (/*_velocity.IsValid(new Vec3((Vec2)_selection, _currentSetting.) && */numLines > 0)
                {
                    if (_lastSetting == null ||
                        NumLinesChanged ||
                        OffsetRadiusChanged ||
                        StepSizeChanged ||
                        IntegrationTypeChanged ||
                        _selectionChanged)
                    {
                        IntegrateLines(_cores?.Lines[_selectedCore], _currentSetting.SliceTimeMain);
                        _lastActiveGraphScale = -1;
                    }
                    //if(!_currentSetting.Graph)
                        
                    if (_currentSetting.Graph &&
                        _lastActiveGraphScale != _currentSetting.IntegrationTime)
                    {
                        ComputeGraph(_cores?.Lines[_selectedCore], _currentSetting.SliceTimeMain);
                        ExtractCurrentBoundary(_currentSetting.SliceTimeMain);

                        _lastActiveGraphScale = _currentSetting.IntegrationTime;
                    }
                    MapLines();
                }
                _selectionChanged = false;
                rebuilt = true;
            }
            else if (SliceTimeReferenceChanged)
                ComputeGraph(_cores?.Lines[_selectedCore], _currentSetting.SliceTimeMain);

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
                _boundaryBallFunction.LowerBound = _currentSetting.WindowStart + _currentSetting.AlphaStable * _currentSetting.IntegrationTime + _currentSetting.SliceTimeMain;
                _boundaryBallFunction.UpperBound = _currentSetting.WindowStart + _currentSetting.AlphaStable * _currentSetting.IntegrationTime + _currentSetting.SliceTimeMain + _currentSetting.WindowWidth;
                _boundaryBallFunction.UsedMap = ColorMapping.GetComplementary(_currentSetting.Colormap);

                _pathlines[0].LowerBound = _currentSetting.WindowStart + _currentSetting.AlphaStable * _currentSetting.IntegrationTime + _currentSetting.SliceTimeMain;
                _pathlines[0].UpperBound = _currentSetting.WindowStart + _currentSetting.AlphaStable * _currentSetting.IntegrationTime + _currentSetting.SliceTimeMain + _currentSetting.WindowWidth;
                _pathlines[0].UsedMap = ColorMapping.GetComplementary(_currentSetting.Colormap);
            }

            // Add the lineball.
            if (_pathlines != null)
                renderables.AddRange(_pathlines);
            if (_graph != null && _currentSetting.Graph)
                renderables = renderables.Concat(_graph.ToList()).ToList();
            if (_boundaryBallFunction != null && _currentSetting.Graph)
                renderables.Add(_boundaryBallFunction);
            if (_boundaryBallSpacetime != null && _currentSetting.Graph)// && !_currentSetting.Flat)
                renderables.Add(_boundaryBallSpacetime);
            if(_currentSetting.SliceTimeMain != _currentSetting.SliceTimeReference)
                renderables.Add(_compareSlice);
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

        protected void ExtractCurrentBoundary(int time = 0)
        {
            if (_currentSetting.Graph)
            {
                int[] indices;

                _boundaries[time] = FieldAnalysis.FindBoundaryFromDistanceAngleDonut(_coreDistancesGraph, _coreAngleGraph, out indices);
                //_boundaries[time] = FieldAnalysis.FindBoundaryFromDistanceDonut(_coreDistancesGraph, out indices);
                Console.WriteLine(indices.Length);
                _boundaryBallFunction = new LineBall(Plane, new LineSet(_boundaries) { Thickness = 0.15f }, LineBall.RenderEffect.HEIGHT, _currentSetting.Colormap);

                Vector3[] positionsSpacetime = new Vector3[indices.Length];
                for (int p = 0; p < positionsSpacetime.Length - 1; ++p)
                {
                    positionsSpacetime[p] = _pathlinesTime[0][p][indices[p]];
                }
                positionsSpacetime[positionsSpacetime.Length - 1] = positionsSpacetime[0];
                _boundariesSpacetime[time] = new Line() { Positions = positionsSpacetime };

                _boundaryBallSpacetime = new LineBall(_linePlane, new LineSet(_boundariesSpacetime), LineBall.RenderEffect.HEIGHT, ColorMapping.GetComplementary(_currentSetting.Colormap));
            }
        }
    }

    class DonutAnalyzer : DataMapper
    {
        // Load this.
        private LineSet _loadedData;
        // Unroll this.
        private LineSet _blockData;

        // Compute this.
        private Line _boundaryLoaded, _boundaryBlock;

        // Render these.
        private LineBall _loadedBall, _blockBall, _boundaryLoadedBall, _boundaryBlockBall;

        // Offset this.
        private Plane _fightPlane;

        public DonutAnalyzer(Plane plane)
        {
            Mapping = Map;
            Plane = plane;
            _fightPlane = new Plane(plane, Vector3.UnitZ * 0.1f);
        }
        public override bool IsUsed(Setting.Element element)
        {
            return true;
        }
        public override string GetName(Setting.Element element)
        {
            switch(element)
            {
                case Setting.Element.Flat:
                    return "Flat";
                case Setting.Element.Graph:
                    return "Unroll";
            }
            return base.GetName(element);
        }

        private List<Renderable> Map()
        {
            List<Renderable> output = new List<Renderable>(2);

            bool update = false;
            if (_lastSetting == null ||
                SliceTimeMainChanged)
            {

                GeometryWriter.ReadFromFile(RedSea.Singleton.DonutFileName + ".donut", out _loadedData);
                _loadedBall = new LineBall(Plane, _loadedData, LineBall.RenderEffect.HEIGHT);
                _boundaryLoaded = FieldAnalysis.FindBoundaryFromDistanceDonut(_loadedData.Lines);

                _blockData = FieldAnalysis.PlotLines2D(_loadedData);
                _boundaryBlock = FieldAnalysis.FindBoundaryFromDistanceDonut(_blockData.Lines);

                update = true;
            }
            
            if (update ||
                FlatChanged)
            {
                _loadedBall = new LineBall(Plane, _loadedData, LineBall.RenderEffect.HEIGHT, _currentSetting.Colormap, _currentSetting.Flat);
                _boundaryLoadedBall = new LineBall(_fightPlane, new LineSet(new Line[] { _boundaryLoaded }) { Thickness = 0.2f }, LineBall.RenderEffect.HEIGHT, _currentSetting.Colormap, _currentSetting.Flat);

                _blockBall = new LineBall(Plane, _blockData, LineBall.RenderEffect.HEIGHT, _currentSetting.Colormap, _currentSetting.Flat);
                _boundaryBlockBall = new LineBall(_fightPlane, new LineSet(new Line[] { _boundaryBlock }) { Thickness = 0.2f }, LineBall.RenderEffect.HEIGHT, _currentSetting.Colormap, _currentSetting.Flat);

                update = true;
            }
            if (_lastSetting == null||
                WindowStartChanged ||
                WindowWidthChanged ||
                ColormapChanged ||
                update)
            {
                _loadedBall.LowerBound = _currentSetting.WindowStart;
                _loadedBall.UpperBound = _currentSetting.WindowWidth + _currentSetting.WindowStart;
                _loadedBall.UsedMap = _currentSetting.Colormap;

                _blockBall.LowerBound = _currentSetting.WindowStart;
                _blockBall.UpperBound = _currentSetting.WindowWidth + _currentSetting.WindowStart;
                _blockBall.UsedMap = _currentSetting.Colormap;

                _boundaryBlockBall.LowerBound = _currentSetting.WindowStart;
                _boundaryBlockBall.UpperBound = _currentSetting.WindowWidth + _currentSetting.WindowStart;
                _boundaryBlockBall.UsedMap = ColorMapping.GetComplementary(_currentSetting.Colormap);

                _boundaryLoadedBall.LowerBound = _currentSetting.WindowStart;
                _boundaryLoadedBall.UpperBound = _currentSetting.WindowWidth + _currentSetting.WindowStart;
                _boundaryLoadedBall.UsedMap = ColorMapping.GetComplementary(_currentSetting.Colormap);
            }

            output.Add(_currentSetting.Graph ? _blockBall : _loadedBall);
            output.Add(_currentSetting.Graph ? _boundaryBlockBall : _boundaryLoadedBall);

            return output;
        }


    }
}
