using SlimDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Integrator = FlowSharp.VectorField.Integrator;

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

        private Plane _linePlane, _graphPlane;
        private VectorFieldUnsteady _velocity;
        private Vector2 _selection;

        private LineBall _pathlines;
        private LineSet _pathlinesTime;
        private LineBall[] _graph;
        private Line[] _coreDistanceGraph;
        private Line[] _coreAngleGraph;
        private FieldPlane _timeSlice, _compareSlice;

        private LineSet _cores;
        private LineBall _coreBall;
        private CriticalPointSet2D _coreOrigins;
        private PointCloud _coreCloud;
        private int _currentEndStep;
        private int _selectedCore = -1;

        private int _everyNthTimestep;

        private LineSet _boundaries;
        private LineBall _boundaryBallFunction;
        private LineSet _boundariesSpacetime;
        private LineBall _boundaryBallSpacetime;

        private List<Point> _allBoundaryPoints;
        private PointCloud _boundaryCloud;

        private float _lastActiveGraphScale = -1;

        private bool _selectionChanged = false;
        public CoreDistanceMapper(int everyNthField, Plane plane)
        {
            _everyNthTimestep = everyNthField;
            Plane = new Plane(plane.Origin, plane.XAxis, plane.YAxis, (plane.ZAxis * RedSea.Singleton.NumSubsteps) / _everyNthTimestep, 1.0f, plane.PointSize);
            _linePlane = plane;
            _graphPlane = new Plane(plane);
            _intersectionPlane = plane;
            Console.WriteLine("Min Core Length: {0}", MinLengthCore);
            Mapping = Map;

            //ComputeCoreOrigins(MemberMain, 0);

            //TraceCore(MemberMain, SliceTimeMain);

            _boundaries = new LineSet(new Line[(RedSea.Singleton.NumSubstepsTotal * 2) / _everyNthTimestep]);
            _boundariesSpacetime = new LineSet(new Line[_boundaries.Length]);
            for (int l = 0; l < _boundaries.Length; ++l)
            {
                _boundaries[l] = new Line() { Positions = new Vector3[0] };
                _boundariesSpacetime[l] = new Line() { Positions = new Vector3[0] };
            }

            _allBoundaryPoints = new List<Point>(10000);
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
            //LoaderRaw file = (RedSea.Singleton.GetLoader(SliceTimeMain, 0, member, RedSea.Variable.SURFACE_HEIGHT) as LoaderRaw);

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
            {
                GeometryWriter.ReadFromFile(corename, out _cores);
                LoadField(0, MemberMain);
            }
            else
            {
                // Re-compute cores.
                ComputeCoreOrigins(MemberMain, 0);

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

                Vector3 selection3D = new Vector3(pos, SliceTimeMain);

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
                        // TODO: DEBUG
                        _selection = pos; //new Vector2(nearest.X, nearest.Y);
                    }
                }

                _coreBall = new LineBall(_linePlane, new LineSet(new Line[] { _cores[_selectedCore] }) { Color = new Vector3(0.8f, 0.1f, 0.1f), Thickness = 0.3f });
                Debug.Assert(_selectedCore >= 0 && _selectedCore < _cores.Length, "The nearest core is invalid.");

            }
            else
                Console.WriteLine("Selection not within field range.");
        }

        protected void MapLines()
        {
            if (Flat)
            {
                // Flatten after line integration. Else, the start point would always be taken.
                LineSet flat = new LineSet(_pathlinesTime);
                flat.FlattenLines(SliceTimeMain);
                _pathlines = new LineBall(_linePlane, flat, LineBall.RenderEffect.HEIGHT)/*, new LineBall(Plane, lineSets[1]) */;
            }
            else
                _pathlines = new LineBall(_linePlane, _pathlinesTime, LineBall.RenderEffect.HEIGHT);
        }

        public List<Renderable> Map()
        {
            List<Renderable> renderables = new List<Renderable>(3 + LineX);
            int numLines = LineX;

            #region BackgroundPlanes
            if (_lastSetting == null ||
                MeasureChanged ||
                SliceTimeMainChanged ||
                MemberMainChanged)
            {
                // Computing which field to load as background.
                int totalTime = Math.Min(RedSea.Singleton.NumSubstepsTotal, SliceTimeMain);
                int time = (totalTime * _everyNthTimestep) / RedSea.Singleton.NumSubsteps;
                int subtime = (totalTime * _everyNthTimestep) % RedSea.Singleton.NumSubsteps;

                _timeSlice = LoadPlane(MemberMain, time, subtime, true);
                _intersectionPlane = _timeSlice.GetIntersectionPlane();

                if (_lastSetting == null ||
                    MemberMainChanged)
                {
                    // Trace / load cores.
                    TraceCore(MemberMain, SliceTimeMain);

                    // Reset boundaries.
                    _boundaries = new LineSet(new Line[(RedSea.Singleton.NumSubstepsTotal * 2) / _everyNthTimestep]);
                    for (int l = 0; l < _boundaries.Length; ++l)
                        _boundaries[l] = new Line() { Positions = new Vector3[0] };
                }
            }

            if (_lastSetting == null || SliceTimeReferenceChanged)
            {
                // Reference slice.
                int totalTime = Math.Min(RedSea.Singleton.NumSubstepsTotal, SliceTimeReference);
                int time = (totalTime * _everyNthTimestep) / RedSea.Singleton.NumSubsteps;
                int subtime = (totalTime * _everyNthTimestep) % RedSea.Singleton.NumSubsteps;
                _compareSlice = LoadPlane(MemberMain, time, subtime, true);
            }

            if (_lastSetting == null ||
                ColormapChanged ||
                ShaderChanged ||
                WindowStartChanged ||
                WindowWidthChanged)
            {
                _timeSlice.SetRenderEffect(Shader);
                _timeSlice.UsedMap = Colormap;
                _timeSlice.LowerBound = WindowStart;
                _timeSlice.UpperBound = WindowWidth + WindowStart;

                _compareSlice.SetRenderEffect(Shader);
                _compareSlice.UsedMap = Colormap;
                _compareSlice.LowerBound = WindowStart;
                _compareSlice.UpperBound = WindowWidth + WindowStart;
            }

            // First item in list: plane.
            renderables.Add(_timeSlice);
            if (_coreCloud != null)
                renderables.Add(_coreCloud);
            if (_coreBall != null)
                renderables.Add(_coreBall);
            #endregion BackgroundPlanes

            // Add Point to indicate clicked position.
            renderables.Add(new PointCloud(_linePlane, new PointSet<Point>(new Point[] { new Point() { Position = new Vector3(_selection, SliceTimeMain), Color = new Vector3(0.7f), Radius = 0.4f } })));
            bool rebuilt = false;

            // Recompute lines if necessary.
            if (numLines > 0 && (
                _lastSetting == null ||
                NumLinesChanged ||
                OffsetRadiusChanged ||
                StepSizeChanged ||
                IntegrationTypeChanged ||
                _selectionChanged ||
                SliceTimeMainChanged))
            {
                FindBoundary();

                _selectionChanged = false;
                rebuilt = true;
            }
            if (rebuilt || (Graph && SliceTimeReferenceChanged))
            {
                UpdateBoundary();
            }
            if (_lastSetting != null &&
                FlatChanged &&
                _pathlinesTime != null)
            {
                _pathlines = new LineBall(_linePlane, _pathlinesTime, Flat? LineBall.RenderEffect.DEFAULT : LineBall.RenderEffect.HEIGHT, ColorMapping.GetComplementary(Colormap), Flat);
            }
            //else if (SliceTimeReferenceChanged)
            //    ComputeGraph(_cores?.Lines[_selectedCore], SliceTimeMain);

            if (_graph != null &&
                LineSetting == RedSea.DisplayLines.LINE && (
                _lastSetting == null || rebuilt ||
                WindowWidthChanged ||
                WindowStartChanged ||
                ColormapChanged))
            {
                foreach (Renderable ball in _graph)
                {
                    (ball as LineBall).LowerBound = WindowStart;
                    (ball as LineBall).UpperBound = WindowStart + WindowWidth;
                    (ball as LineBall).UsedMap = Colormap;
                }
                _boundaryBallFunction.LowerBound = WindowStart;
                _boundaryBallFunction.UpperBound = WindowStart + WindowWidth;
                _boundaryBallFunction.UsedMap = ColorMapping.GetComplementary(Colormap);

                _pathlines.LowerBound = WindowStart;
                _pathlines.UpperBound = WindowStart + WindowWidth;
                _pathlines.UsedMap = ColorMapping.GetComplementary(Colormap);
            }

            // Add the lineball.
            if (_pathlines != null)
                renderables.Add(_pathlines);
            if (_graph != null && Graph)
                renderables = renderables.Concat(_graph.ToList()).ToList();
            if (_boundaryBallFunction != null && Graph)
                renderables.Add(_boundaryBallFunction);
            if (_boundaryBallSpacetime != null && Graph && !Flat)// && !Flat)
                renderables.Add(_boundaryBallSpacetime);
            if (SliceTimeMain != SliceTimeReference)
                renderables.Add(_compareSlice);
            if (_boundaryCloud != null && Graph)
                renderables.Add(_boundaryCloud);

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

        protected void FindBoundary()
        {

            bool output = false;
        // ~~~~~~~~~~~ Variable Initializations ~~~~~~~~~~~~~ \\

            // Find out: Where do we want the boundary to be?
            // "One day": Take Okubo etc as predictor.
            float preferredBoundaryTime = 20.0f / StepSize;
            float stepSizeStreamlines = StepSize;

            // At which point did we find the Boundary?
            int[] boundaryIndices, boundaryIndicesLast;
            // At which point did we find the Boundary last time? Initalize 0.
            boundaryIndicesLast = new int[LineX];
            for (int i = 0; i < boundaryIndicesLast.Length; i++)
                boundaryIndicesLast[i] = int.MaxValue;

            // Keep the chosen lines in here.
            LineSet chosenPathlines = new LineSet(new Line[LineX]);
            Line[] lastPathlines = new Line[LineX];

            // ~~~~~~~~~~~~ Inner Circle ~~~~~~~~~~~~~~~~~~~~~~~~ \\

            //for (float offsetInnerCircle = 7; offsetInnerCircle < 10; offsetInnerCircle += 0.1f)
            float offsetInnerCircle = AlphaStable;
            {
                chosenPathlines = new LineSet(new Line[LineX]);

                Console.WriteLine("Current offset: {0}", offsetInnerCircle);
                //      float offsetInnerCircle = AlphaStable;

                // Save where to transit steady -> unsteady integration.
                float[] offsetSeeds = new float[LineX];
                for (int i = 0; i < offsetSeeds.Length; i++)
                    offsetSeeds[i] = offsetInnerCircle;
                // Create small circle around selection.
                Point[] circle = new Point[LineX];

                // A small circle around selection for integration.
                // Overwrite with pathline seeds lates.
                float angleDiff = 2 * (float)(Math.PI / LineX);
                for (int dir = 0; dir < circle.Length; ++dir)
                {
                    float x = (float)(Math.Sin(angleDiff * dir + Math.PI / 2));
                    float y = (float)(Math.Cos(angleDiff * dir + Math.PI / 2));

                    // Take the selection as center.
                    circle[dir] = new Point() { Position = new Vector3(_selection.X + x * offsetInnerCircle, _selection.Y + y * offsetInnerCircle, SliceTimeMain) };
                }

                // Seeds for pathlines.
                PointSet<Point> seeds = new PointSet<Point>(circle);

                // ~~~~~~~~~~~~ Integrate Pathlines and Adapt ~~~~~~~~~~~~~~~~~~~~~~~~ \\
                // Setup integrator.
                Integrator pathlineIntegrator = Integrator.CreateIntegrator(null, IntegrationType, _cores[_selectedCore], 1);
                pathlineIntegrator.StepSize = StepSize;

                // Count out the runs for debugging.
                int run = 0;

                while (seeds.Length > 0)
                {
                 //   if (output)
                        Console.WriteLine("Starting run {0}, {1} seeds left.", run++, seeds.Length);

                    // ~~~~~~~~~~~~ Integrate Pathlines  ~~~~~~~~~~~~~~~~~~~~~~~~ \\

                    // Do we need to load a field first?
                    if (_velocity.TimeOrigin > SliceTimeMain || _velocity.TimeOrigin + _velocity.Size.T < SliceTimeMain)
                        LoadField(SliceTimeMain, MemberMain);

                    // Integrate first few steps.
                    pathlineIntegrator.Field = _velocity;
                    LineSet pathlines = pathlineIntegrator.Integrate(seeds, false)[0];

                    // Append integrated lines of next loaded vectorfield time slices.
                    float timeLength = RedSea.Singleton.NumSubstepsTotal / _everyNthTimestep; //preferredBoundaryTime * 2;
                    while (_currentEndStep + 1 < timeLength)
                    {
                        // Don't load more steps than we need to!
                        int numSteps = (int)Math.Min(timeLength - _currentEndStep, STEPS_IN_MEMORY);
                        LoadField(_currentEndStep, MemberMain, numSteps);

                        // Integrate further.
                        pathlineIntegrator.Field = _velocity;
                        pathlineIntegrator.IntegrateFurther(pathlines);
                    }

                    // ~~~~~~~~~~~~ Get Boundary ~~~~~~~~~~~~~~~~~~~~~~~~ \\
                    // The two needes functions.
                    Line[] distances = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, pathlines, StepSize, _everyNthTimestep, true);
                    Line[] angles = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, pathlines, StepSize, _everyNthTimestep, false);

                    // Find the boundary based on angle and distance.
                    FieldAnalysis.FindBoundaryFromDistanceAngleDonut(distances, angles, out boundaryIndices);

                    // ~~~~~~~~~~~~ Chose or Offset Pathlines ~~~~~~~~~~~~ \\
                    int numNewSeeds = 0;
                    int numPathlines = 0;
                    // Recompute start points.
                    for (int idx = 0; idx < LineX; ++idx)
                    {
                        // We already have an optimal line here. Continue.
                        if (chosenPathlines[idx] != null)
                            continue;

                        // Should we save this line?
                        //  Console.WriteLine("Run {0}, idx {1}, numPathlines {2}, numNewSeeds{3}", run, idx, numPathlines, numNewSeeds);
                        bool worseThanLast = Math.Abs(boundaryIndices[numPathlines] - preferredBoundaryTime) > Math.Abs(boundaryIndicesLast[idx] - preferredBoundaryTime);

                        Vector3 pos;
                        // Save this point
                        if (boundaryIndices[numPathlines] >= 0)
                        {
                             pos = pathlines[numPathlines][boundaryIndices[numPathlines]];
                            _allBoundaryPoints.Add(new Point(pos) { Color = new Vector3(0.1f, pos.Z * _everyNthTimestep / RedSea.Singleton.NumSubstepsTotal, 0.1f) });
                        }
                        // Finally found it?
           // TODOD: DEBUG!!!!!!!!!!!!!!!!111!!!!!!!!!!!!!!!!!!elf!!!!!!!!!!!!!!!!
                        if (boundaryIndices[numPathlines] > 0)// == preferredBoundaryTime) // We reached the spot! Take this line!
                        {
                            chosenPathlines[idx] = pathlines[numPathlines];
                            if (output)
                                Console.WriteLine("Line {0} was chosen because it is perfect!", idx);
                        }
                        else if (boundaryIndices[numPathlines] < 0 || // We cannot even integrate a line here. We are out of the field. Take best shot until now.
                                worseThanLast) // The last guess we had was better. Take the old one.)
                        {
                            // Take the last line we integrated.
                            chosenPathlines[idx] = lastPathlines[idx];

                            if (boundaryIndices[numPathlines] >= 0)
                                _allBoundaryPoints.RemoveAt(_allBoundaryPoints.Count - 1);
                            pos = lastPathlines[idx][boundaryIndicesLast[idx]];
                            _allBoundaryPoints.Add(new Point(pos) { Color = new Vector3(0.1f, pos.Z * _everyNthTimestep / RedSea.Singleton.NumSubstepsTotal, 0.1f) });

                            if (boundaryIndices[numPathlines] < 0 && output)
                                Console.WriteLine("Line {0} was chosen because the next one in line could not be integrated.", idx);
                            if (worseThanLast && output)
                                Console.WriteLine("Line {0} was chosen because it is even worse than the last one.", idx);
                        }
                        else
                        {
                            // Integrate this line again.
                            boundaryIndicesLast[idx] = boundaryIndices[numPathlines];
                            // We save these in case the next one is worse.
                            lastPathlines[idx] = pathlines[numPathlines];

                            // Add new seed to seed list.
                            float scale = boundaryIndices[numPathlines] / preferredBoundaryTime;
                            offsetSeeds[numNewSeeds] += (scale > 1) ? StepSize : -StepSize;

                            // Recompute position on circle.
                            float x = (float)(Math.Sin(angleDiff * idx + Math.PI / 2));
                            float y = (float)(Math.Cos(angleDiff * idx + Math.PI / 2));

                            // Take the selection as center.
                            seeds.Points[numNewSeeds] = new Point() { Position = new Vector3(_selection.X + x * offsetSeeds[numNewSeeds], _selection.Y + y * offsetSeeds[numNewSeeds], SliceTimeMain) };

                            // Count up number of new seeds.
                            numNewSeeds++;
                        }

                        // We do not count up this value if there is a chosen pathline at this index already.
                        numPathlines++;
                    }

                    // We maybe need less seeds now?
                    if (numNewSeeds < seeds.Length)
                        Array.Resize(ref seeds.Points, numNewSeeds);
                }
            }
            // ~~~~~~~~~~~~ Get Boundary for Rendering~~~~~~~~~~~~~~~~~~~~~~~~ \\

            // The two needes functions.
            _coreDistanceGraph = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, chosenPathlines, StepSize, _everyNthTimestep, true);
            _coreAngleGraph = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, chosenPathlines, StepSize, _everyNthTimestep, false);

            // Find the boundary based on angle and distance.
            _boundaryBallFunction =  new LineBall(_linePlane, new LineSet(new Line[] { FieldAnalysis.FindBoundaryFromDistanceAngleDonut(_coreDistanceGraph, _coreAngleGraph, out boundaryIndices) }));

            // Find the boundary in space-time.
            int time = SliceTimeMain;
            _boundariesSpacetime[time] = new Line() { Positions = new Vector3[LineX + 1] };
            for (int l = 0; l < LineX; ++l)
                _boundariesSpacetime[time][l] = chosenPathlines[l][boundaryIndices[l]];
            _boundariesSpacetime[time][LineX] = _boundariesSpacetime[time][0];
            _boundaryBallSpacetime = new LineBall(_linePlane, _boundariesSpacetime, LineBall.RenderEffect.HEIGHT, Colormap);

            // Pathlines for rendering.
            _pathlinesTime = chosenPathlines;
            _pathlines = new LineBall(_linePlane, chosenPathlines, LineBall.RenderEffect.HEIGHT, ColorMapping.GetComplementary(Colormap), Flat);

            
            Console.WriteLine("Khalas. Boundary indices:");
            float sumTime = 0;
            for (int i = 0; i < chosenPathlines.Length; i++)
            {
                float atTime = chosenPathlines[i][boundaryIndices[i]].Z;
                sumTime += atTime;
                Console.WriteLine("\tTime at {0}: {1}", i, atTime);
            }
            Console.WriteLine("Aerage time: {0}", sumTime / chosenPathlines.Length);


            _boundaryCloud = new PointCloud(_linePlane, new PointSet<Point>(_allBoundaryPoints.ToArray()));

            LineSet set = new LineSet(_coreAngleGraph);
            GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + "Angle.csv", set);
            GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".angle", set);

            set = new LineSet(_coreDistanceGraph);
            GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + "Distance.csv", set);
            GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".distance", set);
        }

        protected void UpdateBoundary()
        {
            LineSet cutLines;
            if (SliceTimeReference > SliceTimeMain)
            {
                // _graph = cut version of _coreAngleGraph.
                int length = SliceTimeReference - SliceTimeMain;
                length = (int)((float)length / StepSize + 0.5f);
                cutLines = FieldAnalysis.CutLength(new LineSet(_coreAngleGraph), length);
            }
            else
                cutLines = new LineSet(_coreAngleGraph);

            _graph = new LineBall[] { new LineBall(_linePlane, cutLines, LineBall.RenderEffect.HEIGHT, Colormap) };
        }
        //protected void IntegrateLines(Line core, int time = 0)
        //{
        //    int numLines = LineX;

        //    // Compute starting positions.
        //    Point[] circle = new Point[numLines];
        //    float offset = AlphaStable;
        //    float angleDiff = 2 * (float)(Math.PI / numLines);
        //    for (int dir = 0; dir < numLines; ++dir)
        //    {
        //        float x = (float)(Math.Sin(angleDiff * dir + Math.PI / 2));
        //        float y = (float)(Math.Cos(angleDiff * dir + Math.PI / 2));
        //        circle[dir] = new Point() { Position = new Vector3(_selection.X + x * offset, _selection.Y + y * offset, SliceTimeMain) };
        //    }


        //    VectorField.Integrator integrator = VectorField.Integrator.CreateIntegrator(_velocity, IntegrationType);
        //    integrator.StepSize = StepSize;

        //    // Do we need to load new data/is the selected time step in the currently loaded field?
        //    LineSet[] lineSets;
        //    if (_velocity.TimeOrigin > time || _velocity.Size.T < time)
        //        LoadField(time, MemberMain);
        //    integrator.Field = _velocity;
        //    lineSets = integrator.Integrate<Point>(new PointSet<Point>(circle));

        //    while (_currentEndStep < RedSea.Singleton.NumSubstepsTotal / _everyNthTimestep - 1)
        //    {
        //        LoadField(_currentEndStep, MemberMain);
        //        integrator.Field = _velocity;
        //        integrator.IntegrateFurther(lineSets[0]);
        //    }
        //    integrator.Field = _velocity;
        //    integrator.IntegrateFurther(lineSets[0]);

        //    if ((SliceTimeMain * _everyNthTimestep) / RedSea.Singleton.NumSubsteps > (_velocity.TimeOrigin ?? 0))
        //        LoadField(SliceTimeMain, MemberMain, 2);

        //    //Console.WriteLine(lineSets[0].Lines[0].Positions.Last());
        //    _pathlinesTime = new LineSet[] { lineSets[0] };
        //}

        #region IntegrationAndGraph

        protected void IntegrateLines(Line core, int time = 0, bool[] doOffset = null, float offsetBy = 0)
        {
            int numLines = LineX;

            // Compute starting positions.
            Point[] circle = new Point[numLines];
            float offset = AlphaStable;
            float angleDiff = 2 * (float)(Math.PI / numLines);

            int newCount = 0;
            for (int dir = 0; dir < numLines; ++dir)
            {
                float x = (float)(Math.Sin(angleDiff * dir + Math.PI / 2));
                float y = (float)(Math.Cos(angleDiff * dir + Math.PI / 2));

                float scale = offset;
                if (doOffset != null && doOffset[dir])
                {
                    scale += offsetBy;
                }
                circle[newCount] = new Point() { Position = new Vector3(_selection.X + x * scale, _selection.Y + y * scale, SliceTimeMain) };

                if (doOffset == null || doOffset[dir])
                    newCount++;
            }

            if (doOffset != null)
                Array.Resize(ref circle, newCount);

            VectorField.Integrator integrator = VectorField.Integrator.CreateIntegrator(_velocity, IntegrationType);
            integrator.StepSize = StepSize;

            // Do we need to load new data/is the selected time step in the currently loaded field?
            LineSet lineSet;
            if (_velocity.TimeOrigin > time || _velocity.Size.T < time)
                LoadField(time, MemberMain);
            integrator.Field = _velocity;
            lineSet = integrator.Integrate<Point>(new PointSet<Point>(circle))[0];

            while (_currentEndStep < RedSea.Singleton.NumSubstepsTotal / _everyNthTimestep - 1)
            {
                LoadField(_currentEndStep, MemberMain);
                integrator.Field = _velocity;
                integrator.IntegrateFurther(lineSet);
            }
            integrator.Field = _velocity;
            integrator.IntegrateFurther(lineSet);

            if ((SliceTimeMain * _everyNthTimestep) / RedSea.Singleton.NumSubsteps > (_velocity.TimeOrigin ?? 0))
                LoadField(SliceTimeMain, MemberMain, 2);

            //Console.WriteLine(lineSets[0].Lines[0].Positions.Last());
            if (doOffset != null)
            {
                newCount = 0;
                Line[] allLines = new Line[numLines];
                for (int l = 0; l < numLines; ++l)
                {
                    allLines[l] = doOffset[l] ? lineSet[newCount++] : _pathlinesTime[l];
                }

                lineSet = new LineSet(allLines);
            }
            _pathlinesTime = lineSet;
        }

        protected void ComputeGraph(Line core, int time = 0)
        {

            //lineSets[1].FlattenLines(SliceTimeMain);
            _graph = new LineBall[0];
            // Compute values (distance to start point).
            LineSet lines = _pathlinesTime;

            _coreDistanceGraph = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, _pathlinesTime, StepSize, _everyNthTimestep, true);
            _coreAngleGraph = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, _pathlinesTime, StepSize, _everyNthTimestep, false);
            //    LineSet set = new LineSet(_coreAngleGraph);
            //    GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + "Angle.csv", set);
            //    GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".angle", set);

            //    set = new LineSet(_coreDistancesGraph);
            //    GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + "Distance.csv", set);
            //    GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".distance", set);

            //    remap = true;
            //}

            //// Compute and show statistics. Since we computed the lines anew, they are not yet flat!
            //if ((remap || FlatChanged || SliceTimeReferenceChanged) &&
            //    Graph)
            //{
            //    LineSet graph = new LineSet(_coreAngleGraph);
            //    if (SliceTimeReference > SliceTimeMain)
            //    {
            //        int length = SliceTimeReference - SliceTimeMain;
            //        length = (int)((float)length / StepSize + 0.5f);
            //        graph = FieldAnalysis.CutLength(graph, length);
            //    }
            //    //var graph = //FieldAnalysis.BuildGraph(Plane, new LineSet(starLines), values, IntegrationTime, LineSetting, Colormap);
            //    _graph = new LineBall[] { new LineBall(_graphPlane, graph, LineBall.RenderEffect.HEIGHT, Colormap/*, !Flat*/) };
            //    //_graph = graph.Concat(_graph).ToArray();
            //}
            //else
            //    _graph = null;
        }
        protected void ExtractCurrentBoundary(int time = 0)
        {
            if (Graph)
            {
                int numLines = _coreDistanceGraph.Length;
                int[] indices;
                bool[] needsOffset = new bool[numLines];
                bool[] needsInset = new bool[numLines];
                float offsetValue = AlphaStable * 0.2f; // This is the stepsize with which we will expand the circle gradually.
                //int firstMinDistance;

                _boundaries[time] = FieldAnalysis.FindBoundaryFromDistanceAngleDonut(_coreDistanceGraph, _coreAngleGraph, out indices);
                //_boundaries[time] = FieldAnalysis.FindBoundaryFromDistanceDonut(_coreDistancesGraph, out indices);


                float maxDist = 2;

                // Now, the offsetting begins.
                int avgDistance = 0;

                // Find minimum border. This will be our goal.
                for (int dir = 0; dir < numLines; ++dir)
                {
                    //if(indices[dir] < minDistance)
                    //{
                    //    mindex = dir;
                    //    minDistance = indices[dir];
                    //}

                    avgDistance += indices[dir];
                    Console.WriteLine("Cut at index {0}: {1}", dir, indices[dir]);
                }

                avgDistance /= numLines;
                Console.WriteLine("Goal (avg): {0}.\n", avgDistance);

                int numIndicesToBig = 0;
                for (int dir = 0; dir < numLines; ++dir)
                {
                    //if (indices[dir] > minDistance)
                    //{
                    //    needsOffset[dir] = true;
                    //    numIndicesToBig++;
                    //}
                    //else
                    //    needsOffset[dir] = false;

                    if (indices[dir] - avgDistance > maxDist)
                    {
                        numIndicesToBig++;
                        needsOffset[dir] = true;
                    }
                    else
                        needsOffset[dir] = false;

                    if (avgDistance - indices[dir] > maxDist)
                    {
                        numIndicesToBig++;
                        needsInset[dir] = true;
                    }
                    else
                        needsInset[dir] = false;
                }

                int run = 1;
                while (numIndicesToBig > 0 && run <= 50)
                {
                    Console.WriteLine("-- Run no. {0}, offset {1} --", run, run * offsetValue);
                    IntegrateLines(_cores[_selectedCore], time, needsOffset, offsetValue * (run));
                    if (offsetValue * run < Math.Abs(AlphaStable))
                        IntegrateLines(_cores[_selectedCore], time, needsInset, -offsetValue * (run));
                    ComputeGraph(_cores[_selectedCore], time);

                    run++;
                    _boundaries[time] = FieldAnalysis.FindBoundaryFromDistanceAngleDonut(_coreDistanceGraph, _coreAngleGraph, out indices);

                    numIndicesToBig = 0;
                    int newAvg = 0;
                    for (int dir = 0; dir < numLines; ++dir)
                    {
                        Console.WriteLine("Cut at index {0}: {1}", dir, indices[dir]);

                        // The pathline is 0 points long. Just ignore it for now.
                        if (indices[dir] < 0)
                        {
                            needsOffset[dir] = false;
                            needsInset[dir] = false;
                            continue;
                        }
                        if (indices[dir] - avgDistance > maxDist)
                        {
                            numIndicesToBig++;
                            needsOffset[dir] = true;
                        }
                        else
                            needsOffset[dir] = false;
                        if (avgDistance - indices[dir] > maxDist)
                        {
                            numIndicesToBig++;
                            needsInset[dir] = true;
                        }
                        else
                            needsInset[dir] = false;

                        newAvg += indices[dir];
                        //if (indices[dir] > minDistance)
                        //{
                        //    needsOffset[dir] = true;
                        //    numIndicesToBig++;
                        //}
                        //else
                        //    needsOffset[dir] = false;
                    }

                    avgDistance = newAvg / numLines;
                    Console.WriteLine("Goal (avg): {0}.\n", avgDistance);
                }
                if (numIndicesToBig > 0)
                    Console.WriteLine("Too many runs!");

                // Finally: Map boundary to lines.
                _boundaryBallFunction = new LineBall(Plane, new LineSet(_boundaries) { Thickness = 0.15f }, LineBall.RenderEffect.HEIGHT, Colormap);

                int numPoints = 0;
                Vector3[] positionsSpacetime = new Vector3[indices.Length];
                for (int p = 0; p < positionsSpacetime.Length - 1; ++p)
                {
                    if (indices[p] >= 0)
                    {
                        positionsSpacetime[numPoints++] = _pathlinesTime[p][indices[p]];
                    }
                }

                if (numPoints + 1 < positionsSpacetime.Length)
                    Array.Resize(ref positionsSpacetime, numPoints + 1);
                positionsSpacetime[positionsSpacetime.Length - 1] = positionsSpacetime[0];
                _boundariesSpacetime[time] = new Line() { Positions = positionsSpacetime };

                _boundaryBallSpacetime = new LineBall(_linePlane, new LineSet(_boundariesSpacetime), LineBall.RenderEffect.HEIGHT, ColorMapping.GetComplementary(Colormap));
            }
        }

        protected void MapAllLines()
        {
            _graphPlane = new Plane(_linePlane, Vector3.UnitZ * SliceTimeMain);
            if (Graph && (_lastSetting == null ||
                NumLinesChanged ||
                OffsetRadiusChanged ||
                StepSizeChanged ||
                IntegrationTypeChanged ||
                GraphScaleChanged ||
                _selectionChanged ||
                IntegrationTime != _lastActiveGraphScale
               || FlatChanged || SliceTimeReferenceChanged))
            {
                LineSet graph = new LineSet(_coreAngleGraph);
                if (SliceTimeReference > SliceTimeMain)
                {
                    int length = SliceTimeReference - SliceTimeMain;
                    length = (int)((float)length / StepSize + 0.5f);
                    graph = FieldAnalysis.CutLength(graph, length);
                }
                //var graph = //FieldAnalysis.BuildGraph(Plane, new LineSet(starLines), values, IntegrationTime, LineSetting, Colormap);
                _graph = new LineBall[] { new LineBall(_graphPlane, graph, LineBall.RenderEffect.HEIGHT, Colormap/*, !Flat*/) };
                //_graph = graph.Concat(_graph).ToArray();
            }
            else
                _graph = null;

            MapLines();
        }
        #endregion IntegrationAndGraph
    }

    class DonutAnalyzer : DataMapper
    {
        // Load this.
        private LineSet _loadedAngle;
        private LineSet _loadedDistance;
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
            switch (element)
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

                GeometryWriter.ReadFromFile(RedSea.Singleton.DonutFileName + ".angle", out _loadedAngle);
                GeometryWriter.ReadFromFile(RedSea.Singleton.DonutFileName + ".distance", out _loadedDistance);
                _loadedBall = new LineBall(Plane, _loadedAngle, LineBall.RenderEffect.HEIGHT);

                int[] indices;
                _boundaryLoaded = FieldAnalysis.FindBoundaryFromDistanceAngleDonut(_loadedDistance.Lines, _loadedAngle.Lines, out indices);

                _blockData = FieldAnalysis.PlotLines2D(_loadedAngle);
                _boundaryBlock = FieldAnalysis.FindBoundaryFromDistanceDonut(_blockData.Lines);

                update = true;
            }

            if (update ||
                FlatChanged)
            {
                _loadedBall = new LineBall(Plane, _loadedAngle, LineBall.RenderEffect.HEIGHT, Colormap, Flat);
                _boundaryLoadedBall = new LineBall(_fightPlane, new LineSet(new Line[] { _boundaryLoaded }) { Thickness = 0.2f }, LineBall.RenderEffect.HEIGHT, Colormap, Flat);

                _blockBall = new LineBall(Plane, _blockData, LineBall.RenderEffect.HEIGHT, Colormap, Flat);
                _boundaryBlockBall = new LineBall(_fightPlane, new LineSet(new Line[] { _boundaryBlock }) { Thickness = 0.2f }, LineBall.RenderEffect.HEIGHT, Colormap, Flat);

                update = true;
            }
            if (_lastSetting == null ||
                WindowStartChanged ||
                WindowWidthChanged ||
                ColormapChanged ||
                update)
            {
                _loadedBall.LowerBound = WindowStart;
                _loadedBall.UpperBound = WindowWidth + WindowStart;
                _loadedBall.UsedMap = Colormap;

                _blockBall.LowerBound = WindowStart;
                _blockBall.UpperBound = WindowWidth + WindowStart;
                _blockBall.UsedMap = Colormap;

                _boundaryBlockBall.LowerBound = WindowStart;
                _boundaryBlockBall.UpperBound = WindowWidth + WindowStart;
                _boundaryBlockBall.UsedMap = ColorMapping.GetComplementary(Colormap);

                _boundaryLoadedBall.LowerBound = WindowStart;
                _boundaryLoadedBall.UpperBound = WindowWidth + WindowStart;
                _boundaryLoadedBall.UsedMap = ColorMapping.GetComplementary(Colormap);
            }

            output.Add(Graph ? _blockBall : _loadedBall);
            output.Add(Graph ? _boundaryBlockBall : _boundaryLoadedBall);

            return output;
        }


    }
}
