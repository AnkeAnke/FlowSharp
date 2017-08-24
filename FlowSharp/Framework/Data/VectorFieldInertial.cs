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
        public override Vector Sample(Vector position, Vector lastDirection = null)
        {
            return base.Sample(position, lastDirection) + lastDirection * Inertia;
        }
    }
}
