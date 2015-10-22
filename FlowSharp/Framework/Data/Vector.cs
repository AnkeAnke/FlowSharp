using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SlimDX;

namespace FlowSharp
{
    enum SignX : int
    {
        POSITIVE = -1,
        NEGATIVE = 1
    }
    struct Sign
    {
        public int Value { get; private set; }
        public static Sign POSITIVE { get; } = new Sign(1);
        public static Sign NEGATIVE { get; } = new Sign(-1);
        private Sign(int x) { Value = x; }

        public static explicit operator int(Sign sign)
        { return sign.Value; }
        public static explicit operator Vector3 (Sign sign)
        {
            return sign == POSITIVE ? Vector3.UnitX : Vector3.UnitZ;
        }
        public static Sign operator !(Sign sign)
        {
            return new Sign(-sign.Value);
        }
        public static bool operator ==(Sign a, Sign b)
        {
            return a.Value == b.Value;
        }
        public static bool operator !=(Sign a, Sign b)
        {
            return !(a == b);
        }
    }
    class Vector
    {
        public virtual int Length { get { return _data.Length; } }

        protected float[] _data;
        public float[] Data
        {
            get { return _data; }
            set { Debug.Assert(_data == null || value.Length == Length); _data = value; }
        }

        public float T { get { return _data[Length - 1]; } set { _data[Length - 1] = value; } }

        public Vector(int dim)
        {
            Data = new float[dim];
        }

        public Vector(float[] data)
        {
            _data = (float[])data.Clone();
        }

        public Vector(Vector copy)
        {
            _data = (float[])copy.Data.Clone();
        }

        /// <summary>
        /// Vector with dim elements set to v.
        /// </summary>
        /// <param name="v"></param>
        /// <param name="dim"></param>
        public Vector(float v, int dim)
        {
            _data = new float[dim];
            for (int d = 0; d < Length; ++d)
                _data[d] = v;
        }

        public static Vector operator *(Vector a, float b)
        {
            Vector prod = new Vector(a);
            for(int dim = 0; dim < a.Length; ++dim)
                prod[dim] *= b;

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

        public static Vector operator /(Vector a, float b)
        {
            Vector quot = new Vector(a);
            for (int dim = 0; dim < a.Length; ++dim)
                quot[dim] /= b;

            return quot;
        }

        public static Vector operator -(Vector a)
        {
            Vector neg = new Vector(a);
            for (int dim = 0; dim < a.Length; ++dim)
                neg[dim] = -neg[dim];

            return neg;
        }

        public static float Dot(Vector a, Vector b)
        {
            return (a * b).Sum();
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
        /// Convert first tree elements to a Vec3. If less, fill with zeros.
        /// </summary>
        public Vec3 ToVec3()
        {
            return new Vec3(this[0], Length > 1 ? this[1] : 0, Length > 2 ? this[2] : 0);
        }

        /// <summary>
        /// Convert first two elements to a Vec2. If less, fill with zeros.
        /// </summary>
        public Vec2 ToVec2()
        {
            return new Vec2(this[0], Length > 1 ? this[1] : 0);
        }

        /// <summary>
        /// Convert first N elements to a VecN. If less, fill with zeros.
        /// </summary>
        public Vector ToVec(int N)
        {
            Vector result = new Vector(0,N);
            for (int dim = 0; dim < Math.Min(Length, N); ++dim)
                result[dim] = this[dim];
            return result;
        }

        /// <summary>
        /// Convert to Vec3. If length is not 3, abort.
        /// </summary>
        public Vec3 AsVec3()
        {
            Debug.Assert(Length == 3);
            return new Vec3(_data);
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

        /// <summary>
        /// Sum of all components.
        /// </summary>
        /// <returns></returns>
        public float Sum()
        {
            float prod = 0;
            foreach (float expansion in _data)
                prod += expansion;

            return prod;
        }

        public float LengthSquared()
        {
            float sum = 0;
            for (int dim = 0; dim < Length; ++dim)
                sum += _data[dim] * _data[dim];

            return sum;
        }

        public float LengthEuclidean()
        {
            return (float)Math.Sqrt(LengthSquared());
        }

        public void Normalize()
        {
            float length = LengthEuclidean();
            //Debug.Assert(length != 0);
            if (length == 0)
                return;
            for (int dim = 0; dim < Length; ++dim)
                _data[dim] /= length;
        }

        public Vector Normalized()
        {
            Vector norm = new Vector(this);
            norm.Normalize();
            return norm;
        }

        public float Max()
        {
            float max = _data[0];
            for (int dim = 1; dim < Length; ++dim)
                max = Math.Max(max, _data[dim]);
            return max;
        }

        public float Min()
        {
            float min = _data[0];
            for (int dim = 1; dim < Length; ++dim)
                min = Math.Min(min, _data[dim]);
            return min;
        }

        public Vector Abs()
        {
            Vector abs = new Vector(this);
            for (int dim = 0; dim < Length; ++dim)
                abs[dim] = Math.Abs(abs[dim]);
            return abs;
        }

        /// <summary>
        /// Returns the minimum positive value.
        /// </summary>
        public float MinPos()
        {
            float min = float.MaxValue;
            for (int dim = 0; dim < Length; ++dim)
                if (_data[dim] > 0 && _data[dim] < min)
                    min = _data[dim];
            return min;
        }

        public override string ToString()
        {
            string res = "[" + _data[0];
            for (int dim = 1; dim < Length; ++dim)
                res += ", " + _data[dim];
            res += ']';
            return res;
        }
    }

    class Vec2 : Vector
    {
        public float X { get { return _data[0]; } }
        public float Y { get { return _data[1]; } }
        public override int Length { get { return 2; } }

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

        public static explicit operator Vec3(Vec2 vec)
        {
            return new Vec3(vec, 0);
        }
    }

    class Vec3 : Vector
    {
        public float X { get { return _data[0]; } }
        public float Y { get { return _data[1]; } }
        public float Z { get { return _data[2]; } }
        public override int Length { get { return 3; }}

        public Vec3() : base(0, 3)
        { }

        public Vec3(float x, float y, float z) : base(new float[] { x, y, z})
        { }

        public Vec3(Vec3 copy) : base(copy)
        { }

        public Vec3(float[] copy) : base(copy)
        { Debug.Assert(copy.Length == 3); }

        public Vec3(Vec2 a, float b) : base(new float[] { a.X, a.Y, b })
        { }

        public Vec3(float a, Vec2 b) : base(new float[] { a, b.X, b.Y })
        { }

        /// <summary>
        /// Vector with dim elements set to v.
        /// </summary>
        /// <param name="v"></param>
        /// <param name="dim"></param>
        public Vec3(float xy) : base(xy, 2)
        { }

        public static Vec3 Cross(Vec3 a, Vec3 b)
        {
            Vec3 ret = new Vec3();
            ret[0] = a.Y * b.Z - a.Z * b.Y;
            ret[1] = a.Z * b.X - a.X * b.Z;
            ret[2] = a.X * b.Y - a.Y * b.X;
            return ret;
        }

        public static explicit operator Vec3(SlimDX.Vector3 vec)  // explicit byte to digit conversion operator
        {
            return new Vec3(vec.X, vec.Y, vec.Z);
        }

        public static Vec3 operator -(Vec3 a)
        {
            return new Vec3(-a.X, -a.Y, -a.Z);
        }

        public static Vec3 operator *(Vec3 a, float b)
        {
            return new Vec3(a.X * b, a.Y * b, a.Z * b);
        }

        public static Vec3 operator *(float b, Vec3 a)
        {
            return a * b;
        }
    }
}
