using SlimDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class PlaygroundMapper : SelectionMapper
    {
        private static int NUM_CELLS = 200;
        List<LineBall> _selections;
        List<LineBall> _selectionsAngle;
        int _lastSelection = 0;
        VectorFieldUnsteady _velocity;
        FieldPlane _bg;
        FieldPlane _steadyBG;
        VectorField.Integrator _integrator;
        LineBall _lines;
        bool _flipColor = true;
        Line _core;
        LineBall _coreBall;
        LineBall _straightCoreBall;

        VectorField _steadyField;
        List<LineBall> _steadySelection;

        public PlaygroundMapper(Plane plane) : base(new Plane(plane, 1), new Int2(NUM_CELLS))
        {
            Mapping = Map;
            _velocity = Tests.CreateBowl(new Vec2(NUM_CELLS/2, 0), NUM_CELLS, new Vec2(-10, 0), 22, 200);
            _selections = new List<LineBall>(10);
            _selectionsAngle = new List<LineBall>(10);
            _steadySelection = new List<LineBall>(10);

            // Core.
            _core = new Line() { Positions = new Vector3[] { new Vector3(50, 100, 0), new Vector3(155, 100, _velocity.Size.T-1) } };
            LineSet set = new LineSet(new Line[] { _core }) { Color = new Vector3(0.2f) };
            set.Thickness *= 3;
            _coreBall = new LineBall(plane, set, LineBall.RenderEffect.DEFAULT);

            // Straight core.
            set = new LineSet(new Line[] { new Line() { Positions = new Vector3[] { _core[0], _core[0] + Vector3.UnitZ * (_velocity.Size.T - 1) } } }) { Color = new Vector3(0.2f) };
            set.Thickness *= 3;
            _straightCoreBall = new LineBall(Plane, set);

            var center = Tests.CreatePerfect(new Vec2(0, 0), NUM_CELLS, new Vec2(0), 1, 200);
            _steadyField = center.GetTimeSlice(0); //[0] as ScalarField;
        }

        public List<Renderable> Map()
        {
            List<Renderable> result = new List<Renderable>(10);

            #region BackgroundPlane
            if (_bg == null)
            {
                _bg = new FieldPlane(Plane, _velocity.GetTimeSlice(0), Shader, Colormap);
                _steadyBG = new FieldPlane(Plane, _steadyField, Shader, Colormap);
            }

            _bg.SetRenderEffect(Shader);
            _bg.UsedMap = Colormap;
            _bg.LowerBound = WindowStart;
            _bg.UpperBound = WindowStart + WindowWidth;

            _steadyBG.SetRenderEffect(Shader);
            _steadyBG.UsedMap = Colormap;
            _steadyBG.LowerBound = WindowStart;
            _steadyBG.UpperBound = WindowStart + WindowWidth;

            result.Add(Graph? _steadyBG : _bg);
            #endregion BackgroundPlane

            if(_lastSetting == null || IntegrationTypeChanged)
            {

                _integrator = VectorField.Integrator.CreateIntegrator(_velocity, IntegrationType);
                _integrator.StepSize = StepSize;
            }

            _integrator.StepSize = StepSize;

            if(_lastSetting != null && LineXChanged)
            {

                int diff = _selections.Count - LineX;
                if (diff > 0)
                {
                    _selections.RemoveRange(0, diff);
                    _selectionsAngle.RemoveRange(0, diff);
                    //var set = new LineSet(_selections.ToArray()) { Color = _selections.Count % 2 == 0 ? Vector3.UnitX : Vector3.UnitY };
                    //_lines = new LineBall(Plane, set, LineBall.RenderEffect.DEFAULT, Colormap, false);

                }

            }

            if (Flat)
            {
                result.AddRange(_selectionsAngle);
                result.Add(_straightCoreBall);
            }
            else if(!Graph)
            {
                result.AddRange(_selections);
                result.Add(_coreBall);
            }

            if(Graph)
            {
                result.AddRange(_steadySelection);
            }

            return result;
        }

        public override void ClickSelection(Vector2 point)
        {
            if(LineX > 0)
            {
                _lastSelection = (_lastSelection + 1) % LineX;
                Vec3 vec = new Vec3((Vec2)point, 0);
                if (_velocity.Grid.InGrid(vec))
                {
                    Vector3[] line = _integrator.IntegrateLineForRendering(vec).Points.ToArray();
                    Line newLine = new Line() { Positions = line };
                    var set = new LineSet(new Line[] { newLine }) { Color = _flipColor? Vector3.UnitX : Vector3.UnitZ};
                    
                    set.Thickness *= 3;
                    var ball = new LineBall(Plane, set, LineBall.RenderEffect.DEFAULT, Colormap, false);
                    _selections.Add(ball);

                    if (_selections.Count > LineX)
                    {
                        _selections.RemoveAt(0);
                        _selectionsAngle.RemoveAt(0);
                        _steadySelection.RemoveAt(0);
                    }

                    Graph2D[] angle = FieldAnalysis.GetDistanceToAngle(_core, Vector2.Zero, new LineSet(new Line[] { newLine }));
                    Debug.Assert(angle.Length == 1);

                    for(int p = 0; p < newLine.Length; ++p)
                    {
                    //    if(angle[0].X[p] > _velocity.Size.T - 1)
                    //    {
                    //        newLine.Resize(p);
                    //        break;
                    //    }
                        
                        Vector3 sph = FieldAnalysis.SphericalPosition(_core[0], (float)(angle[0].X[p] * 0.5f / Math.PI), angle[0].Fx[p]);
                        newLine[p] = new Vector3(sph.X, sph.Y, angle[0].X[p] - angle[0].X[0]);
                    }
                    set = new LineSet(new Line[] { newLine }) { Color = _flipColor ? Vector3.UnitX : Vector3.UnitZ };
                    set.Thickness *= 3;
                    ball = new LineBall(Plane, set, LineBall.RenderEffect.DEFAULT, Colormap, false);
                    _selectionsAngle.Add(ball);

                    _integrator.Field = _steadyField;
                    _integrator.MaxNumSteps = 50;
                    line = _integrator.IntegrateLineForRendering(new Vec2(vec.X, vec.Y)).Points.ToArray();
                    newLine = new Line() { Positions = line };
                    set = new LineSet(new Line[] { newLine }) { Color = _flipColor ? Vector3.UnitX : Vector3.UnitZ };
                    set.Thickness *= 3;
                    ball = new LineBall(new Plane(Plane, Vector3.UnitZ * 0.1f), set, LineBall.RenderEffect.DEFAULT, Colormap, false);
                    _steadySelection.Add(ball);
                    _integrator.Field = _velocity;
                    _integrator.MaxNumSteps = 10000;

                    _flipColor = !_flipColor;
                    //var set = new LineSet(_selections.ToArray()) { Color = _selections.Count % 2 == 0 ? Vector3.UnitX : Vector3.UnitY };
                    //_lines = new LineBall(Plane, set, LineBall.RenderEffect.DEFAULT, Colormap, false);
                }
            }
        }

        public override void EndSelection(Vector2[] points)
        {
        }

        public override bool IsUsed(Setting.Element element)
        {
            return true;
        }
    }
}
