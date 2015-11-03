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
        protected int _time, _endTime;

        protected CudaTextureArray2D _t0X, _t0Y, _t1X, _t1Y;
        public Texture2D FlowMap { get; protected set; }
        protected Texture2D _pongFlowMap;
        protected CudaGraphicsInteropResourceCollection _cudaDxMapper;
        protected static CudaKernel _advectParticlesKernel;

        public void Setup(Texture2D input, Loader.SliceRange fieldEnsemble, int startTime, int endTime)
        {

        }

        public void SetupPixel(Int2 pos, Loader.SliceRange[] fieldEnsemble, int startTime, int endTime)
        {
            // ~~~~~~~~~~~~ Load ensemble ~~~~~~~~~~~~ \\
            // Load fields first to get the grid size.
            Loader ncFile = new Loader(RedSea.Singleton.DataFolder + startTime + RedSea.Singleton.FileName);
            ScalarField t0X = ncFile.LoadFieldSlice(fieldEnsemble[0]);
            ScalarField t0Y = ncFile.LoadFieldSlice(fieldEnsemble[1]);
            ncFile.Close();

            ncFile = new Loader(RedSea.Singleton.DataFolder + (startTime + 1) + RedSea.Singleton.FileName);
            ScalarField t1X = ncFile.LoadFieldSlice(fieldEnsemble[0]);
            ScalarField t1Y = ncFile.LoadFieldSlice(fieldEnsemble[1]);
            ncFile.Close();

            // ~~~~~~~~~~~~~~ Copy relevant data ~~~~~~~~~~~~~~ \\
            // Count up when advection was executed.
            _time = startTime;
            _endTime = endTime;
            // Keep for plane creation and size reference.
            _ensembleGrid = t0X.Grid as RectlinearGrid;
            // Keep for loading the other time steps.
            _ensembleRanges = fieldEnsemble;
            // Mapper for binding the SlimDX texture to CUDA easily.
            _cudaDxMapper = new CudaGraphicsInteropResourceCollection();

            // ~~~~~~~~~~~~ Fill CUDA resources ~~~~~~~~~~~~ \\
            // vX, t=0
            CudaArray2D vecData = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            vecData.CopyFromHostToThis<float>(t0X.Data);
            _t0X = new CudaTextureArray2D(_advectParticlesKernel, "vX_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, vecData);

            // vY, t=0
            vecData = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            vecData.CopyFromHostToThis<float>(t0Y.Data);
            _t0Y = new CudaTextureArray2D(_advectParticlesKernel, "vY_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, vecData);

            // vX, t=1
            vecData = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            vecData.CopyFromHostToThis<float>(t1X.Data);
            _t1X = new CudaTextureArray2D(_advectParticlesKernel, "vX_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, vecData);

            // vY, t=1
            vecData = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            vecData.CopyFromHostToThis<float>(t1Y.Data);
            _t1Y = new CudaTextureArray2D(_advectParticlesKernel, "vY_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, vecData);

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
            _pongFlowMap = new Texture2D(_device, desc, texData);

            // Add one pixel for integration.
            zeros[pos.X + pos.Y * _width] = 1;
            texData = new DataRectangle(_width * sizeof(float), new DataStream(zeros, true, true));

            // Create texture.
            FlowMap = new Texture2D(_device, desc, texData);

            // ~~~~~~~~~ Make textures mappable to CUDA ~~~~~~~~~~ \\
            _cudaDxMapper.Add(new CudaDirectXInteropResource(_pongFlowMap.ComPointer, CUGraphicsRegisterFlags.None, CudaContext.DirectXVersion.D3D11));
            _cudaDxMapper.Add(new CudaDirectXInteropResource(FlowMap.ComPointer, CUGraphicsRegisterFlags.None, CudaContext.DirectXVersion.D3D11));
        }
        public void Step()
        {
            _cudaDxMapper.MapAllResources();
            CudaDeviceVariable<Vector2> currentFlowMap = _cudaDxMapper[0].GetMappedPointer<Vector2>();
            // Advect from each member to each member. In each block, the same configuration is choosen.
            dim3 grid = new dim3((int)((float)_width/BLOCK_SIZE + 0.5f), (int)((float)_height/BLOCK_SIZE + 0.5f), _numMembers * _numMembers);
            // Advect a block in each member-member combination.
            dim3 threads = new dim3(BLOCK_SIZE, BLOCK_SIZE);

            _advectParticlesKernel.GridDimensions = grid;
            _advectParticlesKernel.BlockDimensions = threads;
            _advectParticlesKernel.Run(currentFlowMap.DevicePointer, _ensembleGrid.Size[0], _ensembleGrid.Size[1], _ensembleGrid.Size[2], PARTICLE_DENSITY);
            _cudaDxMapper.UnmapAllResources();
        }

        public FieldPlane GetPlane(Plane plane)
        {
            FieldPlane flowMap = new FieldPlane(plane, _ensembleGrid, FlowMap, _ensembleGrid.Size.ToInt2(), _time, 0, FieldPlane.RenderEffect.DEFAULT);
            return flowMap;
        }

        public static void Initialize()
        {
            _advectParticlesKernel = _context.LoadKernelPTX("FlowMapUncertain", "FlowMapStep", new CUJITOption[] { }, null);
        }
    }
}
