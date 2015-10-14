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

        private WPFHost.ISceneHost _host;
        public SlimDX.Direct3D11.Device Device { get { return _host.Device; } }

        protected List<Renderable> _renderables;

        public Camera Camera { get; set; }

        protected TimeSpan _lastTime;

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
                Console.WriteLine(e);
            }
        }

        protected void SetupRenderer()
        {
            try
            {
                Renderable.Initialize(Device);
                FieldPlane.Initialize();
                ColorMapping.Initialize(Device);
                TheCloud.Initialize();
                LineBall.Initialize();

                Device.ImmediateContext.OutputMerger.SetTargets(_host.RenderTargetView);
                Device.ImmediateContext.Rasterizer.SetViewports(new Viewport(0, 0, _host.RenderTargetWidth, _host.RenderTargetHeight, 0.0f, 1.0f));

                _renderables = new List<Renderable>();
                Camera = new Camera(Device, ((float)_host.RenderTargetWidth) / _host.RenderTargetHeight);
                var desc = new RasterizerStateDescription { CullMode = CullMode.None, FillMode = FillMode.Solid };
                Device.ImmediateContext.Rasterizer.State = RasterizerState.FromDescription(Device, desc);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
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
            foreach (Renderable obj in _renderables)
                if(obj.Active)
                    obj.Render(Device);
        }

        public void Update(TimeSpan timeSpan)
        {
            while (_renderables == null)
            {
                System.Threading.Thread.Sleep(0);
            }
            Camera.Update((float)timeSpan.TotalMilliseconds, Device);

            foreach (Renderable obj in _renderables)
                obj.Update(timeSpan);
        }

        public void AddRenderable(Renderable obj)
        {
            _renderables.Add(obj);
        }

        public void ClearRenderables()
        {
            _renderables.Clear();
        }

        public void AddRenderables(List<Renderable> objs)
        {
            _renderables = _renderables.Concat(objs).ToList();
        }
    }
}
