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
    class CoreOkuboMapper : CoreDistanceMapper
    {
        protected override int _numSeeds
        {
            get
            {
                return (int)(LineX * Math.PI);
            }
        }
        protected Graph2D[] _okubo;
        protected bool _rebuilt = false;
        public CoreOkuboMapper(int everyNthField, Plane plane) : base(everyNthField, plane)
        {
            Mapping = Map;
            _linePlane = new Plane(_linePlane.Origin, _linePlane.XAxis, _linePlane.YAxis, _linePlane.ZAxis, 1.0f);
        }

        protected void IntegrateCircles(float[] radii, float[] angles, out Graph2D[] okuboData)
        {
            float integrationLength = 40; // IntegrationTime;

            // ~~~~~~~~~~~~~~~~~~ Initialize seed points. ~~~~~~~~~~~~~~~~~~~~ \\
            PointSet<Point> circle = new PointSet<Point>(new Point[radii.Length * angles.Length * 4]);
            okuboData = new Graph2D[angles.Length];
            if (_velocity.TimeOrigin > SliceTimeMain || _velocity.TimeOrigin + _velocity.Size.T < SliceTimeMain)
                LoadField(SliceTimeMain, MemberMain, 1);

            var sliceVelocity = _velocity.GetTimeSlice(SliceTimeMain);

            float min = float.MaxValue;
            float max = float.MinValue;

            for (int angle = 0; angle < angles.Length; ++angle)
            {
                okuboData[angle] = new Graph2D(radii.Length);
                for (int rad = 0; rad < radii.Length; ++rad)
                {
                    okuboData[angle].X[rad] = radii[rad];
                    float x = (float)(Math.Sin(angles[angle] + Math.PI / 2));
                    float y = (float)(Math.Cos(angles[angle] + Math.PI / 2));
                    Vec2 pos = new Vec2(_selection.X + x * radii[rad], _selection.Y + y * radii[rad]);

                    if (!sliceVelocity.Grid.InGrid(pos) || !sliceVelocity.IsValid(pos))
                    {
                        okuboData[angle].Fx[rad] = 1;
                        continue;
                    }
                    Vector v = sliceVelocity.Sample(pos);
                    if (v[0] == sliceVelocity.InvalidValue)
                    {
                        okuboData[angle].Fx[rad] = 1;
                        continue;
                    }
                    SquareMatrix J = sliceVelocity.SampleDerivative(pos);

                    float ow = FieldAnalysis.OkuboWeiss(v, J)[0];
                    okuboData[angle].Fx[rad] = ow;
                    min = Math.Min(min, ow);
                    max = Math.Max(max, ow);
                }
            }
            Console.WriteLine("Okubo range [{0},{1}]", min, max);
        }

        protected override void FindBoundary()
        {
            if (LoadGraph("Okubo", out _okubo))
            {
                _graph = new LineBall(_graphPlane, _graphData, LineBall.RenderEffect.HEIGHT, Colormap, Flat, SliceTimeMain);
                return;
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

            Console.WriteLine($"=== Working on Okubo Slice {0} ===", SliceTimeMain);
            IntegrateCircles(offsets, angles, out _okubo);
            //LineSet okuboLines = FieldAnalysis.WriteGraphToSun(_okubo, new Vector3(_selection, SliceTimeMain));
            _rebuilt = true;

            BuildGraph();

            WriteGraph("Okubo", _okubo);
        }
        protected void BuildGraph()
        {
            // Compute ftle.
            if (LineX == 0)
                return;

            _graphData = FieldAnalysis.WriteGraphToSun(_okubo, new Vector3(_selection.X, _selection.Y, 0));
            _graph = new LineBall(_graphPlane, _graphData, LineBall.RenderEffect.HEIGHT, Colormap, Flat, SliceTimeMain);
            _graph.LowerBound = -0.2f;
            _graph.UpperBound = 0.2f;

            //float min = float.MaxValue;
            //float max = float.MinValue;
            //foreach (var g in _okubo)
            //    foreach (var fx in g.Fx)
            //    {
            //        min = Math.Min(fx, min);
            //        max = Math.Max(fx, max);
            //    }

            //Console.WriteLine($"Min value: {0}\nMax value: {1}", min, max);
            _rebuilt = false;
        }

        protected override void UpdateBoundary()
        {
            if (_lastSetting != null && (_rebuilt || FlatChanged || GraphChanged) && (Flat && !Graph))
            {
                _rebuilt = false;
            }
            if (_lastSetting != null && (FlatChanged || IntegrationTimeChanged || _rebuilt || Graph && GraphChanged && Flat))
                BuildGraph();
        }
        protected override void TraceCore(int member = 0, int startSubstep = 0)
        {
            LoadCoreBySelection(member, startSubstep);
        }
        public override void ClickSelection(Vector2 pos)
        {
            base.ClickSelection(pos);
            Console.WriteLine("Selected core: " + _selectedCore);
        }

        public override string GetName(Setting.Element element)
        {
            if (element == Setting.Element.IntegrationTime)
                return "Integration Time";
            return base.GetName(element);
        }
    }
}
