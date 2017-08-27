using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class VectorFieldInertial : VectorField
    {
        public float Inertia;

        public VectorFieldInertial(VectorField field, float inertia = 0.01f) : base(field.Data, field.Grid)
        {
            Inertia = inertia;
        }

        public VectorFieldInertial(VectorData data, FieldGrid grid, float inertia = 0.01f) : base(data, grid)
        {
            Inertia = inertia;
        }

        protected VectorFieldInertial(float inertia = 0.01f)
        {
            Inertia = inertia;
        }

        public VectorFieldInertial(ScalarField[] scalars, float inertia = 0.01f) : base(scalars)
        {
            Inertia = inertia;
        }

        public override Vector Sample(Vector position, Vector lastDirection)
        {
            Vector s = base.Sample(position, lastDirection);
            return s != null? s + lastDirection * Inertia : s;
        }
    }
}
