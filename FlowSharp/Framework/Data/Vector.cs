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
