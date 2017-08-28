using SlimDX;
using SlimDX.D3DCompiler;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPFHost;
using ManagedCuda;
using ManagedCuda.BasicTypes;
using Debug = System.Diagnostics.Debug;

namespace FlowSharp
{
    /// <summary>
    /// Renderer for rendering stuff to a WPF window element.
    /// </summary>
    class Renderer : WPFHost.IScene
    {
        protected static Renderer _instance;
        public bool Initialized = false;
        public static Renderer Singleton
        {
            get
            {
                if (_instance == null)
                    _instance = new Renderer();
                return _instance;
            }
        }

        /// <summary>
        /// The WPF host connected to the scene.
        /// </summary>
        private WPFHost.ISceneHost _host;
        private DPFCanvas _canvas;
        public void SetCanvas(DPFCanvas canv) { _canvas = canv; }
        /// <summary>
        /// THe Direct3D11 device bound to the WPF render target.
        /// </summary>
        public SlimDX.Direct3D11.Device Device { get { return _host.Device; } }
        public ManagedCuda.CudaContext ContextCuda { get; private set; }

        protected List<Renderable> _renderables;

        public Camera Camera { get; set; }
        public bool Wireframe = false;
        //protected TimeSpan _lastTime;

        protected Renderer() { }

        /// <summary>
        /// Attaches the host (the DPFcanvas in the window) to this to access its resources
        /// </summary>
        /// <param name="host"></param>
        public void Attach(ISceneHost host)
        {
            try
            {
                this._host = host;

                // Assure that a device is set.
                if (host.Device == null)
                    throw new Exception("Scene host device is null");

                SetupRenderer();
                FSMain.CreateRenderables();

                Initialized = true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.InnerException);
            }
        }

        protected void SetupRenderer()
        {
            try
            {
                Renderable.Initialize(Device);
                FieldPlane.Initialize();
                ColorMapping.Initialize(Device);
                PointCloud.Initialize();
                LineBall.Initialize();
                Mesh.Initialize();
                

                Device.ImmediateContext.OutputMerger.SetTargets(_host.RenderTargetView);
                Device.ImmediateContext.Rasterizer.SetViewports(new Viewport(0, 0, _host.RenderTargetWidth, _host.RenderTargetHeight, 0.0f, 1.0f));

                _renderables = new List<Renderable>();
                Camera = new Camera(Device, ((float)_host.RenderTargetWidth) / _host.RenderTargetHeight);
                var desc = new RasterizerStateDescription { CullMode = CullMode.None, FillMode = FillMode.Solid };
                Device.ImmediateContext.Rasterizer.State = RasterizerState.FromDescription(Device, desc);

//              SetupCuda();
//              AlgorithmCuda.Initialize(ContextCuda, Device);
//              FlowMapUncertain.Initialize();
//              CutDiffusion.Initialize();
//              LocalDiffusion.Initialize();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.InnerException);
                Debug.Assert(false);
            }
        }

        protected void SetupCuda()
        {
            // Try to bind a CUDA context to the graphics card that WPF is working with.
            Adapter d3dAdapter = Device.Factory.GetAdapter(0);
            CUdevice[] cudaDevices = null;
            try
            {
                // Build a CUDA context from the first adapter in the used D3D11 device.
                cudaDevices = CudaContext.GetDirectXDevices(Device.ComPointer, CUd3dXDeviceList.All, CudaContext.DirectXVersion.D3D11);
                Debug.Assert(cudaDevices.Length > 0);
                Console.WriteLine("> Display Device #" + d3dAdapter
                    + ": \"" + d3dAdapter.Description + "\" supports Direct3D11 and CUDA.\n");
            }
            catch (CudaException)
            {
                // No Cuda device found for this Direct3D11 device.
                Console.Write("> Display Device #" + d3dAdapter
                    + ": \"" + d3dAdapter.Description + "\" supports Direct3D11 but not CUDA.\n");
            }
            ContextCuda = new CudaContext(cudaDevices[0], Device.ComPointer, CUCtxFlags.BlockingSync, CudaContext.DirectXVersion.D3D11);
            var info = ContextCuda.GetDeviceInfo();
            Console.WriteLine("Max. Nr. Threads: " + info.MaxBlockDim + ", Total: " + info.MaxThreadsPerBlock + "\nMax. Nr. Blocks: " + info.MaxGridDim + "\nMax. Bytes Shared Per Block: " + info.SharedMemoryPerBlock);
        }

        public void Detach()
        {
            throw new NotImplementedException();
        }

        public void OnResize(ISceneHost host)
        {
        }

        public void Render()
        {
            if (!Wireframe)
            {
                foreach (Renderable obj in _renderables)
                    if (obj.Active)
                        obj.Render(Device);
            }
            else
            {
                // Everything not a mesh: solid.
                foreach (Renderable obj in _renderables)
                    if (obj.Active && (obj as Mesh) == null)
                        obj.Render(Device);

                var desc = new RasterizerStateDescription { CullMode = CullMode.Front, FillMode = FillMode.Wireframe };
                Device.ImmediateContext.Rasterizer.State = RasterizerState.FromDescription(Device, desc);

                // Every mesh: wireframe.
                foreach (Renderable obj in _renderables)
                    if (obj.Active && (obj as Mesh) != null)
                        obj.Render(Device);

                desc = new RasterizerStateDescription { CullMode = CullMode.None, FillMode = FillMode.Solid };
                Device.ImmediateContext.Rasterizer.State = RasterizerState.FromDescription(Device, desc);
            }
        }

        public void Update(TimeSpan timeSpan)
        {
            while (_renderables == null)
            {
                System.Threading.Thread.Sleep(0);
            }
            if (Camera.Active)
                Camera.Update((float)timeSpan.TotalMilliseconds, Device, _canvas);
            else
                Camera.UpdateInactive();

            foreach (Renderable obj in _renderables)
                obj.Update(timeSpan);
        }

        public void AddRenderable(Renderable obj)
        {
            _renderables.Add(obj);
        }

        public void ClearRenderables()
        {
            //foreach(Renderable rnd in _renderables)
            //{
            //    rnd.Dispose();
            //}
            _renderables.Clear();
        }

        public void AddRenderables(List<Renderable> objs)
        {
            _renderables = _renderables.Concat(objs).ToList();
        }

        public void Remove(Renderable obj)
        {
            _renderables.Remove(obj);
        }

        //public void SetSolid()
        //{
        //    var desc = new RasterizerStateDescription { CullMode = CullMode.None, FillMode = FillMode.Solid };
        //    Device.ImmediateContext.Rasterizer.State = RasterizerState.FromDescription(Device, desc);
        //}
    }
}
