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
        private WPFHost.ISceneHost _host;
        public SlimDX.Direct3D11.Device Device {  get { return _host.Device; } }

        //TODO: remove
        private SlimDX.Direct3D11.Buffer _vertices;

        protected Effect _effect;
        protected InputLayout _layout;

        protected List<Renderable> _renderables;

        /// <summary>
        /// Attaches the host (the DPFcanvas in the window) to this to access its resources
        /// </summary>
        /// <param name="host"></param>
        public void Attach(ISceneHost host)
        {
            this._host = host;

            // Assure that a device is set.
            if (host.Device == null)
                throw new Exception("Scene host device is null");

            SetupRenderer();
        }

        protected void SetupRenderer()
        {
            Plane.Initialize(Device);
            _renderables = new List<Renderable>();
            ScalarField testField = new ScalarField(new RectlinearGrid(new Index(4, 2), new Vector(0.0f, 2), new Vector(0.1f, 2)));
            Plane plane = new Plane(new Vector3(0, 0, 0.5f), Vector3.UnitX, Vector3.UnitY, 1.0f, new ScalarField[] { testField });
            _renderables.Add(plane);
            //var bytecode = ShaderBytecode.CompileFromFile("Framework/Renderer/Colormap.fx", "fx_5_0", ShaderFlags.None, EffectFlags.None);
            //_effect = new Effect(Device, bytecode);
            //var technique = _effect.GetTechniqueByIndex(0);
            //var pass = technique.GetPassByIndex(0);
            //_layout = new InputLayout(Device, pass.Description.Signature, new[] {
            //    new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
            //    new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 16, 0)
            //});

            //var stream = new DataStream(3 * 32, true, true);
            //stream.WriteRange(new[] {
            //    new Vector4(0.0f, 0.5f, 0.5f, 1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
            //    new Vector4(0.5f, -0.5f, 0.5f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
            //    new Vector4(-0.5f, -0.5f, 0.5f, 1.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)
            //});
            //stream.Position = 0;

            //_vertices = new SlimDX.Direct3D11.Buffer(Device, stream, new BufferDescription()
            //{
            //    BindFlags = BindFlags.VertexBuffer,
            //    CpuAccessFlags = CpuAccessFlags.None,
            //    OptionFlags = ResourceOptionFlags.None,
            //    SizeInBytes = 3 * 32,
            //    Usage = ResourceUsage.Default
            //});
            //stream.Dispose();

            //Device.ImmediateContext.OutputMerger.SetTargets(_host.RenderTargetView);
            //Device.ImmediateContext.Rasterizer.SetViewports(new Viewport(0, 0, _host.RenderTargetWidth, _host.RenderTargetHeight, 0.0f, 1.0f));
        }

        public void Detach()
        {
            throw new NotImplementedException();
        }

        public void OnResize(ISceneHost host)
        {
            throw new NotImplementedException();
        }

        float relativeTime;

        public void Render()
        {
            //Device.ImmediateContext.ClearRenderTargetView(_host.RenderTargetView, new Color4(0.0f, 1.0f, 1.0f)); // *(relativeTime/1000.0f));

            //Device.ImmediateContext.InputAssembler.InputLayout = _layout;
            //Device.ImmediateContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            //Device.ImmediateContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(_vertices, 32, 0));

            //EffectTechnique technique = _effect.GetTechniqueByName("RenderTex1");
            //for (int i = 0; i < technique.Description.PassCount; ++i)
            //{
            //    technique.GetPassByIndex(i).Apply(Device.ImmediateContext);
            //    Device.ImmediateContext.Draw(3, 0);
            //}
            foreach (Renderable obj in _renderables)
                obj.Render(Device);
        }

        public void Update(TimeSpan timeSpan)
        {
            //relativeTime = timeSpan.Milliseconds;
            //throw new NotImplementedException();

            foreach (Renderable obj in _renderables)
                obj.Update(timeSpan);
        }
    }
}
