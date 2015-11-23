using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManagedCuda;
using SlimDX;
using SlimDX.Direct3D11;

namespace FlowSharp
{
    abstract class AlgorithmCuda
    {
        protected CudaKernel _kernel;
        protected static CudaContext _context;
        protected static Device _device;

        public static void Initialize(CudaContext context, Device device)
        {
            _context = context;
            _device = device;
        }

        public abstract void CompleteRange(Int2 selection);
        public abstract void Subrange(Int2 min, Int2 max, Int2 selection);
    }
}
