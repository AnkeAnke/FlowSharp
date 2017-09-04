using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class VectorFieldInertial : VectorField
    {
        /// <summary>
        /// The field state is compounded of position and last sampled velocity. Inertia basically means that the velocity is applied "one step later".
        /// </summary>
        public override int NumVectorDimensions { get { return base.NumVectorDimensions * 2; } }

        //public override int NumDimensions { get { return base.NumVectorDimensions * 2; } }

        public float ResponseTime;

        public VectorFieldInertial(VectorField field, float responseTime = 0.01f) : base(field.Data, field.Grid)
        {
            ResponseTime = responseTime;
        }

        public VectorFieldInertial(VectorData data, FieldGrid grid, float responseTime = 0.01f) : base(data, grid)
        {
            ResponseTime = responseTime;
        }

        protected VectorFieldInertial(float responseTime = 0.01f)
        {
            ResponseTime = responseTime;
        }

        public VectorFieldInertial(ScalarField[] scalars, float responseTime = 0.01f) : base(scalars)
        {
            ResponseTime = responseTime;
        }

        public override Vector Sample(Vector state)
        {
            // The size of the original field. Appending the last velocity, we get a 2n-D field.
            int n = Data.VectorLength;
            Vector v = state.SubVec(Data.VectorLength, Data.VectorLength);

            // Sample the field and save that value as v.
            Vector u_t = base.Sample(state.SubVec(Data.VectorLength)) - v;

            // The position is advanced by the last sampled position.
            Vector x_t = ResponseTime * v;
            
            return u_t == null? null : x_t.Append(u_t);
        }

        public Vector Sample(VectorRef state, Index neighs, VectorRef weights)
        {
            Vector u_t = new Vector(Data.VectorLength);
            for (int n = 0; n < neighs.Length; ++n)
                u_t += this[neighs[n]] * weights[n];

            // The position is advanced by the last sampled position.
            Vector x_t = ResponseTime * state.SubVec(Data.VectorLength, Data.VectorLength);

            return u_t == null ? null : x_t.Append(u_t);
        }
        //public override VectorRef Sample(int gridPosition)
        //{
        //    return base.Sample(gridPosition);
        //}
    }
}
