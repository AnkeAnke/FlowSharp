using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManagedCuda;
using SlimDX.Direct3D11;
using SlimDX;
using ManagedCuda.VectorTypes;
using SlimDX.DXGI;
using ManagedCuda.BasicTypes;
using System.IO;
using System.Reflection;

namespace FlowSharp
{
    class FlowMapUncertain : AlgorithmCuda
    {
        public static int BLOCK_SIZE { get; } = 30;
        public static float PARTICLE_DENSITY = 0.1f;

        protected Loader.SliceRange[] _ensembleRanges;
        protected RectlinearGrid _ensembleGrid;
        protected int _width { get { return _ensembleGrid.Size[0]; } }
        protected int _height { get { return _ensembleGrid.Size[1]; } }
        protected int _numMembers { get { return _ensembleGrid.Size[2]; } }
        protected int _endTime;
        protected int _startTime;
        public int CurrentTime;
        protected float _texInvalidValue;

        protected CudaArray2D _t0X, _t0Y, _t1X, _t1Y;
        public Texture2D FlowMap { get; protected set; }
        protected CudaDeviceVariable<float> _pongFlowMap;
        protected CudaGraphicsInteropResourceCollection _cudaDxMapper;
        protected static CudaKernel _advectParticlesKernel;
        protected static CudaKernel _copyMapDataKernel;

        public FlowMapUncertain(Texture2D input, Loader.SliceRange fieldEnsemble, int startTime, int endTime)
        {

        }

        public FlowMapUncertain(Int2 pos, Loader.SliceRange[] fieldEnsemble, int startTime, int endTime)
        {
            // ~~~~~~~~~~~~~~ Copy relevant data ~~~~~~~~~~~~~~ \\
            _endTime = endTime;
            // Keep for loading the other time steps.
            _ensembleRanges = fieldEnsemble;

            // Setup a point texture.
            SetupPoint(pos, startTime);
        }
        /// <summary>
        /// Setup as empty map with only one value at 1.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="fieldEnsemble"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        public void SetupPoint(Int2 pos, int startTime)
        {
            // ~~~~~~~~~~~~~~ Copy relevant data ~~~~~~~~~~~~~~ \\
            // Count up when advection was executed.
            CurrentTime = startTime;
            _startTime = startTime;

            // ~~~~~~~~~~~~ Load ensemble ~~~~~~~~~~~~ \\
            // Load fields first to get the grid size.
            //Loader ncFile = new Loader(RedSea.Singleton.DataFolder + (_startTime + 1) + RedSea.Singleton.FileName);
            //ScalarField t0X = ncFile.LoadFieldSlice(_ensembleRanges[0]);
            //ScalarField t0Y = ncFile.LoadFieldSlice(_ensembleRanges[1]);
            //ncFile.Close();

            Loader ncFile = new Loader(RedSea.Singleton.DataFolder + (_startTime + 1) + RedSea.Singleton.FileName);
            ScalarField t1X = ncFile.LoadFieldSlice(_ensembleRanges[0]);
            ScalarField t1Y = ncFile.LoadFieldSlice(_ensembleRanges[1]);
            ncFile.Close();

            // ~~~~~~~~~~~~~~ Copy relevant data ~~~~~~~~~~~~~~ \\
            // Keep for plane creation and size reference.
            _ensembleGrid = t1X.Grid as RectlinearGrid;
            // Mapper for binding the SlimDX texture to CUDA easily.
            _cudaDxMapper = new CudaGraphicsInteropResourceCollection();
            // Tell CUDA which value is a border.
            _texInvalidValue = t1X.InvalidValue??float.MaxValue;

            // ~~~~~~~~~~~~ Fill CUDA resources ~~~~~~~~~~~~ \\
            // All members are above each other.
            int vHeight = _height * _numMembers;
            //// vX, t=0
            //_t0X = new CudaArray2D(CUArrayFormat.Float, _width, vHeight, CudaArray2DNumChannels.One);
            //_t0X.CopyFromHostToThis<float>(t0X.Data);
            //new CudaTextureArray2D(_advectParticlesKernel, "vX_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0X);

            //// vY, t=0
            //_t0Y = new CudaArray2D(CUArrayFormat.Float, _width, vHeight, CudaArray2DNumChannels.One);
            //_t0Y.CopyFromHostToThis<float>(t0Y.Data);
            //new CudaTextureArray2D(_advectParticlesKernel, "vY_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0Y);

            // vX, t=1
            _t1X = new CudaArray2D(CUArrayFormat.Float, _width, vHeight, CudaArray2DNumChannels.One);
            _t1X.CopyFromHostToThis<float>(t1X.Data);
            new CudaTextureArray2D(_advectParticlesKernel, "vX_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1X);

            // vY, t=1
            _t1Y = new CudaArray2D(CUArrayFormat.Float, _width, vHeight, CudaArray2DNumChannels.One);
            _t1Y.CopyFromHostToThis<float>(t1Y.Data);
            new CudaTextureArray2D(_advectParticlesKernel, "vY_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1Y);

            // ~~~~~~~~~~~~~ Create texture ~~~~~~~~~~~~~~~~~~~~ \\
            // Create texture. Completely zero, except for one point.
            Texture2DDescription desc = new Texture2DDescription
            {
                ArraySize = 1,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                Format = Format.R32_Float,
                Width = _width,
                Height = _height,
                MipLevels = 1,
                OptionFlags = ResourceOptionFlags.None,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default
            };

            // Put field data into stream/rectangle object
            float[] zeros = new float[_width * _height];
            Array.Clear(zeros, 0, zeros.Length);

            // Fill the empty texture.
            DataRectangle texData = new DataRectangle(_width * sizeof(float), new DataStream(zeros, true, true));
            _pongFlowMap = new CudaDeviceVariable<float>(_width * _height);//new Texture2D(_device, desc, texData);
            // Magically, copy to device happens here.
            _pongFlowMap = zeros;

            // Add one pixel for integration.
            zeros[pos.X + pos.Y * _width] = 1;
            texData = new DataRectangle(_width * sizeof(float), new DataStream(zeros, true, true));

            // Create texture.
            FlowMap = new Texture2D(_device, desc, texData);

            // ~~~~~~~~~ Make textures mappable to CUDA ~~~~~~~~~~ \\
            _cudaDxMapper.Add(new CudaDirectXInteropResource(FlowMap.ComPointer, CUGraphicsRegisterFlags.None, CudaContext.DirectXVersion.D3D11));


            _cudaDxMapper.MapAllResources();
            CudaArray2D lastFlowMap = _cudaDxMapper[0].GetMappedArray2D(0, 0);
            new CudaTextureArray2D(_advectParticlesKernel, "flowMap", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, lastFlowMap);
            _cudaDxMapper.UnmapAllResources();
        }
        public void Step(float stepSize)
        {
            // Load the next vector fields to device memory.
            LoadNextField();
            _cudaDxMapper.MapAllResources();
            CudaArray2D lastFlowMap = _cudaDxMapper[0].GetMappedArray2D(0, 0);

            // Advect from each member to each member. In each block, the same configuration is choosen.
            dim3 grid = new dim3((int)((float)_width/BLOCK_SIZE + 0.5f), (int)((float)_height/BLOCK_SIZE + 0.5f), _numMembers);

            // Advect a block in each member-member combination.
            dim3 threads = new dim3(BLOCK_SIZE, BLOCK_SIZE);

            _advectParticlesKernel.GridDimensions = grid;
            _advectParticlesKernel.BlockDimensions = threads;
            // (float* mapT1, const int width, const int height, const int numMembers, /*float timeScale, */ float stepSize, float minDensity, float invalid)
            _advectParticlesKernel.Run(_pongFlowMap.DevicePointer, _width, _height, _numMembers, stepSize, 0.000001f, _texInvalidValue);

            

            // Swap the Texture2D handles.
            CudaSurfObject surf = new CudaSurfObject(lastFlowMap);
            grid.z = 1;
            _copyMapDataKernel.GridDimensions = grid;
            _copyMapDataKernel.BlockDimensions = threads;
            _copyMapDataKernel.Run(surf.SurfObject, _pongFlowMap.DevicePointer, _width);

            _cudaDxMapper.UnmapAllResources();
        }

        protected void LoadNextField()
        {
            // Keep t1 timestep as new t0. Update mapping on device side.
            _t0X = _t1X;
            new CudaTextureArray2D(_advectParticlesKernel, "vX_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0X);
            _t0Y = _t1Y;
            new CudaTextureArray2D(_advectParticlesKernel, "vY_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0Y);
            CurrentTime++;

            // Load new t1.
            Loader ncFile = new Loader(RedSea.Singleton.DataFolder + (CurrentTime + 1) + RedSea.Singleton.FileName);
            ScalarField t1X = ncFile.LoadFieldSlice(_ensembleRanges[0]);
            ScalarField t1Y = ncFile.LoadFieldSlice(_ensembleRanges[1]);
            ncFile.Close();

            // All members are above each other.
            int vHeight = _height * _numMembers;

            // vX, t=1
            _t1X = new CudaArray2D(CUArrayFormat.Float, _width, vHeight, CudaArray2DNumChannels.One);
            _t1X.CopyFromHostToThis(t1X.Data);
            new CudaTextureArray2D(_advectParticlesKernel, "vX_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1X);

            // vY, t=1
            _t1Y = new CudaArray2D(CUArrayFormat.Float, _width, vHeight, CudaArray2DNumChannels.One);
            _t1Y.CopyFromHostToThis(t1Y.Data);
            new CudaTextureArray2D(_advectParticlesKernel, "vY_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1Y);
        }

        public FieldPlane GetPlane(Plane plane)
        {
            FieldPlane flowMap = new FieldPlane(plane, _ensembleGrid, FlowMap, _ensembleGrid.Size.ToInt2(), 0, float.MaxValue, FieldPlane.RenderEffect.DEFAULT);
            return flowMap;
        }

        public static void Initialize()
        {
             CUmodule module = _context.LoadModulePTX("Framework/Algorithms/Kernels/FlowMapUncertain.ptx");
            //__global__ void FlowMapStep(cudaTextureObject_t mapT0, float* mapT1, const int width, const int height, const int numMembers, const float particleDensity, /*float timeScale, */ float stepSize, float minDensity)
            _advectParticlesKernel = new CudaKernel("FlowMapStep", module, _context);
            _copyMapDataKernel = new CudaKernel("FlowMapUpdate", module, _context);
            //_advectParticlesKernel = _context.LoadKernelPTX("Framework/Algorithms/Kernels/FlowMapUncertain.ptx", "FlowMapStep", new CUJITOption[] { }, null);
        }
    }
}
