using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX;
using SlimDX.Direct3D11;
using Buffer = SlimDX.Direct3D11.Buffer;
using System.Runtime.InteropServices;

namespace FlowSharp
{
    class Camera
    {
        protected Buffer _globalConstants;
        public Constants Globals;

        public Camera(Device device, float aspectRatio)
        {
            Globals = new Constants();
            Globals.View = Matrix.Identity;// LookAtLH(new Vector3(0, 0, -15), Vector3.Zero, Vector3.UnitY);
            Globals.Projection = Matrix.Identity;// PerspectiveFovLH(1.5f, aspectRatio, 0.1f, 10000);

            var data = new DataStream(16, true, true);
            data.Write(new Vector4(1.0f, 0.0f, 1.0f, 1.0f));
            data.Position = 0;

            _globalConstants = new Buffer(device, //Device
                data, //Stream
                16, // Size
                ResourceUsage.Dynamic,
                BindFlags.ConstantBuffer,
                CpuAccessFlags.Write,
                ResourceOptionFlags.None,
                4);

            device.ImmediateContext.PixelShader.SetConstantBuffer(_globalConstants, 0);
        }

        /// <summary>
        /// Updates the constants on GPU side.
        /// </summary>
        /// <param name="device"></param>
        public void UpdateResources(Device device)
        {
            var data = new DataStream(16, true, true);
            data.Write(new Vector4(1.0f, 0.0f, 1.0f, 1.0f));
            data.Position = 0;
            
            device.ImmediateContext.UpdateSubresource(new DataBox(0, 0, data), _globalConstants, 0);
            device.ImmediateContext.PixelShader.SetConstantBuffer(_globalConstants, 0);
        }
        [StructLayout(LayoutKind.Explicit)]
        public struct Constants
        {
            [FieldOffset(0)]
            public Matrix View;
            [FieldOffset(64)]
            public Matrix Projection;
            //public Matrix ViewProjection { get { return View * Projection; } }
        }
    }
}
