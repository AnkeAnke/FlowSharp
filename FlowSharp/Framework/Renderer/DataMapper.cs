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
            public RedSea.DisplayLines LineSetting
            {
                get { return (RedSea.DisplayLines)this[Element.LineSetting]; }
                set { this[Element.LineSetting] = (int)value; }
            }
            [FieldOffset(4)]
            public int SliceTimeMain;
            [FieldOffset(8)]
            public int SliceTimeReference;
            [FieldOffset(12)]
            public float AlphaStable;
            [FieldOffset(16)]
            public float StepSize = 1;
            public VectorField.Integrator.Type IntegrationType
            {
                get { return (VectorField.Integrator.Type)this[Element.IntegrationType]; }
                set { this[Element.IntegrationType] = (int)value; }
            }
            [FieldOffset(24)]
            public int LineX;
            [FieldOffset(28)]
            public int MemberMain;
            [FieldOffset(32)]
            public int MemberReference;
            [FieldOffset(36)]
            public RedSea.DisplayTracking Tracking;

            public Colormap Colormap
            {
                get { return (Colormap)this[Element.Colormap]; }
                set { this[Element.Colormap] = (int)value; }
            }
            public FieldPlane.RenderEffect Shader
            {
                get { return (FieldPlane.RenderEffect)this[Element.Shader]; }
                set { this[Element.Shader] = (int)value; }
            }
            [FieldOffset(44)]
            public float WindowWidth;
            [FieldOffset(48)]
            public float WindowStart;
            //[FieldOffset(52)]
            public RedSea.Measure Measure
            {
                get { return (RedSea.Measure)this[Element.Measure]; }
                set { this[Element.Measure] = (int)value; }
            }
            [FieldOffset(56)]
            public int SliceHeight;
            [FieldOffset(60)]
            public float IntegrationTime;
            public RedSea.DiffusionMeasure DiffusionMeasure
            {
                get { return (RedSea.DiffusionMeasure)this[Element.DiffusionMeasure]; }
                set { this[Element.DiffusionMeasure] = (int)value; }
            }
            public Element VarX
            {
                get { return (Element)this[Element.VarX]; }
                set { this[Element.VarX] = (int)value; }
            }
            public Element VarY
            {
                get { return (Element)this[Element.VarX]; }
                set { this[Element.VarX] = (int)value; }
            }

            [FieldOffset(76)]
            public float StartX;

            [FieldOffset(80)]
            public float StartY;

            [FieldOffset(84)]
            public float EndX;

            [FieldOffset(88)]
            public float EndY;

            [FieldOffset(92)]
            public int DimX;

            [FieldOffset(96)]
            public int DimY;

            [FieldOffset(100)]
            public Sign Flat;
            // The real data.
            [FieldOffset(0)]
            private int[] _data = new int[Enum.GetValues(typeof(Element)).Length];
            public int[] Data { get { return _data; } }
            public int this[Element idx]
            {
                get { return _data[(int)idx]; }
                set { _data[(int)idx] = value; }
            }

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
                Flat
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
            }

            public Setting() { }
        }

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
                default:
                    return "I am a severely ignored Text Field :{";
            }
        }

        public virtual int? GetLength (Setting.Element element)
        {
            switch(element)
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
                _currentSetting.SliceTimeReference != _lastSetting.SliceTimeReference)
            {
                _slice1 = new List<Renderable>(2);
                _planes[0] = new FieldPlane(Plane, SlicesToRender[_currentSetting.SliceTimeReference], FieldPlane.RenderEffect.LIC);
                _slice1.Add(_planes[0]);
                _slice1.Add(new PointCloud(Plane, CP[_currentSetting.SliceTimeReference].ToBasicSet()));
            }


            // Something mayor changed. Re-integrate.
            bool mapLines = false;
            if (_lastSetting == null ||
                _currentSetting.AlphaStable != _lastSetting.AlphaStable ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain ||
                _currentSetting.IntegrationType != _lastSetting.IntegrationType ||
                _currentSetting.StepSize != _lastSetting.StepSize)
            {
                if (_lastSetting == null || _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain)
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
                if (_lastSetting == null || _currentSetting.AlphaStable != _lastSetting.AlphaStable)
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
                LineSet cpLinesPos = intVF.Integrate(CP[_currentSetting.SliceTimeMain], false);

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
            if (mapLines || _currentSetting.LineSetting != _lastSetting.LineSetting)
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
        private LoaderNCF.SliceRange[] _variableRanges;

        public MemberComparison(LoaderNCF.SliceRange[] ranges, Plane plane)
        {
            Debug.Assert(ranges.Length == 2);
            //_ranges = ranges;
            _fields = new FieldPlane[2];
            Plane = plane;
            Mapping = LoadMembers;

            LoaderNCF ncFile = RedSea.Singleton.GetLoaderNCF(0);

            int sizeVar = ncFile.GetNumVariables();
            _variableRanges = new LoaderNCF.SliceRange[sizeVar];

            LoaderNCF.SliceRange ensembleU = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.VELOCITY_X);
            ensembleU.SetMember(RedSea.Dimension.TIME, 0);
            LoaderNCF.SliceRange ensembleV = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.VELOCITY_Y);
            ensembleV.SetMember(RedSea.Dimension.TIME, 0);
            LoaderNCF.SliceRange ensembleSal = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.SALINITY);
            ensembleSal.SetMember(RedSea.Dimension.TIME, 0);
            LoaderNCF.SliceRange ensembleTemp = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.TEMPERATURE);
            ensembleTemp.SetMember(RedSea.Dimension.TIME, 0);
            LoaderNCF.SliceRange ensembleHeight = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.SURFACE_HEIGHT);
            ensembleHeight.SetMember(RedSea.Dimension.TIME, 0);

            _variableRanges[(int)RedSea.Variable.VELOCITY_X] = ensembleU;
            _variableRanges[(int)RedSea.Variable.VELOCITY_Y] = ensembleV;
            _variableRanges[(int)RedSea.Variable.SALINITY] = ensembleSal;
            _variableRanges[(int)RedSea.Variable.TEMPERATURE] = ensembleTemp;
            _variableRanges[(int)RedSea.Variable.SURFACE_HEIGHT] = ensembleHeight;

            ncFile.Close();
        }

        private FieldPlane LoadPlane(int member, int time)
        {

            ScalarField[] scalars;// = new ScalarField[2];
            // Read in the data.
            //_ranges[0].SetMember(RedSea.Dimension.MEMBER, _currentSetting.MemberMain);
            //_ranges[1].SetMember(RedSea.Dimension.MEMBER, _currentSetting.MemberMain);

            LoaderNCF ncFile = RedSea.Singleton.GetLoaderNCF(time);
            switch (_currentSetting.Measure)
            {
                case RedSea.Measure.VELOCITY:
                case RedSea.Measure.DIVERGENCE:
                case RedSea.Measure.VORTICITY:
                case RedSea.Measure.SHEAR:
                case RedSea.Measure.DIVERGENCE_2D:
                    scalars = new ScalarField[2];

                    LoadVelocity:
                    _variableRanges[(int)RedSea.Variable.VELOCITY_X].SetMember(RedSea.Dimension.MEMBER, member);
                    _variableRanges[(int)RedSea.Variable.VELOCITY_Y].SetMember(RedSea.Dimension.MEMBER, member);
                    _variableRanges[(int)RedSea.Variable.VELOCITY_X].SetMember(RedSea.Dimension.CENTER_Z, _currentSetting.SliceHeight);
                    _variableRanges[(int)RedSea.Variable.VELOCITY_Y].SetMember(RedSea.Dimension.CENTER_Z, _currentSetting.SliceHeight);

                    scalars[0] = ncFile.LoadFieldSlice(_variableRanges[(int)RedSea.Variable.VELOCITY_X]);
                    scalars[1] = ncFile.LoadFieldSlice(_variableRanges[(int)RedSea.Variable.VELOCITY_Y]);
                    break;

                default:
                    RedSea.Measure var = _currentSetting.Measure;

                    _variableRanges[(int)var].SetMember(RedSea.Dimension.MEMBER, member);
                    if (var != RedSea.Measure.SURFACE_HEIGHT)
                        _variableRanges[(int)var].SetMember(RedSea.Dimension.CENTER_Z, _currentSetting.SliceHeight);


                    // Maybe load vector field too.
                    bool addVelocity = (_currentSetting.Shader == FieldPlane.RenderEffect.LIC || _currentSetting.Shader == FieldPlane.RenderEffect.LIC_LENGTH);
                    scalars = new ScalarField[addVelocity ? 3 : 1];
                    scalars[scalars.Length - 1] = ncFile.LoadFieldSlice(_variableRanges[(int)var]);
                    if (addVelocity)
                        goto LoadVelocity;

                    break;
            }
            ncFile.Close();

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
            // field = new VectorField(velocity, FieldAnalysis.StableFFF, 3, true);

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
                _currentSetting.MemberMain != _lastSetting.MemberMain ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain ||
                _currentSetting.Measure != _lastSetting.Measure ||
                _currentSetting.SliceHeight != _lastSetting.SliceHeight ||
                _currentSetting.Shader != _lastSetting.Shader)
            {
                _fields[0] = LoadPlane(_currentSetting.MemberMain, _currentSetting.SliceTimeMain);
                Vector2 extent = new Vector2((float)_currentSetting.LineX / _grid.Size[0], 1);
                _fields[0].SetToSubrangeFloat(Plane, _grid.Size.ToInt2(), Vector2.Zero, extent);
            }
            else if (_currentSetting.LineX != _lastSetting.LineX)
            {
                Vector2 extent = new Vector2((float)_currentSetting.LineX / _grid.Size[0], 1);
                _fields[0].SetToSubrangeFloat(Plane, _grid.Size.ToInt2(), Vector2.Zero, extent);
            }

            // Changed reference settings.
            if (_lastSetting == null ||
                _currentSetting.MemberReference != _lastSetting.MemberReference ||
                _currentSetting.SliceTimeReference != _lastSetting.SliceTimeReference ||
                _currentSetting.Measure != _lastSetting.Measure ||
                _currentSetting.SliceHeight != _lastSetting.SliceHeight ||
                _currentSetting.Shader != _lastSetting.Shader)
            {
                _fields[1] = LoadPlane(_currentSetting.MemberReference, _currentSetting.SliceTimeReference);
                Vector2 extent = new Vector2((float)_currentSetting.LineX / _grid.Size[0], 1);
                _fields[1].SetToSubrangeFloat(Plane, _grid.Size.ToInt2(), new Vector2((float)_currentSetting.LineX / _grid.Size[0], 0), new Vector2(1 - (float)(_currentSetting.LineX - 1) / _grid.Size[0], 1));
            }
            else if (_currentSetting.LineX != _lastSetting.LineX)
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
            _fieldOW = new VectorFieldUnsteady(velocity, FieldAnalysis.OkuboWeiss, 1);

            Plane = plane;
            Mapping = GetTimeSlice;

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
            if (_lastSetting == null ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain)
            {
                VectorField sliceOW = _fieldOW.GetTimeSlice(_currentSetting.SliceTimeMain);
                _fieldSlice = new FieldPlane(Plane, sliceOW, FieldPlane.RenderEffect.COLORMAP, Colormap.Heatstep);
            }
            if (_lastSetting == null ||
                _currentSetting.WindowWidth != _lastSetting.WindowWidth)
            {
                _fieldSlice.LowerBound = -0.2f * _standardDeviation - _currentSetting.WindowWidth;
                _fieldSlice.UpperBound = -0.2f * _standardDeviation + _currentSetting.WindowWidth;
            }
            if (_lastSetting == null ||
                _currentSetting.Colormap != _lastSetting.Colormap ||
                _currentSetting.Shader != _lastSetting.Shader)
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

    //class SubstepViewer : DataMapper
    //{
    //    //private Loader.SliceRange[] _ranges;
    //    private FieldPlane[] _fields;
    //    private RectlinearGrid _grid;

    //    public SubstepViewer(Plane plane)
    //    {
    //        //_ranges = ranges;
    //        _fields = new FieldPlane[2];
    //        Plane = plane;
    //        Mapping = LoadMembers;

    //    }

    //    private FieldPlane LoadPlane(int member, int time)
    //    {

    //        ScalarField[] scalars;// = new ScalarField[2];

    //        RedSea.Variable measureAsVar;
    //        switch(_currentSetting.Measure)
    //        {
    //            case RedSea.Measure.SALINITY:
    //            case RedSea.Measure.SURFACE_HEIGHT:
    //            case RedSea.Measure.TEMPERATURE:
    //                measureAsVar = (RedSea.Variable)(int)_currentSetting.Measure;
    //                break;
    //            default:
    //                measureAsVar = RedSea.Variable.VELOCITY_Z;
    //                break;
    //        }

    //        LoaderRaw file = RedSea.Singleton.GetLoader(time, 107, member, measureAsVar) as LoaderRaw;

    //        switch (_currentSetting.Measure)
    //        {
    //            //case RedSea.Measure.VELOCITY:
    //            //case RedSea.Measure.DIVERGENCE:
    //            //case RedSea.Measure.VORTICITY:
    //            //case RedSea.Measure.SHEAR:
    //            //case RedSea.Measure.DIVERGENCE_2D:
    //            //    scalars = new ScalarField[2];

    //            //    LoadVelocity:
    //            //    _variableRanges[(int)RedSea.Variable.VELOCITY_X].SetMember(RedSea.Dimension.MEMBER, member);
    //            //    _variableRanges[(int)RedSea.Variable.VELOCITY_Y].SetMember(RedSea.Dimension.MEMBER, member);
    //            //    _variableRanges[(int)RedSea.Variable.VELOCITY_X].SetMember(RedSea.Dimension.CENTER_Z, _currentSetting.SliceHeight);
    //            //    _variableRanges[(int)RedSea.Variable.VELOCITY_Y].SetMember(RedSea.Dimension.CENTER_Z, _currentSetting.SliceHeight);

    //            //    scalars[0] = ncFile.LoadFieldSlice(_variableRanges[(int)RedSea.Variable.VELOCITY_X]);
    //            //    scalars[1] = ncFile.LoadFieldSlice(_variableRanges[(int)RedSea.Variable.VELOCITY_Y]);
    //            //    break;

    //            default:
    //                RedSea.Measure var = _currentSetting.Measure;

    //                //_variableRanges[(int)var].SetMember(RedSea.Dimension.MEMBER, member);
    //                //if (var != RedSea.Measure.SURFACE_HEIGHT)
    //                //    _variableRanges[(int)var].SetMember(RedSea.Dimension.CENTER_Z, _currentSetting.SliceHeight);


    //                // Maybe load vector field too.
    //                //bool addVelocity = (_currentSetting.Shader == FieldPlane.RenderEffect.LIC || _currentSetting.Shader == FieldPlane.RenderEffect.LIC_LENGTH);
    //                //scalars = new ScalarField[addVelocity ? 3 : 1];
    //                //scalars[scalars.Length - 1] = ncFile.LoadFieldSlice(_variableRanges[(int)var]);
    //                //if (addVelocity)
    //                //    goto LoadVelocity;

    //                ScalarField w = file.LoadField();
    //                scalars = new ScalarField[] { w };
    //                break;
    //        }

    //        VectorField field;
    //        switch (_currentSetting.Measure)
    //        {
    //            case RedSea.Measure.DIVERGENCE:
    //                {
    //                    VectorField vel = new VectorField(scalars);

    //                    bool keepField = _currentSetting.Shader == FieldPlane.RenderEffect.LIC;
    //                    scalars = new ScalarField[keepField ? 3 : 1];
    //                    scalars[scalars.Length - 1] = new VectorField(vel, FieldAnalysis.Divergence, 1, true).Scalars[0] as ScalarField;

    //                    if (keepField)
    //                    {
    //                        scalars[0] = vel.Scalars[0] as ScalarField;
    //                        scalars[1] = vel.Scalars[1] as ScalarField;
    //                    }
    //                    break;
    //                }
    //            case RedSea.Measure.DIVERGENCE_2D:
    //                {
    //                    VectorField vel = new VectorField(scalars);
    //                    scalars = new VectorField(vel, FieldAnalysis.Div2D, 2, true).Scalars as ScalarField[];
    //                    break;
    //                }
    //            case RedSea.Measure.VORTICITY:
    //                {
    //                    VectorField vel = new VectorField(scalars);

    //                    bool keepField = _currentSetting.Shader == FieldPlane.RenderEffect.LIC;
    //                    scalars = new ScalarField[keepField ? 3 : 1];
    //                    scalars[scalars.Length - 1] = new VectorField(vel, FieldAnalysis.Vorticity, 1, true).Scalars[0] as ScalarField;

    //                    if (keepField)
    //                    {
    //                        scalars[0] = vel.Scalars[0] as ScalarField;
    //                        scalars[1] = vel.Scalars[1] as ScalarField;
    //                    }
    //                    break;
    //                }
    //            case RedSea.Measure.SHEAR:
    //                {
    //                    VectorField vel = new VectorField(scalars);

    //                    bool keepField = _currentSetting.Shader == FieldPlane.RenderEffect.LIC;
    //                    scalars = new ScalarField[keepField ? 3 : 1];
    //                    scalars[scalars.Length - 1] = new VectorField(vel, FieldAnalysis.Shear, 1, true).Scalars[0] as ScalarField;

    //                    if (keepField)
    //                    {
    //                        scalars[0] = vel.Scalars[0] as ScalarField;
    //                        scalars[1] = vel.Scalars[1] as ScalarField;
    //                    }
    //                    break;
    //                }
    //            default:
    //                break;
    //        }
    //        field = new VectorField(scalars);

    //        _grid = field.Grid as RectlinearGrid;

    //        return new FieldPlane(Plane, field, _currentSetting.Shader, _currentSetting.Colormap);
    //    }

    //    /// <summary>
    //    /// If different planes were chosen, load new fields.
    //    /// </summary>
    //    /// <returns></returns>
    //    public List<Renderable> LoadMembers()
    //    {
    //        // Changed main slice settings.
    //        if (_lastSetting == null ||
    //            _currentSetting.MemberMain != _lastSetting.MemberMain ||
    //            _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain ||
    //            _currentSetting.Measure != _lastSetting.Measure ||
    //            _currentSetting.SliceHeight != _lastSetting.SliceHeight ||
    //            _currentSetting.Shader != _lastSetting.Shader)
    //        {
    //            _fields[0] = LoadPlane(_currentSetting.MemberMain, _currentSetting.SliceTimeMain);
    //            Vector2 extent = new Vector2((float)_currentSetting.LineX / _grid.Size[0], 1);
    //            _fields[0].SetToSubrangeFloat(Plane, _grid.Size.ToInt2(), Vector2.Zero, extent);
    //        }
    //        else if (_currentSetting.LineX != _lastSetting.LineX)
    //        {
    //            Vector2 extent = new Vector2((float)_currentSetting.LineX / _grid.Size[0], 1);
    //            _fields[0].SetToSubrangeFloat(Plane, _grid.Size.ToInt2(), Vector2.Zero, extent);
    //        }

    //        // Changed reference settings.
    //        if (_lastSetting == null ||
    //            _currentSetting.MemberReference != _lastSetting.MemberReference ||
    //            _currentSetting.SliceTimeReference != _lastSetting.SliceTimeReference ||
    //            _currentSetting.Measure != _lastSetting.Measure ||
    //            _currentSetting.SliceHeight != _lastSetting.SliceHeight ||
    //            _currentSetting.Shader != _lastSetting.Shader)
    //        {
    //            _fields[1] = LoadPlane(_currentSetting.MemberReference, _currentSetting.SliceTimeReference);
    //            Vector2 extent = new Vector2((float)_currentSetting.LineX / _grid.Size[0], 1);
    //            _fields[1].SetToSubrangeFloat(Plane, _grid.Size.ToInt2(), new Vector2((float)_currentSetting.LineX / _grid.Size[0], 0), new Vector2(1 - (float)(_currentSetting.LineX - 1) / _grid.Size[0], 1));
    //        }
    //        else if (_currentSetting.LineX != _lastSetting.LineX)
    //        {
    //            _fields[1].SetToSubrangeFloat(Plane, _grid.Size.ToInt2(), new Vector2((float)_currentSetting.LineX / _grid.Size[0], 0), new Vector2(1 - (float)(_currentSetting.LineX - 1) / _grid.Size[0], 1));
    //        }

    //        // Update window with to shader.
    //        //float winMin, winMax;
    //        //switch (_currentSetting.Shader)
    //        //{
    //        //    case FieldPlane.RenderEffect.LIC_LENGTH:
    //        //        winMin = 0;
    //        //        winMax = _currentSetting.WindowWidth;
    //        //        break;
    //        //    default:
    //        //        winMin = -_currentSetting.WindowWidth / 2;
    //        //        winMax = _currentSetting.WindowWidth / 2;
    //        //        break;
    //        //}
    //        // Set mapping values.
    //        _fields[0].LowerBound = _currentSetting.WindowStart;
    //        _fields[0].UpperBound = _currentSetting.WindowWidth + _currentSetting.WindowStart;
    //        _fields[0].SetRenderEffect(_currentSetting.Shader);
    //        _fields[0].UsedMap = _currentSetting.Colormap;

    //        _fields[1].LowerBound = _fields[0].LowerBound;
    //        _fields[1].UpperBound = _fields[0].UpperBound;
    //        _fields[1].SetRenderEffect(_currentSetting.Shader);
    //        _fields[1].UsedMap = _currentSetting.Colormap;

    //        return _fields.ToList<Renderable>();
    //    }

    //    public override bool IsUsed(Setting.Element element)
    //    {
    //        switch (element)
    //        {
    //            case Setting.Element.Colormap:
    //            case Setting.Element.WindowWidth:
    //            case Setting.Element.WindowStart:
    //                return !(_currentSetting.Shader == FieldPlane.RenderEffect.CHECKERBOARD);
    //            case Setting.Element.AlphaStable:
    //            case Setting.Element.IntegrationType:
    //            case Setting.Element.LineSetting:
    //            case Setting.Element.StepSize:
    //            case Setting.Element.Tracking:
    //                return false;
    //            default:
    //                return true;
    //        }
    //    }

    //    public override string GetName(Setting.Element element)
    //    {
    //        switch(element)
    //        {
    //            case Setting.Element.LineX:
    //                return "Global Substep";
    //            default:
    //                return base.GetName(element);
    //        }
    //    }
    //}

    /// <summary>
    /// Integrate a number of pathlines from selected point on.
    /// </summary>
    class PathlineRadius : DataMapper
    {
        private VectorFieldUnsteady _velocity;
        //private Plane _plane;
        private Vector2 _selection;

        private LineBall _pathlines;
        private FieldPlane _timeSlice;

        private bool _selectionChanged = false;
        public PathlineRadius(VectorFieldUnsteady velocity, Plane plane)// : base(plane, velocity.Size.ToInt2())
        {
            _velocity = velocity;
            Plane = plane;
            _intersectionPlane = new Plane(Plane, (velocity.TimeSlice??0) * Plane.ZAxis);

            Mapping = AdvectLines;
        }

        public override void ClickSelection(Vector2 pos)
        {
            _selection = pos;
            _selectionChanged = true;
        }

        public List<Renderable> AdvectLines()
        {
            List<Renderable> renderables = new List<Renderable>(3);
            int numLines = _currentSetting.LineX;

            // Update / create underlying plane.
            if (_lastSetting == null ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain)
            {
                _timeSlice = new FieldPlane(Plane, _velocity.GetTimeSlice(_currentSetting.SliceTimeMain), _currentSetting.Shader, _currentSetting.Colormap);
                _intersectionPlane = new Plane(_intersectionPlane, new Vector3(0, 0, _currentSetting.SliceTimeMain - (_lastSetting?.SliceTimeMain)??0));
            }
            else if(_currentSetting.Colormap != _lastSetting.Colormap ||
                _currentSetting.Shader != _lastSetting.Shader)
            {
                _timeSlice.SetRenderEffect(_currentSetting.Shader);
                _timeSlice.UsedMap = _currentSetting.Colormap;
            }
            // First item in list: plane.
            renderables.Add(_timeSlice);


            // Add Point to indicate clicked position.
            renderables.Add(new PointCloud(Plane, new PointSet<Point>(new Point[] { new Point() { Position = new Vector3(_selection, 0) + Plane.ZAxis * (_currentSetting.SliceTimeMain + _velocity.Grid.TimeOrigin??0), Color = new Vector3(1, 0, 1), Radius = 0.5f } })));

            // Recompute lines if necessary.
            if (_lastSetting == null ||
                _currentSetting.LineX != _lastSetting.LineX ||
                _currentSetting.AlphaStable != _lastSetting.AlphaStable ||
                _currentSetting.StepSize != _lastSetting.StepSize ||
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
                        float x = (float)(Math.Sin(angleDiff * dir));
                        float y = (float)(Math.Cos(angleDiff * dir));
                        circle[dir] = new Point() { Position = new Vector3(_selection.X + x * offset, _selection.Y + y * offset, _currentSetting.SliceTimeMain) };
                    }

                    VectorField.Integrator integrator = VectorField.Integrator.CreateIntegrator(_velocity, _currentSetting.IntegrationType);
                    integrator.StepSize = _currentSetting.StepSize;
                    bool pos = _velocity.SampleDerivative(new Vec3((Vec2)_selection, _currentSetting.SliceTimeMain)).EigenvaluesReal()[0] > 0;
                    integrator.Direction = pos ? Sign.POSITIVE : Sign.NEGATIVE;

                    LineSet lines = integrator.Integrate<Point>(new PointSet<Point>(circle), false);
                    lines.FlattenLines(_currentSetting.SliceTimeMain);
                    _pathlines = new LineBall(Plane, lines);
                }
                _selectionChanged = false;  
            }
            // Add the lineball.
            if(_pathlines!= null)
                renderables.Add(_pathlines);
            return renderables;
        }

        public override bool IsUsed(Setting.Element element)
        {
            switch(element)
            {
                case Setting.Element.DiffusionMeasure:
                case Setting.Element.IntegrationTime:
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
            switch(element)
            {
                case Setting.Element.AlphaStable:
                    return "Offset Start Point";
                case Setting.Element.LineX:
                    return "Number of Lines";
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
