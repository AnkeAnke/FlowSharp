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
using SliceRange = FlowSharp.Loader.SliceRange;
namespace FlowSharp
{
    class LocalDiffusion : AlgorithmCuda
    {
        public static int BLOCK_SIZE { get; } = 15;
        public static int NUM_PARTICLES = 500;

        protected VectorFieldUnsteady _velocity;
        protected int _width { get { return _velocity.Size[0]; } }
        protected int _height { get { return _velocity.Size[1]; } }
        public int StartTime;
        public float EndTime;
        public int CurrentTime;

        protected CudaArray2D _t0X, _t0Y, _t1X, _t1Y;
        public Texture2D SelectionMap { get; protected set; }
        protected CudaDeviceVariable<float> _pongSelectionMap;
        protected CudaGraphicsInteropResourceCollection _cudaDxMapper;
        protected static CudaKernel _advectSelectionMapKernel;
        protected static CudaKernel _copySelectionMap;
        protected Int2 _selection;

        public LocalDiffusion(Texture2D input, Loader.SliceRange fieldEnsemble, int startTime, float time)
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
        /// <param name="pos"></param>
        /// <param name="fieldEnsemble"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        public void SetupMap(Int2 pos, int startTime, float integrationTime)
        {
            // ~~~~~~~~~~~~~~ Copy relevant data ~~~~~~~~~~~~~~ \\
            // Count up when advection was executed.
            CurrentTime = startTime;
            StartTime = startTime;
            EndTime = startTime + integrationTime;
            _selection = pos;
            _cudaDxMapper = new CudaGraphicsInteropResourceCollection();


            // ~~~~~~~~~~~~~~ Fill CUDA resources ~~~~~~~~~~~~~~ \\
            //// vX, t=0
            //_t0X = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            //_t0X.CopyFromHostToThis<float>((_velocity.GetTimeSlice(startTime).Scalars[0] as ScalarField).Data);
            //new CudaTextureArray2D(_advectSelectionMapKernel, "vX_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0X);

            //// vY, t=0
            //_t0Y = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            //_t0Y.CopyFromHostToThis<float>((_velocity.GetTimeSlice(startTime).Scalars[1] as ScalarField).Data);
            //new CudaTextureArray2D(_advectSelectionMapKernel, "vY_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0Y);

            // vX, t=1
            _t1X = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            _t1X.CopyFromHostToThis<float>((_velocity.GetTimeSlice(startTime).Scalars[0] as ScalarField).Data);
            new CudaTextureArray2D(_advectSelectionMapKernel, "vX_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1X);

            // vY, t=1
            _t1Y = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            _t1Y.CopyFromHostToThis<float>((_velocity.GetTimeSlice(startTime).Scalars[1] as ScalarField).Data);
            new CudaTextureArray2D(_advectSelectionMapKernel, "vY_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1Y);

            // ~~~~~~~~~~~~~~ Set CUDA constants ~~~~~~~~~~~~~~~ \\
            _advectSelectionMapKernel.SetConstantVariable("Width", _width);
            _advectSelectionMapKernel.SetConstantVariable("Height", _height);
            _copySelectionMap.SetConstantVariable("Width", _width);
            _copySelectionMap.SetConstantVariable("Height", _height);
            _advectSelectionMapKernel.SetConstantVariable("Invalid", _velocity.InvalidValue??float.MaxValue);
            _advectSelectionMapKernel.SetConstantVariable("TimeInGrid", RedSea.Singleton.DomainScale);

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
            _pongSelectionMap = new CudaDeviceVariable<float>(_width * _height);

            // Magically, copy to device happens here!
            _pongSelectionMap = zeros;

            // Create texture.
            SelectionMap = new Texture2D(_device, desc);

            // ~~~~~~~~~ Make textures mappable to CUDA ~~~~~~~~~~ \\
            _cudaDxMapper.Add(new CudaDirectXInteropResource(SelectionMap.ComPointer, CUGraphicsRegisterFlags.None, CudaContext.DirectXVersion.D3D11));

            _cudaDxMapper.MapAllResources();
            CudaArray2D lastFlowMap = _cudaDxMapper[0].GetMappedArray2D(0, 0);
            new CudaTextureArray2D(_advectSelectionMapKernel, "selectionMap", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, lastFlowMap);
            _cudaDxMapper.UnmapAllResources();
        }
        public void Step(float stepSize, float variance = 2.0f)
        {
            LoadNextField();
            // ~~~~~~~~~~~~~~~ Upload relevant data ~~~~~~~~~~~~~~~ \\
            _cudaDxMapper.MapAllResources();
            CudaArray2D lastFlowMap = _cudaDxMapper[0].GetMappedArray2D(0, 0);

            float integrationStep = (CurrentTime + 1 > EndTime) ? EndTime - CurrentTime : 1.0f;
            CurrentTime++;
            _advectSelectionMapKernel.SetConstantVariable("IntegrationLength", integrationStep * RedSea.Singleton.DomainScale);
            _advectSelectionMapKernel.SetConstantVariable("Variance", variance);
            _advectSelectionMapKernel.SetConstantVariable("StepSize", stepSize);

            // ~~~~~~~~~~~~~~~~~~~~ Run kernels ~~~~~~~~~~~~~~~~~~~~ \\
            // Only one seed currently.
            dim3 grid = new dim3(1);

            // One thread per particle.
            dim3 threads = new dim3(NUM_PARTICLES, 1, 1);

            _advectSelectionMapKernel.GridDimensions = grid;
            _advectSelectionMapKernel.BlockDimensions = threads;

            _advectSelectionMapKernel.Run(_pongSelectionMap.DevicePointer, (int2)_selection);

            

            // Swap the Texture2D handles.
            CudaSurfObject surf = new CudaSurfObject(lastFlowMap);
            grid = new dim3((uint)Math.Ceiling((float)_width /BLOCK_SIZE), (uint)Math.Ceiling((float)_height/BLOCK_SIZE), 1);
            threads = new dim3(BLOCK_SIZE, BLOCK_SIZE);
            _copySelectionMap.GridDimensions = grid;
            _copySelectionMap.BlockDimensions = threads;
            _copySelectionMap.Run(surf.SurfObject, _pongSelectionMap.DevicePointer);

            _cudaDxMapper.UnmapAllResources();
        }

        protected void LoadNextField()
        {
            // Keep t1 timestep as new t0. Update mapping on device side.
            _t0X = _t1X;
            new CudaTextureArray2D(_advectSelectionMapKernel, "vX_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0X);
            _t0Y = _t1Y;
            new CudaTextureArray2D(_advectSelectionMapKernel, "vY_t0", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t0Y);

            // Load new t1.
            ScalarField t1X = _velocity.GetTimeSlice(CurrentTime + 1).Scalars[0] as ScalarField;
            ScalarField t1Y = _velocity.GetTimeSlice(CurrentTime + 1).Scalars[1] as ScalarField;

            // vX, t=1
            _t1X = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            _t1X.CopyFromHostToThis(t1X.Data);
            new CudaTextureArray2D(_advectSelectionMapKernel, "vX_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1X);

            // vY, t=1
            _t1Y = new CudaArray2D(CUArrayFormat.Float, _width, _height, CudaArray2DNumChannels.One);
            _t1Y.CopyFromHostToThis(t1Y.Data);
            new CudaTextureArray2D(_advectSelectionMapKernel, "vY_t1", CUAddressMode.Wrap, CUFilterMode.Linear, CUTexRefSetFlags.None, _t1Y);
        }

        public FieldPlane GetPlane(Plane plane)
        {
            FieldPlane flowMap = new FieldPlane(plane, _velocity.Grid as RectlinearGrid, SelectionMap, _velocity.Grid.Size.ToInt2(), 0, _velocity.InvalidValue??float.MaxValue, FieldPlane.RenderEffect.DEFAULT);
            return flowMap;
        }

        public static void Initialize()
        {
             CUmodule module = _context.LoadModulePTX("Framework/Algorithms/Kernels/LocalDiffusion.ptx");
            _advectSelectionMapKernel = new CudaKernel("AdvectSelectionMap", module, _context);
            _copySelectionMap = new CudaKernel("CopySelectionMap", module, _context);
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
}
