using SlimDX;
using SlimDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Integrator = FlowSharp.VectorField.Integrator;

namespace FlowSharp
{
    class CoherencyMapper : CoreDistanceMapper
    {
        protected override int _numSeeds
        {
            get
            {
                return (int)(LineX * Math.PI);
            }
        }

        protected Graph2D[] _selectionData, _selectionDataRef;
        protected Graph2D[] _coherency;
        protected LineBall _coherencyDisk, _selectionDistRef;
        protected bool _rebuilt = false;
        protected List<LineSet> _stencilPathlines;

        protected string _currentFileName;

        protected float _brushSize = 10.0f;

        public CoherencyMapper(int everyNthField, Plane plane) : base(everyNthField, plane)
        {
            Mapping = CoherencyMap;

            _selectedCore = 2;
        }

        public List<Renderable> CoherencyMap()
        {
            List<Renderable> renderables = new List<Renderable>(10);

            int numLines = LineX;

            #region BackgroundPlanes
            if (_lastSetting == null ||
                MeasureChanged ||
                SliceTimeMainChanged ||
                MemberMainChanged ||
                CoreChanged)
            {

                if (_lastSetting == null && _cores == null ||
                    CoreChanged ||
                    MemberMainChanged)
                {
                    // Trace / load cores.
                    TraceCore(MemberMain, SliceTimeMain);
                    if (_selectedCore >= 0)
                        ClickSelection(_selection);
                }

                // Computing which field to load as background.
                int totalTime = Math.Min(RedSea.Singleton.NumSubstepsTotal, SliceTimeMain);
                int time = (totalTime * _everyNthTimestep) / RedSea.Singleton.NumSubsteps;
                int subtime = (totalTime * _everyNthTimestep) % RedSea.Singleton.NumSubsteps;

                _timeSlice = LoadPlane(MemberMain, time, subtime, true);
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
            #endregion BackgroundPlanes

            // Add Point to indicate clicked position.
            renderables.Add(new PointCloud(_linePlane, new PointSet<Point>(new Point[] { new Point() { Position = new Vector3(_selection, SliceTimeMain), Color = new Vector3(0.7f), Radius = 0.4f } })));
            bool rebuilt = false;

            if (_lastSetting == null ||
                DiffusionMeasureChanged)
            {
                switch (DiffusionMeasure)
                {
                    case RedSea.DiffusionMeasure.FTLE:
                        _currentFileName = "FTLE";
                        break;
                    case RedSea.DiffusionMeasure.Direction:
                        _currentFileName = "Okubo";
                        break;
                    default:
                        _currentFileName = "Concentric";
                        break;
                }
            }

            // Recompute lines if necessary.
            if (numLines > 0 && (
            _lastSetting == null ||
            NumLinesChanged ||
            _selectionChanged ||
            SliceTimeMainChanged ||
            DiffusionMeasureChanged))
            {
                _graph = null;
                // Load selection
                if (LoadGraph(_currentFileName + "Selection", out _selectionData))
                {
                    // Is there a drawing saved? If not, make a new empty graph.
                    if (!LoadGraph(_currentFileName + "Coherency", _selectedCore, out _coherency, out _graphData))
                    {
                        if (_coherency == null || _coherency.Length != _selectionData.Length)
                            _coherency = new Graph2D[_selectionData.Length];

                        for (int angle = 0; angle < _coherency.Length; ++angle)
                        {
                            _coherency[angle] = new Graph2D(_selectionData[angle].Length);
                            for (int rad = 0; rad < _coherency[angle].Length; ++rad)
                            {
                                _coherency[angle].X[rad] = _selectionData[angle].X[rad];
                                _coherency[angle].Fx[rad] = 0;
                            }
                        }

                        IntegrateLines();
                    }
                    else
                    {
                        _stencilPathlines = new List<LineSet>(8);
                        for (int toTime = 10; toTime <= 80; toTime += 10)
                        {
                            string pathlineName = RedSea.Singleton.DiskFileName + _currentFileName + string.Format("Pathlines_{0}_{1}.pathlines", SliceTimeMain, toTime);
                            LineSet paths;
                            GeometryWriter.ReadFromFile(pathlineName, out paths);
                            _stencilPathlines.Add(paths);
                        }
                    }
                    // Some weird things happening, maybe this solves offset drawing... It does.
                    LineSet coherencyLines = FieldAnalysis.WriteGraphToSun(_coherency, new Vector3(_selection.X, _selection.Y, SliceTimeMain));
                    _coherencyDisk = new LineBall(_graphPlane, coherencyLines, LineBall.RenderEffect.HEIGHT, Colormap, true, SliceTimeMain);
                    //_coherencyDisk.LowerBound = SliceTimeMain;
                    //_coherencyDisk.UpperBound = SliceTimeMain + 80;
                    MaskOnData();
                }

                _selectionChanged = false;
                rebuilt = true;
            }

            // Recompute lines if necessary.
            if (numLines > 0 && (
            _lastSetting == null ||
            NumLinesChanged ||
            FlatChanged ||
            _selectionChanged ||
            SliceTimeReferenceChanged ||
            DiffusionMeasureChanged))
            {
                _selectionDistRef = null;
                // Load selection
                if (LoadGraph(_currentFileName + "Selection", _selectedCore, out _selectionDataRef, out _graphData, SliceTimeReference))
                {
                    // Some weird things happening, maybe this solves offset drawing... It does.
                    LineSet selectionLines = FieldAnalysis.WriteGraphToSun(_selectionDataRef, new Vector3(_selection.X, _selection.Y, SliceTimeReference));
                    _selectionDistRef = new LineBall(_graphPlane, selectionLines, LineBall.RenderEffect.HEIGHT, Colormap.Gray, true, SliceTimeReference);
                    _selectionDistRef.LowerBound = SliceTimeReference;
                    _selectionDistRef.UpperBound = SliceTimeReference + 1;

                    if (SliceTimeReference % 10 == 0 && SliceTimeReference != 0)
                    {
                        _pathlinesTime = _stencilPathlines[SliceTimeReference / 10 - 1];
                        _pathlines = new LineBall(_graphPlane, _pathlinesTime, LineBall.RenderEffect.HEIGHT, Colormap.Heat, false);
                        _pathlines.LowerBound = SliceTimeMain;
                        _pathlines.UpperBound = 80;
                    }
                }

                _selectionChanged = false;
                rebuilt = true;
            }

            // Add the lineball.
            if (_pathlines != null && !Flat)
                renderables.Add(_pathlines);
            if (_graph != null)
                renderables.Add(_graph);
            if (_selectionDistRef != null)
                renderables.Add(_selectionDistRef);
            //if (_coherencyDisk != null) // && !Graph && !Flat)
            //    renderables.Add(_coherencyDisk);
            //            if (_selectionDistRef != null && (Graph || Flat))
            //                renderables.Add(_selectionDistRef);
            //if (SliceTimeMain != SliceTimeReference)
            //    renderables.Add(_compareSlice);
            if (_selectedCore >= 0 && _coreBall != null && !Flat)
                renderables.Add(_coreBall);

            return renderables;
        }

        protected void IntegrateLines()
        {
            LineSet seeds = FieldAnalysis.WriteGraphToSun(_coherency, new Vector3(_selection.X, _selection.Y, SliceTimeMain));
            _stencilPathlines = new List<LineSet>(8);

            // ~~~~~~~~~~~~~~~~~~~~~~~~ Integrate Pathlines and Adapt ~~~~~~~~~~~~~~~~~~~~~~~~ \\
            // Setup integrator.
            Integrator pathlineIntegrator = Integrator.CreateIntegrator(null, IntegrationType, _cores[_selectedCore], _repulsion);
            pathlineIntegrator.Direction = Sign.POSITIVE;
            pathlineIntegrator.StepSize = StepSize;

            // Count out the runs for debugging.
            int run = 0;

            // ~~~~~~~~~~~~~~~~~~~~~~~~ Integrate Pathlines  ~~~~~~~~~~~~~~~~~~~~~~~~ \\
            #region IntegratePathlines
            // Do we need to load a field first?
            if (_velocity.TimeOrigin > 0 || _velocity.TimeOrigin + _velocity.Size.T < 11)
            {
                LoadField(0, MemberMain, 11);
            }

            // Integrate first few steps.
            pathlineIntegrator.Field = _velocity;
            _stencilPathlines.Add(pathlineIntegrator.Integrate(seeds.ToPointSet(), false)[0]);

            // ~~~~~~~~~~~~~~~~~~~~~~~~ Filter and Repeat ~~~~~~~~~~~~~~~~~~~~~~~~ \\

            Graph2D[] interimSlice;
            for (int toTime = 10; toTime <= 80; toTime += 10)
            {
                if (!LoadGraph(_currentFileName + "Selection", _selectedCore, out interimSlice, out _graphData, toTime))
                    continue;

                Vector3 corePoint = (Vector3)_cores.Lines[_selectedCore].SampleZ(toTime);

                // ~~~~~~~~~~~~~~~~~~~~~~~~ Keep Only Those Inside ~~~~~~~~~~~~~~~~~~~~~~~~ \\
                List<Line> shrunkenLineSet = new List<Line>();
                foreach (Line pathLine in _stencilPathlines[_stencilPathlines.Count - 1].Lines)
                {
                    if (pathLine.Length < 1)
                        continue;

                    Vector2 endPos = new Vector2(pathLine.Last.X, pathLine.Last.Y);
                    Int2 indexInSun = GetClosestIndex(interimSlice, new Vector2(corePoint.X, corePoint.Y), endPos);

                    if (indexInSun.X < 0 || indexInSun.X >= interimSlice.Length
                       || indexInSun.Y < 0 || indexInSun.Y >= interimSlice[0].Length)
                        continue;

                    // Check against stencil data.
                    if (interimSlice[indexInSun.X].Fx[indexInSun.Y] != 1)
                        continue;

                    // Keep this pathline.
                    shrunkenLineSet.Add(pathLine);

                    // Update coherency map. Use max in case we ever add more in-between slices.
                    Vector2 startPos = new Vector2(pathLine[0].X, pathLine[0].Y);
                    indexInSun = GetClosestIndex(interimSlice, _selection, startPos);
                    _coherency[indexInSun.X].Fx[indexInSun.Y] = Math.Max(_coherency[indexInSun.X].Fx[indexInSun.Y], toTime);
                }

                // Replace last line set in list with new, filtered version.
                _stencilPathlines[_stencilPathlines.Count - 1] = new LineSet(shrunkenLineSet.ToArray());
                string pathlineName = RedSea.Singleton.DiskFileName + _currentFileName + string.Format("Pathlines_{0}_{1}.pathlines", SliceTimeMain, toTime);
                GeometryWriter.WriteToFile(pathlineName, _stencilPathlines[_stencilPathlines.Count - 1]);

                if (toTime == 80)
                    break;

                // Append integrated lines of next loaded vectorfield time slices.
                LoadField(toTime, MemberMain, 11);

                // Integrate further.
                pathlineIntegrator.Field = _velocity;
                _stencilPathlines.Add(new LineSet(_stencilPathlines[_stencilPathlines.Count - 1]));
                pathlineIntegrator.IntegrateFurther(_stencilPathlines[_stencilPathlines.Count - 1]);

                #endregion IntegratePathlines
            }

            // ~~~~~~~~~~~~~~~~~~~~~~~~ Write New Coherency Map to Disk ~~~~~~~~~~~~~~~~~~~~~~~~ \\
            LineSet coherencyLines = FieldAnalysis.WriteGraphToSun(_coherency, new Vector3(_selection, SliceTimeMain));
            WriteGraph(_currentFileName + "Coherency", _selectedCore, _coherency, coherencyLines);
        }

        #region CoreAlgorithm
        public void SelectCore(Vector2 pos)
        {
            Vector3 selection3D = Vector3.Zero;
            if (pos.X >= 0 && pos.Y >= 0 && pos.X < _velocity.Size[0] && pos.Y < _velocity.Size[1])
            {
                if (_cores == null || _cores.Length < 1)
                {
                    _selection = pos;
                    _selectionChanged = true;

                    Console.WriteLine("Pos: {0}", pos);
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

                        // Take the point directly or take core?
                        if (Core == CoreAlgorithm.CLICK)
                            _selection = pos;
                        else
                            _selection = new Vector2(nearest.X, nearest.Y);
                    }
                }
                if (_selectedCore != -1)
                {
                    if (Core == CoreAlgorithm.CLICK)
                        _cores[_selectedCore] = new Line() { Positions = new Vector3[] { new Vector3(_selection.X, _selection.Y, 0), selection3D + Vector3.UnitZ * (RedSea.Singleton.NumSubstepsTotal / _everyNthTimestep - SliceTimeMain) } };
                }
                Debug.Assert(_selectedCore >= 0 && _selectedCore < _cores.Length, "The nearest core is invalid.");

                if (_lastSetting != null)
                    Map();

            }
            else
                Console.WriteLine("Selection not within field range.");
        }

        protected override void TraceCore(int member = 0, int startSubstep = 0)
        {
            LoadCoreBySelection(member, startSubstep);
        }
        #endregion CoreAlgorithm

        protected void MaskOnData()
        {
            if (_coherency == null || _selectionData == null)
                return;

            Renderer.Singleton.Remove(_graph);
            float dataRange = 80;
            float rangeOffset = dataRange * 0.5f;

            Graph2D[] maskGraph = new Graph2D[_coherency.Length];
            for (int angle = 0; angle < maskGraph.Length; ++angle)
                maskGraph[angle] = Graph2D.Operate(_coherency[angle], _selectionData[angle], (b, a) => (Math.Max(0, a + rangeOffset + b * (2 * dataRange))));

            LineSet maskedLines = FieldAnalysis.WriteGraphToSun(maskGraph, new Vector3(_selection.X, _selection.Y, SliceTimeMain));
            _graph = new LineBall(_graphPlane, maskedLines, LineBall.RenderEffect.HEIGHT, Colormap, true, SliceTimeMain);
            _graph.LowerBound = SliceTimeMain;
            _graph.UpperBound = SliceTimeMain + 4 * dataRange;
            _graph.UsedMap = Colormap.ParulaSegmentation;
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

        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.SliceHeight:
                case Setting.Element.Tracking:
                case Setting.Element.Colormap:
                    return false;
                default:
                    return true;
            }
        }
    }
}
