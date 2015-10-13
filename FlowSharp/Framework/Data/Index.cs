using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections;

namespace FlowSharp
{
    /// <summary>
    /// N dimensional int vector for grid-based arithmetic. Methods will be added when needed.
    /// </summary>
    class Index
    {
        public int Length { get; protected set; }

        protected int[] _data;
        public int[] Data
        {
            get { return _data; }
            set { Debug.Assert(value.Length == Length); _data = value; }
        }

        #region Constructors

        public Index(int dim)
        {
            Length = dim;
            Data = new int[Length];
        }

        public Index(int[] data)
        {
            Length = data.Length;
            _data = (int[])data.Clone();
        }

        public Index(Index copy)
        {
            Length = copy.Length;
            _data = (int[])copy.Data.Clone();
        }

        /// <summary>
        /// Index with dim elements set to v.
        /// </summary>
        /// <param name="v"></param>
        /// <param name="dim"></param>
        public Index(int v, int dim)
        {
            Length = dim;
            _data = new int[Length];
            for (int d = 0; d < Length; ++d)
                _data[d] = v;
        }

        #endregion

        #region Operators
        public int this[int index]
        {
            get { return _data[index]; }
            set { _data[index] = value; }
        }

        public static Index operator +(Index a, Index b)
        {
            Debug.Assert(a.Length == b.Length);
            Index sum = new Index(a);
            for (int dim = 0; dim < a.Length; ++dim)
                sum[dim] += b[dim];

            return sum;
        }

        public static Index operator -(Index a, Index b)
        {
            Debug.Assert(a.Length == b.Length);
            Index sum = new Index(a);
            for (int dim = 0; dim < a.Length; ++dim)
                sum[dim] -= b[dim];

            return sum;
        }

        public static Index operator *(Index a, Index b)
        {
            Debug.Assert(a.Length == b.Length);
            Index prod = new Index(a);
            for (int dim = 0; dim < a.Length; ++dim)
                prod[dim] *= b[dim];

            return prod;
        }

        public static Vector operator *(Index a, Vector b)
        {
            Debug.Assert(a.Length == b.Length);
            Vector prod = new Vector(b);
            for (int dim = 0; dim < a.Length; ++dim)
                prod[dim] *= (float)a[dim];

            return prod;
        }

        public static Vector operator *(Vector a, Index b)
        {
            return b * a;
        }

        public static explicit operator Vector(Index vec)  // explicit byte to digit conversion operator
        {
            Vector result = new Vector(vec.Length);
            for(int dim = 0; dim < vec.Length; ++dim)
                result[dim] = (float)vec[dim];
            return result;
        }

        /// <summary>
        /// Compare all values component-wise.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator <(Index a, Index b)
        {
            Debug.Assert(a.Length == b.Length);

            for (int dim = 0; dim < a.Length; ++dim)
                if (a[dim] >= b[dim])
                    return false;

            return true;
        }

        /// <summary>
        /// Compare all values component-wise.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator >(Index a, Index b)
        {
            Debug.Assert(a.Length == b.Length);

            for (int dim = 0; dim < a.Length; ++dim)
                if (a[dim] <= b[dim])
                    return false;

            return true;
        }

        /// <summary>
        /// Compare all values component-wise.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator >=(Index a, Index b)
        {
            return !(a < b);
        }

        /// <summary>
        /// Compare all values component-wise.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool operator <=(Index a, Index b)
        {
            return !(a > b);
        }

        #endregion

        /// <summary>
        /// Product of all components.
        /// </summary>
        /// <returns></returns>
        public int Product()
        {
            int prod = 1;
            foreach (int expansion in _data)
                prod *= expansion;

            return prod;
        }

        public bool IsPositive()
        {
            foreach (int value in _data)
                if (value < 0)
                    return false;
            return true;
        }

        public int Max()
        {
            int max = _data[0];
            for (int dim = 1; dim < Length; ++dim)
                max = Math.Max(max, _data[dim]);
            return max;
        }

        public int Min()
        {
            int min = _data[0];
            for (int dim = 1; dim < Length; ++dim)
                min = Math.Min(min, _data[dim]);
            return min;
        }

        /// <summary>
        /// Component-wise min.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static Index Min(Index a, Index b)
        {
            Debug.Assert(a.Length == b.Length);
            Index ret = new Index(a);
            for(int dim = 0; dim < a.Length; ++dim)
            {
                ret[dim] = (ret[dim] > b[dim]) ? b[dim] : ret[dim];
            }
            return ret;
        }

        /// <summary>
        /// Component-wise max.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static Index Max(Index a, Index b)
        {
            Debug.Assert(a.Length == b.Length);
            Index ret = new Index(a);
            for (int dim = 0; dim < a.Length; ++dim)
            {
                ret[dim] = (ret[dim] < b[dim]) ? b[dim] : ret[dim];
            }
            return ret;
        }


        public int T { get { return _data[Length - 1]; } set { _data[Length - 1] = value; } }
        /// <summary>
        /// Retruns Index as string in format (1.000, 2.000, ... Length.000)
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string str = "(";
            for (int dim = 0; dim < Length - 1; ++dim)
                str += _data[dim] + ", ";
            str += _data[Length - 1];
            str += ')';

            return str;
        }
    }

    class GridIndex : IEnumerator<Index>, IEnumerable<Index>, IEnumerable<GridIndex>, IEnumerator<GridIndex>
    {
        private Index _current;
        private Index _max;
        private int _currentInt;
        public int Int { get { return _currentInt; } }

        //Create internal array in constructor.
        public GridIndex(Index max)
        {
            _max = new Index(max);
            _current = new Index(0, max.Length);
            _current[0] = -1;
            _currentInt = -1;
        }

        public IEnumerator GetEnumerator()
        {
            return (IEnumerator)this;
        }

        //IEnumerator
        public bool MoveNext()
        {
            int dim = 0;
            for(; dim < _max.Length; ++dim)
            {
                if (_current[dim] + 1 < _max[dim])
                {
                    _current[dim]++;
                    _currentInt++;
                    break;
                }
                else
                {
                    _current[dim] = 0;
                }
            }
            return (dim < _max.Length);
        }

        //IEnumerable
        public void Reset()
        {
            _current = new Index(0, _max.Length);
            _currentInt = 0;
        }

        public void Dispose()
        {
        }

        IEnumerator<Index> IEnumerable<Index>.GetEnumerator()
        {
            return this;
        }

        IEnumerator<GridIndex> IEnumerable<GridIndex>.GetEnumerator()
        {
            return this;
        }

        public Index Current
        {
            get
            {
                return _current;
            }
        }

        object IEnumerator.Current
        {
            get
            {
                return this;
            }
        }

        GridIndex IEnumerator<GridIndex>.Current
        {
            get
            {
                return this;
            }
        }

        public static explicit operator int(GridIndex index)
        {
            return index.Int;
        }

        public static implicit operator Index (GridIndex index)
        {
            return index.Current;
        }
    }
}
