using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

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
}
