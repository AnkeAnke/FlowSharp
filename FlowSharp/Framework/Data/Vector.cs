using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace FlowSharp
{
    class Vector
    {
        public int Length { get; protected set; }

        protected float[] _data;
        public float[] Data
        {
            get { return _data; }
            set { Debug.Assert(value.Length == Length); _data = value; }
        }

        public Vector(int dim)
        {
            Length = dim;
            Data = new float[Length];
        }

        public Vector(int[] data)
        {
            Length = data.Length;
            _data = (float[])data.Clone();
        }

        public Vector(Vector copy)
        {
            Length = copy.Length;
            _data = (float[])copy.Data.Clone();
        }

        /// <summary>
        /// Vector with dim elements set to v.
        /// </summary>
        /// <param name="v"></param>
        /// <param name="dim"></param>
        public Vector(float v, int dim)
        {
            Length = dim;
            _data = new float[Length];
            for (int d = 0; d < Length; ++d)
                _data[d] = v;
        }

        public static Vector operator *(Vector a, float b)
        {
            Vector prod = new Vector(a);
            for(int dim = 0; dim < a.Length; ++dim)
                a[dim] *= b;

            return prod;
        }

        public static Vector operator *(float a, Vector b)
        {
            return b * a;
        }

        public float this[int index]
        {
            get { return _data[index]; }
            set { _data[index] = value; }
        }

        public static Vector operator +(Vector a, Vector b)
        {
            Debug.Assert(a.Length == b.Length);
            Vector sum = new Vector(a);
            for (int dim = 0; dim < a.Length; ++dim)
                sum[dim] += b[dim];

            return sum;
        }

        public static Vector operator -(Vector a, Vector b)
        {
            Debug.Assert(a.Length == b.Length);
            Vector diff = new Vector(a);
            for (int dim = 0; dim < a.Length; ++dim)
                diff[dim] -= b[dim];

            return diff;
        }

        public static Vector operator *(Vector a, Vector b)
        {
            Debug.Assert(a.Length == b.Length);
            Vector prod = new Vector(a);
            for (int dim = 0; dim < a.Length; ++dim)
                prod[dim] *= b[dim];

            return prod;
        }

        public static Vector operator /(Vector a, Vector b)
        {
            Debug.Assert(a.Length == b.Length);
            Vector quot = new Vector(a);
            for (int dim = 0; dim < a.Length; ++dim)
                quot[dim] /= b[dim];

            return quot;
        }

        /// <summary>
        /// Floor the vector.
        /// </summary>
        public static explicit operator Index(Vector vec)  // explicit byte to digit conversion operator
        {
            Index result = new Index(vec.Length);
            for(int dim = 0; dim < vec.Length; ++dim)
                result[dim] = (int)vec[dim];
            return result;
        }

        /// <summary>
        /// Product of all components.
        /// </summary>
        /// <returns></returns>
        public float Product()
        {
            float prod = 1;
            foreach (float expansion in _data)
                prod *= expansion;

            return prod;
        }
    }
}
