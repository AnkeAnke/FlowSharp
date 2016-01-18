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
using SliceRange = FlowSharp.LoaderNCF.SliceRange;
using Debug = System.Diagnostics.Debug;
namespace FlowSharp
{
    class CutDiffusion : AlgorithmCuda
    {
        public static int BLOCK_SIZE { get; } = 15;
        public static int NUM_PARTICLES = 512;
        public static int NUM_PARTICLES_REFERENCE = 1024;

        protected VectorFieldUnsteady _velocity;
        protected int _width { get { return _velocity.Size[0]; } }
        protected int _height { get { return _velocity.Size[1]; } }
        public int StartTime;
        public float EndTime;
        public int CurrentTime;

        protected CudaArray2D _t0X, _t0Y, _t1X, _t1Y;
        public Texture2D ReferenceMap { get; protected set; }
        public Texture2D CutMap { get; protected set; }
        protected CudaDeviceVariable<float> _pongReferencenMap;
        protected CudaGraphicsInteropResourceCollection _cudaDxMapper;

        //__global__ void LoadAdvectReference(float2* positions, int2 seed)
        protected static CudaKernel _loadAdvectReference;
        //__global__ void LoadAdvectCut(float2* positions, int2 origin)
        protected static CudaKernel _loadAdvectCut;
        //__global__ void AdvectCut(float2* positions, int2 origin)
        protected static CudaKernel _advectCut;
        //__global__ void AdvectReference(float2* positions)
        protected static CudaKernel _advectReference;
        //__global__ void AdvectStoreReference(float2* positions, float* referenceMap)
        protected static CudaKernel _advectStoreReference;
        //__global__ void FetchSumStoreCut(cudaSurfaceObject_t cuts, float2* positions, float* referenceMap)
        protected static CudaKernel _fetchSumStoreCut;
        //__global__ void ReferenceToTexture(cudaSurfaceObject_t referenceTex, float* data)
        protected static CudaKernel _referenceToTexture;

        protected CudaStream _streamCut;
        protected CudaStream _streamRef;
        protected CudaDeviceVariable<float2> _particlesRef;
        protected CudaDeviceVariable<float2> _particlesCut;

        protected Int2 _selection;

        protected int _cellToSeedRatio;

        protected bool _initialized = false;

        public CutDiffusion(Texture2D input, LoaderNCF.SliceRange fieldEnsemble, int startTime, float time)
        {

        }

        public CutDiffusion(VectorFieldUnsteady velocity, int startTime, float integrationTime)
        {
            // ~~~~~~~~~~~~~~ Copy relevant data ~~~~~~~~~~~~~~ \\
            _velocity = velocity;
            _selection = new Int2(10, 10);


        }
        /// <summary>
        /// Setup as empty map with only one value at 1.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="fieldEnsemble"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        public void SetupMap(Int2 pos, int startTime, float integrationTime, int cellToSeedRatio)
        {
            // ~~~~~~~~~~~~~~ Copy relevant data ~~~~~~~~~~~~~~ \\
            // Count up when advection was executed.
            CurrentTime = startTime;
            StartTime = startTime;
            EndTime = startTime + integrationTime;
            _cellToSeedRatio = cellToSeedRatio;
            _selection = pos;

            if (!_initialized)
                InitializeDeviceStorage();
        }

        protected void InitializeDeviceStorage()
        {
            // ~~~~~~~~~~~~~~~ Allocate "Cache" ~~~~~~~~~~~~~~~~ \\
            // Buffer for advecting reference particles.
            _particlesRef = new CudaDeviceVariable<float2>(NUM_PARTICLES_REFERENCE);
            _particlesCut = new CudaDeviceVariable<float2>(NUM_PARTICLES * _width * _height);

            _t1X = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            _t1Y = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            _t0X = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            _t0Y = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            new CudaTextureArray2D(_loadAdvectReference, "vX_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0X);
            new CudaTextureArray2D(_loadAdvectReference, "vY_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0Y);

            // Streams to allow for parallel execution.
            _streamCut = new CudaStream();
            _streamRef = new CudaStream();

            // ~~~~~~~~~~~~~~ Set CUDA constants ~~~~~~~~~~~~~~~ \\
            // For selection advection.
            _loadAdvectReference.SetConstantVariable("Width", _width);
            _loadAdvectReference.SetConstantVariable("Height", _height);
            _loadAdvectReference.SetConstantVariable("WidthCells", _width * _cellToSeedRatio);
            _loadAdvectReference.SetConstantVariable("HeightCells", _height * _cellToSeedRatio);
            _loadAdvectReference.SetConstantVariable("CellToSeedRatio", _cellToSeedRatio);
            _loadAdvectReference.SetConstantVariable("Invalid", _velocity.InvalidValue ?? float.MaxValue);
            _loadAdvectReference.SetConstantVariable("TimeInGrid", RedSea.Singleton.DomainScale);

            // ~~~~~~~~~~~~~~ Fill CUDA resources ~~~~~~~~~~~~~~ \\
            // vX, t=1
            _t1X.CopyFromHostToThis<float>((_velocity.GetTimeSlice(StartTime).Scalars[0] as ScalarField).Data);
            new CudaTextureArray2D(_loadAdvectReference, "vX_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1X);

            // vY, t=1
            _t1Y.CopyFromHostToThis<float>((_velocity.GetTimeSlice(StartTime).Scalars[1] as ScalarField).Data);
            new CudaTextureArray2D(_loadAdvectReference, "vY_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1Y);

            // ~~~~~~~~~~~~~ Create texture ~~~~~~~~~~~~~~~~~~~~ \\
            // Create texture. Completely zero, except for one point.
            Texture2DDescription desc = new Texture2DDescription
            {
                ArraySize = 1,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                Format = Format.R32_Float,
                Width = _width * _cellToSeedRatio,
                Height = _height * _cellToSeedRatio,
                MipLevels = 1,
                OptionFlags = ResourceOptionFlags.None,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default
            };

            // Fill the empty texture with zeros.
            _pongReferencenMap = new CudaDeviceVariable<float>(_width * _height * _cellToSeedRatio * _cellToSeedRatio);
            _pongReferencenMap.Memset(0); // = zeros;

            // Create texture.
            ReferenceMap = new Texture2D(_device, desc);
            desc.Height = _height;
            desc.Width = _width;
            CutMap = new Texture2D(_device, desc);

            // ~~~~~~~~~ Make textures mappable to CUDA ~~~~~~~~~~ \\
            _cudaDxMapper = new CudaGraphicsInteropResourceCollection();
            _cudaDxMapper.Add(new CudaDirectXInteropResource(ReferenceMap.ComPointer, CUGraphicsRegisterFlags.None, CudaContext.DirectXVersion.D3D11));
            _cudaDxMapper.Add(new CudaDirectXInteropResource(CutMap.ComPointer, CUGraphicsRegisterFlags.None, CudaContext.DirectXVersion.D3D11));

            //            _cudaDxMapper.MapAllResources();
            //            CudaArray2D lastFlowMap = _cudaDxMapper[0].GetMappedArray2D(0, 0);
            ////            new CudaTextureArray2D(_loadAdvectReference, "selectionMap", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, lastFlowMap);
            //            _cudaDxMapper.UnmapAllResources();

            // ~~~~~~~~~~~~~~~ Set Kernel Sizes ~~~~~~~~~~~~~~~ \\
            SetKernelSizes();
            _initialized = true;
        }
        public void Advect(float stepSize, float variance = 2.0f, Int2 selection = null)
        {
            LoadNextField();
            _selection = selection ?? _selection;

            // ~~~~~~~~~~~~~~~ Upload relevant data ~~~~~~~~~~~~~~~ \\
            _cudaDxMapper.MapAllResources();
            CudaArray2D texRef = _cudaDxMapper[0].GetMappedArray2D(0, 0);
            CudaArray2D texCut = _cudaDxMapper[1].GetMappedArray2D(0, 0);

            _loadAdvectReference.SetConstantVariable("Variance", variance);
            _loadAdvectReference.SetConstantVariable("StepSize", stepSize);

            // Compute and upload integration time.
            float integrationStep = (CurrentTime + 1 > EndTime) ? EndTime - CurrentTime : 1.0f;
            integrationStep *= RedSea.Singleton.TimeScale;
            CurrentTime++;
            _loadAdvectReference.SetConstantVariable("IntegrationLength", integrationStep);

            bool severalSteps = false;

            // ~~~~~~~~~~~ Start Reference Map Integration ~~~~~~~~~~~ \\
            //__global__ void LoadAdvectReference(float2* positions, int2 seed)
            _loadAdvectReference.RunAsync(_streamRef.Stream, _particlesRef.DevicePointer, (int2)_selection);

            // ~~~~~~~~~~~ Start Cut Map Integration ~~~~~~~~~~~ \\
            //__global__ void LoadAdvectCut(float2* positions, int2 origin)
            _loadAdvectCut.RunAsync(_streamCut.Stream, _particlesCut.DevicePointer, new int2(0));

            // ~~~~~~~~~~~ Advect the Particle Buffers further ~~~~~~~~~~~ \\
            while (CurrentTime < EndTime)
            {
                severalSteps = true;
                // If we actually go in here, an integration time of 1 has to be set.
                Debug.Assert(integrationStep == RedSea.Singleton.TimeScale);

                // As texture loading happens inbetwen, a synchronization is performed automatically.
                // Thus, we always have the right vector fields bound.
                // The cut and reference streams are synchronized now, even though they would not need to be.
                LoadNextField();
                //__global__ void AdvectReference(float2 * positions)
                _advectReference.RunAsync(_streamRef.Stream, _particlesRef.DevicePointer);

                //__global__ void AdvectCut(float2* positions, int2 origin)
                _advectCut.RunAsync(_streamCut.Stream, _particlesCut.DevicePointer, new int2(0));
                CurrentTime++;
            }

            integrationStep = EndTime - CurrentTime + 1;
            integrationStep *= RedSea.Singleton.TimeScale;

            if (!severalSteps)
                integrationStep = 0;
            _loadAdvectReference.SetConstantVariable("IntegrationLength", integrationStep * RedSea.Singleton.DomainScale);

            //__global__ void AdvectStoreReference(float2* positions, float* referenceMap)
            _advectStoreReference.RunAsync(_streamRef.Stream, _particlesRef.DevicePointer, _pongReferencenMap.DevicePointer);

            if (severalSteps)
                //__global__ void AdvectCut(float2* positions, int2 origin)
                _advectCut.RunAsync(_streamCut.Stream, _particlesCut.DevicePointer, new int2(0));

            // Get surface objects of the textures.
            CudaSurfObject surfCut = new CudaSurfObject(texCut);
            CudaSurfObject surfRef = new CudaSurfObject(texRef);

            // Now, the streams need to wait for each other. Fetching from the cut threads needs a cultivated reference map.
            _context.Synchronize();

            //__global__ void FetchSumStoreCut(cudaSurfaceObject_t cuts, float2* positions, float* referenceMap)
            _fetchSumStoreCut.RunAsync(_streamRef.Stream, surfCut.SurfObject, _particlesCut.DevicePointer, _pongReferencenMap.DevicePointer);

            //__global__ void ReferenceToTexture(cudaSurfaceObject_t referenceTex, float* data)
            _referenceToTexture.RunAsync(_streamCut.Stream, surfRef.SurfObject, _pongReferencenMap.DevicePointer);

            _cudaDxMapper.UnmapAllResources();
        }

        protected void SetKernelSizes()
        {
            // Reference resources - one high-resolution map.
            // Only one seed currently.
            dim3 blocksRef = new dim3(1);
            // One thread per particle.
            dim3 threadsRef = new dim3(NUM_PARTICLES_REFERENCE, 1, 1);

            // Cut blocks - one particle cloud per seed point.
            // For each point in the domain, compute cut value.
            dim3 blocksCut = new dim3(_width, _height);
            // One thread per particle.
            dim3 threadsCut = new dim3(NUM_PARTICLES, 1, 1);

            // ~~~~~~~~~~~~~~ Set ~~~~~~~~~~~~~~~ \\
            _loadAdvectReference.GridDimensions = blocksRef;
            _loadAdvectReference.BlockDimensions = threadsRef;

            _loadAdvectCut.GridDimensions = blocksCut;
            _loadAdvectCut.BlockDimensions = threadsCut;

            _advectReference.GridDimensions = blocksRef;
            _advectReference.BlockDimensions = threadsRef;

            _advectCut.GridDimensions = blocksCut;
            _advectCut.BlockDimensions = threadsCut;

            _advectStoreReference.GridDimensions = blocksRef;
            _advectStoreReference.BlockDimensions = threadsRef;

            _fetchSumStoreCut.GridDimensions = blocksCut;
            _fetchSumStoreCut.BlockDimensions = threadsCut;

            // Copy one complete cell-map.
            dim3 blocksCells = new dim3((uint)Math.Ceiling((float)_width * _cellToSeedRatio / BLOCK_SIZE), (uint)Math.Ceiling((float)_height * _cellToSeedRatio / BLOCK_SIZE));
            dim3 threadsCells = new dim3(BLOCK_SIZE, BLOCK_SIZE, 1);

            _referenceToTexture.GridDimensions = blocksCells;
            _referenceToTexture.BlockDimensions = threadsCells;
        }

        protected void LoadNextField()
        {
            // Keep t1 timestep as new t0. Update mapping on device side.
            var tmp = _t0X;
            _t0X = _t1X;
            _t1X = tmp;
            new CudaTextureArray2D(_loadAdvectReference, "vX_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0X);
            tmp = _t0Y;
            _t0Y = _t1Y;
            _t1Y = tmp;
            new CudaTextureArray2D(_loadAdvectReference, "vY_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0Y);

            // Load new t1.
            ScalarField t1X = _velocity.GetTimeSlice(CurrentTime + 1).Scalars[0] as ScalarField;
            ScalarField t1Y = _velocity.GetTimeSlice(CurrentTime + 1).Scalars[1] as ScalarField;

            // vX, t=1
            //            _t1X = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            _t1X.CopyFromHostToThis(t1X.Data);
            new CudaTextureArray2D(_loadAdvectReference, "vX_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1X);


            // vY, t=1
            //            _t1Y = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            _t1Y.CopyFromHostToThis(t1Y.Data);
            new CudaTextureArray2D(_loadAdvectReference, "vY_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1Y);
        }

        public FieldPlane GetPlane(Plane plane)
        {
            FieldPlane flowMap = new FieldPlane(plane, CutMap, _velocity.Grid.Size.ToInt2(), 0, _velocity.InvalidValue ?? float.MaxValue, FieldPlane.RenderEffect.DEFAULT);
            return flowMap;
        }

        public static void Initialize()
        {
            //__constant__ float Variance = 1.0f;
            //// Change those two depending on cut or reference execution.
            //__constant__ int Width = 200;
            //__constant__ int Height = 200;
            //__constant__ int NumParticles = 1024;
            //__constant__ float TimeInGrid = 15.0f / 2.59f;
            //__constant__ float IntegrationLength = 1.0f;
            //__constant__ float StepSize = 0.3f;
            //__constant__ float Invalid = 3600000000;
            //__constant__ int CellToSeedRatio = 10;

            //__global__ void LoadAdvectReference(float2* positions, int2 seed)
            //__global__ void LoadAdvectCut(float2* positions, int2 origin)
            //__global__ void AdvectCut(float2* positions, int2 origin)
            //__global__ void AdvectReference(float2 * positions)
            //__global__ void AdvectStoreReference(float2* positions, float* referenceMap)
            //__global__ void FetchSumStoreCut(cudaSurfaceObject_t cuts, float2* positions, float* referenceMap)
            //__global__ void ReferenceToTexture(cudaSurfaceObject_t referenceTex, float* data)

            CUmodule module = _context.LoadModulePTX("Framework/Algorithms/Kernels/LocalDiffusion.ptx");

            _loadAdvectReference = new CudaKernel("LoadAdvectReference", module, _context);
            _loadAdvectCut = new CudaKernel("LoadAdvectCut", module, _context);
            _advectCut = new CudaKernel("AdvectCut", module, _context);
            _advectReference = new CudaKernel("AdvectReference", module, _context);
            _advectStoreReference = new CudaKernel("AdvectStoreReference", module, _context);
            _fetchSumStoreCut = new CudaKernel("FetchSumStoreCut", module, _context);
            _referenceToTexture = new CudaKernel("ReferenceToTexture", module, _context);
        }

        public override void CompleteRange(Int2 selection)
        {
            return;
        }

        public override void Subrange(Int2 min, Int2 max, Int2 selection)
        {
            return;
        }
    }

    class LocalDiffusion : AlgorithmCuda
    {
        protected static int NUM_ADJACENT_CELLS = 8;
        public static int BLOCK_SIZE { get; } = 15;
        public static int NUM_PARTICLES = 1;

        protected VectorFieldUnsteady _velocity;
        protected int _width { get { return _velocity.Size[0]; } }
        protected int _height { get { return _velocity.Size[1]; } }
        public int StartTime;
        public float EndTime;
        public int CurrentTime;

        protected CudaArray2D _t0X, _t0Y, _t1X, _t1Y;
        public Texture2D Map { get; protected set; }
        protected CudaGraphicsInteropResourceCollection _cudaDxMapper;

        //__global__ void LoadAdvectCut(float2* positions, int2 origin)
        protected static CudaKernel _loadAdvectCut;
        //__global__ void AdvectCut(float2* positions, int2 origin)
        protected static CudaKernel _advectCut;
        //__global__ void CutNeighbor(float* neighborMap, float2* positions, unsigned int offset, unsigned int neighborOffset, unsigned int neighbor)
        protected static CudaKernel _cutNeighbor;
        //__global__ void DeformationTensorFTLE (float* neighborMap, float2* positions, unsigned int offset, unsigned int neighborOffset, unsigned int neighbor)
        protected static CudaKernel _deformationTensorFTLE;

        //__global__ void ScanCut{Density|Min|Max|Range}(cudaSurfaceObject_t grads, float* cuts)
        protected static CudaKernel[] _scanStoreMeasure;
        //protected static CudaKernel _storeY;

        protected CudaStream[] _streamCuts;
        protected CudaDeviceVariable<float2> _particlesCut;
        protected CudaDeviceVariable<float> _neighbors;

        protected bool _initialized = false;

        public LocalDiffusion(Texture2D input, LoaderNCF.SliceRange fieldEnsemble, int startTime, float time)
        {

        }

        public LocalDiffusion(VectorFieldUnsteady velocity, int startTime, float integrationTime)
        {
            // ~~~~~~~~~~~~~~ Copy relevant data ~~~~~~~~~~~~~~ \\
            _velocity = velocity;
        }
        /// <summary>
        /// Setup as empty map with only one value at 1.
        /// </summary>
        /// <param name="fieldEnsemble"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        public void SetupMap(int startTime, float integrationTime)
        {
            // ~~~~~~~~~~~~~~ Copy relevant data ~~~~~~~~~~~~~~ \\
            // Count up when advection was executed.
            CurrentTime = startTime;
            StartTime = startTime;
            EndTime = startTime + integrationTime;

            if (!_initialized)
                InitializeDeviceStorage();

            // ~~~~~~~~~~~~~~ Fill CUDA resources ~~~~~~~~~~~~~~ \\
            // vX, t=1
            _t1X.CopyFromHostToThis<float>((_velocity.GetTimeSlice(StartTime).Scalars[0] as ScalarField).Data);
            new CudaTextureArray2D(_loadAdvectCut, "vX_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1X);

            // vY, t=1
            _t1Y.CopyFromHostToThis<float>((_velocity.GetTimeSlice(StartTime).Scalars[1] as ScalarField).Data);
            new CudaTextureArray2D(_loadAdvectCut, "vY_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1Y);
        }

        protected void InitializeDeviceStorage()
        {
            // ~~~~~~~~~~~~~~~ Allocate "Cache" ~~~~~~~~~~~~~~~~ \\
            // Buffer for advecting reference particles.
            _particlesCut = new CudaDeviceVariable<float2>(NUM_PARTICLES * _width * _height);
            _neighbors = new CudaDeviceVariable<float>((_width) * (_height) * NUM_ADJACENT_CELLS/2);

            _t1X = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            _t1Y = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            _t0X = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            _t0Y = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            new CudaTextureArray2D(_loadAdvectCut, "vX_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0X);
            new CudaTextureArray2D(_loadAdvectCut, "vY_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0Y);

            // Streams to allow for parallel execution.
            _streamCuts = new CudaStream[NUM_ADJACENT_CELLS];
            for(int i = 0; i < NUM_ADJACENT_CELLS; ++i)
                _streamCuts[i] = new CudaStream();

            // ~~~~~~~~~~~~~~ Set CUDA constants ~~~~~~~~~~~~~~~ \\
            // For selection advection.
            _loadAdvectCut.SetConstantVariable("Width", _width);
            _loadAdvectCut.SetConstantVariable("Height", _height);
            _loadAdvectCut.SetConstantVariable("Invalid", _velocity.InvalidValue ?? float.MaxValue);
            _loadAdvectCut.SetConstantVariable("TimeInGrid", 1);// RedSea.Singleton.DomainScale);

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

            // Create texture.
            Map = new Texture2D(_device, desc);

            // ~~~~~~~~~ Make textures mappable to CUDA ~~~~~~~~~~ \\
            _cudaDxMapper = new CudaGraphicsInteropResourceCollection();
            _cudaDxMapper.Add(new CudaDirectXInteropResource(Map.ComPointer, CUGraphicsRegisterFlags.None, CudaContext.DirectXVersion.D3D11));

            //            _cudaDxMapper.MapAllResources();
            //            CudaArray2D lastFlowMap = _cudaDxMapper[0].GetMappedArray2D(0, 0);
            ////            new CudaTextureArray2D(_loadAdvectReference, "selectionMap", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, lastFlowMap);
            //            _cudaDxMapper.UnmapAllResources();

            // ~~~~~~~~~~~~~~~ Set Kernel Sizes ~~~~~~~~~~~~~~~ \\
            SetKernelSizes();
            _initialized = true;
        }
        public void Advect(float stepSize, float variance = 2.0f, RedSea.DiffusionMeasure measure = RedSea.DiffusionMeasure.Density, uint neighborID = 0)
        {
            LoadNextField();

            // ~~~~~~~~~~~~~~~ Upload relevant data ~~~~~~~~~~~~~~~ \\
            _loadAdvectCut.SetConstantVariable("Variance", variance);
            _loadAdvectCut.SetConstantVariable("StepSize", (float)Math.Max(0.0000001f, stepSize));

            // Compute and upload integration time.
            float integrationStep = (CurrentTime + 1 > EndTime) ? EndTime - CurrentTime : 1.0f;
//            integrationStep *= RedSea.Singleton.TimeScale;
            CurrentTime++;
            _loadAdvectCut.SetConstantVariable("IntegrationLength", integrationStep);

            bool severalSteps = false;

            // ~~~~~~~~~~~ Start Map Integration ~~~~~~~~~~~ \\
            //__global__ void LoadAdvectCut(float2* positions, int2 origin)
            _loadAdvectCut.RunAsync(_streamCuts[0].Stream, _particlesCut.DevicePointer, new int2(0));

            // ~~~~~~~~~~~ Advect the Particle Buffers further ~~~~~~~~~~~ \\
            while (CurrentTime < EndTime)
            {
                severalSteps = true;
                // If we actually go in here, an integration time of "1" has to be set.
                Debug.Assert(integrationStep == 1); // RedSea.Singleton.TimeScale);

                // As texture loading happens inbetwen, a synchronization is performed automatically.
                // Thus, we always have the right vector fields bound.
                // The cut and reference streams are synchronized now, even though they would not need to be.
                LoadNextField();

                //__global__ void AdvectCut(float2* positions, int2 origin)
                _advectCut.RunAsync(_streamCuts[0].Stream, _particlesCut.DevicePointer, new int2(0));
                CurrentTime++;
            }

            integrationStep = EndTime - CurrentTime + 1;
//            integrationStep *= RedSea.Singleton.TimeScale;

            if (!severalSteps)
                integrationStep = 0;
            _loadAdvectCut.SetConstantVariable("IntegrationLength", integrationStep); // * RedSea.Singleton.DomainScale);

            if (severalSteps)
                //__global__ void AdvectCut(float2* positions, int2 origin)
                _advectCut.RunAsync(_streamCuts[0].Stream, _particlesCut.DevicePointer, new int2(0));

            // Before stream Y may start, the main stream (=X) has to be completed.
            _context.Synchronize();

            //__global__ void CutNeighbors(float* neighborMap, float2* positions, unsigned int offset, unsigned int neighborOffset, unsigned int neighbor)
            for (int i = 0; i < NUM_ADJACENT_CELLS/2; ++i)
            {
                // Should the original pixel be offset?
                int offset = (i == 3) ? 1 : 0;

                // What number needs to be added to finde the reference pixel?
                int neighborOffset = (i > 0) ? _width : 0;
                switch(i)
                {
                    case 0:
                    case 2:
                        neighborOffset++; break;
                    case 3:
                        neighborOffset--; break;
                    default: break;
                }
                if (measure == RedSea.DiffusionMeasure.FTLE)
                    _deformationTensorFTLE.RunAsync(_streamCuts[i].Stream, _neighbors.DevicePointer, _particlesCut.DevicePointer, (uint) offset, (uint)neighborOffset, i);
                else
                    _cutNeighbor.RunAsync(_streamCuts[i].Stream, _neighbors.DevicePointer, _particlesCut.DevicePointer, (uint) offset, (uint)neighborOffset, i);
            }

            StoreMeasure(measure, neighborID);
        }

        public void StoreMeasure(RedSea.DiffusionMeasure measure, uint neighborID = 0, CudaDirectXInteropResource storeTex = null, Int2 origin = null)
        {
            if (storeTex != null)
                _cudaDxMapper.Add(storeTex);

            _cudaDxMapper.MapAllResources();
            CudaArray2D texture = _cudaDxMapper[storeTex == null? 0 : _cudaDxMapper.Count - 1].GetMappedArray2D(0, 0);
            // Get surface objects of the textures.
            CudaSurfObject surf = new CudaSurfObject(texture);
            if (measure == RedSea.DiffusionMeasure.FTLE)
                _scanStoreMeasure[(int)measure].SetConstantVariable("IntegrationLength", EndTime - StartTime);
            _context.Synchronize();

            //__global__ void CutStoreX/Y(cudaSurfaceObject_t grads, float* cuts)
            _scanStoreMeasure[(int)measure].RunAsync(_streamCuts[0].Stream, surf.SurfObject, _neighbors.DevicePointer, neighborID % NUM_ADJACENT_CELLS, (int2)(new Int2(0, 0)));// (int2)(origin ?? Int2.ZERO));

            _cudaDxMapper.UnmapAllResources();

           // Remove the texture again.
            _cudaDxMapper.Remove(storeTex);
        }

        protected void SetKernelSizes()
        {
            // Cut blocks - one particle cloud per seed point.
            // For each point in the domain, compute cut value.
            dim3 blocksCut = new dim3(_width, _height);
            // One thread per particle.
            dim3 threadsCut = new dim3(NUM_PARTICLES, 1, 1);

            // ~~~~~~~~~~~~~~ Set ~~~~~~~~~~~~~~~ \\
            _loadAdvectCut.GridDimensions = blocksCut;
            _loadAdvectCut.BlockDimensions = threadsCut;
            

            _advectCut.GridDimensions = blocksCut;
            _advectCut.BlockDimensions = threadsCut;

            _cutNeighbor.GridDimensions = blocksCut;
            _cutNeighbor.BlockDimensions = threadsCut;

            // FTLE: Only one thread for tensor computation. (In fact, we need only one for integration too, but Khalas).
            _deformationTensorFTLE.GridDimensions = new dim3(_width, _height);
            _deformationTensorFTLE.BlockDimensions = new dim3(1, 1, 1);

            // Copy one complete cell-map.
            dim3 blocksSeeds = new dim3((uint)Math.Ceiling((float)_width / BLOCK_SIZE), (uint)Math.Ceiling((float)_height / BLOCK_SIZE));
            dim3 threadsSeeds = new dim3(BLOCK_SIZE, BLOCK_SIZE, 1);

            foreach(CudaKernel kernel in _scanStoreMeasure)
            {
                kernel.GridDimensions = blocksSeeds;
                kernel.BlockDimensions = threadsSeeds;
            }
        }

        protected void LoadNextField()
        {
            // Keep t1 timestep as new t0. Update mapping on device side.
            var tmp = _t0X;
            _t0X = _t1X;
            _t1X = tmp;
            new CudaTextureArray2D(_loadAdvectCut, "vX_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0X);
            tmp = _t0Y;
            _t0Y = _t1Y;
            _t1Y = tmp;
            new CudaTextureArray2D(_loadAdvectCut, "vY_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0Y);

            // Load new t1.
            ScalarField t1X = _velocity.GetTimeSlice(CurrentTime + 1).Scalars[0] as ScalarField;
            ScalarField t1Y = _velocity.GetTimeSlice(CurrentTime + 1).Scalars[1] as ScalarField;

            // vX, t=1
            //            _t1X = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            _t1X.CopyFromHostToThis(t1X.Data);
            new CudaTextureArray2D(_loadAdvectCut, "vX_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1X);


            // vY, t=1
            //            _t1Y = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            _t1Y.CopyFromHostToThis(t1Y.Data);
            new CudaTextureArray2D(_loadAdvectCut, "vY_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1Y);
        }

        public FieldPlane GetPlane(Plane plane)
        {
            FieldPlane gradMap = new FieldPlane(plane, Map, _velocity.Grid.Size.ToInt2(), 0, _velocity.InvalidValue ?? float.MaxValue, FieldPlane.RenderEffect.DEFAULT);
            //gradMap.AddScalar(MapY);
            return gradMap;
        }

        public static void Initialize()
        {
            //__constant__ float Variance = 1.0f;
            //// Change those two depending on cut or reference execution.
            //__constant__ int Width = 200;
            //__constant__ int Height = 200;
            //__constant__ int NumParticles = 1024;
            //__constant__ float TimeInGrid = 15.0f / 2.59f;
            //__constant__ float IntegrationLength = 1.0f;
            //__constant__ float StepSize = 0.3f;
            //__constant__ float Invalid = 3600000000;
            //__constant__ int CellToSeedRatio = 10;

            //__global__ void LoadAdvectReference(float2* positions, int2 seed)
            //__global__ void LoadAdvectCut(float2* positions, int2 origin)
            //__global__ void AdvectCut(float2* positions, int2 origin)
            //__global__ void AdvectReference(float2 * positions)
            //__global__ void AdvectStoreReference(float2* positions, float* referenceMap)
            //__global__ void FetchSumStoreCut(cudaSurfaceObject_t cuts, float2* positions, float* referenceMap)
            //__global__ void ReferenceToTexture(cudaSurfaceObject_t referenceTex, float* data)

            CUmodule module = _context.LoadModulePTX("Framework/Algorithms/Kernels/LocalDiffusion.ptx");
            
            _loadAdvectCut = new CudaKernel("LoadAdvectCut", module, _context);
            _advectCut = new CudaKernel("AdvectCut", module, _context);
            _cutNeighbor = new CudaKernel("CutNeighbors", module, _context);
            _deformationTensorFTLE = new CudaKernel("DeformationTensorFTLE", module, _context);
            _scanStoreMeasure = new CudaKernel[Enum.GetValues(typeof(RedSea.DiffusionMeasure)).Length];
            //_scanStoreMeasure[(int)RedSea.DiffusionMeasure.DENSITY] = new CudaKernel("ScanStoreDensity", module, _context);
            //_scanStoreMeasure[(int)RedSea.DiffusionMeasure.MIN] = new CudaKernel("ScanStoreMin", module, _context);
            //_scanStoreMeasure[(int)RedSea.DiffusionMeasure.MAX] = new CudaKernel("ScanStoreMax", module, _context);
            //_scanStoreMeasure[(int)RedSea.DiffusionMeasure.RANGE] = new CudaKernel("ScanStoreRange", module, _context);
            foreach (RedSea.DiffusionMeasure measure in Enum.GetValues(typeof(RedSea.DiffusionMeasure)))
            {
                _scanStoreMeasure[(int)measure] = new CudaKernel("ScanStore" + measure.ToString(), module, _context);
            }
        }

        public override void CompleteRange(Int2 selection)
        {
            return;
        }

        public override void Subrange(Int2 min, Int2 max, Int2 selection)
        {
            return;
        }
    }

    //class FTLE : AlgorithmCuda
    //{
    //    protected static int NUM_ADJACENT_CELLS = 8;
    //    public static int BLOCK_SIZE { get; } = 15;
    //    public static int NUM_PARTICLES = 512;

    //    protected VectorFieldUnsteady _velocity;
    //    protected int _width { get { return _velocity.Size[0]; } }
    //    protected int _height { get { return _velocity.Size[1]; } }
    //    public int StartTime;
    //    public float EndTime;
    //    public int CurrentTime;

    //    protected CudaArray2D _t0X, _t0Y, _t1X, _t1Y;
    //    public Texture2D Map { get; protected set; }
    //    protected CudaGraphicsInteropResourceCollection _cudaDxMapper;

    //    //__global__ void LoadAdvectCut(float2* positions, int2 origin)
    //    protected static CudaKernel _loadAdvectCut;
    //    //__global__ void AdvectCut(float2* positions, int2 origin)
    //    protected static CudaKernel _advectCut;
    //    //__global__ void CutNeighbor(float* neighborMap, float2* positions, unsigned int offset, unsigned int neighborOffset, unsigned int neighbor)
    //    protected static CudaKernel _cutNeighbor;
    //    //__global__ void DeformationTensorFTLE (float* neighborMap, float2* positions, unsigned int offset, unsigned int neighborOffset, unsigned int neighbor)
    //    protected static CudaKernel _deformationTensorFTLE;

    //    //__global__ void ScanCut{Density|Min|Max|Range}(cudaSurfaceObject_t grads, float* cuts)
    //    protected static CudaKernel[] _scanStoreMeasure;
    //    //protected static CudaKernel _storeY;

    //    protected CudaStream[] _streamCuts;
    //    protected CudaDeviceVariable<float2> _particlesCut;
    //    protected CudaDeviceVariable<float> _neighbors;

    //    protected bool _initialized = false;

    //    public FTLE(Texture2D input, LoaderNCF.SliceRange fieldEnsemble, int startTime, float time)
    //    {

    //    }

    //    public FTLE(VectorFieldUnsteady velocity, int startTime, float integrationTime)
    //    {
    //        // ~~~~~~~~~~~~~~ Copy relevant data ~~~~~~~~~~~~~~ \\
    //        _velocity = velocity;
    //    }
    //    /// <summary>
    //    /// Setup as empty map with only one value at 1.
    //    /// </summary>
    //    /// <param name="pos"></param>
    //    /// <param name="fieldEnsemble"></param>
    //    /// <param name="startTime"></param>
    //    /// <param name="endTime"></param>
    //    public void SetupMap(int startTime, float integrationTime)
    //    {
    //        // ~~~~~~~~~~~~~~ Copy relevant data ~~~~~~~~~~~~~~ \\
    //        // Count up when advection was executed.
    //        CurrentTime = startTime;
    //        StartTime = startTime;
    //        EndTime = startTime + integrationTime;

    //        if (!_initialized)
    //            InitializeDeviceStorage();

    //        // ~~~~~~~~~~~~~~ Fill CUDA resources ~~~~~~~~~~~~~~ \\
    //        // vX, t=1
    //        _t1X.CopyFromHostToThis<float>((_velocity.GetTimeSlice(StartTime).Scalars[0] as ScalarField).Data);
    //        new CudaTextureArray2D(_loadAdvectCut, "vX_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1X);

    //        // vY, t=1
    //        _t1Y.CopyFromHostToThis<float>((_velocity.GetTimeSlice(StartTime).Scalars[1] as ScalarField).Data);
    //        new CudaTextureArray2D(_loadAdvectCut, "vY_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1Y);
    //    }

    //    protected void InitializeDeviceStorage()
    //    {
    //        // ~~~~~~~~~~~~~~~ Allocate "Cache" ~~~~~~~~~~~~~~~~ \\
    //        // Buffer for advecting reference particles.
    //        _particlesCut = new CudaDeviceVariable<float2>(NUM_PARTICLES * _width * _height);
    //        _neighbors = new CudaDeviceVariable<float>((_width) * (_height) * NUM_ADJACENT_CELLS / 2);

    //        _t1X = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
    //        _t1Y = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
    //        _t0X = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
    //        _t0Y = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
    //        new CudaTextureArray2D(_loadAdvectCut, "vX_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0X);
    //        new CudaTextureArray2D(_loadAdvectCut, "vY_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0Y);

    //        // Streams to allow for parallel execution.
    //        _streamCuts = new CudaStream[NUM_ADJACENT_CELLS];
    //        for (int i = 0; i < NUM_ADJACENT_CELLS; ++i)
    //            _streamCuts[i] = new CudaStream();

    //        // ~~~~~~~~~~~~~~ Set CUDA constants ~~~~~~~~~~~~~~~ \\
    //        // For selection advection.
    //        _loadAdvectCut.SetConstantVariable("Width", _width);
    //        _loadAdvectCut.SetConstantVariable("Height", _height);
    //        _loadAdvectCut.SetConstantVariable("Invalid", _velocity.InvalidValue ?? float.MaxValue);
    //        _loadAdvectCut.SetConstantVariable("TimeInGrid", 1);// RedSea.Singleton.DomainScale);

    //        // ~~~~~~~~~~~~~ Create texture ~~~~~~~~~~~~~~~~~~~~ \\
    //        // Create texture. Completely zero, except for one point.
    //        Texture2DDescription desc = new Texture2DDescription
    //        {
    //            ArraySize = 1,
    //            BindFlags = BindFlags.ShaderResource,
    //            CpuAccessFlags = CpuAccessFlags.None,
    //            Format = Format.R32_Float,
    //            Width = _width,
    //            Height = _height,
    //            MipLevels = 1,
    //            OptionFlags = ResourceOptionFlags.None,
    //            SampleDescription = new SampleDescription(1, 0),
    //            Usage = ResourceUsage.Default
    //        };

    //        // Create texture.
    //        Map = new Texture2D(_device, desc);

    //        // ~~~~~~~~~ Make textures mappable to CUDA ~~~~~~~~~~ \\
    //        _cudaDxMapper = new CudaGraphicsInteropResourceCollection();
    //        _cudaDxMapper.Add(new CudaDirectXInteropResource(Map.ComPointer, CUGraphicsRegisterFlags.None, CudaContext.DirectXVersion.D3D11));

    //        //            _cudaDxMapper.MapAllResources();
    //        //            CudaArray2D lastFlowMap = _cudaDxMapper[0].GetMappedArray2D(0, 0);
    //        ////            new CudaTextureArray2D(_loadAdvectReference, "selectionMap", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, lastFlowMap);
    //        //            _cudaDxMapper.UnmapAllResources();

    //        // ~~~~~~~~~~~~~~~ Set Kernel Sizes ~~~~~~~~~~~~~~~ \\
    //        SetKernelSizes();
    //        _initialized = true;
    //    }
    //    public void Advect(float stepSize, float variance = 2.0f)
    //    {
    //        LoadNextField();

    //        // ~~~~~~~~~~~~~~~ Upload relevant data ~~~~~~~~~~~~~~~ \\
    //        _loadAdvectCut.SetConstantVariable("Variance", variance);
    //        _loadAdvectCut.SetConstantVariable("StepSize", (float)Math.Max(0.0000001f, stepSize));

    //        // Compute and upload integration time.
    //        float integrationStep = (CurrentTime + 1 > EndTime) ? EndTime - CurrentTime : 1.0f;
    //        //            integrationStep *= RedSea.Singleton.TimeScale;
    //        CurrentTime++;
    //        _loadAdvectCut.SetConstantVariable("IntegrationLength", integrationStep);

    //        bool severalSteps = false;

    //        // ~~~~~~~~~~~ Start Map Integration ~~~~~~~~~~~ \\
    //        //__global__ void LoadAdvectCut(float2* positions, int2 origin)
    //        _loadAdvectCut.RunAsync(_streamCuts[0].Stream, _particlesCut.DevicePointer, new int2(0));

    //        // ~~~~~~~~~~~ Advect the Particle Buffers further ~~~~~~~~~~~ \\
    //        while (CurrentTime < EndTime)
    //        {
    //            severalSteps = true;
    //            // If we actually go in here, an integration time of "1" has to be set.
    //            Debug.Assert(integrationStep == 1); // RedSea.Singleton.TimeScale);

    //            // As texture loading happens inbetwen, a synchronization is performed automatically.
    //            // Thus, we always have the right vector fields bound.
    //            // The cut and reference streams are synchronized now, even though they would not need to be.
    //            LoadNextField();

    //            //__global__ void AdvectCut(float2* positions, int2 origin)
    //            _advectCut.RunAsync(_streamCuts[0].Stream, _particlesCut.DevicePointer, new int2(0));
    //            CurrentTime++;
    //        }

    //        integrationStep = EndTime - CurrentTime + 1;
    //        //            integrationStep *= RedSea.Singleton.TimeScale;

    //        if (!severalSteps)
    //            integrationStep = 0;
    //        _loadAdvectCut.SetConstantVariable("IntegrationLength", integrationStep); // * RedSea.Singleton.DomainScale);

    //        if (severalSteps)
    //            //__global__ void AdvectCut(float2* positions, int2 origin)
    //            _advectCut.RunAsync(_streamCuts[0].Stream, _particlesCut.DevicePointer, new int2(0));

    //        // Before stream Y may start, the main stream (=X) has to be completed.
    //        _context.Synchronize();

    //        //__global__ void CutNeighbors(float* neighborMap, float2* positions, unsigned int offset, unsigned int neighborOffset, unsigned int neighbor)
    //        for (int i = 0; i < NUM_ADJACENT_CELLS / 2; ++i)
    //        {
    //            // Should the original pixel be offset?
    //            int offset = (i == 3) ? 1 : 0;

    //            // What number needs to be added to finde the reference pixel?
    //            int neighborOffset = (i > 0) ? _width : 0;
    //            switch (i)
    //            {
    //                case 0:
    //                case 2:
    //                    neighborOffset++; break;
    //                case 3:
    //                    neighborOffset--; break;
    //                default: break;
    //            }
    //            //if(measure == RedSea.DiffusionMeasure.FTLE)
    //            //    _deformationTensorFTLE.RunAsync(_streamCuts[i].Stream, _neighbors.DevicePointer, _particlesCut.DevicePointer, (uint)offset, (uint)neighborOffset, i);
    //            //else
    //            _cutNeighbor.RunAsync(_streamCuts[i].Stream, _neighbors.DevicePointer, _particlesCut.DevicePointer, (uint)offset, (uint)neighborOffset, i);
    //        }

    //        StoreMeasure();
    //    }

    //    public void StoreMeasure()
    //    {

    //        _cudaDxMapper.MapAllResources();
    //        CudaArray2D texture = _cudaDxMapper[0].GetMappedArray2D(0, 0);
    //        // Get surface objects of the textures.
    //        CudaSurfObject surf = new CudaSurfObject(texture);

    //        _context.Synchronize();

    //        //__global__ void CutStoreX/Y(cudaSurfaceObject_t grads, float* cuts)
    //        _scanStoreMeasure[(int)RedSea.DiffusionMeasure.FTLE].RunAsync(_streamCuts[0].Stream, surf.SurfObject, _neighbors.DevicePointer, 0, (int2)(new Int2(0, 0)));// (int2)(origin ?? Int2.ZERO));

    //        _cudaDxMapper.UnmapAllResources();
    //    }

    //    protected void SetKernelSizes()
    //    {
    //        // Cut blocks - one particle cloud per seed point.
    //        // For each point in the domain, compute cut value.
    //        dim3 blocksCut = new dim3(_width, _height);
    //        // One thread per particle.
    //        dim3 threadsCut = new dim3(1, 1, 1);

    //        // ~~~~~~~~~~~~~~ Set ~~~~~~~~~~~~~~~ \\
    //        _loadAdvectCut.GridDimensions = blocksCut;
    //        _loadAdvectCut.BlockDimensions = threadsCut;


    //        _advectCut.GridDimensions = blocksCut;
    //        _advectCut.BlockDimensions = threadsCut;

    //        _cutNeighbor.GridDimensions = blocksCut;
    //        _cutNeighbor.BlockDimensions = threadsCut;

    //        // Copy one complete cell-map.
    //        dim3 blocksSeeds = new dim3((uint)Math.Ceiling((float)_width / BLOCK_SIZE), (uint)Math.Ceiling((float)_height / BLOCK_SIZE));
    //        dim3 threadsSeeds = new dim3(BLOCK_SIZE, BLOCK_SIZE, 1);

    //        foreach (CudaKernel kernel in _scanStoreMeasure)
    //        {
    //            kernel.GridDimensions = blocksSeeds;
    //            kernel.BlockDimensions = threadsSeeds;
    //        }
    //    }

    //    protected void LoadNextField()
    //    {
    //        // Keep t1 timestep as new t0. Update mapping on device side.
    //        var tmp = _t0X;
    //        _t0X = _t1X;
    //        _t1X = tmp;
    //        new CudaTextureArray2D(_loadAdvectCut, "vX_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0X);
    //        tmp = _t0Y;
    //        _t0Y = _t1Y;
    //        _t1Y = tmp;
    //        new CudaTextureArray2D(_loadAdvectCut, "vY_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0Y);

    //        // Load new t1.
    //        ScalarField t1X = _velocity.GetTimeSlice(CurrentTime + 1).Scalars[0] as ScalarField;
    //        ScalarField t1Y = _velocity.GetTimeSlice(CurrentTime + 1).Scalars[1] as ScalarField;

    //        // vX, t=1
    //        //            _t1X = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
    //        _t1X.CopyFromHostToThis(t1X.Data);
    //        new CudaTextureArray2D(_loadAdvectCut, "vX_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1X);


    //        // vY, t=1
    //        //            _t1Y = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
    //        _t1Y.CopyFromHostToThis(t1Y.Data);
    //        new CudaTextureArray2D(_loadAdvectCut, "vY_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1Y);
    //    }

    //    public FieldPlane GetPlane(Plane plane)
    //    {
    //        FieldPlane gradMap = new FieldPlane(plane, Map, _velocity.Grid.Size.ToInt2(), 0, _velocity.InvalidValue ?? float.MaxValue, FieldPlane.RenderEffect.DEFAULT);
    //        //gradMap.AddScalar(MapY);
    //        return gradMap;
    //    }

    //    public static void Initialize()
    //    {
    //        //__constant__ float Variance = 1.0f;
    //        //// Change those two depending on cut or reference execution.
    //        //__constant__ int Width = 200;
    //        //__constant__ int Height = 200;
    //        //__constant__ int NumParticles = 1024;
    //        //__constant__ float TimeInGrid = 15.0f / 2.59f;
    //        //__constant__ float IntegrationLength = 1.0f;
    //        //__constant__ float StepSize = 0.3f;
    //        //__constant__ float Invalid = 3600000000;
    //        //__constant__ int CellToSeedRatio = 10;

    //        //__global__ void LoadAdvectReference(float2* positions, int2 seed)
    //        //__global__ void LoadAdvectCut(float2* positions, int2 origin)
    //        //__global__ void AdvectCut(float2* positions, int2 origin)
    //        //__global__ void AdvectReference(float2 * positions)
    //        //__global__ void AdvectStoreReference(float2* positions, float* referenceMap)
    //        //__global__ void FetchSumStoreCut(cudaSurfaceObject_t cuts, float2* positions, float* referenceMap)
    //        //__global__ void ReferenceToTexture(cudaSurfaceObject_t referenceTex, float* data)

    //        CUmodule module = _context.LoadModulePTX("Framework/Algorithms/Kernels/LocalDiffusion.ptx");

    //        _loadAdvectCut = new CudaKernel("LoadAdvectCut", module, _context);
    //        _advectCut = new CudaKernel("AdvectCut", module, _context);
    //        _cutNeighbor = new CudaKernel("CutNeighbors", module, _context);
    //        _deformationTensorFTLE = new CudaKernel("DeformationTensorFTLE", module, _context);
    //        _scanStoreMeasure = new CudaKernel[Enum.GetValues(typeof(RedSea.DiffusionMeasure)).Length];
    //        //_scanStoreMeasure[(int)RedSea.DiffusionMeasure.DENSITY] = new CudaKernel("ScanStoreDensity", module, _context);
    //        //_scanStoreMeasure[(int)RedSea.DiffusionMeasure.MIN] = new CudaKernel("ScanStoreMin", module, _context);
    //        //_scanStoreMeasure[(int)RedSea.DiffusionMeasure.MAX] = new CudaKernel("ScanStoreMax", module, _context);
    //        //_scanStoreMeasure[(int)RedSea.DiffusionMeasure.RANGE] = new CudaKernel("ScanStoreRange", module, _context);
    //        foreach (RedSea.DiffusionMeasure measure in Enum.GetValues(typeof(RedSea.DiffusionMeasure)))
    //        {
    //            _scanStoreMeasure[(int)measure] = new CudaKernel("ScanStore" + measure.ToString(), module, _context);
    //        }
    //    }

    //    public override void CompleteRange(Int2 selection)
    //    {
    //        return;
    //    }

    //    public override void Subrange(Int2 min, Int2 max, Int2 selection)
    //    {
    //        return;
    //    }
    //}
}
