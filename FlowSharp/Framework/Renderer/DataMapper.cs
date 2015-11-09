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

        public Setting CurrentSetting { get { return _currentSetting; } set { _currentSetting = value; } }
        protected Setting _currentSetting;
        protected Setting _lastSetting;

        public void UpdateMapping()
        {
            Renderer.Singleton.AddRenderables( Mapping() );
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
                set {this[Element.LineSetting] = (int)value; }
            }
            [FieldOffset(4)]
            public int SliceTimeMain;
            [FieldOffset(8)]
            public int SliceTimeReference;
            [FieldOffset(12)]
            public float AlphaStable;
            [FieldOffset(16)]
            public float StepSize;
            public VectorField.Integrator.Type IntegrationType
            {
                get { return (VectorField.Integrator.Type)this[Element.IntegrationType]; }
                set {this[Element.IntegrationType] = (int)value; }
            }
            [FieldOffset(24)]
            public int LineX;
            [FieldOffset(28)]
            public int MemberMain;
            [FieldOffset(32)]
            public int MemberReference;
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
                WindowWidth
            }

            public Setting(Setting cpy)
            {
                //Array.Copy(cpy.Data, _data, cpy.Data.Length);
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
            }

            public Setting() { }
        }

        public ViewFunction Mapping;

        protected DataMapper()
        {
            CurrentSetting = new Setting();
        }
    }

    class CriticalPointTracking : DataMapper
    {
        public CriticalPointSet2D[] CP;
        public VectorField ForwardFFF, BackwardFFF;
        public Plane Plane;
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
                intVF.WorldPosition = false;
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

                switch(_currentSetting.LineSetting)
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
                        foreach(LineSet line in _rawLines)
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
            switch(element)
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

    class PathlineCoreTracking  : CriticalPointTracking
    {
        public PathlineCoreTracking(VectorFieldUnsteady velocity, /*VectorField fffPos, VectorField fffNeg,*/ Plane plane)
        {
            Velocity = new VectorField(velocity, FieldAnalysis.PathlineCore, 3);
            CP = new CriticalPointSet2D[velocity.Size.T];
            for(int slice = 0; slice < velocity.Size.T; ++slice)
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
        private Loader.SliceRange[] _ranges;
        public Plane Plane;
        private FieldPlane[] _fields;
        private RectlinearGrid _grid;

        public MemberComparison(Loader.SliceRange[] ranges, Plane plane)
        {
            Debug.Assert(ranges.Length == 2);
            _ranges = ranges;
            _fields = new FieldPlane[2];
            Plane = plane;
            Mapping = LoadMembers;
        }

        /// <summary>
        /// If different planes were chosen, load new fields.
        /// </summary>
        /// <returns></returns>
        public List<Renderable> LoadMembers()
        {
            if (_lastSetting == null || 
                _currentSetting.MemberMain != _lastSetting.MemberMain ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain)
            {
                ScalarField[] uv = new ScalarField[2];
                string main = RedSea.Singleton.DataFolder + (_currentSetting.SliceTimeMain + 1) + RedSea.Singleton.FileName;
                // Read in the data.
                _ranges[0].SetMember(RedSea.Dimension.MEMBER, _currentSetting.MemberMain);
                _ranges[1].SetMember(RedSea.Dimension.MEMBER, _currentSetting.MemberMain);

                Loader ncFile = new Loader(main);
                uv[0] = ncFile.LoadFieldSlice(_ranges[0]);
                uv[1] = ncFile.LoadFieldSlice(_ranges[1]);
                ncFile.Close();

                VectorField field = new VectorField(uv);
                _grid = field.Grid as RectlinearGrid;

                _fields[0] = new FieldPlane(Plane, field, _currentSetting.Shader, _currentSetting.Colormap);
                Vector2 extent = new Vector2((float)_currentSetting.LineX / _grid.Size[0], 1);
                _fields[0].SetToSubrangeFloat(Plane, _grid as RectlinearGrid, Vector2.Zero, extent);
            }
            else if(_currentSetting.LineX != _lastSetting.LineX)
            {
                Vector2 extent = new Vector2((float)_currentSetting.LineX / _grid.Size[0], 1);
                _fields[0].SetToSubrangeFloat(Plane, _grid as RectlinearGrid, Vector2.Zero, extent);
            }

            if (_lastSetting == null || 
                _currentSetting.MemberReference != _lastSetting.MemberReference ||
                _currentSetting.SliceTimeReference != _lastSetting.SliceTimeReference)
            {
                ScalarField[] uv = new ScalarField[2];
                string main = RedSea.Singleton.DataFolder + (_currentSetting.SliceTimeReference+ 1) + RedSea.Singleton.FileName;
                // Read in the data.                
                _ranges[0].SetMember(RedSea.Dimension.MEMBER, _currentSetting.MemberReference);
                _ranges[1].SetMember(RedSea.Dimension.MEMBER, _currentSetting.MemberReference);

                Loader ncFile = new Loader(main);
                Loader.SliceRange sliceU = new Loader.SliceRange(ncFile, RedSea.Variable.VELOCITY_X);
                uv[0] = ncFile.LoadFieldSlice(_ranges[0]);
                uv[1] = ncFile.LoadFieldSlice(_ranges[1]);
                ncFile.Close();

                VectorField field = new VectorField(uv);

                _fields[1] = new FieldPlane(Plane, field, _currentSetting.Shader, _currentSetting.Colormap);
                _fields[1].SetToSubrangeFloat(Plane, _grid, new Vector2((float)_currentSetting.LineX / _grid.Size[0], 0), new Vector2(1 - (float)(_currentSetting.LineX-1) / _grid.Size[0], 1));
            }
            else if (_currentSetting.LineX != _lastSetting.LineX)
            {
                _fields[1].SetToSubrangeFloat(Plane, _grid, new Vector2((float)_currentSetting.LineX / _grid.Size[0], 0), new Vector2(1 - (float)(_currentSetting.LineX-1) / _grid.Size[0], 1));
            }

            // Set mapping values.
            _fields[0].LowerBound = -_currentSetting.WindowWidth / 2;
            _fields[0].UpperBound = _currentSetting.WindowWidth/2;
            _fields[0].SetRenderEffect(_currentSetting.Shader);
            _fields[0].UsedMap = _currentSetting.Colormap;

            _fields[1].LowerBound = -_currentSetting.WindowWidth / 2;
            _fields[1].UpperBound = _currentSetting.WindowWidth / 2;
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
                    return (_currentSetting.Shader == FieldPlane.RenderEffect.COLORMAP || _currentSetting.Shader == FieldPlane.RenderEffect.DEFAULT);
                case Setting.Element.AlphaStable:
                case Setting.Element.IntegrationType:
                case Setting.Element.LineSetting:
                case Setting.Element.StepSize:
                    return false;
                default:
                    return true;
            }
        }
    }

    class OkuboWeiss : DataMapper
    {
        public Plane Plane;
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
                _currentSetting.StepSize != _lastSetting.StepSize)
            {
                _fieldSlice.LowerBound = -0.2f * _standardDeviation - _currentSetting.StepSize;
                _fieldSlice.UpperBound = -0.2f * _standardDeviation + _currentSetting.StepSize;
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
            _fieldSlice.UpperBound = _currentSetting.WindowWidth;
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

    class FlowMapMapper : DataMapper
    {
        public Plane Plane;
        private FieldPlane _currentState;
        private FlowMapUncertain _flowMap;
        private VectorFieldUnsteady _velocity;

        public FlowMapMapper(Loader.SliceRange[] uv, Plane plane, VectorFieldUnsteady velocity)
        {
            _flowMap = new FlowMapUncertain(new Int2(120, 40), uv, 0, 9);
            Plane = plane;
            Mapping = GetCurrentMap;
            _velocity = velocity;
        }

        /// <summary>
        /// If different planes were chosen, load new fields.
        /// </summary>
        /// <returns></returns>
        public List<Renderable> GetCurrentMap()
        {
            if (_lastSetting == null ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain)
            {
                if (_lastSetting == null || _currentSetting.SliceTimeMain < _flowMap.CurrentTime)
                {
                    _flowMap.SetupPoint(new Int2(130, 30), _currentSetting.SliceTimeMain);
                }

                // Integrate to the desired time step.
                while(_flowMap.CurrentTime < _currentSetting.SliceTimeMain)
                    _flowMap.Step(_currentSetting.StepSize);

                 //.GetPlane(Plane);
                //_currentState = _flowMap.GetPlane(Plane);

            }
            if (_lastSetting == null ||
                _currentSetting.StepSize != _lastSetting.StepSize)
            {
            }
            if (_lastSetting == null ||
                _currentSetting.Colormap != _lastSetting.Colormap ||
                _currentSetting.Shader != _lastSetting.Shader ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain)
            {
                switch(_currentSetting.Shader)
                {
                    case FieldPlane.RenderEffect.LIC:
                        var tmp = _velocity.GetTimeSlice(_currentSetting.SliceTimeMain);
                        tmp.TimeSlice = null;
                        _currentState = new FieldPlane(Plane, tmp, _currentSetting.Shader, _currentSetting.Colormap);
                        _currentState.AddScalar(_flowMap.FlowMap);
                        break;
                    default:
                        _currentState = _flowMap.GetPlane(Plane);
                        _currentState.UsedMap = _currentSetting.Colormap;
                        _currentState.SetRenderEffect(_currentSetting.Shader);
                        break;
                }
                _currentState.LowerBound = 0;
                _currentState.UpperBound = _currentSetting.WindowWidth;
            }
            if(_lastSetting == null ||
                _lastSetting.WindowWidth != _currentSetting.WindowWidth)
            {
                _currentState.LowerBound = 0;
                _currentState.UpperBound = _currentSetting.WindowWidth;
            }
            List<Renderable> list = new List<Renderable>(1);
            list.Add(_currentState);
            return list;
        }
        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.Colormap:
                case Setting.Element.WindowWidth:
                    //return _currentSetting.Shader == FieldPlane.RenderEffect.COLORMAP || _currentSetting.Shader == FieldPlane.RenderEffect.DEFAULT;
                case Setting.Element.SliceTimeMain:
                case Setting.Element.Shader:
                case Setting.Element.StepSize:
                    return true;
                default:
                    return false;
            }
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
    }
}
