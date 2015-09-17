﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using Device = SlimDX.Direct3D11.Device;
using Buffer = SlimDX.Direct3D11.Buffer;
using Debug = System.Diagnostics.Debug;
using SlimDX;
using SlimDX.D3DCompiler;
using System.IO;


namespace FlowSharp
{
    class Plane : ColormapRenderable
    {
        /// <summary>
        /// A number of equally sized textures. Depending on the number, a differnet mapping to color will be applied.
        /// </summary>
        protected ShaderResourceView[] _fields;

        /// <summary>
        /// Plane to display scalar/vector field data on. Condition: Fields domain is 2D.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="xAxis"></param>
        /// <param name="yAxis"></param>
        /// <param name="scale">Scaling the field extent.</param>
        /// <param name="field"></param>
        public Plane(Vector3 origin, Vector3 xAxis, Vector3 yAxis, float scale, ScalarField[] fields, RenderEffect effect = RenderEffect.DEFAULT, Colormap map = Colormap.Parula)
        {
#if DEBUG
            // Assert that the fields are 2 dimensional.
            foreach(ScalarField field in fields)
                System.Diagnostics.Debug.Assert(field.Size.Length == 2);
#endif
            this._effect = _planeEffect;
            this._vertexSizeBytes = 32;
            this._numVertices = 6;
            this.UsedMap = map;
            _planeEffect.GetVariableByName("width").AsScalar().Set(fields[0].Size[1]);
            _planeEffect.GetVariableByName("height").AsScalar().Set(fields[0].Size[0]);
            _planeEffect.GetVariableByName("invalidNum").AsScalar().Set((float)fields[0].InvalidValue);

            // Setting up the vertex buffer. 
            GenerateGeometry(origin, xAxis,yAxis, scale, (RectlinearGrid)fields[0].Grid);


            // Generating Textures from the fields.
            _fields = new ShaderResourceView[fields.Length];
            for(int f = 0; f < fields.Length; ++f)
            {
                Texture2D tex = ColorMapping.GenerateTextureFromField(_device, fields[f]);
                _fields[f] = new ShaderResourceView(_device, tex);
            }

            this.SetRenderEffect(effect);
            this._vertexLayout = new InputLayout(_device, _technique.GetPassByIndex(0).Description.Signature, new[] {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("TEXTURE", 0, Format.R32G32B32A32_Float, 16, 0)
            });
        }

        public void SetRenderEffect(RenderEffect effect)
        {
            switch (effect)
            {
                case RenderEffect.LIC:
                    Debug.Assert(_fields.Length == 2);
                    this._technique = _planeEffect.GetTechniqueByName("RenderLIC");
                    break;
                case RenderEffect.DEFAULT:
                default:
                    this._technique = _planeEffect.GetTechniqueByName("RenderTex" + _fields.Length);
                    break;
                
            }

        }

        /// <summary>
        /// Setting up the vertex buffer. Vertex size and number has to be known.
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="xAxis"></param>
        /// <param name="yAxis"></param>
        /// <param name="scale"></param>
        protected void GenerateGeometry(Vector3 origin, Vector3 xAxis, Vector3 yAxis, float scale, RectlinearGrid grid)
        {
            Vector Extent = grid.Extent;
            Vector3 maximum = origin + xAxis * Extent[1] * scale + yAxis * Extent[0] * scale;

            // Write poition and UV-map data.
            var stream = new DataStream(_numVertices * _vertexSizeBytes, true, true);
            stream.WriteRange(new[] {
                new Vector4( origin[0], maximum[1], 0.5f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
                new Vector4(maximum[0], maximum[1], 0.5f, 1.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f),
                new Vector4(maximum[0],  origin[1], 0.5f, 1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f),

                new Vector4( origin[0], maximum[1], 0.5f, 1.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f),
                new Vector4(maximum[0],  origin[1], 0.5f, 1.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f),               
                new Vector4( origin[0],  origin[1], 0.5f, 1.0f), new Vector4(0.0f, 0.0f, 0.0f, 1.0f)

            });
            stream.Position = 0;

            // Create and fill buffer.
            _vertices = new Buffer(_device, stream, new BufferDescription()
            {
                BindFlags = BindFlags.VertexBuffer,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = _numVertices * _vertexSizeBytes,
                Usage = ResourceUsage.Default
            });
            stream.Dispose();
        }

        public override void Render(Device device)
        {
            for(int fieldNr = 0; fieldNr < _fields.Length; ++fieldNr)
                _planeEffect.GetVariableByName("field" + fieldNr).AsResource().SetResource(_fields[fieldNr]);

            base.Render(device);
        }

        public override void Update(TimeSpan totalTime)
        {
        }

        /// <summary>
        /// Initialize the static components.
        /// </summary>
        /// <param name="device"></param>
        public static void Initialize(Device device)
        {
            _device = device;
            try
            {
                var bytecode = ShaderBytecode.CompileFromFile("Framework/Renderer/Data/DataEffects/Plane.fx", "fx_5_0", ShaderFlags.None, EffectFlags.None);
                _planeEffect = new Effect(_device, bytecode);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// The effects that will be used by the plane. Use technique "RenderTex"+numTextures to get the right technique.
        /// </summary>
        protected static Effect _planeEffect;

        /// <summary>
        /// Device for creating resources.
        /// </summary>
        protected static Device _device;

        public enum RenderEffect
        {
            DEFAULT = 0,
            LIC = 1
        }
    }
}