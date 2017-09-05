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
        public override bool Equals(object obj)
        {
            return (obj as Sign?) == null ? false : (Sign)obj == this;
        }
        public override int GetHashCode()
        {
            return Value;
        }
        public Sign(Sign a)
        {
            Value = (int)a;
        }
        public static implicit operator bool(Sign a)
        {
            return (int)a > 0;
        }

        public static explicit operator Sign(bool a)
        {
            return a ? POSITIVE : NEGATIVE;
        }

        public override string ToString()
        {
            return this? "+" : "-";
        }
    }

    class Vector : VectorRef
    {
        protected float[] _data;
        public float[] Data
        {
            get { return _data; }
            set { Debug.Assert(_data == null || value.Length == Length); _data = value; }
        }
        public override float this[int index]
        {
            get { return _data[index]; }
            set { _data[index] = value; }
        }
        public override int Length { get { return _data.Length; } }

        protected override void SetSize(int size)
        {
            _data = new float[size];
        }
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
        public Vector(VectorRef copy)
        {
            _data = new float[copy.Length];
            for (int l = 0; l < Length; ++l)
                _data[l] = copy[l];
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

        public Vector(Vector3 vec)
        {
            _data = new float[] { vec.X, vec.Y, vec.Z };
        }

        public Vector(Vector4 vec)
        {
            _data = new float[] { vec.X, vec.Y, vec.Z, vec.W };
        }
    }
    abstract class VectorRef
    {
        public abstract int Length { get; }

        public abstract float this[int index] { get; set; }

        public float T { get { return this[Length - 1]; } set { this[Length - 1] = value; } }

        protected abstract void SetSize(int size);
        public VectorRef(VectorRef vec)
        {
            SetSize(vec.Length);
            for (int i = 0; i < vec.Length; ++i)
                this[i] = vec[i];
        }
        protected VectorRef() { }

        public static Vector operator *(VectorRef a, float b)
        {
            Vector prod = new Vector(a);
            for(int dim = 0; dim < a.Length; ++dim)
                prod[dim] *= b;

            return prod;
        }

        public static Vector operator *(float a, VectorRef b)
        {
            return b * a;
        }

        public static Vector operator +(VectorRef a, VectorRef b)
        {
            Debug.Assert(a.Length == b.Length);
            Vector sum = new Vector(a);
            for (int dim = 0; dim < a.Length; ++dim)
                sum[dim] += b[dim];

            return sum;
        }

        public static Vector operator +(VectorRef a, float b)
        {
            Vector sum = new Vector(a);
            for (int dim = 0; dim < a.Length; ++dim)
                sum[dim] += b;

            return sum;
        }

        public static Vector operator -(VectorRef a, VectorRef b)
        {
            Debug.Assert(a.Length == b.Length);
            Vector diff = new Vector(a);
            for (int dim = 0; dim < a.Length; ++dim)
                diff[dim] -= b[dim];

            return diff;
        }

        public static Vector operator -(VectorRef a, float b)
        {
            Vector diff = new Vector(a);
            for (int dim = 0; dim < a.Length; ++dim)
                diff[dim] -= b;

            return diff;
        }

        public static Vector operator *(VectorRef a, VectorRef b)
        {
            Debug.Assert(a.Length == b.Length);
            Vector prod = new Vector(a);
            for (int dim = 0; dim < a.Length; ++dim)
                prod[dim] *= b[dim];

            return prod;
        }

        public static Vector operator /(VectorRef a, VectorRef b)
        {
            Debug.Assert(a.Length == b.Length);
            Vector quot = new Vector(a);
            for (int dim = 0; dim < a.Length; ++dim)
                quot[dim] /= b[dim];

            return quot;
        }

        public static Vector operator /(VectorRef a, float b)
        {
            Vector quot = new Vector(a);
            for (int dim = 0; dim < a.Length; ++dim)
                quot[dim] /= b;

            return quot;
        }

        public static Vector operator -(VectorRef a)
        {
            Vector neg = new Vector(a);
            for (int dim = 0; dim < a.Length; ++dim)
                neg[dim] = -neg[dim];

            return neg;
        }

        public static bool operator <(VectorRef a, VectorRef b)
        {
            for (int dim = 0; dim < a.Length; ++dim)
                if (a[dim] >= b[dim])
                    return false;

            return true;
        }

        public static bool operator >(VectorRef a, VectorRef b)
        {
            for (int dim = 0; dim < a.Length; ++dim)
                if (a[dim] <= b[dim])
                    return false;

            return true;
        }

        public static bool operator <=(VectorRef a, VectorRef b)
        {
            for (int dim = 0; dim < a.Length; ++dim)
                if (a[dim] > b[dim])
                    return false;

            return true;
        }

        public static bool operator >=(VectorRef a, VectorRef b)
        {
            for (int dim = 0; dim < a.Length; ++dim)
                if (a[dim] < b[dim])
                    return false;

            return true;
        }

        public bool IsStrictlyPositive()
        {
            for (int dim = 0; dim < Length; ++dim)
                if (this[dim] <= 0)
                    return false;
            return true;
        }

        public bool IsPositive()
        {
            for (int dim = 0; dim < Length; ++dim)
                if (this[dim] < 0)
                    return false;
            return true;
        }

        public static float Dot(VectorRef a, VectorRef b)
        {
            return (a * b).Sum();
        }

        public static Vec3 CrossUnchecked(VectorRef a, VectorRef b)
        {
            return new Vec3(
                a[1] * b[2] - a[2] * b[1],
                a[2] * b[0] - a[0] * b[2],
                a[0] * b[1] - a[1] * b[0]);
        }

        /// <summary>
        /// Floor the vector.
        /// </summary>
        public static explicit operator Index(VectorRef vec)  // explicit byte to digit conversion operator
        {
            Index result = new Index(vec.Length);
            for(int dim = 0; dim < vec.Length; ++dim)
                result[dim] = (int)vec[dim];
            return result;
        }


        public static explicit operator VectorRef(float f)
        {
            return new Vector(f, 1);
        }

        public static explicit operator float(VectorRef v)
        {
            Debug.Assert(v.Length == 1, "Can only cast 1D vector to scalar, given vector is " + v.Length + "D.");
            return v[0];
        }

        /// <summary>
        /// Convert first two elements to SlimDX.Vector2.
        /// </summary>
        public static explicit operator SlimDX.Vector2(VectorRef vec)  // explicit byte to digit conversion operator
        {
            return new SlimDX.Vector2(vec[0], vec.Length > 1 ? vec[1] : 0);
        }

        /// <summary>
        /// Convert first tree elements to SlimDX.Vector3. If less, fill with zeros.
        /// </summary>
        public static explicit operator SlimDX.Vector3(VectorRef vec)  // explicit byte to digit conversion operator
        {
            return new SlimDX.Vector3(vec[0], vec.Length > 1? vec[1] : 0, vec.Length > 2 ? vec[2] : 0);
        }

        /// <summary>
        /// Convert first four elements to SlimDX.Vector4. If less, fill with (0 0 0 1).
        /// </summary>
        public static explicit operator SlimDX.Vector4(VectorRef vec)  // explicit byte to digit conversion operator
        {
            return new SlimDX.Vector4(vec[0], vec.Length > 1 ? vec[1] : 0, vec.Length > 2 ? vec[2] : 0, vec.Length > 3 ? vec.T : 0);
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
        public Vector SubVec(int N, int offset = 0)
        {
            Vector result = new Vector(0,N);
            for (int dim = offset; dim < Math.Min(Length, offset + N); ++dim)
                result[dim-offset] = this[dim];
            return result;
        }

        public Vector Append(Vector app)
        {
            Vector comp = new Vector(Length + app.Length);
            for (int t = 0; t < Length; ++t)
                comp[t] = this[t];
            for (int a = 0; a < app.Length; ++a)
                comp[Length + a] = app[a];

            return comp;
        }
        /// <summary>
        /// Convert to Vec3. If length is not 3, abort.
        /// </summary>
        public Vec3 AsVec3()
        {
            return this.ToVec3();
        }

        /// <summary>
        /// Product of all components.
        /// </summary>
        /// <returns></returns>
        public float Product()
        {
            float prod = 1;
            for (int i = 0; i < Length; ++i)
                prod *= this[i];

            return prod;
        }

        /// <summary>
        /// Sum of all components.
        /// </summary>
        /// <returns></returns>
        public float Sum()
        {
            float prod = 0;
            for (int i = 0; i < Length; ++i)
                prod += this[i];

            return prod;
        }

        public float LengthSquared()
        {
            float sum = 0;
            for (int dim = 0; dim < Length; ++dim)
                sum += this[dim] * this[dim];

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
                this[dim] /= length;
        }

        public Vector Normalized()
        {
            Vector norm = new Vector(this);
            norm.Normalize();
            return norm;
        }

        public float Max()
        {
            float max = this[0];
            for (int dim = 1; dim < Length; ++dim)
                max = Math.Max(max, this[dim]);
            return max;
        }

        public float Min()
        {
            float min = this[0];
            for (int dim = 1; dim < Length; ++dim)
                min = Math.Min(min, this[dim]);
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
                if (this[dim] > 0 && this[dim] < min)
                    min = this[dim];
            return min;
        }

        public float AbsSumNegatives()
        {
            float sum = 0;
            for (int dim = 0; dim < Length; ++dim)
                sum += Math.Min(0, this[dim]);
            return -sum;
        }

        public override string ToString()
        {
            string res = "[" + this[0];
            for (int dim = 1; dim < Length; ++dim)
                res += ", " + this[dim];
            res += ']';
            return res;
        }

        public static Vector Subvec(VectorRef v, int length, int offset = 0)
        {
            Debug.Assert(offset >= 0 && length + offset < v.Length, "Range outside the vector length.");
            Vector ret = new Vector(length);
            for (int i = offset; i < offset + length; ++i)
                ret[i] = v[i];
            //Array.Copy(v.Data, offset, ret.Data, 0, length);
            return ret;
        }

        public static Vector ToUnsteady(VectorRef v, float time = 1)
        {
            Vector ret = new Vector(v.Length + 1);
            for (int i = 0; i < v.Length; ++i)
                ret[i] = v[i];
            //Array.Copy(v.Data, ret.Data, v.Length);
            ret.T = time;
            return ret;
        }

        public static Vector ToUnsteady(Vector3 v)
        {
            return new Vector(new float[] { v.X, v.Y, v.Z, 1 });
        }

        public static Vector ToSteady(Vector v)
        {
            Debug.Assert(v.Length > 1 && v.T == 1, "Not an unsteady vector.");
            Vector ret = new Vector(v.Length - 1);
            Array.Copy(v.Data, ret.Data, v.Length-1);
            return ret;
        }

        public static Vector Min(VectorRef a, VectorRef b)
        {
            Debug.Assert(a.Length == b.Length, "Not the same length.");
            Vector ret = new Vector(a.Length);
            for (int d = 0; d < a.Length; ++d)
                ret[d] = Math.Min(a[d], b[d]);
            return ret;
        }

        public static Vector Max(VectorRef a, VectorRef b)
        {
            Debug.Assert(a.Length == b.Length, "Not the same length.");
            Vector ret = new Vector(a.Length);
            for (int d = 0; d < a.Length; ++d)
                ret[d] = Math.Max(a[d], b[d]);
            return ret;
        }

        public VectorRef MinOf(VectorRef b)
        {
            Debug.Assert(this.Length == b.Length, "Not the same length.");
            for (int d = 0; d < Length; ++d)
                this[d] = Math.Min(this[d], b[d]);
            return this;
        }

        public VectorRef MaxOf(VectorRef b)
        {
            Debug.Assert(this.Length == b.Length, "Not the same length.");
            for (int d = 0; d < Length; ++d)
                this[d] = Math.Max(this[d], b[d]);
            return this;
        }

        /// <summary>
        /// COmponent-wise comparison. Element is + iff vec >= reference.
        /// </summary>
        /// <param name="vec"></param>
        /// <param name="reference"></param>
        /// <returns></returns>
        public static Sign[] Compare(VectorRef vec, VectorRef reference)
        {
            Debug.Assert(vec.Length == reference.Length);
            Sign[] comp = new Sign[vec.Length];
            for (int d = 0; d < vec.Length; ++d)
                comp[d] = (Sign)(vec[d] >= reference[d]);
            return comp;
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

        public static Vec2 operator +(Vec2 a, Vec2 b)
        {
            return new Vec2(a.X+ b.X, a.Y + b.Y);
        }
        public static Vec2 operator *(Vec2 a, float b)
        {
            return new Vec2(a.X * b, a.Y * b);
        }
        public static Vec2 operator *(float a, Vec2 b) { return b * a; }

        public static explicit operator Vec3(Vec2 vec)
        {
            return new Vec3(vec, 0);
        }

        public static explicit operator Vec2(Vector2 vec)  // explicit byte to digit conversion operator
        {
            return new Vec2(vec.X, vec.Y);
        }

        public static explicit operator Vec2(Vector3 vec)
        {
            return new Vec2(vec.X, vec.Y);
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
        public Vec3(float xyz) : base(xyz, 3)
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

    class Vec4 : Vector
    {
        public float X { get { return _data[0]; } }
        public float Y { get { return _data[1]; } }
        public float Z { get { return _data[2]; } }
        public float W { get { return _data[3]; } }
        public override int Length { get { return 4; } }

        public Vec4() : base(0, 3)
        { }

        public Vec4(float x, float y, float z, float w) : base(new float[] { x, y, z, w })
        { }

        public Vec4(Vec4 copy) : base(copy)
        { }

        public Vec4(float[] copy) : base(copy)
        { Debug.Assert(copy.Length == 4); }

        public Vec4(Vec3 a, float b) : base(new float[] { a.X, a.Y, a.Z, b }) { }

        /// <summary>
        /// Vector with dim elements set to v.
        /// </summary>
        /// <param name="v"></param>
        /// <param name="dim"></param>
        public Vec4(float xyzw) : base(xyzw, 4)
        { }

        public static explicit operator Vec4(SlimDX.Vector4 vec)  // explicit byte to digit conversion operator
        {
            return new Vec4(vec.X, vec.Y, vec.Z, vec.W);
        }

        public static Vec4 operator -(Vec4 a)
        {
            return new Vec4(-a.X, -a.Y, -a.Z, -a.W);
        }

        public static Vec4 operator *(Vec4 a, float b)
        {
            return new Vec4(a.X * b, a.Y * b, a.Z * b, a.W * b);
        }

        public static Vec4 operator *(float b, Vec4 a)
        {
            return a * b;
        }
    }
}
