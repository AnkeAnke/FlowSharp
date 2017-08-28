using SlimDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    static class Vector3Extensions
    {
        public static bool IsPositive(this SlimDX.Vector3 vec)
        {
            return vec.X >= 0 && vec.Y >= 0 && vec.Z >= 0;
        }

        public static bool IsLess (this SlimDX.Vector3 a, SlimDX.Vector3 b)
        {
            return a.X < b.X && a.Y < b.Y && a.Z < b.Z;
        }

        public static bool IsLess(this SlimDX.Vector3 a,float b)
        {
            return a.X < b && a.Y < b && a.Z < b;
        }

        public static bool IsLessOrEqual(this SlimDX.Vector3 a, SlimDX.Vector3 b)
        {
            return a.X <= b.X && a.Y <= b.Y && a.Z <= b.Z;
        }

        public static bool IsLargerEqual(this SlimDX.Vector3 a, SlimDX.Vector3 b)
        {
            return a.X >= b.X && a.Y >= b.Y && a.Z >= b.Z;
        }

        public static Vector3 Divide(this SlimDX.Vector3 a, SlimDX.Vector3 b)
        {
            return new Vector3(a.X / b.X, a.Y / b.Y, a.Z / b.Z);
        }

        public static Vector3 Multiply(this Vector3 a, Vector3 b)
        {
            return new Vector3(a.X * b.X, a.Y * b.Y, a.Z * b.Z);
        }

        public static float Sum(this Vector3 a)
        {
            return a.X + a.Y + a.Z;
        }

        public static float Sum(this Vector4 a)
        {
            return a.X + a.Y + a.Z + a.W;
        }

        public static float Min(this Vector3 a)
        {
            return Math.Min(Math.Min(a.X, a.Y), a.Z);
        }

        public static float Max(this Vector3 a)
        {
            return Math.Max(Math.Max(a.X, a.Y), a.Z);
        }

        //public static Sign[] Compare(this SlimDX.Vector3 a, SlimDX.Vector3 b)
        //{
        //    return new Sign[] { (Sign)(a.X >= b.X), (Sign)(a.Y >= b.Y), (Sign)(a.Z >= b.Z) };
        //}
        public static int CompareForIndex(this SlimDX.Vector3 a, SlimDX.Vector3 b)
        {
            return ((a.X >= b.X) ? 1 : 0) + ((a.Y >= b.Y) ? 2 : 0) + ((a.Z >= b.Z) ? 4 : 0);
        }
    }
}
