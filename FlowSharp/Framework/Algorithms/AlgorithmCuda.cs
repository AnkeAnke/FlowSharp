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
    class AlgorithmCuda
    {
        protected CudaKernel _kernel;
        protected static CudaContext _context;
        protected static Device _device;

        public static void Initialize(CudaContext context, Device device)
        {
            _context = context;
            _device = device;
        }
        
    }
}
