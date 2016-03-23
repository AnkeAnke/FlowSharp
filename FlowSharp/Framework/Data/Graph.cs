using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SlimDX;

namespace FlowSharp
{
    class Graph2D
    {
        private float[] _x, _fx;
        public float[] X
        {
            get { return _x; }
            set { _x = value; Debug.Assert(_x.Length == _fx.Length); }
        }
        public float[] Fx
        {
            get { return _fx; }
            set { _fx = value; Debug.Assert(_x.Length == _fx.Length); }
        }
        public int Length { get { return X.Length; } }
        private float? _offset;
        public float Offset { get { return _offset ?? (Fx.Length > 0 ? Fx[0] : 0); } set { _offset = value; } }
        /// <summary>
        /// Samples the graph at a given X value. Linear segments.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        // protected float this[float index]
        public float Sample(float index)
        {
            //get
            //{
            int i = GetLastBelowX(index);
            if (i == Length - 1)
            {
               // Console.WriteLine("NaN NaN  BATMAN!");
                return float.NaN;
            }

            float t = (index - X[i]) / (X[i + 1] - X[i]);
            return (1.0f - t) * Fx[i] + t * Fx[i + 1];
            //}
        }
        public int GetLastBelowX(float index)
        {
            int i = 0;
            for (; i < Length - 1; ++i)
            {
                if ((X[i] - index) * (X[i + 1] - index) < 0) // We are between the values we need.
                    break;
            }
            return i;
        }

        public Graph2D() { _x = new float[0]; _fx = new float[0]; }
        public Graph2D(float[] x, float[] fx)
        {
            Set(x, fx);
        }
        public Graph2D(Graph2D cpy)
        {
            _x = new float[cpy.Length];
            _fx = new float[cpy.Length];
            Array.Copy(cpy._x, _x, cpy.Length);
            Array.Copy(cpy._fx, _fx, cpy.Length);
        }

        public void Set(float[] x, float[] fx)
        {
            Debug.Assert(x.Length == fx.Length);
            _x = x; _fx = fx;
        }

        public void SetLineHeightSampleGraph(Line l, Range r)
        {
            for (int i = 0; i < l.Length; ++i)
            {
                float fx = Sample(r[i]);
                if (float.IsNaN(fx))
                    fx = 0;

                l.Positions[i].Z = fx;
            }
        }

        public Line SetLineHeightSampleLine(Vector3 start, Vector3 end, Vector3 up)
        {
            Vector3 unitStep = (end - start) / (_x[Length - 1] - _x[0]);
            Vector3[] line = new Vector3[Length];
            for (int i = 0; i < Length; ++i)
            {
                line[i] = start + (_x[i] - _x[0]) * unitStep + up * _fx[i];
            }
            return new Line() { Positions = line };
        }
        public Line SetLineHeightStraight(Vector3 start, Vector3 dir, Vector3 up)
        {
            Vector3[] line = new Vector3[Length];
            for (int i = 0; i < Length; ++i)
            {
                line[i] = start + _x[i] * dir + up * _fx[i];
            }
            return new Line() { Positions = line };
        }

        public void CutGraph(float cutValue)
        {
            int i = 0;
            for (; i < Length - 1; ++i)
            {
                if ((X[i] - cutValue) * (X[i + 1] - cutValue) < 0) // We are between the values we need.
                    break;
            }
            if (i == Length - 1 || Length <= 1)
                return;

            float t = (cutValue - X[i]) / (X[i + 1] - X[i]);
            float val = (1.0f - t) * Fx[i] + t * Fx[i + 1];

            Array.Resize(ref _x, i + 2);
            Array.Resize(ref _fx, i + 2);
            _x[i + 1] = cutValue;
            _fx[i + 1] = val;
        }

        public float SquaredError(FieldAnalysis.StraightLine line, int? length = null)
        {
            int end = length ?? Length;
            float sum = 0;
            for (int x = 0; x < end; ++x)
            {
                float diff = line[_x[x]] - _fx[x];
                sum += diff * diff;
            }
            sum /= end;
            return sum;
        }

        public List<int> Maxima()
        {
            List<int> pos = new List<int>(Length / 20);
            for (int p = 1; p < Length - 1; ++p)
            {
                if (Fx[p - 1] < Fx[p] && Fx[p + 1] < Fx[p])
                    pos.Add(p);
            }
            return pos;
        }

        public List<int> MaximaThreshold(float thresh)
        {
            List<int> pos = new List<int>(Length / 20);
            for (int p = 1; p < Length - 1; ++p)
            {
                if (Fx[p] > thresh)
                    return pos;
                if (Fx[p - 1] < Fx[p] && Fx[p + 1] < Fx[p])
                    pos.Add(p);
            }
            return pos;
        }

        public List<int> MaximaRange(float min, float max)
        {
            List<int> maxs = new List<int>(8);
            int p = GetLastBelowX(min) + 1;

            for (; p < Length-1; ++p)
            {
                if (X[p] > max)
                    return maxs;
                if (Fx[p - 1] < Fx[p] && Fx[p + 1] < Fx[p])
                    maxs.Add(p);
            }
            return maxs;
        }

        public List<int> MaximaSides(float val, float maxDist)
        {
            List<int> maxs = new List<int>(8);
            int mid = GetLastBelowX(val);
            for (int dir = -1; dir <= 1; dir += 2)
            {
                for (int p = mid; p > 1 && p < Length - 1; p += dir)
                {
                    if (X[p] < val - maxDist || X[p] > val + maxDist)
                        break;

                    if (Fx[p - 1] < Fx[p] && Fx[p + 1] < Fx[p])
                    {
                        maxs.Add(p);
                        break;
                    }
                }
                mid++; // Don't test middle double.
            }
            return maxs;
        }

        public float Curvature(int pos)
        {
            Debug.Assert(pos > 0 && pos < Length - 1);
            float leftS = (Fx[pos] - Fx[pos - 1]) / (X[pos] - X[pos - 1]);
            float rightS = (Fx[pos + 1] - Fx[pos]) / (X[pos + 1] - X[pos]);
            return rightS - leftS;
        }

        public delegate float ValueOperator(float a, float b);
        protected static Graph2D Operate(Graph2D g0, Graph2D g1, ValueOperator func)
        {
            if (g0.Length == 0 || g1.Length == 0)
            {
                return new Graph2D(new float[0], new float[0]) { Offset = 0 };
            }
            float[] x = new float[g0.Length + g1.Length];
            float[] fx = new float[x.Length];

            int p0 = 0; int p1 = 0; int pCount = 0;
            if (g0.X[0] < g1.X[0])
            {
                p0 = g0.GetLastBelowX(g1.X[0]) + 1;
            }
            if (g1.X[0] < g0.X[0])
            {
                p1 = g1.GetLastBelowX(g0.X[0]) + 1;
            }

            float maxX = Math.Min(g0.X[g0.Length - 1], g1.X[g1.Length - 1]);
            // Interleave
            while (p0 < g0.Length && p1 < g1.Length)
            {
                float v0 = p0 < g0.Length ? g0.X[p0] : float.MaxValue;
                float v1 = p1 < g1.Length ? g1.X[p1] : float.MaxValue;

                if (v0 < v1)
                {
                    x[pCount] = v0;
                    fx[pCount] = func(g1.Sample(v0), g0.Fx[p0]);
                    p0++;
                }
                if (v0 > v1)
                {
                    x[pCount] = v1;
                    fx[pCount] = func(g1.Fx[p1], g0.Sample(v1));
                    p1++;
                }
                if (v0 == v1)
                {
                    x[pCount] = v0;
                    fx[pCount] = func(g1.Fx[p1], g0.Fx[p0]);
                    p0++; p1++;
                }

                ++pCount;
            }
            if (pCount < x.Length)
            {
                Array.Resize(ref x, pCount);
                Array.Resize(ref fx, pCount);
            }
            return new Graph2D(x, fx);
        }

        protected static Graph2D OperateBackwards(Graph2D g0, Graph2D g1, ValueOperator func)
        {
            if (g0.Length == 0 || g1.Length == 0)
            {
                return new Graph2D(new float[0], new float[0]) { Offset = 0 };
            }
            float[] x = new float[g0.Length + g1.Length];
            float[] fx = new float[x.Length];

            int p0 = 0; int p1 = 0; int pCount = 0;
            if (g0.X[0] > g1.X[0])
            {
                p0 = g0.GetLastBelowX(g1.X[0]) + 1;
            }
            if (g1.X[0] > g0.X[0])
            {
                p1 = g1.GetLastBelowX(g0.X[0]) + 1;
            }

            float maxX = Math.Min(g0.X[g0.Length - 1], g1.X[g1.Length - 1]);
            // Interleave
            while (p0 < g0.Length && p1 < g1.Length)
            {
                float v0 = p0 < g0.Length ? g0.X[p0] : float.MaxValue;
                float v1 = p1 < g1.Length ? g1.X[p1] : float.MaxValue;

                if (v0 > v1)
                {
                    x[pCount] = v0;
                    fx[pCount] = func(g1.Sample(v0), g0.Fx[p0]);
                    p0++;
                }
                if (v0 < v1)
                {
                    x[pCount] = v1;
                    fx[pCount] = func(g1.Fx[p1], g0.Sample(v1));
                    p1++;
                }
                if (v0 == v1)
                {
                    x[pCount] = v0;
                    fx[pCount] = func(g1.Fx[p1], g0.Fx[p0]);
                    p0++; p1++;
                }

                ++pCount;
            }
            if (pCount < x.Length)
            {
                Array.Resize(ref x, pCount);
                Array.Resize(ref fx, pCount);
            }
            return new Graph2D(x, fx);
        }
        public static Graph2D operator -(Graph2D g0, Graph2D g1)
        {
            return Operate(g0, g1, (a, b) => (a - b));
        }
        public static Graph2D operator +(Graph2D g0, Graph2D g1)
        {
            return Operate(g0, g1, (a, b) => (a + b));
        }

        public static Graph2D Distance(Graph2D a, Graph2D b, bool forward = true)
        {
            if(forward)
                return Operate(a, b, (x, y) => ((x - y) * (x - y)));
            return OperateBackwards(a, b, (x, y) => ((x - y) * (x - y)));
        }

        public float Sum()
        {
            float sum = 0;
            foreach (float fx in _fx)
                sum += fx;
            return sum;
        }

        public float RelativeSumTo(float endX)
        {
            float sum = 0;
            int last = GetLastBelowX(endX);
            for (int f = 0; f < last; ++f)
            {
                sum += _fx[f];
            }
            return sum / last;
        }
        public float RelativeSumOver(float endX)
        {
            float sum = 0;
            int last = GetLastBelowX(endX + _x[0]);
            for (int f = 0; f < last; ++f)
            {
                sum += _fx[f];
            }
            return sum / last;
        }

        //internal List<int> MaximaBroadth(float diameter)
        //{
        //    List<int> maxs = Maxima();
        //    int maxLength = maxs.Count;
        //    List<int> maxB = new List<int>(maxLength);
        //    for(int m = 0; m < maxLength; ++m)
        //    {

        //    }
        //}

        public void SmoothLaplacian(float strength = 0.5f)
        {
            float[] newFx = new float[Fx.Length];
            for(int p = 1; p < Length -1; ++p)
            {
                float est = Fx[p - 1] + Fx[p + 1];
                est /= 2;
                newFx[p] = Fx[p] * (1.0f - strength) + est * strength;
            }

            _fx = newFx;
        }

        public int Threshold(float thresh)
        {
            int p = 1;
            if (Fx[1] > thresh)
                for (; p < Length; ++p)
                    if (Fx[p] < thresh)
                        break;
            for(; p< Length; ++p)
            {
                if (Fx[p] >= thresh && !float.IsInfinity(Fx[p]) && !float.IsNaN(Fx[p]))
                {
                    if (p < 20)
                        Console.Write("Shorty!");
                    return p;
                }
            }

            return Length - 1;
        }

        public List<int> ThresholdBounds(float thresh)
        {
            List<int> result = new List<int>(16);
            int p = 0;
            for (; p < Length-1; ++p)
            {
                if ((Fx[p] - thresh) * (Fx[p+1] - thresh) <= 0 && !float.IsInfinity(Fx[p]) && !float.IsNaN(Fx[p]) && !float.IsInfinity(Fx[p+1]) && !float.IsNaN(Fx[p+1]))
                {
                    result.Add(p);
                }
            }

            return result;
        }

        public List<int> ThresholdFronts(float thresh)
        {
            List<int> result = new List<int>(16);
            int p = 0;
            for (; p < Length - 1; ++p)
            {
                if ((Fx[p] < thresh) &&  (Fx[p + 1] > thresh) && !float.IsInfinity(Fx[p]) && !float.IsNaN(Fx[p]))
                {
                    result.Add(p);
                }
            }

            return result;
        }

        public int ThresholdRange(int min, int max, float thresh)
        {
            int p = Math.Max(0, min);
            if (Fx[p] > thresh)
                for (; p < Length; ++p)
                    if (Fx[p] < thresh)
                        break;
            for (; p < Math.Min(Length, max+1); ++p)
            {
                if (Fx[p] >= thresh && !float.IsInfinity(Fx[p]) && !float.IsNaN(Fx[p]))
                {
                    //if (p < 20)
                    //    Console.Write("Shorty!");
                    return p;
                }
            }

            return Length - 1;
        }
    }
    class Range
    {
        public int NumPoints;
        public float Start, StepSize;
        public float End { get { return Start + StepSize * (NumPoints - 1); } }
        public Range() { }
        public Range(float start, float end, int numPoints)
        {
            Start = start; NumPoints = numPoints; StepSize = (end - start) / NumPoints;
        }
        public Range(float start, int numPoints, float stepSize)
        {
            Start = start; NumPoints = numPoints; StepSize = stepSize;
        }

        public float this[int index] { get { Debug.Assert(index < NumPoints); return Start + index * StepSize; } }
        public float this[float index] { get { Debug.Assert(index < NumPoints); return Start + index * StepSize; } }
    }
}
