using SlimDX;
using System;

namespace FlowSharp
{
    class FSMain
    {
        static ScalarField temperature;
        static VectorField velocity;
        static PointSet points;

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


            Point a = new Point()
            {
                Position = new Vector3(5.0f, 10.0f, 0.5f),
                Color = new Vector3(0.0f, 0.0f, 1.0f),
                Radius = 0.015f
            };

            Point kaust = new Point()
            {
                Position = new Vector3(39.0f - 32.0f, 23.0f - 9.0f,  0.5f),
                Color = new Vector3(0.4f, 0.0f, 0.0f),
                Radius = 0.015f
            };
            points = new PointSet() { Points = new Point[] { kaust } };


            //Tests.TestCP();
            points = FieldAnalysis.ComputeCriticalPointsRectlinear2D(velocity);



            ncFile.Close();
        }

        public static void CreateRenderables()
        {
            Plane temperaturePlane = new Plane(new Vector3(-0.9f, -0.9f, 0.5f), Vector3.UnitX, Vector3.UnitY, 0.04f, new ScalarField[] { temperature });
            Plane velocityPlane = new Plane(new Vector3(-0.9f, 0.1f, 0.5f), Vector3.UnitX, Vector3.UnitY, 0.04f, velocity.Scalars, Plane.RenderEffect.LIC);
            PointCloud cloud = new PointCloud(new Vector3(-0.9f, 0.1f, 0.5f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, 0.04f, points);

            Renderer.Singleton.AddRenderable(temperaturePlane);
            Renderer.Singleton.AddRenderable(velocityPlane);
            Renderer.Singleton.AddRenderable(cloud);
        }
    }
}
