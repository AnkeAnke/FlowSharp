using SlimDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    partial class VectorField
    {
        public class IntegratorRK4 : IntegratorEuler
        {
            public IntegratorRK4(VectorField field) : base(field)
            { }

            public override Status Step(ref Vector pos, out float stepLength)
            {
                Vector originPos = new Vector(pos);
                stepLength = 0;
                Status status;
                Vector v0, v1, v2, v3;

                // v0
                status = CheckPosition(pos, out v0);

                // Check original position and vector length.
                if (status != Status.OK)
                {
                    pos += v0;
                    return status;
                }
                if (!ScaleAndCheckVector(v0))
                {
                    pos += v0;
                    return Status.CP;
                }
                

                // v1
                status = CheckPosition(pos + v0 / 2, out v1);

                // Check original position and vector length.
                if (status != Status.OK)
                {
                    pos += v1;
                    return status;
                }
                if (!ScaleAndCheckVector(v1))
                {
                    pos += v1;
                    return Status.CP;
                }

                // v2
                status = CheckPosition(pos + v1 / 2, out v2);

                // Check original position and vector length.
                if (status != Status.OK)
                {
                    pos += v2;
                    return status;
                }
                if (!ScaleAndCheckVector(v0))
                {
                    pos += v2;
                    return Status.CP;
                }

                // v3
                status = CheckPosition(pos + v2, out v3);

                // Check original position and vector length.
                Vector dir = (v0 + (v1 + v2) * 2 + v3) / 6;

                pos += dir;
                if (status != Status.OK)
                    return status;
                if (!ScaleAndCheckVector(dir))
                    return Status.CP;

                stepLength = dir.LengthEuclidean();

                return status;
            }
        }

        public class IntegratorRK4Repelling : IntegratorEuler
        {
            public Line Core { get; set; }
            public float Force { get; set; }
            public IntegratorRK4Repelling(VectorField field, Line core, float outwardForce) : base(field)
            {
                Core = core;
                Force = outwardForce;
            }

            protected Vector Repell(Vector pos)
            {
                Vector4 dir;
                float dist = Core.DistanceToPointInZ((Vector4)pos, out dir);
                dir = (Vector4)pos - dir;
                dir /= dist;
                return ((Vec4)(dir * Force)).SubVec(pos.Length);
            }

            public override Status Step(ref Vector pos, out float stepLength)
            {
                stepLength = 0;

                return Status.INVALID;
            }
        }
    }
}
