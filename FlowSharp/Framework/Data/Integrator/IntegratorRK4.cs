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

            public override Status Step(Vector pos, Vector sample, Vector inertial, out Vector nextPos, out Vector nextSample, out float stepLength)
            {
                nextPos = new Vector(pos);
                stepLength = 0;
                Status status;
                Vector v0, v1, v2, v3;

                // v0
                v0 = new Vector(sample);
                if (!ScaleAndCheckVector(v0, out v0))
                {
                    nextSample = v0;
                    return Status.CP;
                }
                status = CheckPosition(pos + v0 / 2, inertial, out v1);
                if (status != Status.OK)
                {
                    nextSample = v1;
                    return status;
                }

                // v1
                if (!ScaleAndCheckVector(v1, out v1))
                {
                    nextSample = v1;
                    return Status.CP;
                }
                status = CheckPosition(pos + v1 / 2, inertial, out v2);
                if (status != Status.OK)
                {
                    nextSample = v2;
                    return status;
                }

                // v2
                if (!ScaleAndCheckVector(v2, out v2))
                {
                    nextSample = v2;
                    return Status.CP;
                }
                status = CheckPosition(pos + v2, inertial, out v3);
                if (status != Status.OK)
                {
                    nextSample = v3;
                    return status;
                }

                // v3
                if (!ScaleAndCheckVector(v3, out v3))
                {
                    nextSample = v3;
                    return Status.CP;
                }

                Vector dir = (v0 + (v1 + v2) * 2 + v3) / 6;
                nextPos += dir;
                stepLength = dir.LengthEuclidean();

                return CheckPosition(nextPos, inertial, out nextSample);
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
                return ((Vec4)(dir * Force)).ToVec(pos.Length);
            }

            public override Status Step(Vector pos, Vector sample, Vector inertial, out Vector nextPos, out Vector nextSample, out float stepLength)
            {
                nextPos = new Vector(pos);
                stepLength = 0;
                Status status;

                Vector v0, v1, v2, v3;

                // v0
                v0 = new Vector(sample) + Repell(pos);
                if (!ScaleAndCheckVector(v0, out v0))
                {
                    nextSample = v0;
                    return Status.CP;
                }
                status = CheckPosition(pos + v0 / 2, inertial, out v1);
                if (status != Status.OK)
                {
                    nextSample = v1;
                    return status;
                }

                // v1
                v1 += Repell(pos + v0 / 2);
                if (!ScaleAndCheckVector(v1, out v1))
                {
                    nextSample = v1;
                    return Status.CP;
                }
                status = CheckPosition(pos + v1 / 2, inertial, out v2);
                if (status != Status.OK)
                {
                    nextSample = v2;
                    return status;
                }

                // v2
                v2 += Repell(pos + v1 / 2);
                if (!ScaleAndCheckVector(v2, out v2))
                {
                    nextSample = v2;
                    return Status.CP;
                }
                status = CheckPosition(pos + v2, inertial, out v3);
                if (status != Status.OK)
                {
                    nextSample = v3;
                    return status;
                }

                // v3
                v3 += Repell(pos + v2);
                if (!ScaleAndCheckVector(v3, out v3))
                {
                    nextSample = v3;
                    return Status.CP;
                }

                Vector dir = (v0 + (v1 + v2) * 2 + v3) / 6;
                nextPos += dir;
                stepLength = dir.LengthEuclidean();
                nextSample = v3;

                return CheckPosition(nextPos, inertial, out sample);
            }
        }
    }
}
