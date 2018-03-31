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
        protected float _standardDeviation;
        protected bool _rebuilt = false;
        public CoreOkuboMapper(int everyNthField, Plane plane) : base(everyNthField, plane)
        {
            Mapping = Map;
            _linePlane = new Plane(_linePlane.Origin, _linePlane.XAxis, _linePlane.YAxis, _linePlane.ZAxis, 1.0f);
        }

        protected void ComputeOkubo(float[] radii, float[] angles, out Graph2D[] okuboData)
        {
            float integrationLength = 40; // IntegrationTime;

            // ~~~~~~~~~~~~~~~~~~ Initialize seed points. ~~~~~~~~~~~~~~~~~~~~ \\
            PointSet<Point> circle = new PointSet<Point>(new Point[radii.Length * angles.Length * 4]);
            okuboData = new Graph2D[angles.Length];
            if (_velocity.TimeOrigin > SliceTimeMain || _velocity.TimeOrigin + _velocity.Size.T < SliceTimeMain)
                LoadField(SliceTimeMain, MemberMain, 1);

            VectorField okuboField = new VectorField(_velocity.GetSlice(SliceTimeMain), FieldAnalysis.OkuboWeiss, 1);

            float mean, fill, standardDeviation;
            (okuboField.Scalars[0] as ScalarField).ComputeStatistics(out fill, out mean, out _standardDeviation);
            Console.WriteLine("Mean: " + mean + ", SD: " + _standardDeviation + ", valid cells: " + fill);

            for (int angle = 0; angle < angles.Length; ++angle)
            {
                okuboData[angle] = new Graph2D(radii.Length);
                for (int rad = 0; rad < radii.Length; ++rad)
                {
                    okuboData[angle].X[rad] = radii[rad];
                    float x = (float)(Math.Sin(angles[angle] + Math.PI / 2));
                    float y = (float)(Math.Cos(angles[angle] + Math.PI / 2));
                    Vec2 pos = new Vec2(_selection.X + x * radii[rad], _selection.Y + y * radii[rad]);

                    if (!okuboField.Grid.InGrid(pos) || !okuboField.IsValid(pos))
                    {
                        okuboData[angle].Fx[rad] = 1;
                        continue;
                    }
                    okuboData[angle].Fx[rad] = okuboField[0].Sample(pos) + 0.2f * _standardDeviation;
                }
            }
        }

        protected override void FindBoundary()
        {
            if (LoadGraph("Okubo", out _okubo))
            {
                _graph = new LineBall(_graphPlane, _graphData, LineBall.RenderEffect.HEIGHT, Colormap, Flat, SliceTimeMain);
                return;
            }

            float angleDiff = (float)((Math.PI * 2) / _numSeeds);
            float[] radii = new float[LineX];
            float[] angles = new float[_numSeeds];
            for (int seed = 0; seed < _numSeeds; ++seed)
            {
                float angle = seed * angleDiff;
                angles[seed] = angle;
            }
            for (int o = 0; o < LineX; ++o)
            {
                radii[o] = o * _lengthRadius / (LineX - 1);
            }

            Console.WriteLine($"=== Working on Okubo Slice {0} ===", SliceTimeMain);
            ComputeOkubo(radii, angles, out _okubo);
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
            _graph.LowerBound = -0.05f;
            _graph.UpperBound = 0.05f;

            _rebuilt = false;

            // Load or compute selection by floodfill.
            Graph2D[] okuboSelection;
            LineSet okuboLines;
            if (LoadGraph("OkuboSelection", _selectedCore, out okuboSelection, out okuboLines))
                return;

            // Floodfill.
            int numAngles = _okubo.Length;
            int numRadii = _okubo[0].Length;
            HashSet<Int2> toFlood = new HashSet<Int2>();

            okuboSelection = new Graph2D[numAngles];
            for (int angle = 0; angle < numAngles; ++angle)
            {
                toFlood.Add(new Int2(angle, 0));

                okuboSelection[angle] = new Graph2D(numRadii);
                for (int r = 1; r < numRadii; ++r)
                {
                    okuboSelection[angle].Fx[r] = 0;
                    okuboSelection[angle].X[r] = _okubo[angle].X[r];
                }
            }

            while (toFlood.Count > 0)
            {
                Int2 current = toFlood.Last();
                toFlood.Remove(current);
                okuboSelection[current.X].Fx[current.Y] = 1;

                // In each direction, go negative and positive.
                for (int dim = 0; dim < 2; ++dim)
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        Int2 neighbor = new Int2(current);
                        neighbor[dim] += sign;

                        // Wrap angle around.
                        neighbor[0] = (neighbor[0] + numAngles) % numAngles;
                        if (neighbor.Y >= 0 && neighbor.Y < numRadii
                            && _okubo[neighbor.X].Fx[neighbor.Y] <= 0
                            && okuboSelection[neighbor.X].Fx[neighbor.Y] == 0)
                        {
                            toFlood.Add(neighbor);
                        }
                    }
            }

            LineSet sun = FieldAnalysis.WriteGraphToSun(okuboSelection, new Vector3(_selection, SliceTimeMain));
            WriteGraph("OkuboSelection", _selectedCore, okuboSelection, sun);
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
