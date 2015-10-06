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
        public virtual int Length { get; protected set; }

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

        public Vector(float[] data)
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
        /// Convert first two elements to SlimDX.Vector2.
        /// </summary>
        public static explicit operator SlimDX.Vector2(Vector vec)  // explicit byte to digit conversion operator
        {
            return new SlimDX.Vector2(vec[0], vec.Length > 1 ? vec[1] : 0);
        }

        /// <summary>
        /// Convert first tree elements to SlimDX.Vector3. If less, fill with zeros.
        /// </summary>
        public static explicit operator SlimDX.Vector3(Vector vec)  // explicit byte to digit conversion operator
        {
            return new SlimDX.Vector3(vec[0], vec.Length > 1? vec[1] : 0, vec.Length > 2 ? vec[2] : 0);
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

    class Vec2 : Vector
    {
        public override int Length { get { return 2; } protected set { value = 2; } }

        public Vec2() : base(0, 2)
        { }

        public Vec2(float x, float y) : base(new float[]{x,y})
        { }

        public Vec2(Vec2 copy) : base(copy)
        { }

        /// <summary>
        /// Vector with dim elements set to v.
        /// </summary>
        /// <param name="v"></param>
        /// <param name="dim"></param>
        public Vec2(float xy) : base(xy, 2)
        { }
    }
}
