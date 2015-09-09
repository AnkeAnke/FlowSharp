using SlimDX;
using System;

namespace FlowSharp
{
    class FSMain
    {
        static ScalarField temperature;
        static VectorField velocity;

        public static void LoadData()
        {
            // Loading the temperature variable to have a frame of reference (should be ~20-35 degree at upper levels).
            Loader ncFile = new Loader("E:/Anke/Dev/Data/First/s1/Posterior_Diag.nc");

            Loader.SliceRange sliceT = new Loader.SliceRange(ncFile, RedSea.Variable.TEMPERATURE);
            sliceT.SetOffset(RedSea.Dimension.MEMBER, 0);
            sliceT.SetOffset(RedSea.Dimension.TIME, 0);
            sliceT.SetOffset(RedSea.Dimension.CENTER_Z, 0);
            temperature = ncFile.LoadFieldSlice(sliceT);

            Loader.SliceRange sliceV0 = new Loader.SliceRange(ncFile, RedSea.Variable.VELOCITY_X);
            sliceV0.SetOffset(RedSea.Dimension.MEMBER, 0);
            sliceV0.SetOffset(RedSea.Dimension.TIME, 0);
            sliceV0.SetOffset(RedSea.Dimension.CENTER_Z, 0);
            ScalarField v0 = ncFile.LoadFieldSlice(sliceV0);

            Loader.SliceRange sliceV1 = new Loader.SliceRange(ncFile, RedSea.Variable.VELOCITY_Y);
            sliceV1.SetOffset(RedSea.Dimension.MEMBER, 0);
            sliceV1.SetOffset(RedSea.Dimension.TIME, 0);
            sliceV1.SetOffset(RedSea.Dimension.CENTER_Z, 0);
            ScalarField v1 = ncFile.LoadFieldSlice(sliceV1);

            velocity = new VectorField(new ScalarField[] { v0, v1 });

            ncFile.Close();
        }

        public static void CreateRenderables()
        {
            Plane temperaturePlane = new Plane(new Vector3(-0.9f, -0.9f, 0.5f), Vector3.UnitX, Vector3.UnitY, 0.04f, new ScalarField[] { temperature });
            Plane velocityPlane = new Plane(new Vector3(-0.9f, 0.1f, 0.5f), Vector3.UnitX, Vector3.UnitY, 0.04f, velocity.Scalars);

            Renderer.Singleton.AddRenderable(temperaturePlane);
            Renderer.Singleton.AddRenderable(velocityPlane);
        }
    }
}
