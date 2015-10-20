using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SlimDX;

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

        /// <summary>
        /// A class encapsulating all settings that are currently possible via WPF.
        /// </summary>
        public class Setting
        {
            public RedSea.DisplayLines LineSetting = RedSea.DisplayLines.LINE;
            public int SlicePositionMain = 0;
            public int SlicePositionReference = 1;
            public float AlphaStable = 0;
            public float StepSize = 1;
            public VectorField.Integrator.Type IntegrationType = VectorField.Integrator.Type.EULER;
            //public RedSea.Display SetFunction;

            public Setting(Setting cpy)
            {
                LineSetting = cpy.LineSetting;
                SlicePositionMain = cpy.SlicePositionMain;
                SlicePositionReference = cpy.SlicePositionReference;
                AlphaStable = cpy.AlphaStable;
                IntegrationType = cpy.IntegrationType;
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
        public VectorFieldUnsteady Velocity;
        //private VectorField[] _sliceFields;

        private List<Renderable> _slice0;
        private List<Renderable> _slice1;
        private List<LineSet> _rawLines;
        private List<Renderable> _lines;

        public CriticalPointTracking(CriticalPointSet2D[] cp, VectorFieldUnsteady velocity, /*VectorField fffPos, VectorField fffNeg,*/ Plane plane)
        {
            Debug.Assert(cp.Length == velocity.Size.T);
            CP = cp;
            //ForwardFFF = fffPos;
            //BackwardFFF = fffNeg;
            Velocity = velocity;
            //for (int slice = 0; slice < velocity.Size.T; ++slice)
            //{
            //    _sliceFields[slice] = velocity.GetSlice(slice);
            //}
            Mapping = TrackCP;
            Plane = plane;
        }

        public List<Renderable> TrackCP()
        {
            // The reference slice was changed. Update the field and its critical points.
            if (_lastSetting == null ||
                _currentSetting.SlicePositionReference != _lastSetting.SlicePositionReference)
            {
                _slice1 = new List<Renderable>(2);
                _slice1.Add(new FieldPlane(Plane, Velocity.GetTimeSlice(_currentSetting.SlicePositionReference), FieldPlane.RenderEffect.LIC));
                _slice1.Add(new PointCloud(Plane, CP[_currentSetting.SlicePositionReference].ToBasicSet()));
            }

            // Something mayor changed. Re-integrate.
            bool mapLines = false;
            if (_lastSetting == null ||
                _currentSetting.AlphaStable != _lastSetting.AlphaStable || 
                _currentSetting.SlicePositionMain != _lastSetting.SlicePositionMain || 
                _currentSetting.IntegrationType != _lastSetting.IntegrationType || 
                _currentSetting.StepSize != _lastSetting.StepSize)
            {
                if (_lastSetting == null || _currentSetting.SlicePositionMain != _lastSetting.SlicePositionMain)
                {
                    // Clear the slice mapping.
                    _slice0 = new List<Renderable>(2);

                    // ~~~~~~~~~~~~ Field Mapping ~~~~~~~~~~~~~ \\
                    _slice0.Add(new FieldPlane(Plane, Velocity.GetTimeSlice(_currentSetting.SlicePositionMain), FieldPlane.RenderEffect.LIC));

                    // ~~~~~~~~ Critical Point Mapping ~~~~~~~~ \\
                    _slice0.Add(new PointCloud(Plane, CP[_currentSetting.SlicePositionMain].ToBasicSet()));
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
                intVF.MaxNumSteps = 1000;
                intVF.StepSize = _currentSetting.StepSize;
                intVF.WorldPosition = false;

                // Integrate the forward field.
                LineSet cpLinesPos = intVF.Integrate(CP[_currentSetting.SlicePositionMain], false);

                // Negative FFF integration. Reversed stabilising field.
                //intVF.Direction = Sign.NEGATIVE;
                intVF.Field = BackwardFFF;
                var cpLinesNeg = intVF.Integrate(CP[_currentSetting.SlicePositionMain], false);
                cpLinesNeg.Color = new Vector3(0, 0.8f, 0);

                // Add the data to the list.
                _rawLines.Add(cpLinesPos);
                _rawLines.Add(cpLinesNeg);
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
    
            return _slice0.Concat(_slice1).Concat(_lines).ToList();
        }
    }

    class EmptyMapper : DataMapper
    {
        public EmptyMapper()
        {
            Mapping = (() => new List<Renderable>(0));
            _currentSetting = new Setting();
        }
    }
}
