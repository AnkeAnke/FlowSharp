using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Device = SlimDX.Direct3D11.Device;
using System.IO;

namespace FlowSharp.Framework.Renderer.Data
{
    class ColorMapping
    {
        /// <summary>
        /// Takes a scala field as input anf generates a 2D texture.
        /// </summary>
        /// <param name="device"></param>
        /// <param name="field"></param>
        /// <returns></returns>
        public static Texture2D GenerateTextureObject(Device device, ScalarField field, Texture2DDescription? description = null)
        {
            System.Diagnostics.Debug.Assert(field.Size.Length == 2);
            Texture2DDescription desc;

            // Either use the given description, or create a render target/shader resource bindable one.
            if (description == null)
                desc = new Texture2DDescription()
                {
                    Width = field.Size[0],
                    Height = field.Size[1],
                    MipLevels = 1,
                    Format = Format.R32_Float,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                    CpuAccessFlags = CpuAccessFlags.None,
                    OptionFlags = ResourceOptionFlags.None
                };
            else
                desc = (Texture2DDescription)description;

            Texture2D tex = new Texture2D(device, desc);
            unsafe
            {
                fixed (float* fPtr = field.Data)
                {
                    IntPtr bPtr = (IntPtr)fPtr;
                    tex = Texture2D.FromPointer(bPtr);
                }
                
            }
            // Create stream from field data.
            //Stream stream = new MemoryStream();

            //Texture2D.FromStream(device, stream, sizeof(float) * field.Size.Product());
            return tex;
        }
    }

    class Colormap
    {
        // TODO
    }
}
