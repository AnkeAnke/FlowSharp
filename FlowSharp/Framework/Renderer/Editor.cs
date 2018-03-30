using SlimDX;
using SlimDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Integrator = FlowSharp.VectorField.Integrator;

namespace FlowSharp
{
    class ConcentricEditorMapper : CoreDistanceMapper
    {
        protected override int _numSeeds
        {
            get
            {
                return (int)(LineX * Math.PI);
            }
        }

        protected Graph2D[] _selectionData;
        protected float _rangeGraphData, _minGraphData;
        protected Graph2D[] _dataGraph;
        protected bool _rebuilt = false;
        //protected Graph2D[] _distanceDistance;

        private LineSet _selectionLines;
        //private LineBall _selectionGraph;

        protected Point _mouseCircle;
        protected PointCloud _mouseCloud;
        protected string _currentFileName;

        protected float _brushSize = 10.0f;

        public ConcentricEditorMapper(int everyNthField, Plane plane) : base(everyNthField, plane)
        {
            Mapping = EditorMap;

            _selectedCore = 2;
        }

        public List<Renderable> EditorMap()
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

            // Recompute lines if necessary.
            if (numLines > 0 && (
                _lastSetting == null ||
                NumLinesChanged ||
                OffsetRadiusChanged ||
                _selectionChanged ||
                SliceTimeMainChanged ||
                DiffusionMeasureChanged))
            {
                switch (DiffusionMeasure)
                {
                    case RedSea.DiffusionMeasure.FTLE:
                        _currentFileName = "FTLE";
                        _rangeGraphData = 0.095f;
                        break;
                    case RedSea.DiffusionMeasure.Direction:
                        _currentFileName = "Okubo";
                        _rangeGraphData = 0.2f;
                        break;
                    default:
                        _currentFileName = "Concentric";
                        _rangeGraphData = 20;
                        break;
                }

                _graph = null;
                // Load computed data.
                if (LoadGraph(_currentFileName, out _dataGraph))
                {
                    // Is there a drawing saved? If not, make a new empty graph.
                    if (!LoadGraph(_currentFileName + "Selection", _selectedCore, out _selectionData, out _selectionLines))
                    {
                        if (_selectionData == null || _selectionData.Length != _dataGraph.Length)
                            _selectionData = new Graph2D[_dataGraph.Length];

                        for (int angle = 0; angle < _selectionData.Length; ++angle)
                        {
                            _selectionData[angle] = new Graph2D(_dataGraph[angle].Length);
                            for (int rad = 0; rad < _selectionData[angle].Length; ++rad)
                            {
                                _selectionData[angle].X[rad] = _dataGraph[angle].X[rad];
                                _selectionData[angle].Fx[rad] = 0;
                            }
                        }

                        //_selectionLines = FieldAnalysis.WriteGraphToSun(_selectionData, new Vector3(_selection.X, _selection.Y, SliceTimeMain));
                    }

                    // Some weird things happening, maybe this solves offset drawing...
                    _graphData = FieldAnalysis.WriteGraphToSun(_dataGraph, new Vector3(_selection.X, _selection.Y, SliceTimeMain));


                    var dataRange = _graphData.GetRange(2);
                    //_rangeGraphData = dataRange.Item2 - SliceTimeMain;
                    _minGraphData = SliceTimeMain;

                    MaskOnData();
                }

                _selectionChanged = false;
                rebuilt = true;
            }

            // Add the lineball.
            if (_pathlines != null)
                renderables.Add(_pathlines);
            if (_graph != null) // && !Graph && !Flat)
                renderables.Add(_graph);
            //if (_selectionGraph != null && (Graph || Flat))
            //    renderables.Add(_selectionGraph);
            if (_boundaryBallSpacetime != null && !Graph)// && Graph && !Flat)
                renderables.Add(_boundaryBallSpacetime);
            if (SliceTimeMain != SliceTimeReference)
                renderables.Add(_compareSlice);
            if (_boundaryCloud != null)// && Graph)
                renderables.Add(_boundaryCloud);
            if (_specialObject != null)
                renderables.Add(_specialObject);
            if (_selectedCore >= 0 && _coreBall != null && !Flat)
                renderables.Add(_coreBall);

            return renderables;
        }

        protected void MaskOnData()
        {
            if (_dataGraph == null || _selectionData == null)
                return;

            Renderer.Singleton.Remove(_graph);
            float rangeOffset = _rangeGraphData * 0.5f;

            Graph2D[] maskGraph = new Graph2D[_dataGraph.Length];
            for (int angle = 0; angle < maskGraph.Length; ++angle)
                maskGraph[angle] = Graph2D.Operate(_dataGraph[angle], _selectionData[angle], (b,a) => (Math.Max(0, a + rangeOffset + b * (2 * _rangeGraphData))) );

            LineSet maskedLines = FieldAnalysis.WriteGraphToSun(maskGraph, new Vector3(_selection.X, _selection.Y, SliceTimeMain));
            _graph = new LineBall(_graphPlane, maskedLines, LineBall.RenderEffect.HEIGHT, Colormap, true, SliceTimeMain);
            _graph.LowerBound = _minGraphData;
            _graph.UpperBound = _minGraphData + 4 * _rangeGraphData;
            _graph.UsedMap = Colormap.ParulaSegmentation;

            Renderer.Singleton.AddRenderable(_graph);
        }

        protected void PaintOnPosition(Vector2 mousePos)
        {
            if (_selectionData == null || _graphData == null)
                return;

            KeyboardState keyState = Renderer.Singleton.Camera.Keyboard.GetCurrentState();
            float brushChange = 0.05f;
            if (keyState.IsPressed(Key.Q))
                _brushSize *= (1.0f - brushChange);
            if (keyState.IsPressed(Key.E))
                _brushSize *= (1.0f + brushChange);

            if (_mouseCircle != null)
            {
                // Nothing much moved, avoid unnecessary update.
                Vector2 oldMousePos = new Vector2(_mouseCircle.Position.X, _mouseCircle.Position.Y);
                if ((oldMousePos - mousePos).Length() < _brushSize * 0.01f)
                {
                    RedrawMouse();
                    return;
                }
            }

            bool addArea = true;

            if (keyState.IsPressed(Key.LeftShift) || keyState.IsPressed(Key.RightShift))
            {
                addArea = false;
            }

            _mouseCircle = new Point(new Vector3(mousePos, 0.00001f + SliceTimeMain));
            _mouseCircle.Radius = _brushSize;

            // Calculate whether (and where) within round canvas.
            float distMouseToCore = (mousePos - _selection).Length();
            if (distMouseToCore < _lengthRadius + _mouseCircle.Radius)
            {
                _mouseCircle.Color = new Vector3(1, 0, 0);
                _mouseCircle.Radius *= 0.5f;

                // Paint now.
                float minDistToCore = Math.Max(distMouseToCore - _brushSize, 0);
                float maxDistToCore = distMouseToCore + _brushSize;

                Int2 nearestIndex = GetClosestIndex(_dataGraph, _selection, mousePos);
                int mouseAngle = nearestIndex.X;
                int mouseRad = nearestIndex.Y;

                // Establish basic measures.
                int numRads = _dataGraph[0].Length;
                float radPerIndex = (_dataGraph[0].X[1] - _dataGraph[0].X[0]);

                // Calculate angle and radian indices needed to each side.
                float nearestRad = mouseRad - _brushSize / radPerIndex;
                float furthestRad = Math.Min(mouseRad + _brushSize / radPerIndex, numRads - 1);
                if (nearestRad < 0)
                {
                    furthestRad = (float)Math.Ceiling(Math.Max(-nearestRad, furthestRad));
                    nearestRad = 0;
                }

                for (int angle = 0; angle < _selectionData.Length; ++angle)
                {
                    for (int rad = (int)nearestRad; rad < furthestRad; ++rad)
                    {
                        if (rad < 0 || rad >= _dataGraph[0].Length)
                            break;
                        Vector3 dataPos = _graphData[angle][rad];
                        float distToBrush = (new Vector2(dataPos[0], dataPos[1]) - mousePos).Length();
                        if (distToBrush < _brushSize)
                        {
                            _selectionData[angle].Fx[rad] = addArea ? 1 : 0;
                        }
                    }
                }

                MaskOnData();
            }

            RedrawMouse();
        }

        protected void RedrawMouse()
        {
            // ======================= Save State, Change Rendering ======================= \\

            // Add point to renderer manually, so we do not need to wait for an update.
            Renderer.Singleton.Remove(_mouseCloud);
            if (_mouseCircle != null)
            {
                Plane editorPlane = new Plane(_graphPlane);
                editorPlane.PointSize = editorPlane.Scale;
                _mouseCloud = new PointCloud(editorPlane, new PointSet<Point>(new Point[] { _mouseCircle }));
            }
            Renderer.Singleton.AddRenderable(_mouseCloud);
        }
        
    #region Mouse
        public override void UpdateSelection()
        {
            Vector2[] points = Renderer.Singleton.Camera.IntersectPlane(_intersectionPlane);

            PaintOnPosition(points[1]);
        }


        public override void ClickSelection(Vector2 pos)
        {
            try
            {
                KeyboardState state = Renderer.Singleton.Camera.Keyboard.GetCurrentState();
                if (state.IsPressed(Key.Period))
                {
                    SelectCore(pos);
                    return;
                }
            }
            catch (Exception e) { }

            PaintOnPosition(pos);
            Renderer.Singleton.Remove(_mouseCloud);
        }

        public override void EndSelection(Vector2[] corners)
        {
            if (_selectionData == null)
                return;

            _selectionLines = FieldAnalysis.WriteGraphToSun(_selectionData, new Vector3(_selection.X, _selection.Y, SliceTimeMain));
            WriteGraph(_currentFileName + "Selection", _selectedCore, _selectionData, _selectionLines);

            Renderer.Singleton.Remove(_mouseCloud);
            _mouseCircle = null;
        }
    #endregion Mouse

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
                    //_coreBall = new LineBall(_linePlane, new LineSet(new Line[] { _cores[_selectedCore] }) { Color = new Vector3(0.8f, 0.1f, 0.1f), Thickness = 0.3f }, LineBall.RenderEffect.HEIGHT, Colormap);
                    //_coreBall.UpperBound = WindowStart + WindowWidth;
                    //_coreBall.LowerBound = WindowStart;
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
                case Setting.Element.MemberReference:
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
