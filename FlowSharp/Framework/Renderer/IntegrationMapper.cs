using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    abstract class IntegrationMapper : DataMapper
    {
        protected LineSet IntegratePoints<P>(PointSet<P> points, float? startStep) where P : Point
        {
            return null;
        } 

    }
}
