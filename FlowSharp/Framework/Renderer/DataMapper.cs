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
    abstract class DataMapper
    {
        /// <summary>
        /// One function will generate a set of renderables based on the set parameters. Corresponds to the Disply/Preset setting.
        /// </summary>
        public delegate List<Renderable> ViewFunction();
        public Plane Plane
        {
            get { return _plane; }
            set
            {
                _plane = value; _intersectionPlane = new Plane(value, _planeOffsetZ * Vector3.UnitZ);
            }
        }

        protected virtual FieldPlane LoadPlane(int member, int time, int subtime = 0, bool timeOffset = false)
        {
            return LoadPlaneAndGrid(member, time, subtime, timeOffset).Item1;
        }

        protected FieldPlane LoadPlane(int member, int time, out RectlinearGrid grid, int subtime = 0)
        {
            var result = LoadPlaneAndGrid(member, time, subtime);
            grid = result.Item2;
            return result.Item1;
        }

        private Tuple<FieldPlane, RectlinearGrid> LoadPlaneAndGrid(int member, int time, int subtime = 0, bool timeOffset = false)
        {
            LoaderRaw loader = new LoaderRaw();
            ScalarField[] scalars;// = new ScalarField[2];
            // Read in the data.
            //_ranges[0].SetMember(RedSea.Dimension.MEMBER, _currentSetting.MemberMain);
            //_ranges[1].SetMember(RedSea.Dimension.MEMBER, _currentSetting.MemberMain);

            //LoaderNCF ncFile = RedSea.Singleton.GetLoaderNCF(time);
            loader.Range.SetMember(RedSea.Dimension.MEMBER, member);
            loader.Range.SetMember(RedSea.Dimension.TIME, time);
            loader.Range.SetMember(RedSea.Dimension.SUBTIME, subtime);
            switch (_currentSetting.Measure)
            {
                case RedSea.Measure.VELOCITY:
                case RedSea.Measure.DIVERGENCE:
                case RedSea.Measure.VORTICITY:
                case RedSea.Measure.SHEAR:
                case RedSea.Measure.DIVERGENCE_2D:
                    scalars = new ScalarField[2];

                    LoadVelocity:

                    loader.Range.SetMember(RedSea.Dimension.GRID_Z, _currentSetting.SliceHeight);
                    //_variableRanges[(int)RedSea.Variable.VELOCITY_X].SetMember(RedSea.Dimension.MEMBER, member);
                    //_variableRanges[(int)RedSea.Variable.VELOCITY_Y].SetMember(RedSea.Dimension.MEMBER, member);
                    //_variableRanges[(int)RedSea.Variable.VELOCITY_X].SetMember(RedSea.Dimension.CENTER_Z, _currentSetting.SliceHeight);
                    //_variableRanges[(int)RedSea.Variable.VELOCITY_Y].SetMember(RedSea.Dimension.CENTER_Z, _currentSetting.SliceHeight);

                    scalars[0] = loader.LoadFieldSlice(RedSea.Variable.VELOCITY_X);
                    scalars[1] = loader.LoadFieldSlice(RedSea.Variable.VELOCITY_Y);
                    //scalars[0] = ncFile.LoadFieldSlice( _variableRanges[(int)RedSea.Variable.VELOCITY_X]);
                    //scalars[1] = ncFile.LoadFieldSlice(_variableRanges[(int)RedSea.Variable.VELOCITY_Y]);
                    break;

                default:
                    RedSea.Measure var = _currentSetting.Measure;

                    //_variableRanges[(int)var].SetMember(RedSea.Dimension.MEMBER, member);
                    int sliceHeight = (var == RedSea.Measure.SURFACE_HEIGHT) ? 0 : _currentSetting.SliceHeight;
                    loader.Range.SetMember(RedSea.Dimension.GRID_Z, sliceHeight);
                    //_variableRanges[(int)var].SetMember(RedSea.Dimension.CENTER_Z, _currentSetting.SliceHeight);


                    // Maybe load vector field too.
                    bool addVelocity = (_currentSetting.Shader == FieldPlane.RenderEffect.LIC || _currentSetting.Shader == FieldPlane.RenderEffect.LIC_LENGTH);
                    scalars = new ScalarField[addVelocity ? 3 : 1];
                    scalars[scalars.Length - 1] = loader.LoadFieldSlice((RedSea.Variable)var); //ncFile.LoadFieldSlice(_variableRanges[(int)var]);
                    if (addVelocity)
                        goto LoadVelocity;

                    break;
            }
            //ncFile.Close();

            VectorField field;
            switch (_currentSetting.Measure)
            {
                case RedSea.Measure.DIVERGENCE:
                    {
                        VectorField vel = new VectorField(scalars);

                        bool keepField = _currentSetting.Shader == FieldPlane.RenderEffect.LIC;
                        scalars = new ScalarField[keepField ? 3 : 1];
                        scalars[scalars.Length - 1] = new VectorField(vel, FieldAnalysis.Divergence, 1, true).Scalars[0] as ScalarField;

                        if (keepField)
                        {
                            scalars[0] = vel.Scalars[0] as ScalarField;
                            scalars[1] = vel.Scalars[1] as ScalarField;
                        }
                        break;
                    }
                case RedSea.Measure.DIVERGENCE_2D:
                    {
                        VectorField vel = new VectorField(scalars);
                        scalars = new VectorField(vel, FieldAnalysis.Div2D, 2, true).Scalars as ScalarField[];
                        break;
                    }
                case RedSea.Measure.VORTICITY:
                    {
                        VectorField vel = new VectorField(scalars);

                        bool keepField = _currentSetting.Shader == FieldPlane.RenderEffect.LIC;
                        scalars = new ScalarField[keepField ? 3 : 1];
                        scalars[scalars.Length - 1] = new VectorField(vel, FieldAnalysis.Vorticity, 1, true).Scalars[0] as ScalarField;

                        if (keepField)
                        {
                            scalars[0] = vel.Scalars[0] as ScalarField;
                            scalars[1] = vel.Scalars[1] as ScalarField;
                        }
                        break;
                    }
                case RedSea.Measure.SHEAR:
                    {
                        VectorField vel = new VectorField(scalars);

                        bool keepField = _currentSetting.Shader == FieldPlane.RenderEffect.LIC;
                        scalars = new ScalarField[keepField ? 3 : 1];
                        scalars[scalars.Length - 1] = new VectorField(vel, FieldAnalysis.Shear, 1, true).Scalars[0] as ScalarField;

                        if (keepField)
                        {
                            scalars[0] = vel.Scalars[0] as ScalarField;
                            scalars[1] = vel.Scalars[1] as ScalarField;
                        }
                        break;
                    }
                default:
                    break;
            }
            field = new VectorField(scalars);
            field.TimeSlice = timeOffset ? time * RedSea.Singleton.NumSubsteps + subtime/*time + (float)subtime / RedSea.Singleton.NumSubsteps*/ : 0;
            // field = new VectorField(velocity, FieldAnalysis.StableFFF, 3, true);
            RectlinearGrid grid = field.Grid as RectlinearGrid;

            return new Tuple<FieldPlane, RectlinearGrid>(new FieldPlane(Plane, field, _currentSetting.Shader, _currentSetting.Colormap), grid);
        }

        #region IntersectionPlane
        private float _planeOffsetZ_intern = 0;
        protected float _planeOffsetZ
        {
            get { return _planeOffsetZ_intern; }
            set { _planeOffsetZ_intern = value; Plane = Plane; }
        }
        protected Plane _intersectionPlane;
        private Plane _plane;

        private LineBall _selectionRect;
        #endregion

        #region Settings
        public Setting CurrentSetting { get { return _currentSetting; } set { _currentSetting = value; } }
        protected Setting _currentSetting;
        protected Setting _lastSetting;

        public void UpdateMapping()
        {
            Renderer.Singleton.AddRenderables(Mapping());
            _lastSetting = new Setting(_currentSetting);
        }

        public abstract bool IsUsed(Setting.Element element);

        /// <summary>
        /// A class encapsulating all settings that are currently possible via WPF. "Union" with an int field.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        public class Setting
        {
            [FieldOffset(0)]
            public RedSea.DisplayLines LineSetting;
            //{
            //    get { return (RedSea.DisplayLines)this[Element.LineSetting]; }
            //    set { this[Element.LineSetting] = (int)value; }
            //}
            [FieldOffset(4)]
            public int SliceTimeMain;
            [FieldOffset(8)]
            public int SliceTimeReference;
            [FieldOffset(12)]
            public float AlphaStable;
            [FieldOffset(16)]
            public float StepSize = 1;
            [FieldOffset(20)]
            public VectorField.Integrator.Type IntegrationType;
            //{
            //    get { return (VectorField.Integrator.Type)this[Element.IntegrationType]; }
            //    set { this[Element.IntegrationType] = (int)value; }
            //}
            [FieldOffset(24)]
            public int LineX;
            [FieldOffset(28)]
            public int MemberMain;
            [FieldOffset(32)]
            public int MemberReference;
            [FieldOffset(36)]
            public RedSea.DisplayTracking Tracking;

            [FieldOffset(40)]
            public Colormap Colormap;
            //{
            //    get { return (Colormap)this[Element.Colormap]; }
            //    set { this[Element.Colormap] = (int)value; }
            //}
            [FieldOffset(44)]
            public FieldPlane.RenderEffect Shader;
            //{
            //    get { return (FieldPlane.RenderEffect)this[Element.Shader]; }
            //    set { this[Element.Shader] = (int)value; }
            //}
            [FieldOffset(48)]
            public float WindowWidth;
            [FieldOffset(52)]
            public float WindowStart;
            [FieldOffset(56)]
            public RedSea.Measure Measure;
            //{
            //    get { return (RedSea.Measure)this[Element.Measure]; }
            //    set { this[Element.Measure] = (int)value; }
            //}
            [FieldOffset(60)]
            public int SliceHeight;
            [FieldOffset(64)]
            public float IntegrationTime;
            [FieldOffset(68)]
            public RedSea.DiffusionMeasure DiffusionMeasure;
            //{
            //    get { return (RedSea.DiffusionMeasure)this[Element.DiffusionMeasure]; }
            //    set { this[Element.DiffusionMeasure] = (int)value; }
            //}
            [FieldOffset(72)]
            public Element VarX;
            //{
            //    get { return (Element)this[Element.VarX]; }
            //    set { this[Element.VarX] = (int)value; }
            //}
            [FieldOffset(76)]
            public Element VarY;
            //{
            //    get { return (Element)this[Element.VarX]; }
            //    set { this[Element.VarX] = (int)value; }
            //}

            [FieldOffset(80)]
            public float StartX;

            [FieldOffset(84)]
            public float StartY;

            [FieldOffset(88)]
            public float EndX;

            [FieldOffset(92)]
            public float EndY;

            [FieldOffset(96)]
            public int DimX;

            [FieldOffset(100)]
            public int DimY;

            [FieldOffset(104)]
            public Sign Flat = Sign.NEGATIVE;
            [FieldOffset(108)]
            public Sign Graph = Sign.NEGATIVE;
            // The real data.
            //[FieldOffset(0)]
            //private int[] _data = new int[Enum.GetValues(typeof(Element)).Length];
            //public int[] Data { get { return _data; } }

            //public int this[Element idx]
            //{
            //    get { return _data[(int)idx]; }
            //    set { _data[(int)idx] = value; }
            //}

            public enum Element : int
            {
                LineSetting = 0,
                SliceTimeMain,
                SliceTimeReference,
                AlphaStable,
                StepSize,
                IntegrationType,
                LineX,
                MemberMain,
                MemberReference,
                Colormap,
                Shader,
                WindowWidth,
                Tracking,
                WindowStart,
                Measure,
                SliceHeight,
                IntegrationTime,
                DiffusionMeasure,
                VarX,
                VarY,
                StartX,
                StartY,
                EndX,
                EndY,
                DimX,
                DimY,
                Flat,
                Graph
            }

            public Setting(Setting cpy)
            {
                //Array.Copy(cpy.Data, _data, _data.Length);
                ////Array.Copy(cpy.Data, _data, cpy.Data.Length);
                LineSetting = cpy.LineSetting;
                SliceTimeMain = cpy.SliceTimeMain;
                SliceTimeReference = cpy.SliceTimeReference;
                AlphaStable = cpy.AlphaStable;
                IntegrationType = cpy.IntegrationType;
                LineX = cpy.LineX;
                MemberMain = cpy.MemberMain;
                MemberReference = cpy.MemberReference;
                Colormap = cpy.Colormap;
                StepSize = cpy.StepSize;
                Shader = cpy.Shader;
                WindowWidth = cpy.WindowWidth;
                Tracking = cpy.Tracking;
                WindowStart = cpy.WindowStart;
                Measure = cpy.Measure;
                SliceHeight = cpy.SliceHeight;
                IntegrationTime = cpy.IntegrationTime;
                DiffusionMeasure = cpy.DiffusionMeasure;
                VarX = cpy.VarX;
                VarY = cpy.VarY;
                StartX = cpy.StartX;
                StartY = cpy.StartY;
                EndX = cpy.EndX;
                EndY = cpy.EndY;
                DimX = cpy.DimX;
                DimY = cpy.DimY;
                Flat = cpy.Flat;
                Graph = cpy.Graph;
            }

            public Setting() { }
        }
        #endregion Settings

        public ViewFunction Mapping;

        protected DataMapper()
        {
            CurrentSetting = new Setting();
        }

        public virtual void UpdateSelection()
        {
            if (_intersectionPlane == null)
                return;

            Vector2[] points = Renderer.Singleton.Camera.IntersectPlane(_intersectionPlane);
            Renderer.Singleton.Remove(_selectionRect);
            if (points == null)
                return;

            Vector3[] corners = new Vector3[5];
            corners[0] = new Vector3(points[0], 0);
            corners[1] = new Vector3(points[0].X, points[1].Y, 0);
            corners[2] = new Vector3(points[1], 0);
            corners[3] = new Vector3(points[1].X, points[0].Y, 0);
            corners[4] = corners[0];

            _selectionRect = new LineBall(_intersectionPlane, new LineSet(new Line[] { new Line() { Positions = corners } }) { Thickness = 0.2f, Color = Vector3.UnitX });
            Renderer.Singleton.AddRenderable(_selectionRect);
        }

        public virtual void OnRelease()
        {
            if (_intersectionPlane == null)
                return;
            Vector2[] points = Renderer.Singleton.Camera.IntersectPlane(_intersectionPlane);
            if (points == null)
                return;
            if (Vector2.DistanceSquared(points[0], points[1]) < 1)
            {
                ClickSelection(points[0]);
                return;
            }
            EndSelection(points);
        }

        public virtual void EndSelection(Vector2[] corners)
        {
            Renderer.Singleton.Remove(_selectionRect);
        }

        public virtual void ClickSelection(Vector2 pos) { }

        public virtual string GetName(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.MemberMain:
                    return "Ensemble Member";
                case Setting.Element.SliceHeight:
                    return "Slice Height";
                case Setting.Element.AlphaStable:
                    return "Alpha for stable FFF";
                case Setting.Element.StepSize:
                    return "Integrator Step Size";
                case Setting.Element.LineX:
                    return "Comparison Line Position";
                case Setting.Element.IntegrationTime:
                    return "Integration Time";
                case Setting.Element.WindowWidth:
                    return "Colormap Window Width";
                case Setting.Element.WindowStart:
                    return "Colormap Window Start";
                case Setting.Element.Flat:
                    return "Flatten";
                case Setting.Element.Graph:
                    return "Statistics";
                default:
                    return "I am a severely ignored Text Field :{";
            }
        }

        public virtual int? GetLength(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.MemberMain:
                case Setting.Element.MemberReference:
                    return RedSea.Singleton.NumSteps;
                case Setting.Element.LineX:
                    return 500;
                default:
                    return null;
            }
        }

        #region SettingChanged
        public virtual bool LineSettingChanged { get { return _currentSetting.LineSetting != _lastSetting.LineSetting; } }
        public virtual bool SliceTimeMainChanged { get { return _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain; } }
        public virtual bool SliceTimeReferenceChanged { get { return _currentSetting.SliceTimeReference != _lastSetting.SliceTimeReference; } }
        public virtual bool AlphaStableChanged { get { return _currentSetting.AlphaStable != _lastSetting.AlphaStable; } }
        public virtual bool StepSizeChanged { get { return _currentSetting.StepSize != _lastSetting.StepSize; } }
        public virtual bool IntegrationTypeChanged { get { return _currentSetting.IntegrationType != _lastSetting.IntegrationType; } }
        public virtual bool LineXChanged { get { return _currentSetting.LineX != _lastSetting.LineX; } }
        public virtual bool MemberMainChanged { get { return _currentSetting.MemberMain != _lastSetting.MemberMain; } }
        public virtual bool MemberReferenceChanged { get { return _currentSetting.MemberReference != _lastSetting.MemberReference; } }
        public virtual bool ColormapChanged { get { return _currentSetting.Colormap != _lastSetting.Colormap; } }
        public virtual bool ShaderChanged { get { return _currentSetting.Shader != _lastSetting.Shader; } }
        public virtual bool WindowWidthChanged { get { return _currentSetting.WindowWidth != _lastSetting.WindowWidth; } }
        public virtual bool TrackingChanged { get { return _currentSetting.Tracking != _lastSetting.Tracking; } }
        public virtual bool WindowStartChanged { get { return _currentSetting.WindowStart != _lastSetting.WindowStart; } }
        public virtual bool MeasureChanged { get { return _currentSetting.Measure != _lastSetting.Measure; } }
        public virtual bool SliceHeightChanged { get { return _currentSetting.SliceHeight != _lastSetting.SliceHeight; } }
        public virtual bool IntegrationTimeChanged { get { return _currentSetting.IntegrationTime != _lastSetting.IntegrationTime; } }
        public virtual bool DiffusionMeasureChanged { get { return _currentSetting.DiffusionMeasure != _lastSetting.DiffusionMeasure; } }
        public virtual bool VarXChanged { get { return _currentSetting.VarX != _lastSetting.VarX; } }
        public virtual bool VarYChanged { get { return _currentSetting.VarY != _lastSetting.VarY; } }
        public virtual bool StartXChanged { get { return _currentSetting.StartX != _lastSetting.StartX; } }
        public virtual bool StartYChanged { get { return _currentSetting.StartY != _lastSetting.StartY; } }
        public virtual bool EndXChanged { get { return _currentSetting.EndX != _lastSetting.EndX; } }
        public virtual bool EndYChanged { get { return _currentSetting.EndY != _lastSetting.EndY; } }
        public virtual bool DimXChanged { get { return _currentSetting.DimX != _lastSetting.DimX; } }
        public virtual bool DimYChanged { get { return _currentSetting.DimY != _lastSetting.DimY; } }
        public virtual bool FlatChanged { get { return _currentSetting.Flat != _lastSetting.Flat; } }
        public virtual bool GraphChanged { get { return _currentSetting.Graph != _lastSetting.Graph; } }
        #endregion SettingChanged

        #region CurrentSetting
        protected RedSea.DisplayLines LineSetting
        { get { return _currentSetting.LineSetting; } }

        protected int SliceTimeMain
        { get { return _currentSetting.SliceTimeMain; } }

        protected int SliceTimeReference
        { get { return _currentSetting.SliceTimeReference; } }

        protected float AlphaStable
        { get { return _currentSetting.AlphaStable; } }

        protected float StepSize
        { get { return _currentSetting.StepSize; } }

        protected VectorField.Integrator.Type IntegrationType
        { get { return _currentSetting.IntegrationType; } }

        protected int LineX
        { get { return _currentSetting.LineX; } }

        protected int MemberMain
        { get { return _currentSetting.MemberMain; } }

        protected int MemberReference
        { get { return _currentSetting.MemberReference; } }

        protected RedSea.DisplayTracking Tracking
        { get { return _currentSetting.Tracking; } }

        protected Colormap Colormap
        { get { return _currentSetting.Colormap; } }

        protected FieldPlane.RenderEffect Shader
        { get { return _currentSetting.Shader; } }

        protected float WindowWidth
        { get { return _currentSetting.WindowWidth; } }

        protected float WindowStart
        { get { return _currentSetting.WindowStart; } }

        protected RedSea.Measure Measure
        { get { return _currentSetting.Measure; } }

        protected int SliceHeight
        { get { return _currentSetting.SliceHeight; } }

        protected float IntegrationTime
        { get { return _currentSetting.IntegrationTime; } }

        protected RedSea.DiffusionMeasure DiffusionMeasure
        { get { return _currentSetting.DiffusionMeasure; } }

        protected Sign Flat
        { get { return _currentSetting.Flat; } }

        protected Sign Graph
        { get { return _currentSetting.Graph; } }
        #endregion CurrentSetting

    }

    class CriticalPointTracking : DataMapper
    {
        public CriticalPointSet2D[] CP;
        public VectorField ForwardFFF, BackwardFFF;
        public VectorField Velocity;
        public VectorField[] SlicesToRender
        {
            get;
            set;
        }

        //private VectorField[] _sliceFields;

        protected List<Renderable> _slice0;
        protected List<Renderable> _slice1;
        protected List<LineSet> _rawLines;
        protected List<Renderable> _lines;
        protected FieldPlane[] _planes = new FieldPlane[2];

        public CriticalPointTracking(CriticalPointSet2D[] cp, VectorFieldUnsteady velocity, Plane plane)
        {
            Debug.Assert(cp.Length == velocity.Size.T);
            CP = cp;
            Velocity = velocity;
            SlicesToRender = new VectorField[velocity.Size.T];
            for (int slice = 0; slice < velocity.Size.T; ++slice)
            {
                SlicesToRender[slice] = velocity.GetSlice(slice);
            }
            Mapping = TrackCP;
            Plane = plane;
        }

        protected CriticalPointTracking() { }

        public List<Renderable> TrackCP()
        {

            // The reference slice was changed. Update the field and its critical points.
            if (_lastSetting == null ||
                SliceTimeReferenceChanged)
            {
                _slice1 = new List<Renderable>(2);
                _planes[0] = new FieldPlane(Plane, SlicesToRender[_currentSetting.SliceTimeReference], FieldPlane.RenderEffect.LIC);
                _slice1.Add(_planes[0]);
                _slice1.Add(new PointCloud(Plane, CP[_currentSetting.SliceTimeReference].ToBasicSet()));
            }


            // Something mayor changed. Re-integrate.
            bool mapLines = false;
            if (_lastSetting == null ||
                AlphaStableChanged ||
                SliceTimeMainChanged ||
                IntegrationTypeChanged ||
                StepSizeChanged)
            {
                if (_lastSetting == null || SliceTimeMainChanged)
                {
                    // Clear the slice mapping.
                    _slice0 = new List<Renderable>(2);

                    // ~~~~~~~~~~~~ Field Mapping ~~~~~~~~~~~~~ \\
                    _planes[1] = new FieldPlane(Plane, SlicesToRender[_currentSetting.SliceTimeMain], FieldPlane.RenderEffect.LIC);
                    _slice0.Add(_planes[1]);

                    // ~~~~~~~~ Critical Point Mapping ~~~~~~~~ \\
                    _slice0.Add(new PointCloud(Plane, CP[_currentSetting.SliceTimeMain].SelectTypes(new CriticalPoint2D.TypeCP[] { CriticalPoint2D.TypeCP.ATTRACTING_FOCUS, CriticalPoint2D.TypeCP.REPELLING_FOCUS }).ToBasicSet()));
                }

                // Re-compute the feature flow field. Costly operation.
                if (_lastSetting == null || AlphaStableChanged)
                {
                    FieldAnalysis.AlphaStableFFF = _currentSetting.AlphaStable;
                    ForwardFFF = new VectorField(Velocity, FieldAnalysis.StableFFF, 3);
                    BackwardFFF = new VectorField(Velocity, FieldAnalysis.StableFFFNegative, 3);
                }
                // ~~~~~~~~~~~ Line Integration ~~~~~~~~~~~ \\
                // Clear the raw lines.
                _rawLines = new List<LineSet>(2);
                // Setup an integrator.
                VectorField.Integrator intVF = VectorField.Integrator.CreateIntegrator(ForwardFFF, _currentSetting.IntegrationType);
                intVF.MaxNumSteps = 10000;
                intVF.StepSize = _currentSetting.StepSize;
                intVF.NormalizeField = true;

                // Integrate the forward field.
                LineSet cpLinesPos = intVF.Integrate(CP[_currentSetting.SliceTimeMain], false)[0];

                // Negative FFF integration. Reversed stabilising field.
                //intVF.Direction = Sign.NEGATIVE;
                //                intVF.Field = BackwardFFF;
                //                var cpLinesNeg = intVF.Integrate(CP[_currentSetting.SliceTimeMain], false);
                //                cpLinesNeg.Color = new Vector3(0, 0.8f, 0);

                // Add the data to the list.
                _rawLines.Add(cpLinesPos);
                //                _rawLines.Add(cpLinesNeg);
                mapLines = true;
            }

            // The line settings have changed. Create new renderables from the lines.
            if (mapLines || LineSettingChanged)
            {
                _lines = new List<Renderable>(_rawLines.Count);

                switch (_currentSetting.LineSetting)
                {
                    // Map the vertices to colored points.
                    case RedSea.DisplayLines.POINTS_2D_LENGTH:
                        foreach (LineSet line in _rawLines)
                        {
                            PointSet<Point> linePoints = Velocity.ColorCodeArbitrary(line, RedSea.DisplayLineFunctions[(int)_currentSetting.LineSetting]);
                            _lines.Add(new PointCloud(Plane, linePoints));
                        }
                        break;

                    // Render as line.
                    default:
                    case RedSea.DisplayLines.LINE:
                        foreach (LineSet line in _rawLines)
                        {
                            _lines.Add(new LineBall(Plane, line));
                        }
                        break;
                }
            }

            // Set mapping values.
            _planes[0].UpperBound = _currentSetting.WindowWidth;
            _planes[0].UsedMap = _currentSetting.Colormap;
            _planes[0].SetRenderEffect(_currentSetting.Shader);
            _planes[1].UpperBound = _currentSetting.WindowWidth;
            _planes[1].UsedMap = _currentSetting.Colormap;
            _planes[1].SetRenderEffect(_currentSetting.Shader);

            return _slice0.Concat(_slice1).Concat(_lines).ToList();
        }

        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.Colormap:
                case Setting.Element.WindowWidth:
                    return _currentSetting.Shader == FieldPlane.RenderEffect.COLORMAP || _currentSetting.Shader == FieldPlane.RenderEffect.DEFAULT;
                case Setting.Element.LineX:
                case Setting.Element.MemberMain:
                case Setting.Element.MemberReference:
                    return false;
                default:
                    return true;
            }
        }
    }

    class PathlineCoreTracking : CriticalPointTracking
    {
        public PathlineCoreTracking(VectorFieldUnsteady velocity, /*VectorField fffPos, VectorField fffNeg,*/ Plane plane)
        {
            Velocity = new VectorField(velocity, FieldAnalysis.PathlineCore, 3);
            CP = new CriticalPointSet2D[velocity.Size.T];
            for (int slice = 0; slice < velocity.Size.T; ++slice)
            {
                CP[slice] = FieldAnalysis.ComputeCriticalPointsRegularSubdivision2D(Velocity.GetSlicePlanarVelocity(slice), 5, 0.3f);
            }
            // Render original field.
            SlicesToRender = new VectorField[velocity.Size.T];
            for (int slice = 0; slice < velocity.Size.T; ++slice)
            {
                SlicesToRender[slice] = Velocity.GetSlice(slice);
            }
            Mapping = TrackCP;
            Plane = plane;
        }
    }

    class MemberComparison : DataMapper
    {
        //private Loader.SliceRange[] _ranges;
        private FieldPlane[] _fields;
        private RectlinearGrid _grid;
        //private LoaderNCF.SliceRange[] _variableRanges;
        //private LoaderNCF.SliceRange _variableRange;
        private LoaderRaw _loader;

        public MemberComparison(/*LoaderNCF.SliceRange[] ranges,*/ Plane plane)
        {
            //Debug.Assert(ranges.Length == 2);
            //_ranges = ranges;
            _fields = new FieldPlane[2];
            Plane = plane;
            Mapping = LoadMembers;

            //LoaderNCF ncFile = RedSea.Singleton.GetLoaderNCF(0);

            //int sizeVar = ncFile.GetNumVariables();
            //_variableRanges = new LoaderNCF.SliceRange[sizeVar];

            ////LoaderNCF.SliceRange ensembleU = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.VELOCITY_X);
            ////ensembleU.SetMember(RedSea.Dimension.TIME, 0);
            ////LoaderNCF.SliceRange ensembleV = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.VELOCITY_Y);
            ////ensembleV.SetMember(RedSea.Dimension.TIME, 0);
            ////LoaderNCF.SliceRange ensembleSal = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.SALINITY);
            ////ensembleSal.SetMember(RedSea.Dimension.TIME, 0);
            ////LoaderNCF.SliceRange ensembleTemp = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.TEMPERATURE);
            ////ensembleTemp.SetMember(RedSea.Dimension.TIME, 0);
            ////LoaderNCF.SliceRange ensembleHeight = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.SURFACE_HEIGHT);
            ////ensembleHeight.SetMember(RedSea.Dimension.TIME, 0);

            ////_variableRanges[(int)RedSea.Variable.VELOCITY_X] = ensembleU;
            ////_variableRanges[(int)RedSea.Variable.VELOCITY_Y] = ensembleV;
            ////_variableRanges[(int)RedSea.Variable.SALINITY] = ensembleSal;
            ////_variableRanges[(int)RedSea.Variable.TEMPERATURE] = ensembleTemp;
            ////_variableRanges[(int)RedSea.Variable.SURFACE_HEIGHT] = ensembleHeight;
            //_variableRange = new LoaderRaw.SliceRangeRaw();
            //_variableRange.SetMember(RedSea.Dimension.TIME, 0);

            _loader = new LoaderRaw();
            _loader.Range.SetMember(RedSea.Dimension.SUBTIME, 0);

            //ncFile.Close();
        }

        /// <summary>
        /// If different planes were chosen, load new fields.
        /// </summary>
        /// <returns></returns>
        public List<Renderable> LoadMembers()
        {
            // Changed main slice settings.
            if (_lastSetting == null ||
                MemberMainChanged ||
                SliceTimeMainChanged ||
                MeasureChanged ||
                SliceHeightChanged ||
                ShaderChanged)
            {
                _fields[0] = LoadPlane(_currentSetting.MemberMain, _currentSetting.SliceTimeMain, out _grid);
                Vector2 extent = new Vector2((float)_currentSetting.LineX / _grid.Size[0], 1);
                _fields[0].SetToSubrangeFloat(Plane, _grid.Size.ToInt2(), Vector2.Zero, extent);
            }
            else if (LineXChanged)
            {
                Vector2 extent = new Vector2((float)_currentSetting.LineX / _grid.Size[0], 1);
                _fields[0].SetToSubrangeFloat(Plane, _grid.Size.ToInt2(), Vector2.Zero, extent);
            }

            // Changed reference settings.
            if (_lastSetting == null ||
                MemberReferenceChanged ||
                SliceTimeReferenceChanged ||
                MeasureChanged ||
                SliceHeightChanged ||
                ShaderChanged)
            {
                _fields[1] = LoadPlane(_currentSetting.MemberReference, _currentSetting.SliceTimeReference, out _grid);
                Vector2 extent = new Vector2((float)_currentSetting.LineX / _grid.Size[0], 1);
                _fields[1].SetToSubrangeFloat(Plane, _grid.Size.ToInt2(), new Vector2((float)_currentSetting.LineX / _grid.Size[0], 0), new Vector2(1 - (float)(_currentSetting.LineX - 1) / _grid.Size[0], 1));
            }
            else if (LineXChanged)
            {
                _fields[1].SetToSubrangeFloat(Plane, _grid.Size.ToInt2(), new Vector2((float)_currentSetting.LineX / _grid.Size[0], 0), new Vector2(1 - (float)(_currentSetting.LineX - 1) / _grid.Size[0], 1));
            }

            // Update window with to shader.
            //float winMin, winMax;
            //switch (_currentSetting.Shader)
            //{
            //    case FieldPlane.RenderEffect.LIC_LENGTH:
            //        winMin = 0;
            //        winMax = _currentSetting.WindowWidth;
            //        break;
            //    default:
            //        winMin = -_currentSetting.WindowWidth / 2;
            //        winMax = _currentSetting.WindowWidth / 2;
            //        break;
            //}
            // Set mapping values.

            _fields[0].LowerBound = _currentSetting.WindowStart;
            _fields[0].UpperBound = _currentSetting.WindowWidth + _currentSetting.WindowStart;
            _fields[0].SetRenderEffect(_currentSetting.Shader);
            _fields[0].UsedMap = _currentSetting.Colormap;

            _fields[1].LowerBound = _fields[0].LowerBound;
            _fields[1].UpperBound = _fields[0].UpperBound;
            _fields[1].SetRenderEffect(_currentSetting.Shader);
            _fields[1].UsedMap = _currentSetting.Colormap;


            var list = _fields.ToList<Renderable>();
            if (_lastSetting == null)
            {
                tests = new List<Renderable>(5);
                Line[] lines = new Line[2];
                lines[0] = new Line() { Positions = new Vector3[] { Vector3.Zero, Vector3.UnitX * 100, new Vector3(200, 10, 5) } };
                lines[1] = new Line() { Positions = new Vector3[] { Vector3.UnitZ * 10, new Vector3(90, -10, 10), new Vector3(190, 0, 12) } };
                LineSet linesTest = new LineSet(lines);

                Vector3[,] test = new Vector3[,] { { Vector3.Zero, Vector3.UnitX * 100, new Vector3(200, 10, 5) }, { Vector3.UnitZ * 10, new Vector3(90, -10, 10), new Vector3(190, 0, 12) } };
                Mesh mesh1 = new Mesh(Plane, new TileSurface() { Positions = test }, Mesh.RenderEffect.DEFAULT, Colormap);
                Mesh mesh2 = new Mesh(new Plane(Plane, Vector3.UnitZ * 10), new TileSurface(linesTest));
                tests.Add(mesh1);
                tests.Add(mesh2);
                tests.Add(new LineBall(Plane, linesTest));


            }
            return list.Concat(tests).ToList();
        }
        List<Renderable> tests;

        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.Colormap:
                case Setting.Element.WindowWidth:
                case Setting.Element.WindowStart:
                    return !(_currentSetting.Shader == FieldPlane.RenderEffect.CHECKERBOARD);
                case Setting.Element.AlphaStable:
                case Setting.Element.IntegrationType:
                case Setting.Element.LineSetting:
                case Setting.Element.StepSize:
                case Setting.Element.Tracking:
                    return false;
                default:
                    return true;
            }
        }
    }

    class OkuboWeiss : DataMapper
    {
        private VectorFieldUnsteady _fieldOW;
        private FieldPlane _fieldSlice;
        private float _standardDeviation;

        public OkuboWeiss(VectorFieldUnsteady velocity, Plane plane)
        {
            Debug.Assert(velocity.NumVectorDimensions == 2 + 1);
            _fieldOW = velocity;

            Plane = plane;
            Mapping = GetTimeSlice;
        }

        protected void Initialize()
        {
            _fieldOW = new VectorFieldUnsteady(_fieldOW, FieldAnalysis.OkuboWeiss, 1);

            float mean, fill;
            _fieldOW.ScalarsAsSFU[0].TimeSlices[0].ComputeStatistics(out fill, out mean, out _standardDeviation);
            Console.WriteLine("Mean: " + mean + ", SD: " + _standardDeviation + ", valid cells: " + fill);
        }

        /// <summary>
        /// If different planes were chosen, load new fields.
        /// </summary>
        /// <returns></returns>
        public List<Renderable> GetTimeSlice()
        {
            if (_lastSetting == null)
                Initialize();

            if (_lastSetting == null ||
                SliceTimeMainChanged)
            {
                VectorField sliceOW = _fieldOW.GetTimeSlice(_currentSetting.SliceTimeMain);
                _fieldSlice = new FieldPlane(Plane, sliceOW, FieldPlane.RenderEffect.COLORMAP, Colormap.Heatstep);
            }
            if (_lastSetting == null ||
                WindowWidthChanged)
            {
                _fieldSlice.LowerBound = -0.2f * _standardDeviation - _currentSetting.WindowWidth;
                _fieldSlice.UpperBound = -0.2f * _standardDeviation + _currentSetting.WindowWidth;
            }
            if (_lastSetting == null ||
                ColormapChanged ||
                ShaderChanged)
            {
                _fieldSlice.UsedMap = _currentSetting.Colormap;
                _fieldSlice.SetRenderEffect(_currentSetting.Shader);
            }
            List<Renderable> list = new List<Renderable>(1);

            // Set mapping.
            //            _fieldSlice.UpperBound = _currentSetting.WindowWidth;
            _fieldSlice.UsedMap = _currentSetting.Colormap;
            _fieldSlice.SetRenderEffect(_currentSetting.Shader);
            list.Add(_fieldSlice);
            return list;
        }

        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.Colormap:
                case Setting.Element.WindowWidth:
                    return _currentSetting.Shader == FieldPlane.RenderEffect.COLORMAP || _currentSetting.Shader == FieldPlane.RenderEffect.DEFAULT;
                case Setting.Element.SliceTimeMain:
                case Setting.Element.Shader:
                    return true;
                default:
                    return false;
            }
        }
    }

    class SubstepViewer : DataMapper
    {
        //private Loader.SliceRange[] _ranges;
        private FieldPlane[] _fields;
        private RectlinearGrid _grid;

        public SubstepViewer(Plane plane)
        {
            //_ranges = ranges;
            _fields = new FieldPlane[1];
            Plane = plane;
            Mapping = LoadMembers;
        }

        private FieldPlane LoadPlane(int member, int time)
        {

            ScalarField[] scalars;// = new ScalarField[2];

            //RedSea.Variable measureAsVar;
            //switch (_currentSetting.Measure)
            //{
            //    case RedSea.Measure.SALINITY:
            //    case RedSea.Measure.SURFACE_HEIGHT:
            //    case RedSea.Measure.TEMPERATURE:
            //        measureAsVar = (RedSea.Variable)(int)_currentSetting.Measure;
            //        break;
            //    default:
            //        measureAsVar = RedSea.Variable.VELOCITY_Z;
            //        break;
            //}

            int stepTime = time / 12;
            int substepTime = time - (stepTime * 12);
           // substepTime *= 9;


            LoaderRaw file = RedSea.Singleton.GetLoader(stepTime, substepTime, member, RedSea.Variable.VELOCITY_X) as LoaderRaw;
            int height = _currentSetting.Measure == RedSea.Measure.SURFACE_HEIGHT ? 0 : _currentSetting.SliceHeight;
            file.Range.SetMember(RedSea.Dimension.GRID_Z, height);
            file.Range.CorrectEndian = false;

            switch (_currentSetting.Measure)
            {
                case RedSea.Measure.VELOCITY:
                case RedSea.Measure.DIVERGENCE:
                case RedSea.Measure.VORTICITY:
                case RedSea.Measure.SHEAR:
                case RedSea.Measure.DIVERGENCE_2D:
                    scalars = new ScalarField[2];

                    LoadVelocity:

                    scalars[0] = file.LoadFieldSlice();
                    file.Range.SetVariable(RedSea.Variable.VELOCITY_Y);
                    scalars[1] = file.LoadFieldSlice();
                    break;

                default:
                    RedSea.Measure var = _currentSetting.Measure;

                    // Maybe load vector field too.
                    bool addVelocity = (_currentSetting.Shader == FieldPlane.RenderEffect.LIC || _currentSetting.Shader == FieldPlane.RenderEffect.LIC_LENGTH);
                    scalars = new ScalarField[addVelocity ? 3 : 1];
                    file.Range.SetVariable((RedSea.Variable)var);
                    scalars[scalars.Length - 1] = file.LoadFieldSlice();
                    if (addVelocity)
                        goto LoadVelocity;

                    break;
            }

            VectorField field;
            switch (_currentSetting.Measure)
            {
                case RedSea.Measure.DIVERGENCE:
                    {
                        VectorField vel = new VectorField(scalars);

                        bool keepField = _currentSetting.Shader == FieldPlane.RenderEffect.LIC;
                        scalars = new ScalarField[keepField ? 3 : 1];
                        scalars[scalars.Length - 1] = new VectorField(vel, FieldAnalysis.Divergence, 1, true).Scalars[0] as ScalarField;

                        if (keepField)
                        {
                            scalars[0] = vel.Scalars[0] as ScalarField;
                            scalars[1] = vel.Scalars[1] as ScalarField;
                        }
                        break;
                    }
                case RedSea.Measure.DIVERGENCE_2D:
                    {
                        VectorField vel = new VectorField(scalars);
                        scalars = new VectorField(vel, FieldAnalysis.Div2D, 2, true).Scalars as ScalarField[];
                        break;
                    }
                case RedSea.Measure.VORTICITY:
                    {
                        VectorField vel = new VectorField(scalars);

                        bool keepField = _currentSetting.Shader == FieldPlane.RenderEffect.LIC;
                        scalars = new ScalarField[keepField ? 3 : 1];
                        scalars[scalars.Length - 1] = new VectorField(vel, FieldAnalysis.Vorticity, 1, true).Scalars[0] as ScalarField;

                        if (keepField)
                        {
                            scalars[0] = vel.Scalars[0] as ScalarField;
                            scalars[1] = vel.Scalars[1] as ScalarField;
                        }
                        break;
                    }
                case RedSea.Measure.SHEAR:
                    {
                        VectorField vel = new VectorField(scalars);

                        bool keepField = _currentSetting.Shader == FieldPlane.RenderEffect.LIC;
                        scalars = new ScalarField[keepField ? 3 : 1];
                        scalars[scalars.Length - 1] = new VectorField(vel, FieldAnalysis.Shear, 1, true).Scalars[0] as ScalarField;

                        if (keepField)
                        {
                            scalars[0] = vel.Scalars[0] as ScalarField;
                            scalars[1] = vel.Scalars[1] as ScalarField;
                        }
                        break;
                    }
                default:
                    break;
            }
            field = new VectorField(scalars);

            _grid = field.Grid as RectlinearGrid;

            return new FieldPlane(Plane, field, _currentSetting.Shader, _currentSetting.Colormap);
        }

        /// <summary>
        /// If different planes were chosen, load new fields.
        /// </summary>
        /// <returns></returns>
        public List<Renderable> LoadMembers()
        {
            // Changed main slice settings.
            if (_lastSetting == null ||
                MemberMainChanged ||
                SliceTimeMainChanged ||
                MeasureChanged ||
                SliceHeightChanged ||
                ShaderChanged ||
                LineXChanged)
            {
                _fields[0] = LoadPlane(_currentSetting.MemberMain, _currentSetting.LineX);
            }
            _fields[0].LowerBound = _currentSetting.WindowStart;
            _fields[0].UpperBound = _currentSetting.WindowWidth + _currentSetting.WindowStart;
            _fields[0].SetRenderEffect(_currentSetting.Shader);
            _fields[0].UsedMap = _currentSetting.Colormap;

            return _fields.ToList<Renderable>();
        }

        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.Colormap:
                case Setting.Element.WindowWidth:
                case Setting.Element.WindowStart:
                    return !(_currentSetting.Shader == FieldPlane.RenderEffect.CHECKERBOARD);
                case Setting.Element.LineX:
                case Setting.Element.MemberMain:
                case Setting.Element.Shader:
                case Setting.Element.Measure:
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
                    return "Global Substep";
                default:
                    return base.GetName(element);
            }
        }
    }

    /// <summary>
    /// Integrate a number of pathlines from selected point on.
    /// </summary>
    class PathlineRadius : DataMapper
    {
        private VectorFieldUnsteady _velocity;
        //private Plane _plane;
        private Vector2 _selection;

        private LineBall[] _pathlines;
        private Renderable[] _graph;
        private FieldPlane _timeSlice;

        private bool _selectionChanged = false;
        public PathlineRadius(VectorFieldUnsteady velocity, Plane plane)// : base(plane, velocity.Size.ToInt2())
        {
            _velocity = velocity;
            Plane = plane;
            _intersectionPlane = new Plane(Plane, (velocity.TimeSlice ?? 0) * Plane.ZAxis);

            Mapping = AdvectLines;
        }

        public override void ClickSelection(Vector2 pos)
        {
            _selection = pos;
            _selectionChanged = true;
        }

        public List<Renderable> AdvectLines()
        {
            List<Renderable> renderables = new List<Renderable>(3 + _currentSetting.LineX);
            int numLines = _currentSetting.LineX;

            // Update / create underlying plane.
            if (_lastSetting == null ||
                SliceTimeMainChanged)
            {
                _timeSlice = new FieldPlane(Plane, _velocity.GetTimeSlice(_currentSetting.SliceTimeMain), _currentSetting.Shader, _currentSetting.Colormap);
                _intersectionPlane = new Plane(_intersectionPlane, new Vector3(0, 0, _currentSetting.SliceTimeMain - (_lastSetting?.SliceTimeMain) ?? 0));
            }
            else if (ColormapChanged ||
                ShaderChanged)
            {
                _timeSlice.SetRenderEffect(_currentSetting.Shader);
                _timeSlice.UsedMap = _currentSetting.Colormap;
            }
            // First item in list: plane.
            renderables.Add(_timeSlice);


            // Add Point to indicate clicked position.
            renderables.Add(new PointCloud(Plane, new PointSet<Point>(new Point[] { new Point() { Position = new Vector3(_selection, _currentSetting.SliceTimeMain + _velocity.Grid.TimeOrigin ?? 0), Color = new Vector3(1, 0, 1), Radius = 0.5f } })));
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
                case Setting.Element.Measure:
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
    abstract class SelectionMapper : DataMapper
    {
        protected AlgorithmCuda _algorithm;
        protected Int2 _startPoint = new Int2(50, 20);
        protected bool _subrange = false;
        protected Plane _subrangePlane;
        protected Int2 _minPlane = new Int2(0);
        protected Int2 _maxPlane;
        protected Int2 _globalMaxPlane;

        public SelectionMapper(Plane plane, Int2 fieldSize2D)
        {
            Plane = plane;
            _subrangePlane = Plane;
            _maxPlane = fieldSize2D;
            _globalMaxPlane = fieldSize2D;
        }

        public override void EndSelection(Vector2[] points)
        {
            base.EndSelection(points);
            // When currently a subrange is selected, switch to full range on click.
            if (_subrange)
            {
                _startPoint += _minPlane;
                _algorithm.CompleteRange(_startPoint);
                _subrangePlane = Plane;
            }
            // Select subrange.
            else
            {
                Vector2 minV = new Vector2(
                    Math.Min(points[0].X, points[1].X),
                    Math.Min(points[0].Y, points[1].Y));

                // Int2 values.
                Int2 min = (Int2)minV;
                Vector2 maxV = points[0] + points[1] - minV;
                Int2 max = (Int2)maxV;

                min = Int2.Max(min, new Int2(0)).AsInt2();
                min = Int2.Min(min, _globalMaxPlane).AsInt2();

                max = Int2.Min(max, _globalMaxPlane).AsInt2();
                max = Int2.Max((Int2)max, new Int2(0)).AsInt2();

                if (min.X - max.X == 0 || min.Y - max.Y == 0)
                {
                    return;
                }

                _startPoint = _startPoint - min;

                if (!_startPoint.IsPositive() || _startPoint > max - min)
                    _startPoint = (max - min) / 2;

                _algorithm.Subrange(min, max - min, _startPoint);
                _subrangePlane = new Plane(Plane, new Vector3(min.X, min.Y, 0));
                _minPlane = min;
                _maxPlane = max;
            }

            _subrange = !_subrange;
        }

        public override void ClickSelection(Vector2 point)
        {
            Int2 selection = (Int2)point - _minPlane;
            if (selection.IsPositive() && selection < _maxPlane)
                _startPoint = selection;
        }
    }
    class EmptyMapper : DataMapper
    {
        public EmptyMapper()
        {
            Mapping = (() => new List<Renderable>(0));
            _currentSetting = new Setting();
        }

        // We want to be able to make all settings before loading other data.
        public override bool IsUsed(Setting.Element element)
        {
            return true;
        }

        public override string GetName(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.LineX:
                    return "Comparison X | Neighbor ID | Substep";
                case Setting.Element.AlphaStable:
                    return "Alpha for SFFF | Gauss Variance";

                default:
                    return base.GetName(element);
            }
        }
    }
}
