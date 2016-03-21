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

        protected virtual int _numSeeds
        {
            get;
        } = 300;
        protected float _lengthRadius = 35;
        protected float _epsOffset = 0.1f;

        #region PropertyChanged
        protected bool NumLinesChanged { get { return LineXChanged; } }
        protected bool OffsetRadiusChanged { get { return AlphaStableChanged; } }
        protected bool RepulsionChanged { get { return IntegrationTimeChanged; } }
        #endregion PropertyChanged

        protected static float SLA_THRESHOLD = 0.0f;
        protected static float SLA_RELAXED_THRESHOLD = 0.00f;
        protected int MinLengthCore
        {
            get { return 1; }// (int)((float)(10 * 24) / _everyNthTimestep + 0.5f); }
        }
        protected static int STEPS_IN_MEMORY = 20;
        protected float _repulsion { get { return (float)(_everyNthTimestep) * IntegrationTime/*/ 40.0f*/; } }

        protected VectorField.IntegratorPredictorCorrector _coreIntegrator;
        #region Properties
        protected Plane _linePlane, _graphPlane;
        protected VectorFieldUnsteady _velocity;
        protected Vector2 _selection;

        protected LineBall _pathlines;
        protected LineSet _pathlinesTime;
        protected LineBall _graph;
        protected Line[] _coreDistanceGraph;
        protected Line[] _coreAngleGraph;
        protected FieldPlane _timeSlice, _compareSlice;

        protected LineSet _cores;
        protected LineBall _coreBall;
        protected CriticalPointSet2D _coreOrigins;
        protected PointCloud _coreCloud;
        protected int _currentEndStep;
        protected int _selectedCore = -1;
        protected ColormapRenderable _specialObject;

        protected int _everyNthTimestep;

        protected LineSet _boundaries;
        protected LineBall _boundaryBallFunction;
        protected LineSet _boundariesSpacetime;
        protected LineBall _boundaryBallSpacetime;

        protected List<Point> _allBoundaryPoints;
        protected PointCloud _boundaryCloud;

        protected float _lastActiveGraphScale = -1;

        protected bool _selectionChanged = false;
        #endregion Properties
        public CoreDistanceMapper(int everyNthField, Plane plane)
        {
            _everyNthTimestep = everyNthField;
            Plane = new Plane(plane.Origin, plane.XAxis, plane.YAxis, (plane.ZAxis) /(10 * _everyNthTimestep), 1.0f, plane.PointSize);
            _linePlane = new Plane(plane.Origin, plane.XAxis, plane.YAxis, plane.ZAxis / 10, 1.0f);
            _graphPlane = new Plane(_linePlane, Vector3.UnitZ * 0.01f);//.Origin, _linePlane.YAxis, _linePlane.ZAxis * 10);
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

        protected void IntegrateCore(int member = 0, int startSubstep = 0)
        {

        }

        protected void TraceCore(int member = 0, int startSubstep = 0)
        {
            float integratorStep = 0.1f;
            string corename = RedSea.Singleton.CoreFileName + member + ".line";
            if (System.IO.File.Exists(corename))
            {
                GeometryWriter.ReadFromFile(corename, out _cores);
                LoadField(0, MemberMain);
                // TODO: Remove
                
                //_coreIntegrator = FieldAnalysis.PathlineCoreIntegrator(_velocity, integratorStep);
            }
            else
            {
                // Re-compute cores.
                ComputeCoreOrigins(MemberMain, 0);
                _coreOrigins = new CriticalPointSet2D( FilterCores(_coreOrigins.Points, SLA_RELAXED_THRESHOLD).ToArray() );
                // How often do we have to load a VF stack? 
                int numBlocks = (int)Math.Ceiling((float)(RedSea.Singleton.NumSubstepsTotal - startSubstep) / (_everyNthTimestep * STEPS_IN_MEMORY));

                //List<List<Vector3>> coreLines = new List<List<Vector3>>(_coreOrigins.Length * 5);

                //// Enter first core points to list.
                //for (int p = 0; p < _coreOrigins.Length; ++p)
                //{
                //    coreLines.Add(new List<Vector3>(numBlocks * STEPS_IN_MEMORY));
                //    coreLines[p].Add(_coreOrigins.Points[p].Position);
                //}

                // ~~~~~~~~~~~~~~~~ Split dataset into blocks to not exceed memory ~~~~~~~~~~~~~~~ \\
                for (int block = 0; block < numBlocks; ++block)
                {
                    int startStep = block * STEPS_IN_MEMORY;
                    int numSteps = Math.Min(RedSea.Singleton.NumSubstepsTotal / _everyNthTimestep - startStep, STEPS_IN_MEMORY + 1);

                    // Load the VFU.
                    LoadField(startStep, member, numSteps);

                    _coreIntegrator = FieldAnalysis.PathlineCoreIntegrator(_velocity, integratorStep);
                    if (block == 0)
                        _cores = _coreIntegrator.Integrate(_coreOrigins)[0];
                    else
                        _coreIntegrator.IntegrateFurther(_cores);
                    //// Generate Core Field.
                    //VectorFieldUnsteady pathlineCores = new VectorFieldUnsteady(_velocity, FieldAnalysis.PathlineCore, 3);

                    //// ~~~~~~~~~~~~~~~ Trace a line through time ~~~~~~~~~~~~~~~~ \\
                    //for (int slice = 0; slice < numSteps; ++slice)
                    //{
                    //    // Take core points with high enough surface height.
                    //    CriticalPointSet2D pointsT = FieldAnalysis.ComputeCriticalPointsRegularSubdivision2D(pathlineCores.GetTimeSlice(startStep + slice), 4, 0.1f, 0.000001f);
                    //    List<FloatCP2D> valid = FilterCores(pointsT.Points, SLA_RELAXED_THRESHOLD);
                    //    bool[] validUsedYet = new bool[valid.Count];

                    //    // Connect old lines.
                    //    foreach (List<Vector3> line in coreLines)
                    //    {
                    //        Vector3 end = line.Last();
                    //        // Break if the list already ended before.
                    //        if (end.Z < startStep + slice - 3) // Allows to skip 2 steps.
                    //        {
                    //            //Console.WriteLine("Line ends at {0}, trying to connect {1}", end.Z, startStep + slice);
                    //            continue;
                    //        }

                    //        // Look for next closest point.
                    //        float closestDiff = float.MaxValue;
                    //        int nextIdx = -1;
                    //        for (int i = 0; i < valid.Count; ++i)
                    //        {
                    //            if (valid[i] == null)
                    //                continue;

                    //            float dist = (valid[i].Position - end).LengthSquared();

                    //            if (dist < closestDiff)
                    //            {
                    //                closestDiff = dist;
                    //                nextIdx = i;
                    //            }
                    //        }

                    //        // Is that point close enough? Add it.
                    //        if (closestDiff < 30)
                    //        {
                    //            line.Add(valid[nextIdx].Position);
                    //            //Console.WriteLine("Extended line till {0}.", valid[nextIdx].Position.Z);
                    //            //valid[nextIdx] = null;
                    //            validUsedYet[nextIdx] = true;
                    //        }
                    //    }

                    //    // ~~~~~~~~~~~~~ Start new lines ~~~~~~~~~~~~~ \\
                    //    for (int i = 0; i < validUsedYet.Length; i++)
                    //    {
                    //        FloatCP2D p = valid[i];
                    //        if (p != null && !validUsedYet[i] && p.Value > SLA_THRESHOLD)
                    //        {
                    //            coreLines.Add(new List<Vector3>(numBlocks * STEPS_IN_MEMORY - startStep));
                    //            coreLines.Last().Add(p.Position);
                    //        }
                    //    }

                    //}
                    Console.WriteLine("Tracked cores until step {0}.", startStep + numSteps);
                }

                //Line[] lines = new Line[coreLines.Count];
                //int numLines = 0;
                //for (int i = 0; i < coreLines.Count; ++i)
                //{
                //    if (coreLines[i].Count < MinLengthCore)
                //        continue;

                //    lines[numLines++] = new Line()
                //    {
                //        Positions = coreLines[i].ToArray()
                //    };
                //}

                //Array.Resize(ref lines, numLines);
                //_cores = new LineSet(lines);
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

            int count = selected.Count;
            for (int i = 0; i < count; ++i)
                for(int j = i + 1; j < count; ++j)
                    if((selected[i].Position - selected[j].Position).LengthSquared() < 1)
                    {
                        selected.RemoveAt(j);
                        j--;
                        count--;
                    }

            return selected;
        }

        public override void ClickSelection(Vector2 pos)
        {
            Vector3 selection3D = Vector3.Zero;
            if (pos.X >= 0 && pos.Y >= 0 && pos.X < _velocity.Size[0] && pos.Y < _velocity.Size[1])
            {
                if (_cores == null || _cores.Length < 1)
                {
                    _selection = pos;
                    _selectionChanged = true;

                    Console.WriteLine("Pos: {0}", pos);//, _debugCore.Sample((Vec2)pos));
                    return;
                }

                selection3D = new Vector3(pos, SliceTimeMain);

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
                if (_selectedCore != -1)
                {
                    _cores[_selectedCore] = new Line() { Positions = new Vector3[] { selection3D, selection3D + Vector3.UnitZ * (RedSea.Singleton.NumSubstepsTotal / _everyNthTimestep - SliceTimeMain) } };
                    _coreBall = new LineBall(_linePlane, new LineSet(new Line[] { _cores[_selectedCore] }) { Color = new Vector3(0.8f, 0.1f, 0.1f), Thickness = 0.3f });
                }
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

                // Computing which field to load as background.
                int totalTime = Math.Min(RedSea.Singleton.NumSubstepsTotal, SliceTimeMain);
                int time = (totalTime * _everyNthTimestep) / RedSea.Singleton.NumSubsteps;
                int subtime = (totalTime * _everyNthTimestep) % RedSea.Singleton.NumSubsteps;

                //LoadField(SliceTimeMain, MemberMain, 2);

                //VectorFieldUnsteady pathlineLength = new VectorFieldUnsteady(_velocity, FieldAnalysis.AccelerationLength, 1);
                //VectorFieldUnsteady correctorF = new VectorFieldUnsteady(pathlineLength, FieldAnalysis.NegativeGradient, 2);
                _timeSlice = /*new FieldPlane(Plane, correctorF.GetTimeSlice(SliceTimeMain), Shader, Colormap);//*/LoadPlane(MemberMain, time, subtime, true);
                _intersectionPlane = _timeSlice.GetIntersectionPlane();
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
            //if (_coreCloud != null)
            //    renderables.Add(_coreCloud);
            //if (_coreBall != null)
            //    renderables.Add(_coreBall);
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
                //RepulsionChanged ||
                SliceTimeMainChanged))
            {
                FindBoundary();

                _selectionChanged = false;
                rebuilt = true;
            }

            UpdateBoundary();

            if (_lastSetting != null &&
                FlatChanged &&
                _pathlinesTime != null)
            {
                if (Flat)
                    _pathlines = null;
                else
                    _pathlines = new LineBall(_linePlane, _pathlinesTime, Flat ? LineBall.RenderEffect.DEFAULT : LineBall.RenderEffect.HEIGHT, ColorMapping.GetComplementary(Colormap), Flat);
            }
            //else if (SliceTimeReferenceChanged)
            //    ComputeGraph(_cores?.Lines[_selectedCore], SliceTimeMain);

            if (LineSetting == RedSea.DisplayLines.LINE && (
                _lastSetting == null || rebuilt ||
                WindowWidthChanged ||
                WindowStartChanged ||
                ColormapChanged))
            {
                if (_graph != null)
                {
                    _graph.LowerBound = WindowStart;
                    _graph.UpperBound = WindowStart + WindowWidth;
                    _graph.UsedMap = Colormap;
                }

                if (_boundaryBallFunction != null)
                {
                    _boundaryBallFunction.LowerBound = WindowStart;
                    _boundaryBallFunction.UpperBound = WindowStart + WindowWidth;
                    _boundaryBallFunction.UsedMap = ColorMapping.GetComplementary(Colormap);
                }

                if (_pathlines != null)
                {
                    _pathlines.LowerBound = WindowStart;
                    _pathlines.UpperBound = WindowStart + WindowWidth;
                    _pathlines.UsedMap = ColorMapping.GetComplementary(Colormap);
                }
                if (_specialObject != null)
                {
                    _specialObject.LowerBound = WindowStart;
                    _specialObject.UpperBound = WindowStart + WindowWidth;
                    _specialObject.UsedMap = ColorMapping.GetComplementary(Colormap);
                }
            }

            // Add the lineball.
            //if (_pathlines != null)
            //    renderables.Add(_pathlines);
            if (_graph != null && (Graph || Flat))
                renderables.Add(_graph);
            if (_boundaryBallFunction != null)// && Graph)
                renderables.Add(_boundaryBallFunction);
            if (_boundaryBallSpacetime != null && !Graph)// && Graph && !Flat)
                renderables.Add(_boundaryBallSpacetime);
            if (SliceTimeMain != SliceTimeReference)
                renderables.Add(_compareSlice);
            if (_boundaryCloud != null)// && Graph)
                renderables.Add(_boundaryCloud);
            if (_specialObject != null)
                renderables.Add(_specialObject);

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
                    return "Repulsion Strength";
                default:
                    return base.GetName(element);
            }
        }

        protected virtual void FindBoundary()
        {

            bool output = false;

            //float acceptanceRatio = 0.9f;
            //float maxTimeDistance = 0.5f;

            // ~~~~~~~~~~~ Variable Initializations ~~~~~~~~~~~~~ \\

            // Find out: Where do we want the boundary to be?
            // "One day": Take Okubo etc as predictor.
            float preferredBoundaryTime = 12.0f;
            float maxTimeDistance = 0.1f;
            float maxRadius = 12;
            float minRadius = 1;
            float radiusGrowth = StepSize * 3;
            //float avgBoundaryTime = 0;
            float stepSizeStreamlines = StepSize;

            // At which point did we find the Boundary?
            int[] boundaryIndices, boundaryIndicesLast;
            // At which point did we find the Boundary last time? Initalize 0.
            boundaryIndicesLast = new int[LineX];
            for (int i = 0; i < boundaryIndicesLast.Length; i++)
                boundaryIndicesLast[i] = int.MaxValue;

            // Keep the chosen lines in here.
            LineSet chosenPathlines = new LineSet(new Line[LineX]);
            // These are lines that do not fullfill the criteria, but are the best ones we got so far.
            Line[] bestPathlines = new Line[LineX];
            float[] bestTimeDistance = new float[LineX];
            for (int i = 0; i < bestTimeDistance.Length; i++)
                bestTimeDistance[i] = float.MaxValue;

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
                Integrator pathlineIntegrator = Integrator.CreateIntegrator(null, IntegrationType, _cores[_selectedCore], _repulsion);
                pathlineIntegrator.StepSize = StepSize;

                // Count out the runs for debugging.
                int run = 0;

                while (seeds.Length > 0)
                {
                    //   if (output)
                    Console.WriteLine("Starting run {0}, {1} seeds left.", run++, seeds.Length);

                    // ~~~~~~~~~~~~ Integrate Pathlines  ~~~~~~~~~~~~~~~~~~~~~~~~ \\
                    #region IntegratePathlines
                    // Do we need to load a field first?
                    if (_velocity.TimeOrigin > SliceTimeMain || _velocity.TimeOrigin + _velocity.Size.T < SliceTimeMain)
                        LoadField(SliceTimeMain, MemberMain);

                    // Integrate first few steps.
                    pathlineIntegrator.Field = _velocity;
                    Line[] seedLines = new Line[seeds.Length];
                    for (int s = 0; s < seeds.Length; ++s)
                        seedLines[s] = new Line() { Positions = new Vector3[] { seeds[s].Position } };
                    LineSet pathlines = new LineSet(seedLines);
                    pathlineIntegrator.IntegrateFurther(pathlines);
                    //LineSet pathlines = pathlineIntegrator.Integrate(seeds, false)[0];

                    // Append integrated lines of next loaded vectorfield time slices.
                    float timeLength = RedSea.Singleton.NumSubstepsTotal / _everyNthTimestep; //preferredBoundaryTime * 2;
                    while (_currentEndStep + 1 < timeLength)
                    {
                        // Don't load more steps than we need to!
                        int numSteps = (int)Math.Min(timeLength - _currentEndStep, STEPS_IN_MEMORY);
                        pathlineIntegrator.Field = null;
                        LoadField(_currentEndStep, MemberMain, numSteps);

                        // Integrate further.
                        pathlineIntegrator.Field = _velocity;
                        pathlineIntegrator.IntegrateFurther(pathlines);
                    }
                    #endregion IntegratePathlines

                    // ~~~~~~~~~~~~ Get Boundary ~~~~~~~~~~~~~~~~~~~~~~~~ \\
                    #region GetBoundary
                    // The two needes functions.
                    Line[] distances = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, pathlines, (StepSize * _everyNthTimestep) / 24.0f, _everyNthTimestep, true);
                    Line[] angles = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, pathlines, (StepSize * _everyNthTimestep) / 24.0f, _everyNthTimestep, false);

                    // Find the boundary based on angle and distance.
                    FieldAnalysis.FindBoundaryFromDistanceAngleDonut(distances, angles, out boundaryIndices);
                    #endregion GetBoundary

                    // ~~~~~~~~~~~~ Chose or Offset Pathlines ~~~~~~~~~~~~ \\
                    int numNewSeeds = 0;
                    int numPathlines = 0;
                    float avgTimeDistance = 0;
                    // Recompute start points.
                    for (int idx = 0; idx < LineX; ++idx)
                    {
                        // We already have an optimal line here. Continue.
                        if (chosenPathlines[idx] != null)
                            continue;

                        // Should we save this line?

                        Vector3 pos;
                        // Save this point
                        if (boundaryIndices[numPathlines] >= 0)
                        {
                            pos = pathlines[numPathlines][boundaryIndices[numPathlines]];
                            _allBoundaryPoints.Add(new Point(pos) { Color = new Vector3(0.1f, pos.Z * _everyNthTimestep / RedSea.Singleton.NumSubstepsTotal, 0.1f) });
                        }
                        // Finally found it?
                        // TODOD: DEBUG!!!!!!!!!!!!!!!!111!!!!!!!!!!!!!!!!!!elf!!!!!!!!!!!!!!!!
                        float timeDistance = Math.Abs((boundaryIndices[numPathlines] * StepSize) - preferredBoundaryTime);
                        avgTimeDistance += timeDistance;
                        if (timeDistance < maxTimeDistance || run >= 0) // We reached the spot! Take this line!
                        {
                            chosenPathlines[idx] = pathlines[numPathlines];
                            if (output)
                                Console.WriteLine("Line {0} was chosen because it is perfect!", idx);
                        }
                        else
                        {
                            // Add new seed to seed list.
                            float scale = boundaryIndices[numPathlines] / preferredBoundaryTime;
                            float newOffset = offsetSeeds[numPathlines] + ((scale > 1) ? radiusGrowth : -radiusGrowth);
                            newOffset = Math.Max(minRadius, newOffset);
                            newOffset = Math.Min(maxRadius, newOffset);


                            // We are stuck. Take best value we reached so far and go on.
                            if (newOffset == offsetSeeds[numPathlines] && bestPathlines[idx] != null)
                            {
                                chosenPathlines[idx] = bestPathlines[idx];
                                continue;
                            }
                            if (timeDistance < bestTimeDistance[idx])
                            {
                                bestPathlines[idx] = pathlines[numPathlines];
                                bestTimeDistance[idx] = timeDistance;
                            }

                            offsetSeeds[numPathlines] = newOffset;

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

                    Console.WriteLine("Average time distance: {0}", avgTimeDistance / numPathlines);
                }
            }
            // ~~~~~~~~~~~~ Get Boundary for Rendering~~~~~~~~~~~~~~~~~~~~~~~~ \\

            // The two needes functions.
            _coreDistanceGraph = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, chosenPathlines, StepSize, _everyNthTimestep, true);
            _coreAngleGraph = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, chosenPathlines, StepSize, _everyNthTimestep, false);

            // Find the boundary based on angle and distance.
            _boundaryBallFunction = new LineBall(_linePlane, new LineSet(new Line[] { FieldAnalysis.FindBoundaryFromDistanceAngleDonut(_coreDistanceGraph, _coreAngleGraph, out boundaryIndices) }));

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
            Console.WriteLine("Average time: {0}", sumTime / chosenPathlines.Length);


            _boundaryCloud = new PointCloud(_linePlane, new PointSet<Point>(_allBoundaryPoints.ToArray()));

            LineSet set = new LineSet(_coreAngleGraph);
            GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + "Angle.csv", set);
            GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".angle", set);

            set = new LineSet(_coreDistanceGraph);
            GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + "Distance.csv", set);
            GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".distance", set);
        }

        protected virtual void UpdateBoundary()
        {
            if (_lastSetting != null && LineX > 0 && (
                NumLinesChanged ||
                OffsetRadiusChanged ||
                StepSizeChanged ||
                IntegrationTypeChanged ||
                _selectionChanged ||
                RepulsionChanged ||
                SliceTimeMainChanged))
            {
                LineSet cutLines;
                if (SliceTimeReference > SliceTimeMain)
                {
                    // _graph = cut version of _coreAngleGraph.
                    int length = SliceTimeReference - SliceTimeMain;
                    length = (int)((float)length / StepSize + 0.5f);
                    cutLines = FieldAnalysis.CutLength(new LineSet(_coreDistanceGraph), length);
                }
                else
                    cutLines = new LineSet(_coreDistanceGraph);

                cutLines = new LineSet(new Line[0]);

                _graph = new LineBall(_graphPlane, cutLines, LineBall.RenderEffect.HEIGHT, Colormap);
            }
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

            VectorField.Integrator integrator = VectorField.Integrator.CreateIntegrator(_velocity, IntegrationType, _cores[_selectedCore], 24.0f / _everyNthTimestep);
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
            _graph = null;
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
                RepulsionChanged ||
                _selectionChanged
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
                _graph = new LineBall(_graphPlane, graph, LineBall.RenderEffect.HEIGHT, Colormap/*, !Flat*/);
                //_graph = graph.Concat(_graph).ToArray();
            }
            else
                _graph = null;

            MapLines();
        }
        #endregion IntegrationAndGraph
    }

    class PredictedCoreDistanceMapper : CoreDistanceMapper
    {
        protected ScalarField _okubo;
        protected float _standardDerivation = -1;
        public PredictedCoreDistanceMapper(int everyNthField, Plane plane) : base(everyNthField, plane)
        {
            Mapping = Map;
        }

        protected override FieldPlane LoadPlane(int member, int time, int subtime = 0, bool timeOffset = false)
        {
            FieldPlane p = base.LoadPlane(member, time, subtime, timeOffset);

            Console.WriteLine("1. Nr. of textures: {0}", p.NumTextures);
            if (_okubo == null)
                return p;

            switch (Shader)
            {
                case FieldPlane.RenderEffect.LIC:
                    p.AddScalar(_okubo);
                    Console.WriteLine("Added Okubo!");
                    break;
                case FieldPlane.RenderEffect.COLORMAP:
                    VectorField ok = new VectorField(new Field[] { _okubo });
                    ok.TimeSlice = timeOffset ? time * RedSea.Singleton.NumSubsteps + subtime : 0;
                    p = new FieldPlane(Plane, ok, FieldPlane.RenderEffect.COLORMAP, Colormap);
                    Console.WriteLine("Only Okubo!");
                    break;
            }
            Console.WriteLine("2. Nr. of textures: {0}", p.NumTextures);
            return p;
        }

        protected PointSet<Point> OkuboBoundary(int time)
        {
            Vector3 center3;
            _cores[_selectedCore].DistanceToPointInZ(Vector3.UnitZ * time, out center3);
            Vec2 center = ((Vec3)center3).ToVec2();

            // Compute Okubo Weiss field for goal slice.
            if (_okubo == null)
            {
                if (time > _velocity.TimeOrigin || time < _velocity.TimeOrigin + _velocity.Size.T)
                    LoadField(time, MemberMain, 1);
                _okubo = new VectorField(_velocity.GetTimeSlice(time), FieldAnalysis.OkuboWeiss, 1, true)[0] as ScalarField;
                if (_compareSlice != null && _compareSlice.NumTextures < 3)
                    _compareSlice.AddScalar(_okubo);
                float fill, mean;
                _okubo.ComputeStatistics(out fill, out mean, out _standardDerivation);
            }
            float threshold = _standardDerivation * (-0.2f);
            Debug.Assert(_okubo.Sample(center) < threshold);

            // Run into each direction and cross Okubo.
            Point[] circleOW = new Point[LineX];
            float angleDiff = 2 * (float)(Math.PI / LineX);
            for (int dir = 0; dir < LineX; ++dir)
            {
                float x = (float)(Math.Sin(angleDiff * dir + Math.PI / 2));
                float y = (float)(Math.Cos(angleDiff * dir + Math.PI / 2));

                Vec2 ray = new Vec2(x, y);
                ray.Normalize();
                ray *= StepSize;
                int step = 0;

                while (true) // Hehe!
                {
                    Vec2 pos = center + ray * step++;
                    if (!_okubo.IsValid(pos) || _okubo.Sample(pos) >= threshold)
                    {
                        pos = center + ray * (step - 2);
                        circleOW[dir] = new Point(new Vector3(pos.X, pos.Y, time)) { Color = new Vector3(0.1f, 0.1f, 0.9f) };
                        break;
                    }
                }
            }

            return new PointSet<Point>(circleOW);
        }
        protected override void FindBoundary()
        {

            bool output = false;

            //float acceptanceRatio = 0.9f;
            //float maxTimeDistance = 0.5f;

            // ~~~~~~~~~~~ Variable Initializations ~~~~~~~~~~~~~ \\

            // Find out: Where do we want the boundary to be?
            // "One day": Take Okubo etc as predictor.
            int preferredBoundaryTime = SliceTimeMain + (24 * RedSea.Singleton.NumSubsteps) / _everyNthTimestep;
            float maxTimeDistance = 1.0f;
            float maxRadius = 12;
            float minRadius = 1;
            float radiusGrowth = StepSize * 3;
            float stepSizeStreamlines = StepSize;


            PointSet<Point> boundaryOW = OkuboBoundary(preferredBoundaryTime);

            // At which point did we find the Boundary?
            int[] boundaryIndices, boundaryIndicesLast;
            // At which point did we find the Boundary last time? Initalize 0.
            boundaryIndicesLast = new int[LineX];
            for (int i = 0; i < boundaryIndicesLast.Length; i++)
                boundaryIndicesLast[i] = int.MaxValue;

            // Keep the chosen lines in here.
            LineSet chosenPathlines = new LineSet(new Line[LineX]);
            // These are lines that do not fullfill the criteria, but are the best ones we got so far.
            Line[] bestPathlines = new Line[LineX];
            float[] bestTimeDistance = new float[LineX];
            for (int i = 0; i < bestTimeDistance.Length; i++)
                bestTimeDistance[i] = float.MaxValue;

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

                // Seeds for pathlines.
                PointSet<Point> seeds = boundaryOW; //new PointSet<Point>(circle);

                // ~~~~~~~~~~~~ Integrate Pathlines and Adapt ~~~~~~~~~~~~~~~~~~~~~~~~ \\
                // Setup integrator.
                Integrator pathlineIntegrator = Integrator.CreateIntegrator(null, IntegrationType, _cores[_selectedCore], _repulsion);
                pathlineIntegrator.StepSize = StepSize;

                // Count out the runs for debugging.
                int run = 0;

                //while (seeds.Length > 0)
                //{
                //   if (output)
                Console.WriteLine("Starting run {0}, {1} seeds left.", run++, seeds.Length);

                // ~~~~~~~~~~~~ Integrate Pathlines  ~~~~~~~~~~~~~~~~~~~~~~~~ \\
                #region IntegratePathlines
                // Do we need to load a field first?
                if (_velocity.TimeOrigin > preferredBoundaryTime - 1 || _velocity.TimeOrigin + _velocity.Size.T < preferredBoundaryTime + 1)
                    LoadField(Math.Max(preferredBoundaryTime - STEPS_IN_MEMORY / 2, 0), MemberMain, STEPS_IN_MEMORY);
                int startStep = (int)_velocity.TimeOrigin;

                // Integrate first few steps.
                pathlineIntegrator.Field = _velocity;
                pathlineIntegrator.Direction = Sign.POSITIVE;
                LineSet[] pathlines = pathlineIntegrator.Integrate(seeds, true);

                // Append integrated lines of next loaded vectorfield time slices.
                float timeLength = RedSea.Singleton.NumSubstepsTotal / _everyNthTimestep; //preferredBoundaryTime * 2;
                while (_currentEndStep + 1 < timeLength)
                {
                    // Don't load more steps than we need to!
                    int numSteps = (int)Math.Min(timeLength - _currentEndStep, STEPS_IN_MEMORY);
                    pathlineIntegrator.Field = null;
                    LoadField(_currentEndStep, MemberMain, numSteps);

                    // Integrate further.
                    pathlineIntegrator.Field = _velocity;
                    pathlineIntegrator.IntegrateFurther(pathlines[0]);
                }

                // Now, integrate backwards.
                pathlineIntegrator.Direction = Sign.NEGATIVE;
                while (startStep > 0)
                {
                    // Don't load more steps than we need to!
                    int numSteps = (int)Math.Min(startStep + 1, STEPS_IN_MEMORY);
                    pathlineIntegrator.Field = null;
                    LoadField(startStep - numSteps + 1, MemberMain, numSteps);
                    startStep = (int)_velocity.TimeOrigin;

                    // Integrate further.
                    pathlineIntegrator.Field = _velocity;
                    pathlineIntegrator.IntegrateFurther(pathlines[1]);
                }

                pathlines[1].Reverse();
                pathlines[1].Append(pathlines[0]);

                chosenPathlines = pathlines[1];
                #endregion IntegratePathlines

                //        // ~~~~~~~~~~~~ Get Boundary ~~~~~~~~~~~~~~~~~~~~~~~~ \\
                //        #region GetBoundary
                //        // The two needes functions.
                //        Line[] distances = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, pathlines[0], StepSize, _everyNthTimestep, true);
                //        Line[] angles = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, pathlines[0], StepSize, _everyNthTimestep, false);

                //        // Find the boundary based on angle and distance.
                //        FieldAnalysis.FindBoundaryFromDistanceAngleDonut(distances, angles, out boundaryIndices);
                //        #endregion GetBoundary

                //        // ~~~~~~~~~~~~ Chose or Offset Pathlines ~~~~~~~~~~~~ \\
                //        int numNewSeeds = 0;
                //        int numPathlines = 0;
                //        float avgTimeDistance = 0;
                //        // Recompute start points.
                //        for (int idx = 0; idx < LineX; ++idx)
                //        {
                //            // We already have an optimal line here. Continue.
                //            if (chosenPathlines[idx] != null)
                //                continue;

                //            // Should we save this line?

                //            Vector3 pos;
                //            // Save this point
                //            if (boundaryIndices[numPathlines] >= 0)
                //            {
                //                pos = pathlines[numPathlines][boundaryIndices[numPathlines]];
                //                _allBoundaryPoints.Add(new Point(pos) { Color = new Vector3(0.1f, pos.Z * _everyNthTimestep / RedSea.Singleton.NumSubstepsTotal, 0.1f) });
                //            }
                //            // Finally found it?
                //            // TODOD: DEBUG!!!!!!!!!!!!!!!!111!!!!!!!!!!!!!!!!!!elf!!!!!!!!!!!!!!!!
                //            float timeDistance = Math.Abs((boundaryIndices[numPathlines] * StepSize) - preferredBoundaryTime);
                //            avgTimeDistance += timeDistance;
                //            if (timeDistance < maxTimeDistance || run >= 100) // We reached the spot! Take this line!
                //            {
                //                chosenPathlines[idx] = pathlines[numPathlines];
                //                if (output)
                //                    Console.WriteLine("Line {0} was chosen because it is perfect!", idx);
                //            }
                //            else
                //            {
                //                // Add new seed to seed list.
                //                float scale = boundaryIndices[numPathlines] / preferredBoundaryTime;
                //                float newOffset = offsetSeeds[numPathlines] + ((scale > 1) ? radiusGrowth : -radiusGrowth);
                //                newOffset = Math.Max(minRadius, newOffset);
                //                newOffset = Math.Min(maxRadius, newOffset);


                //                // We are stuck. Take best value we reached so far and go on.
                //                if (newOffset == offsetSeeds[numPathlines] && bestPathlines[idx] != null)
                //                {
                //                    chosenPathlines[idx] = bestPathlines[idx];
                //                    continue;
                //                }
                //                if (timeDistance < bestTimeDistance[idx])
                //                {
                //                    bestPathlines[idx] = pathlines[numPathlines];
                //                    bestTimeDistance[idx] = timeDistance;
                //                }

                //                offsetSeeds[numPathlines] = newOffset;

                //                // Recompute position on circle.
                //                float x = (float)(Math.Sin(angleDiff * idx + Math.PI / 2));
                //                float y = (float)(Math.Cos(angleDiff * idx + Math.PI / 2));

                //                // Take the selection as center.
                //                seeds.Points[numNewSeeds] = new Point() { Position = new Vector3(_selection.X + x * offsetSeeds[numNewSeeds], _selection.Y + y * offsetSeeds[numNewSeeds], SliceTimeMain) };

                //                // Count up number of new seeds.
                //                numNewSeeds++;
                //            }

                //            // We do not count up this value if there is a chosen pathline at this index already.
                //            numPathlines++;
                //        }

                //        // We maybe need less seeds now?
                //        if (numNewSeeds < seeds.Length)
                //            Array.Resize(ref seeds.Points, numNewSeeds);

                //        Console.WriteLine("Average time distance: {0}", avgTimeDistance / numPathlines);
                //    }
            }
            // ~~~~~~~~~~~~ Get Boundary for Rendering~~~~~~~~~~~~~~~~~~~~~~~~ \\

            // The two needes functions.
            _coreDistanceGraph = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, chosenPathlines, StepSize, _everyNthTimestep, true);
            _coreAngleGraph = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, chosenPathlines, StepSize, _everyNthTimestep, false);

            // Find the boundary based on angle and distance.
            _boundaryBallFunction = new LineBall(_linePlane, new LineSet(new Line[] { FieldAnalysis.FindBoundaryFromDistanceAngleDonut(_coreDistanceGraph, _coreAngleGraph, out boundaryIndices) }));

            // Find the boundary in space-time.
            int time = SliceTimeMain;
            _boundariesSpacetime[time] = new Line() { Positions = new Vector3[LineX + 1] };
            for (int l = 0; l < LineX; ++l)
            {
                Vector3 pos = chosenPathlines[l][boundaryIndices[l]];
                _allBoundaryPoints.Add(new Point(pos) { Color = Vector3.UnitY * pos.Z / RedSea.Singleton.NumSubstepsTotal * _everyNthTimestep });
                _allBoundaryPoints.Add(boundaryOW.Points[l]);
                _boundariesSpacetime[time][l] = pos;
            }
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
            Console.WriteLine("Average time: {0}", sumTime / chosenPathlines.Length);

            _boundaryCloud = new PointCloud(_linePlane, new PointSet<Point>(_allBoundaryPoints.ToArray()));

            LineSet set = new LineSet(_coreAngleGraph);
            GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + "Angle.csv", set);
            GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".angle", set);

            set = new LineSet(_coreDistanceGraph);
            GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + "Distance.csv", set);
            GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".distance", set);
        }
    }

    class ConcentricTubeMapper : CoreDistanceMapper
    {
        protected override int _numSeeds
        {
            get
            {
                return (int)(LineX * Math.PI);
            }
        }
        protected Graph2D[] _distanceAngleGraph;
        protected Graph2D[] _errorGraph;
        protected bool _rebuilt = false;
        protected Graph2D[] _distanceDistance;

        protected char _methode = 't';
        protected bool _started = false;

        protected List<Line> _tube;


        public ConcentricTubeMapper(int everyNthField, Plane plane) : base(everyNthField, plane)
        {
            Mapping = Map;
            Plane = new Plane(Plane, 12);
        }

        //protected Line IntegrateCircle(float angle, float radius, out Graph2D graph, float time = 0)
        //{
        //}

        protected LineSet IntegrateCircles(float[] radii, float[] angles, out Graph2D[] graph, float time = 0)
        {
            // ~~~~~~~~~~~~~~~~~~ Initialize seed points. ~~~~~~~~~~~~~~~~~~~~ \\
            PointSet<Point> circle = new PointSet<Point>(new Point[radii.Length * angles.Length]);
            for (int a = 0; a < angles.Length; ++a)
            {
                float x = (float)(Math.Sin(angles[a] + Math.PI / 2));
                float y = (float)(Math.Cos(angles[a] + Math.PI / 2));

                for (int r = 0; r < radii.Length; ++r)
                {
                    // Take the selection as center.
                    circle[a*radii.Length + r] = new Point() { Position = new Vector3(_selection.X + x * radii[r], _selection.Y + y * radii[r], time) };
                }
            }

            // ~~~~~~~~~~~~ Integrate Pathlines and Adapt ~~~~~~~~~~~~~~~~~~~~~~~~ \\
            // Setup integrator.
            Integrator pathlineIntegrator = Integrator.CreateIntegrator(null, IntegrationType, _cores[_selectedCore], _repulsion);
            pathlineIntegrator.StepSize = StepSize;
            LineSet pathlines;

            // Count out the runs for debugging.
            int run = 0;

            // ~~~~~~~~~~~~ Integrate Pathlines  ~~~~~~~~~~~~~~~~~~~~~~~~ \\
            #region IntegratePathlines
            // Do we need to load a field first?
            if (_velocity.TimeOrigin > SliceTimeMain || _velocity.TimeOrigin + _velocity.Size.T < SliceTimeMain)
                LoadField(SliceTimeMain, MemberMain);

            // Integrate first few steps.
            pathlineIntegrator.Field = _velocity;
            pathlines = pathlineIntegrator.Integrate(circle, false)[0];

            // Append integrated lines of next loaded vectorfield time slices.
            float timeLength = STEPS_IN_MEMORY * 3 - 2/*RedSea.Singleton.NumSubstepsTotal / _everyNthTimestep / 4*/ + SliceTimeMain;
            while (_currentEndStep + 1 < timeLength)
            {
                // Don't load more steps than we need to!
                int numSteps = (int)Math.Min(timeLength - _currentEndStep, STEPS_IN_MEMORY);
                pathlineIntegrator.Field = null;
                LoadField(_currentEndStep, MemberMain, numSteps);

                // Integrate further.
                pathlineIntegrator.Field = _velocity;
                pathlineIntegrator.IntegrateFurther(pathlines);
            }
            #endregion IntegratePathlines

            // ~~~~~~~~~~~~ Get Boundary ~~~~~~~~~~~~~~~~~~~~~~~~ \\
            #region GetBoundary
            // The two needes functions.
            //Line[] distances = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, pathlines, (StepSize * _everyNthTimestep) / 24.0f, _everyNthTimestep, true);
            //Line[] angles = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, pathlines, (StepSize * _everyNthTimestep) / 24.0f, _everyNthTimestep, false);
            graph = FieldAnalysis.GetDistanceToAngle(_cores[_selectedCore], _selection, pathlines);
            //graph[0].CutGraph((float)(Math.PI * 2));
            //Array.Resize(ref pathlines[0].Positions, graph[0].Length);
            FieldAnalysis.WriteXToLinesetAttribute(pathlines, graph);

            #endregion GetBoundary
            //LineSet[] subsets = new LineSet[angles.Length];
            //for(int s = 0; s < subsets.Length; ++ s)
            //{
            //    subsets[s] = new LineSet(pathlines, s * radii.Length, radii.Length);
            //}
            //return subsets;
            return pathlines;



//            LineSet set = new LineSet(_coreAngleGraph);
//GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + "Angle.csv", set);
//            GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".angle", set);

//            set = new LineSet(_coreDistanceGraph);
//GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + "Distance.csv", set);
//            GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".distance", set);
        }

        /// <summary>
        /// This function will be called in the Map methode if something relevant changes.
        /// </summary>
        protected override void FindBoundary()
        {
            /*
            if(not jet loaded)
	            if(file does not exist)
	            {
	                Integrate Lines
	                Compute ErrorFkt
	                Find Boundary
	            }
	            else
	                loadFile

            */
            // Initialize the tube.
            if (!_started && LineX > 0)
            {
                _started = true;
                _tube = new List<Line>(RedSea.Singleton.NumSubstepsTotal);

                string ending = "Bound_" + _numSeeds + '_' + AlphaStable + '_' + _lengthRadius + '_' + StepSize + '_' + _methode + "_*.ring";

                string[] files = System.IO.Directory.GetFiles(RedSea.Singleton.RingFileName, ending);
                foreach (string path in files)
                {
                    int startTimeStep = path.LastIndexOf('_') + 1;
                    string nr = "";
                    while (char.IsDigit(path[startTimeStep]))
                    {
                        nr += path[startTimeStep++];
                    }
                    int timestep = int.Parse(nr);

                    LineSet line;
                    GeometryWriter.ReadFromFile(path, out line);

                    Debug.Assert(line.Length == 1);
                    Debug.Assert(line[0].Length == _numSeeds + 1);

                    // We map the height differently for all _everyNthStep values, since 2 slices are always 1 apart. 
                    if(line[0][0].Z != timestep)
                    {
                        for (int p = 0; p < line[0].Length; ++p)
                        {
                            line[0].Positions[p].Z = timestep;
                        }
                        // Now, write it out to save the effort next time.
                        GeometryWriter.WriteToFile(path, line);
                    }

                    _tube.Add(line[0]);
                }

                TubeToRenderables();
            }

            // For currently set time step: Loaded something yet?
            int timeStep = SliceTimeMain * _everyNthTimestep;
            foreach (Line l in _tube)
            {
                if (l.Length > 0 && l[0].Z == timeStep)
                {
                    Console.WriteLine("Already loaded this file :3");
                    return;
                }
            }

            float angleDiff = (float)((Math.PI * 2) / _numSeeds);
            float[] offsets = new float[LineX];
            float[] angles = new float[_numSeeds];
            for (int seed = 0; seed < _numSeeds; ++seed)
            {
                float angle = seed * angleDiff;
                angles[seed] = angle;
            }
            for (int o = 0; o < LineX; ++o)
            {
                offsets[o] = AlphaStable + o * _lengthRadius / (LineX - 1);

            }

            _pathlinesTime = IntegrateCircles(offsets, angles, out _distanceAngleGraph, SliceTimeMain);
            _rebuilt = true;

            _distanceDistance = FieldAnalysis.GraphDifferenceForward(_distanceAngleGraph);


            // Boundary adding.
            Line boundary = Boundary(timeStep);
            _tube.Add(boundary);

            TubeToRenderables();

        }

        protected void TubeToRenderables()
        {
            if (_tube.Count < 1)
                return;
            _tube = _tube.OrderBy(o => o[0].Z).ToList();

            //_boundaryBallSpacetime = new LineBall(new Plane(_graphPlane, Vector3.UnitZ * 0.01f), _boundariesSpacetime, LineBall.RenderEffect.HEIGHT, ColorMapping.GetComplementary(Colormap), Flat);
            LineSet rings = new LineSet(_tube.ToArray());
            rings.Thickness *= 0.5f;
            _boundaryBallSpacetime = new LineBall(_linePlane, rings, LineBall.RenderEffect.HEIGHT, ColorMapping.GetComplementary(Colormap), false);

            //if (_tube.Count > 1)
            //    _specialObject = new Mesh(_linePlane, new TileSurface(rings), Mesh.RenderEffect.DEFAULT, Colormap);
        }
        protected Line Boundary(int timestep)
        {

            float cutValue = 2000.0f;

            // Compute error.
            if (LineX == 0)
                return null;
            _errorGraph = new Graph2D[_numSeeds];
            _allBoundaryPoints = new List<Point>();
            for (int seed = 0; seed < _numSeeds; ++seed)
            {
                // Smaller field: the difference diminishes it by one line.
                float[] fx = new float[LineX-1];
                float[] x = new float[LineX-1];
                for (int e = 0; e < fx.Length; ++e)
                {
                    // Inbetween graphs, there is one useless one.
                    int index = seed * LineX + e;
                    if (_distanceDistance[index].Length <= 1)
                    {
                        fx[e] = cutValue * 1.5f;
                        x[e] = _distanceDistance[index].Offset;
                    }
                    else
                    {
                        fx[e] = _distanceDistance[index].RelativeSumOver(IntegrationTime);// / _distanceDistance[index].Length;
                        if (float.IsNaN(fx[e]) || float.IsInfinity(fx[e]) || fx[e] == float.MaxValue)
                            fx[e] = 0;
                        x[e] = _distanceDistance[index].Offset;
                        //if (fx[e] > cutValue)
                        //{
                        //    Array.Resize(ref fx, e);
                        //    Array.Resize(ref x, e);
                        //    break;
                        //}
                    }
                }

                _errorGraph[seed] = new Graph2D(x, fx);
                _errorGraph[seed].SmoothLaplacian(0.8f);
                _errorGraph[seed].SmoothLaplacian(0.8f);
            }

            // Do whatever this methode does.
            LineSet line = FieldAnalysis.FindBoundaryInErrors3(_errorGraph, new Vector3(_selection, SliceTimeMain));
            
            string ending = "Bound_" + _numSeeds + '_' + AlphaStable + '_' + _lengthRadius + '_' + StepSize + '_' + _methode + '_' + timestep + ".ring";

            Debug.Assert(line.Length == 1);
            // Directly rescale in Z.
            if (line[0][0].Z != timestep)
            {
                for (int p = 0; p < line[0].Length; ++p)
                {
                    line[0].Positions[p].Z = timestep;
                }
            }
            GeometryWriter.WriteToFile(RedSea.Singleton.RingFileName + ending, line);
            foreach(Line l in line.Lines)
            _tube.Add(l);
            // ~~~~~~~~~~~~ Get Boundary for Rendering ~~~~~~~~~~~~ \\

            // Show the current graph.
            _graph = new LineBall(_graphPlane, FieldAnalysis.WriteGraphToSun(_errorGraph, new Vector3(_selection.X, _selection.Y, timestep)), LineBall.RenderEffect.HEIGHT, Colormap, false);

            _rebuilt = false;

            if (line.Length != 1)
                Console.WriteLine("Not exactly one boundary!");

            if (line.Length < 1)
                return null;
            return line[0];
        }

        protected override void UpdateBoundary()
        {
            //if(_lastSetting != null && (_rebuilt || FlatChanged || GraphChanged) && (Flat && !Graph))
            //{
            //    Graph2D[] dist = FieldAnalysis.GraphDifferenceForward(_distanceAngleGraph);
            //    Plane zPlane = new Plane(_graphPlane, Vector3.UnitZ * 2);
            //    _graph = new LineBall(_graphPlane, FieldAnalysis.WriteGraphToSun(_errorGraph, new Vector3(_selection.X, _selection.Y, 0)), LineBall.RenderEffect.HEIGHT, Colormap, false);
            //    //               _graph = new LineBall(zPlane, FieldAnalysis.WriteGraphsToCircles(dist, new Vector3(_selection.X, _selection.Y, SliceTimeMain)), LineBall.RenderEffect.HEIGHT, Colormap, false);
            //    _rebuilt = false;
            //}
            //if (_lastSetting != null && (IntegrationTimeChanged || _rebuilt || Graph && GraphChanged && Flat))
            //    BuildGraph();
                //_graph = new LineBall(_graphPlane, FieldAnalysis.WriteGraphToSun(_errorGraph, new Vector3(_selection.X, _selection.Y, 0)), LineBall.RenderEffect.HEIGHT, Colormap, false);
            // new LineBall(_graphPlane, FieldAnalysis.WriteGraphsToCircles(_distanceAngleGraph, new Vector3(_selection.X, _selection.Y, SliceTimeMain)), LineBall.RenderEffect.HEIGHT, Colormap, false);
            //LineSet cutLines;
            ////if (SliceTimeReference > SliceTimeMain)
            ////{
            ////    // _graph = cut version of _coreAngleGraph.
            ////    int length = SliceTimeReference - SliceTimeMain;
            ////    length = (int)((float)length / StepSize + 0.5f);
            ////    cutLines = FieldAnalysis.CutLength(new LineSet(_coreDistanceGraph), length);
            ////}
            ////else
            ////    cutLines = new LineSet(_coreDistanceGraph);

            //cutLines = new LineSet(FieldAnalysis.);

            //_graph = new LineBall[] { new LineBall(_graphPlane, cutLines, LineBall.RenderEffect.HEIGHT, Colormap) };
        }

        public override string GetName(Setting.Element element)
        {
            switch(element)
            {
                case Setting.Element.IntegrationTime:
                    return "Integrate until Angle";
                default:
                    return base.GetName(element);
            }          
        }
    }

    class ConcentricDistanceMapper : CoreDistanceMapper
    {
        protected override int _numSeeds
        {
            get
            {
                return (int)(LineX * Math.PI);
            }
        }

        protected Graph2D[] _distanceAngleGraph;
        protected Graph2D[] _errorGraph;
        protected bool _rebuilt = false;
        protected Graph2D[] _distanceDistance;
        public ConcentricDistanceMapper(int everyNthField, Plane plane) : base(everyNthField, plane)
        {
            Mapping = Map;
        }

        //protected Line IntegrateCircle(float angle, float radius, out Graph2D graph, float time = 0)
        //{
        //}

        protected LineSet IntegrateCircles(float[] radii, float[] angles, out Graph2D[] graph, float time = 0)
        {
            // ~~~~~~~~~~~~~~~~~~ Initialize seed points. ~~~~~~~~~~~~~~~~~~~~ \\
            PointSet<Point> circle = new PointSet<Point>(new Point[radii.Length * angles.Length]);
            //float angleDiff = 2 * (float)(Math.PI / LineX);
            for (int a = 0; a < angles.Length; ++a)
            {
                float x = (float)(Math.Sin(angles[a] + Math.PI / 2));
                float y = (float)(Math.Cos(angles[a] + Math.PI / 2));

                for (int r = 0; r < radii.Length; ++r)
                {
                    // Take the selection as center.
                    circle[a * radii.Length + r] = new Point() { Position = new Vector3(_selection.X + x * radii[r], _selection.Y + y * radii[r], time) };
                }
            }

            // ~~~~~~~~~~~~ Integrate Pathlines and Adapt ~~~~~~~~~~~~~~~~~~~~~~~~ \\
            // Setup integrator.
            Integrator pathlineIntegrator = Integrator.CreateIntegrator(null, IntegrationType, _cores[_selectedCore], _repulsion);
            pathlineIntegrator.StepSize = StepSize;
            LineSet pathlines;

            // Count out the runs for debugging.
            int run = 0;

            // ~~~~~~~~~~~~ Integrate Pathlines  ~~~~~~~~~~~~~~~~~~~~~~~~ \\
            #region IntegratePathlines
            // Do we need to load a field first?
            if (_velocity.TimeOrigin > SliceTimeMain || _velocity.TimeOrigin + _velocity.Size.T < SliceTimeMain)
                LoadField(SliceTimeMain, MemberMain);

            // Integrate first few steps.
            pathlineIntegrator.Field = _velocity;
            pathlines = pathlineIntegrator.Integrate(circle, false)[0];

            // Append integrated lines of next loaded vectorfield time slices.
            float timeLength = STEPS_IN_MEMORY * 2 - 1/*RedSea.Singleton.NumSubstepsTotal / _everyNthTimestep / 4*/ + SliceTimeMain;
            while (_currentEndStep + 1 < timeLength)
            {
                // Don't load more steps than we need to!
                int numSteps = (int)Math.Min(timeLength - _currentEndStep, STEPS_IN_MEMORY);
                pathlineIntegrator.Field = null;
                LoadField(_currentEndStep, MemberMain, numSteps);

                // Integrate further.
                pathlineIntegrator.Field = _velocity;
                pathlineIntegrator.IntegrateFurther(pathlines);
            }
            #endregion IntegratePathlines

            // ~~~~~~~~~~~~ Get Boundary ~~~~~~~~~~~~~~~~~~~~~~~~ \\
            #region GetBoundary
            // The two needes functions.
            //Line[] distances = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, pathlines, (StepSize * _everyNthTimestep) / 24.0f, _everyNthTimestep, true);
            //Line[] angles = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, pathlines, (StepSize * _everyNthTimestep) / 24.0f, _everyNthTimestep, false);
            graph = FieldAnalysis.GetDistanceToAngle(_cores[_selectedCore], _selection, pathlines);
            //graph[0].CutGraph((float)(Math.PI * 2));
            //Array.Resize(ref pathlines[0].Positions, graph[0].Length);
            FieldAnalysis.WriteXToLinesetAttribute(pathlines, graph);

            #endregion GetBoundary
            //LineSet[] subsets = new LineSet[angles.Length];
            //for(int s = 0; s < subsets.Length; ++ s)
            //{
            //    subsets[s] = new LineSet(pathlines, s * radii.Length, radii.Length);
            //}
            //return subsets;
            return pathlines;



            //            LineSet set = new LineSet(_coreAngleGraph);
            //GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + "Angle.csv", set);
            //            GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".angle", set);

            //            set = new LineSet(_coreDistanceGraph);
            //GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + "Distance.csv", set);
            //            GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".distance", set);
        }

        protected override void FindBoundary()
        {
            float angleDiff = (float)((Math.PI * 2) / _numSeeds);
            float[] offsets = new float[LineX];
            float[] angles = new float[_numSeeds];
            for (int seed = 0; seed < _numSeeds; ++seed)
            {
                float angle = seed * angleDiff;
                angles[seed] = angle;
            }
            for (int o = 0; o < LineX; ++o)
            {
                offsets[o] = AlphaStable + o * _lengthRadius / (LineX - 1);

            }

            _pathlinesTime = IntegrateCircles(offsets, angles, out _distanceAngleGraph, SliceTimeMain);
            _rebuilt = true;

            _distanceDistance = FieldAnalysis.GraphDifferenceForward(_distanceAngleGraph);
            BuildGraph();
        }
        protected void BuildGraph()
        {
            float cutValue = 2000.0f;

            // Compute error.
            if (LineX == 0)
                return;
            _errorGraph = new Graph2D[_numSeeds];
            _allBoundaryPoints = new List<Point>();
            for (int seed = 0; seed < _numSeeds; ++seed)
            {
                // Smaller field: the difference diminishes it by one line.
                float[] fx = new float[LineX - 1];
                float[] x = new float[LineX - 1];
                for (int e = 0; e < fx.Length; ++e)
                {
                    // Inbetween graphs, there is one useless one.
                    int index = seed * LineX + e;
                    if (_distanceDistance[index].Length <= 1)
                    {
                        fx[e] = float.MaxValue;
                        x[e] = _distanceDistance[index].Offset;
                    }
                    else
                    {
                        fx[e] = _distanceDistance[index].RelativeSumOver(IntegrationTime);// / _distanceDistance[index].Length;
                        x[e] = _distanceDistance[index].Offset;
                        if (fx[e] > cutValue)
                        {
                            Array.Resize(ref fx, e);
                            Array.Resize(ref x, e);
                            break;
                        }
                    }
                }

                _errorGraph[seed] = new Graph2D(x, fx);
                _errorGraph[seed].SmoothLaplacian(0.8f);
                _errorGraph[seed].SmoothLaplacian(0.8f);

                //var maxs = _errorGraph[seed].Maxima();
                //float angle = (float)((float)seed * Math.PI * 2 / _errorGraph.Length);
                //foreach (int p in maxs)
                //{
                //    float px = _errorGraph[seed].X[p];
                //    _allBoundaryPoints.Add(new Point(new Vector3(_selection.X + (float)(Math.Sin(angle + Math.PI / 2)) * px, _selection.Y + (float)(Math.Cos(angle + Math.PI / 2)) * px, cutValue)) { Color = Vector3.UnitX });
                //}

                //int[] errorBound = FieldAnalysis.FindBoundaryInError(_errorGraph[seed]);
                //foreach (int bound in errorBound)
                //    _allBoundaryPoints.Add(new Point(_pathlinesTime[seed * LineX + bound][0]));
            }
            //_boundariesSpacetime = FieldAnalysis.FindBoundaryInErrors(_errorGraph, new Vector3(_selection, SliceTimeMain));
            //_boundaryBallSpacetime = new LineBall(_linePlane, _boundariesSpacetime, LineBall.RenderEffect.HEIGHT, ColorMapping.GetComplementary(Colormap));

            //if (errorBound >= 0)
            //    _allBoundaryPoints.Add(new Point(_pathlinesTime[seed * LineX + errorBound][0]));
            GeometryWriter.WriteGraphCSV(RedSea.Singleton.DonutFileName + "Error.csv", _errorGraph);
            Console.WriteLine("Radii without boundary point: {0} of {1}", _numSeeds - _allBoundaryPoints.Count, _numSeeds);
            //   _graphPlane.ZAxis = Plane.ZAxis * WindowWidth;
            _boundaryCloud = new PointCloud(_graphPlane, new PointSet<Point>(_allBoundaryPoints.ToArray()));
            //LineSet lineCpy = new LineSet(_pathlinesTime);
            //lineCpy.CutAllHeight(_repulsion);
            //_pathlines = new LineBall(_linePlane, lineCpy, LineBall.RenderEffect.HEIGHT, Colormap, false);
            //int errorBound = FieldAnalysis.FindBoundaryInError(_errorGraph[0]);
            //_pathlinesTime.Cut(errorBound);
            // ~~~~~~~~~~~~ Get Boundary for Rendering ~~~~~~~~~~~~ \\

            // _pathlines = new LineBall(_linePlane, _pathlinesTime, LineBall.RenderEffect.HEIGHT, ColorMapping.GetComplementary(Colormap), Flat);

            // _graph = new LineBall(_graphPlane, FieldAnalysis.WriteGraphsToCircles(_distanceAngleGraph, new Vector3(_selection.X, _selection.Y, SliceTimeMain)), LineBall.RenderEffect.HEIGHT, Colormap, false);
            _graph = new LineBall(_graphPlane, FieldAnalysis.WriteGraphToSun(_errorGraph, new Vector3(_selection.X, _selection.Y, 0)), LineBall.RenderEffect.HEIGHT, Colormap, Flat);

            _rebuilt = false;
        }

        protected override void UpdateBoundary()
        {
            if (_lastSetting != null && (_rebuilt || FlatChanged || GraphChanged) && (Flat && !Graph))
            {
                Graph2D[] dist = FieldAnalysis.GraphDifferenceForward(_distanceAngleGraph);
                Plane zPlane = new Plane(_graphPlane, Vector3.UnitZ * 2);
                _graph = new LineBall(_graphPlane, FieldAnalysis.WriteGraphToSun(_errorGraph, new Vector3(_selection.X, _selection.Y, 0)), LineBall.RenderEffect.HEIGHT, Colormap, false);
                //               _graph = new LineBall(zPlane, FieldAnalysis.WriteGraphsToCircles(dist, new Vector3(_selection.X, _selection.Y, SliceTimeMain)), LineBall.RenderEffect.HEIGHT, Colormap, false);
                _rebuilt = false;
            }
            if (_lastSetting != null && (IntegrationTimeChanged || _rebuilt || Graph && GraphChanged && Flat))
                BuildGraph();
            //_graph = new LineBall(_graphPlane, FieldAnalysis.WriteGraphToSun(_errorGraph, new Vector3(_selection.X, _selection.Y, 0)), LineBall.RenderEffect.HEIGHT, Colormap, false);
            // new LineBall(_graphPlane, FieldAnalysis.WriteGraphsToCircles(_distanceAngleGraph, new Vector3(_selection.X, _selection.Y, SliceTimeMain)), LineBall.RenderEffect.HEIGHT, Colormap, false);
            //LineSet cutLines;
            ////if (SliceTimeReference > SliceTimeMain)
            ////{
            ////    // _graph = cut version of _coreAngleGraph.
            ////    int length = SliceTimeReference - SliceTimeMain;
            ////    length = (int)((float)length / StepSize + 0.5f);
            ////    cutLines = FieldAnalysis.CutLength(new LineSet(_coreDistanceGraph), length);
            ////}
            ////else
            ////    cutLines = new LineSet(_coreDistanceGraph);

            //cutLines = new LineSet(FieldAnalysis.);

            //_graph = new LineBall[] { new LineBall(_graphPlane, cutLines, LineBall.RenderEffect.HEIGHT, Colormap) };
        }

        public override string GetName(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.IntegrationTime:
                    return "Integrate until Angle";
                default:
                    return base.GetName(element);
            }
        }
    }

    class ConcentricDifference : ConcentricDistanceMapper
    {
        public ConcentricDifference(int everyNthField, Plane plane) : base(everyNthField, plane)
        {
            Mapping = Map;
        }

        protected override void FindBoundary()
        {
            float[] offsets = new float[LineX];
            float[] angles = new float[LineX];
            for (int o = 0; o < offsets.Length; ++o)
            {
                offsets[o] = AlphaStable + o * IntegrationTime;
                angles[o] = 0;
            }

            _pathlinesTime = IntegrateCircles(offsets, angles, out _distanceAngleGraph, SliceTimeMain);

            // ~~~~~~~~~~~~ Get Boundary for Rendering~~~~~~~~~~~~~~~~~~~~~~~~ \\
            _pathlines = new LineBall(_linePlane, _pathlinesTime, LineBall.RenderEffect.HEIGHT, ColorMapping.GetComplementary(Colormap), Flat);

            _graph = new LineBall(_graphPlane, FieldAnalysis.WriteGraphsToCircles(_distanceAngleGraph, new Vector3(_selection.X, _selection.Y, SliceTimeMain)), LineBall.RenderEffect.HEIGHT, Colormap, false);

        }

        protected override void UpdateBoundary()
        {

            //LineSet cutLines;
            ////if (SliceTimeReference > SliceTimeMain)
            ////{
            ////    // _graph = cut version of _coreAngleGraph.
            ////    int length = SliceTimeReference - SliceTimeMain;
            ////    length = (int)((float)length / StepSize + 0.5f);
            ////    cutLines = FieldAnalysis.CutLength(new LineSet(_coreDistanceGraph), length);
            ////}
            ////else
            ////    cutLines = new LineSet(_coreDistanceGraph);

            //cutLines = new LineSet(FieldAnalysis.);

            //_graph = new LineBall[] { new LineBall(_graphPlane, cutLines, LineBall.RenderEffect.HEIGHT, Colormap) };
        }
    }

    class DonutAnalyzer : DataMapper
    {
        // Load this.
        protected LineSet _loadedAngle;
        protected LineSet _loadedDistance;
        // Unroll this.
        protected LineSet _blockData;

        // Compute this.
        protected Line _boundaryLoaded, _boundaryBlock;

        // Render these.
        protected LineBall _loadedBall, _blockBall, _boundaryLoadedBall, _boundaryBlockBall;

        // Offset this.
        protected Plane _fightPlane;

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

        protected List<Renderable> Map()
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

    class MapperFTLE : CoreDistanceMapper
    {
        protected override int _numSeeds
        {
            get
            {
                return LineX;  //(int)(Math.PI * LineX * 2.0f);
            }
        }
        protected Graph2D[] _ftle;
        protected bool _rebuilt = false;
        public MapperFTLE(int everyNthField, Plane plane) : base(everyNthField, plane)
        {
            Mapping = Map;
            _linePlane = new Plane(_linePlane.Origin, _linePlane.XAxis, _linePlane.YAxis, _linePlane.ZAxis * 10, 1.0f);
        }

        //protected Line IntegrateCircle(float angle, float radius, out Graph2D graph, float time = 0)
        //{
        //}

        protected LineSet IntegrateCircles(float[] radii, float[] angles, out Graph2D[] graph, float time = 0)
        {
            // ~~~~~~~~~~~~~~~~~~ Initialize seed points. ~~~~~~~~~~~~~~~~~~~~ \\
            PointSet<Point> circle = new PointSet<Point>(new Point[radii.Length * angles.Length * 4]);
            //float angleDiff = 2 * (float)(Math.PI / LineX);
            for (int a = 0; a < angles.Length; ++a)
            {
                float x = (float)(Math.Sin(angles[a] + Math.PI / 2));
                float y = (float)(Math.Cos(angles[a] + Math.PI / 2));

                for (int r = 0; r < radii.Length; ++r)
                {
                    // Take the selection as center.
                    circle[(a * radii.Length + r)*4 + 0] = new Point() { Position = new Vector3(_selection.X + x * radii[r] + _epsOffset, _selection.Y + y * radii[r], time) };
                    circle[(a * radii.Length + r)*4 + 1] = new Point() { Position = new Vector3(_selection.X + x * radii[r] - _epsOffset, _selection.Y + y * radii[r], time) };
                    circle[(a * radii.Length + r)*4 + 2] = new Point() { Position = new Vector3(_selection.X + x * radii[r], _selection.Y + y * radii[r] + _epsOffset, time) };
                    circle[(a * radii.Length + r)*4 + 3] = new Point() { Position = new Vector3(_selection.X + x * radii[r], _selection.Y + y * radii[r] - _epsOffset, time) };
                }
            }

            // ~~~~~~~~~~~~ Integrate Pathlines and Adapt ~~~~~~~~~~~~~~~~~~~~~~~~ \\
            // Setup integrator.
            Integrator pathlineIntegrator = Integrator.CreateIntegrator(null, IntegrationType, _cores[_selectedCore], _repulsion);
            pathlineIntegrator.StepSize = StepSize;
            LineSet pathlines;

            // Count out the runs for debugging.

            // ~~~~~~~~~~~~ Integrate Pathlines  ~~~~~~~~~~~~~~~~~~~~~~~~ \\
            #region IntegratePathlines
            // Do we need to load a field first?
            if (_velocity.TimeOrigin > SliceTimeMain || _velocity.TimeOrigin + _velocity.Size.T < SliceTimeMain)
                LoadField(SliceTimeMain, MemberMain);

            // Integrate first few steps.
            pathlineIntegrator.Field = _velocity;
            Console.WriteLine("Starting integration of {0} pathlines", circle.Length);
            pathlines = pathlineIntegrator.Integrate(circle, false, 10)[0];

            // Append integrated lines of next loaded vectorfield time slices.
            //float timeEnd = (float)(12*15) / _everyNthTimestep + SliceTimeMain;
            //while (_currentEndStep + 1 < timeEnd)
            //{
            //    // Don't load more steps than we need to!
            //    int numSteps = (int)Math.Min(timeEnd - _currentEndStep, STEPS_IN_MEMORY);
            //    pathlineIntegrator.Field = null;
            //    LoadField(_currentEndStep, MemberMain, numSteps);

            //    // Integrate further.
            //    pathlineIntegrator.Field = _velocity;
            //    pathlineIntegrator.IntegrateFurther(pathlines);
            //}
            #endregion IntegratePathlines
            Console.WriteLine("Integrated all {0} pathlines", pathlines.Length);
            graph = FieldAnalysis.ComputeFTLE2D(pathlines, new Vector3(_selection, time), angles, radii, time, IntegrationTime);

            return pathlines;



            //            LineSet set = new LineSet(_coreAngleGraph);
            //GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + "Angle.csv", set);
            //            GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".angle", set);

            //            set = new LineSet(_coreDistanceGraph);
            //GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + "Distance.csv", set);
            //            GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".distance", set);
        }

        protected override void FindBoundary()
        {
            float angleDiff = (float)((Math.PI * 2) / _numSeeds);
            float[] offsets = new float[LineX];
            float[] angles = new float[_numSeeds];
            for (int seed = 0; seed < _numSeeds; ++seed)
            {
                float angle = seed * angleDiff;
                angles[seed] = angle;
            }
            for (int o = 0; o < LineX; ++o)
            {
                offsets[o] = AlphaStable + o * _lengthRadius / (LineX - 1);

            }

            _pathlinesTime = IntegrateCircles(offsets, angles, out _ftle, SliceTimeMain);
            _rebuilt = true;

            //_distanceDistance = FieldAnalysis.GraphDifferenceForward(_distanceAngleGraph);
            BuildGraph();
        }
        protected void BuildGraph()
        {
            // Compute ftle.
            if (LineX == 0)
                return;
            //for (int seed = 0; seed < _numSeeds; ++seed)
            //{
            //    // Smaller field: the difference diminishes it by one line.
            //    float[] fx = new float[LineX - 1];
            //    float[] x = new float[LineX - 1];
            //    for (int e = 0; e < fx.Length; ++e)
            //    {
            //        // Inbetween graphs, there is one useless one.
            //        int index = seed * LineX + e;
            //        if (_distanceDistance[index].Length <= 1)
            //        {
            //            fx[e] = float.MaxValue;
            //            x[e] = _distanceDistance[index].Offset;
            //        }
            //        else
            //        {
            //            fx[e] = _distanceDistance[index].RelativeSumOver(IntegrationTime);// / _distanceDistance[index].Length;
            //            x[e] = _distanceDistance[index].Offset;
            //        }
            //    }

            //    _errorGraph[seed] = new Graph2D(x, fx);
            //    int errorBound = FieldAnalysis.FindBoundaryInError(_errorGraph[seed]);
            //    if (errorBound >= 0)
            //        _allBoundaryPoints.Add(new Point(_pathlinesTime[seed * LineX + errorBound][0]));
            //}
            ////   GeometryWriter.WriteGraphCSV(RedSea.Singleton.DonutFileName + "Error.csv", _errorGraph);
            //Console.WriteLine("Radii without boundary point: {0} of {1}", _numSeeds - _allBoundaryPoints.Count, _numSeeds);

            //_boundaryCloud = new PointCloud(_graphPlane, new PointSet<Point>(_allBoundaryPoints.ToArray()));
            ////int errorBound = FieldAnalysis.FindBoundaryInError(_errorGraph[0]);
            ////_pathlinesTime.Cut(errorBound);
            //// ~~~~~~~~~~~~ Get Boundary for Rendering ~~~~~~~~~~~~ \\
            //_pathlinesTime.Thickness *= 0.1f;
            //_pathlines = new LineBall(_linePlane, _pathlinesTime, LineBall.RenderEffect.HEIGHT, ColorMapping.GetComplementary(Colormap), Flat);

            //// _graph = new LineBall(_graphPlane, FieldAnalysis.WriteGraphsToCircles(_distanceAngleGraph, new Vector3(_selection.X, _selection.Y, SliceTimeMain)), LineBall.RenderEffect.HEIGHT, Colormap, false);
            _graph = new LineBall(_graphPlane, FieldAnalysis.WriteGraphToSun(_ftle, new Vector3(_selection.X, _selection.Y, 0)), LineBall.RenderEffect.HEIGHT, Colormap, Flat);

            _rebuilt = false;
        }

        protected override void UpdateBoundary()
        {
            if (_lastSetting != null && (_rebuilt || FlatChanged || GraphChanged) && (Flat && !Graph))
            {
                //               _graph = new LineBall(zPlane, FieldAnalysis.WriteGraphsToCircles(dist, new Vector3(_selection.X, _selection.Y, SliceTimeMain)), LineBall.RenderEffect.HEIGHT, Colormap, false);
                _rebuilt = false;
            }
            if (_lastSetting != null && (FlatChanged || IntegrationTimeChanged || _rebuilt || Graph && GraphChanged && Flat))
                BuildGraph();
            //_graph = new LineBall(_graphPlane, FieldAnalysis.WriteGraphToSun(_errorGraph, new Vector3(_selection.X, _selection.Y, 0)), LineBall.RenderEffect.HEIGHT, Colormap, false);
            // new LineBall(_graphPlane, FieldAnalysis.WriteGraphsToCircles(_distanceAngleGraph, new Vector3(_selection.X, _selection.Y, SliceTimeMain)), LineBall.RenderEffect.HEIGHT, Colormap, false);
            //LineSet cutLines;
            ////if (SliceTimeReference > SliceTimeMain)
            ////{
            ////    // _graph = cut version of _coreAngleGraph.
            ////    int length = SliceTimeReference - SliceTimeMain;
            ////    length = (int)((float)length / StepSize + 0.5f);
            ////    cutLines = FieldAnalysis.CutLength(new LineSet(_coreDistanceGraph), length);
            ////}
            ////else
            ////    cutLines = new LineSet(_coreDistanceGraph);

            //cutLines = new LineSet(FieldAnalysis.);

            //_graph = new LineBall[] { new LineBall(_graphPlane, cutLines, LineBall.RenderEffect.HEIGHT, Colormap) };
        }

        public override string GetName(Setting.Element element)
        {
            if (element == Setting.Element.IntegrationTime)
                return "Integration Time";
            return base.GetName(element);
        }
    }

    class DistanceMapper : CoreDistanceMapper
    {
        protected override int _numSeeds
        {
            get
            {
                return (int)(LineX * Math.PI);
            }
        }
        protected Graph2D[] _errorGraph;
        protected Graph2D[][] _lineDistance;
        protected bool _rebuilt = false;
        public DistanceMapper(int everyNthField, Plane plane) : base(everyNthField, plane)
        {
            Mapping = Map;
        }

        //protected Line IntegrateCircle(float angle, float radius, out Graph2D graph, float time = 0)
        //{
        //}

        protected LineSet IntegrateCircles(float[] radii, float[] angles, out Graph2D[][] graph, float time = 0)
        {
            // ~~~~~~~~~~~~~~~~~~ Initialize seed points. ~~~~~~~~~~~~~~~~~~~~ \\
            PointSet<Point> circle = new PointSet<Point>(new Point[radii.Length * angles.Length]);
            //float angleDiff = 2 * (float)(Math.PI / LineX);
            for (int a = 0; a < angles.Length; ++a)
            {
                float x = (float)(Math.Sin(angles[a] + Math.PI / 2));
                float y = (float)(Math.Cos(angles[a] + Math.PI / 2));

                for (int r = 0; r < radii.Length; ++r)
                {
                    // Take the selection as center.
                    circle[a * radii.Length + r] = new Point() { Position = new Vector3(_selection.X + x * radii[r], _selection.Y + y * radii[r], time) };
                }
            }

            // ~~~~~~~~~~~~ Integrate Pathlines and Adapt ~~~~~~~~~~~~~~~~~~~~~~~~ \\
            // Setup integrator.
            Integrator pathlineIntegrator = Integrator.CreateIntegrator(null, IntegrationType, _cores[_selectedCore], _repulsion);
            pathlineIntegrator.StepSize = StepSize;
            LineSet pathlines;

            // Count out the runs for debugging.
            int run = 0;

            // ~~~~~~~~~~~~ Integrate Pathlines  ~~~~~~~~~~~~~~~~~~~~~~~~ \\
            #region IntegratePathlines
            // Do we need to load a field first?
            if (_velocity.TimeOrigin > SliceTimeMain || _velocity.TimeOrigin + _velocity.Size.T < SliceTimeMain)
                LoadField(SliceTimeMain, MemberMain);

            // Integrate first few steps.
            pathlineIntegrator.Field = _velocity;
            pathlines = pathlineIntegrator.Integrate(circle, false)[0];

            // Append integrated lines of next loaded vectorfield time slices.
            float timeLength = STEPS_IN_MEMORY * 2 - 1/*RedSea.Singleton.NumSubstepsTotal / _everyNthTimestep / 4*/ + SliceTimeMain;
            while (_currentEndStep + 1 < timeLength)
            {
                // Don't load more steps than we need to!
                int numSteps = (int)Math.Min(timeLength - _currentEndStep, STEPS_IN_MEMORY);
                pathlineIntegrator.Field = null;
                LoadField(_currentEndStep, MemberMain, numSteps);

                // Integrate further.
                pathlineIntegrator.Field = _velocity;
                pathlineIntegrator.IntegrateFurther(pathlines);
            }
            #endregion IntegratePathlines

            // ~~~~~~~~~~~~ Get Boundary ~~~~~~~~~~~~~~~~~~~~~~~~ \\
            #region GetBoundary
            // The two needes functions.
            //Line[] distances = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, pathlines, (StepSize * _everyNthTimestep) / 24.0f, _everyNthTimestep, true);
            //Line[] angles = FieldAnalysis.GetGraph(_cores[_selectedCore], _selection, pathlines, (StepSize * _everyNthTimestep) / 24.0f, _everyNthTimestep, false);
            graph = FieldAnalysis.GetErrorsToTime(pathlines, angles.Length, radii);
            
            //graph[0].CutGraph((float)(Math.PI * 2));
            //Array.Resize(ref pathlines[0].Positions, graph[0].Length);
        //    FieldAnalysis.WriteXToLinesetAttribute(pathlines, graph);

            #endregion GetBoundary
            //LineSet[] subsets = new LineSet[angles.Length];
            //for(int s = 0; s < subsets.Length; ++ s)
            //{
            //    subsets[s] = new LineSet(pathlines, s * radii.Length, radii.Length);
            //}
            //return subsets;
            return pathlines;



            //            LineSet set = new LineSet(_coreAngleGraph);
            //GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + "Angle.csv", set);
            //            GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".angle", set);

            //            set = new LineSet(_coreDistanceGraph);
            //GeometryWriter.WriteHeightCSV(RedSea.Singleton.DonutFileName + "Distance.csv", set);
            //            GeometryWriter.WriteToFile(RedSea.Singleton.DonutFileName + ".distance", set);
        }

        protected override void FindBoundary()
        {
            float angleDiff = (float)((Math.PI * 2) / _numSeeds);
            float[] offsets = new float[LineX];
            float[] angles = new float[_numSeeds];
            for (int seed = 0; seed < _numSeeds; ++seed)
            {
                float angle = seed * angleDiff;
                angles[seed] = angle;
            }
            for (int o = 0; o < LineX; ++o)
            {
                offsets[o] = AlphaStable + o * _lengthRadius / (LineX - 1);

            }

            _pathlinesTime = IntegrateCircles(offsets, angles, out _lineDistance, SliceTimeMain);
            _rebuilt = true;
            BuildGraph();
        }
        protected void BuildGraph()
        {
            float cutValue = 2000.0f;

            // Compute error.
            if (LineX == 0)
                return;
            _errorGraph = new Graph2D[_numSeeds];
            _allBoundaryPoints = new List<Point>();
            for (int seed = 0; seed < _numSeeds; ++seed)
            {
                // Smaller field: the difference diminishes it by one line.
                float[] fx = new float[LineX - 1];
                float[] x = new float[LineX - 1];
                for (int e = 0; e < fx.Length; ++e)
                {
                    if (_lineDistance[seed][e].Length <= 1)
                    {
                        fx[e] = float.MaxValue;
                        x[e] = _lineDistance[seed][e].Offset;
                    }
                    else
                    {
                        fx[e] = _lineDistance[seed][e].RelativeSumOver(IntegrationTime);// / _distanceDistance[index].Length;
                        x[e] = _lineDistance[seed][e].Offset;
                        if (fx[e] > cutValue)
                        {
                            Array.Resize(ref fx, e);
                            Array.Resize(ref x, e);
                            break;
                        }
                    }
                }

                _errorGraph[seed] = new Graph2D(x, fx);
                _errorGraph[seed].SmoothLaplacian(0.8f);
                _errorGraph[seed].SmoothLaplacian(0.8f);

                //var maxs = _errorGraph[seed].Maxima();
                //float angle = (float)((float)seed * Math.PI * 2 / _errorGraph.Length);
                //foreach (int p in maxs)
                //{
                //    float px = _errorGraph[seed].X[p];
                //    _allBoundaryPoints.Add(new Point(new Vector3(_selection.X + (float)(Math.Sin(angle + Math.PI / 2)) * px, _selection.Y + (float)(Math.Cos(angle + Math.PI / 2)) * px, cutValue)) { Color = Vector3.UnitX });
                //}

                //int[] errorBound = FieldAnalysis.FindBoundaryInError(_errorGraph[seed]);
                //foreach (int bound in errorBound)
                //    _allBoundaryPoints.Add(new Point(_pathlinesTime[seed * LineX + bound][0]));
            }
            //_boundariesSpacetime = FieldAnalysis.FindBoundaryInErrors(_errorGraph, new Vector3(_selection, SliceTimeMain));
            //_boundaryBallSpacetime = new LineBall(_linePlane, _boundariesSpacetime, LineBall.RenderEffect.HEIGHT, ColorMapping.GetComplementary(Colormap));
            //if (errorBound >= 0)
            //    _allBoundaryPoints.Add(new Point(_pathlinesTime[seed * LineX + errorBound][0]));
       //     GeometryWriter.WriteGraphCSV(RedSea.Singleton.DonutFileName + "Error.csv", _errorGraph);
            Console.WriteLine("Radii without boundary point: {0} of {1}", _numSeeds - _allBoundaryPoints.Count, _numSeeds);
            //   _graphPlane.ZAxis = Plane.ZAxis * WindowWidth;
        //    _boundaryCloud = new PointCloud(_graphPlane, new PointSet<Point>(_allBoundaryPoints.ToArray()));
            //LineSet lineCpy = new LineSet(_pathlinesTime);
            //lineCpy.CutAllHeight(_repulsion);
            //_pathlines = new LineBall(_linePlane, lineCpy, LineBall.RenderEffect.HEIGHT, Colormap, false);
            //int errorBound = FieldAnalysis.FindBoundaryInError(_errorGraph[0]);
            //_pathlinesTime.Cut(errorBound);
            // ~~~~~~~~~~~~ Get Boundary for Rendering ~~~~~~~~~~~~ \\

            // _pathlines = new LineBall(_linePlane, _pathlinesTime, LineBall.RenderEffect.HEIGHT, ColorMapping.GetComplementary(Colormap), Flat);

            // _graph = new LineBall(_graphPlane, FieldAnalysis.WriteGraphsToCircles(_distanceAngleGraph, new Vector3(_selection.X, _selection.Y, SliceTimeMain)), LineBall.RenderEffect.HEIGHT, Colormap, false);
            _graph = new LineBall(_graphPlane, FieldAnalysis.WriteGraphToSun(_errorGraph, new Vector3(_selection.X, _selection.Y, 0)), LineBall.RenderEffect.HEIGHT, Colormap, Flat);

            _rebuilt = false;
        }

        protected override void UpdateBoundary()
        {
            if (_lastSetting != null && (_rebuilt || FlatChanged || GraphChanged) && _errorGraph != null) // && (Flat && !Graph))
            {
                //Graph2D[] dist = FieldAnalysis.GraphDifferenceForward(_distanceTauGraph);
                //Plane zPlane = new Plane(_graphPlane, Vector3.UnitZ * 2);
                _graph = new LineBall(_graphPlane, FieldAnalysis.WriteGraphToSun(_errorGraph, new Vector3(_selection.X, _selection.Y, 0)), LineBall.RenderEffect.HEIGHT, Colormap, Flat);
                //               _graph = new LineBall(zPlane, FieldAnalysis.WriteGraphsToCircles(dist, new Vector3(_selection.X, _selection.Y, SliceTimeMain)), LineBall.RenderEffect.HEIGHT, Colormap, false);
                _rebuilt = false;
            }
            if (_lastSetting != null && (IntegrationTimeChanged || _rebuilt || Graph && GraphChanged && Flat))
                BuildGraph();
            //_graph = new LineBall(_graphPlane, FieldAnalysis.WriteGraphToSun(_errorGraph, new Vector3(_selection.X, _selection.Y, 0)), LineBall.RenderEffect.HEIGHT, Colormap, false);
            // new LineBall(_graphPlane, FieldAnalysis.WriteGraphsToCircles(_distanceAngleGraph, new Vector3(_selection.X, _selection.Y, SliceTimeMain)), LineBall.RenderEffect.HEIGHT, Colormap, false);
            //LineSet cutLines;
            ////if (SliceTimeReference > SliceTimeMain)
            ////{
            ////    // _graph = cut version of _coreAngleGraph.
            ////    int length = SliceTimeReference - SliceTimeMain;
            ////    length = (int)((float)length / StepSize + 0.5f);
            ////    cutLines = FieldAnalysis.CutLength(new LineSet(_coreDistanceGraph), length);
            ////}
            ////else
            ////    cutLines = new LineSet(_coreDistanceGraph);

            //cutLines = new LineSet(FieldAnalysis.);

            //_graph = new LineBall[] { new LineBall(_graphPlane, cutLines, LineBall.RenderEffect.HEIGHT, Colormap) };
        }

        public override string GetName(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.IntegrationTime:
                    return "Integrate until Angle";
                default:
                    return base.GetName(element);
            }
        }
    }
}
