using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Device = SlimDX.Direct3D11.Device;
using System.IO;
using SlimDX;

namespace FlowSharp
{
    class ColorMapping
    {
        /// <summary>
        /// Takes a scala field as input anf generates a 2D texture.
        /// </summary>
        /// <param name="device"></param>
        /// <param name="field"></param>
        /// <returns></returns>
        public static Texture2D GenerateTextureFromField(Device device, ScalarField field, Texture2DDescription? description = null)
        {
            System.Diagnostics.Debug.Assert(field.Size.Length == 2);

            Texture2DDescription desc;

            // Either use the given description, or create a render target/shader resource bindable one.
            if (description == null)
                desc = new Texture2DDescription
                {
                    ArraySize = 1,
                    BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                    CpuAccessFlags = CpuAccessFlags.None,
                    Format = Format.R32_Float,
                    Width = field.Size[1],
                    Height = field.Size[0],
                    MipLevels = 1,
                    OptionFlags = ResourceOptionFlags.None,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default
                };
            else
                desc = (Texture2DDescription)description;

            // Put field data into stream/rectangle object
            DataStream slimStream = new DataStream(field.Data, true, false);
            DataRectangle texData = new DataRectangle(field.Size[1] * sizeof(float), slimStream);

            // Create texture.
            Texture2D tex = new Texture2D(device, desc, texData);

            return tex;
        }

        /// <summary>
        /// Returns the associated colormap as texture.
        /// </summary>
        /// <param name="map"></param>
        /// <returns></returns>
        public static ShaderResourceView GetColormapTexture(Colormap map)
        {
            return _maps[(int)map];
        }

        public static void Initialize(Device device)
        {
            // Fill resource views of colormap textures.
            string[] names = Enum.GetNames(typeof(Colormap));
            _maps = new ShaderResourceView[names.Length];
            for(int mapNr = 0; mapNr < names.Length; ++mapNr)
            {
                Texture2D map = Texture2D.FromFile(device, "Framework/Renderer/Resources/Colormap" + names[mapNr] + ".png");
                _maps[mapNr] = new ShaderResourceView(device, map);
            }
        }

        /// <summary>
        /// The maps associated with the Colormap enum.
        /// </summary>
        private static ShaderResourceView[] _maps;
    }

    /// <summary>
    /// All colormaps available.
    /// </summary>
    public enum Colormap
    {
        Parula = 0
    }
}
