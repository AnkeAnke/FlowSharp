using SlimDX;
using System;

namespace FlowSharp
{
    class FSMain
    {
        static VectorFieldUnsteady velocity;
        static VectorField velocityT0;
        static VectorField velocityT1;
        //static VectorField fff;
        static CriticalPointSet2D seedData;
        static CriticalPointSet2D cpT0;
        static CriticalPointSet2D cpT1;
        static LineSet cpLinesPos, cpLinesNeg;

        static PointSet<Point> colorCoded;

        static CriticalPointSet2D[] allCpsSlices;

        public static void LoadData()
        {
            int numTimeSlices = 10;
            Loader ncFile = new Loader("E:/Anke/Dev/Data/First/s1/Posterior_Diag.nc");
            ScalarField[] u = new ScalarField[numTimeSlices];
            Loader.SliceRange sliceU = new Loader.SliceRange(ncFile, RedSea.Variable.VELOCITY_X);
            sliceU.SetOffset(RedSea.Dimension.MEMBER, 0);
            sliceU.SetOffset(RedSea.Dimension.TIME, 0);
            sliceU.SetOffset(RedSea.Dimension.CENTER_Z, 0);

            ScalarField[] v = new ScalarField[numTimeSlices];
            Loader.SliceRange sliceV = new Loader.SliceRange(ncFile, RedSea.Variable.VELOCITY_Y);
            sliceV.SetOffset(RedSea.Dimension.MEMBER, 0);
            sliceV.SetOffset(RedSea.Dimension.TIME, 0);
            sliceV.SetOffset(RedSea.Dimension.CENTER_Z, 0);

            // Load first time slice.
            u[0] = ncFile.LoadFieldSlice(sliceU);
            v[0] = ncFile.LoadFieldSlice(sliceV);

            ncFile.Close();

            for (int time = 1; time < numTimeSlices; ++time)
            { 
                ncFile = new Loader("E:/Anke/Dev/Data/First/s" + (time+1) + "/Posterior_Diag.nc");
                u[time] = ncFile.LoadFieldSlice(sliceU);
                v[time] = ncFile.LoadFieldSlice(sliceV);
                ncFile.Close();
            }

            ScalarFieldUnsteady uTime = new ScalarFieldUnsteady(u);
            ScalarFieldUnsteady vTime = new ScalarFieldUnsteady(v);
            velocity = new VectorFieldUnsteady(new ScalarFieldUnsteady[] { uTime, vTime });

            Console.WriteLine("Completed loading data.");


            velocityT0 = velocity.GetTimeSlice(1);
            velocityT1 = velocity.GetTimeSlice(8);

            seedData = FieldAnalysis.ComputeCriticalPointsRegularSubdivision2D(velocityT0, 5, 0.3f);

            seedData = seedData.SelectTypes(new CriticalPoint2D.TypeCP[] { CriticalPoint2D.TypeCP.ATTRACTING_FOCUS, CriticalPoint2D.TypeCP.REPELLING_FOCUS });
            // Critical points.
            cpT0 = seedData.SelectTypes(new CriticalPoint2D.TypeCP[] { CriticalPoint2D.TypeCP.ATTRACTING_NODE, CriticalPoint2D.TypeCP.REPELLING_NODE, CriticalPoint2D.TypeCP.SADDLE });
            cpT1 = FieldAnalysis.ComputeCriticalPointsRegularSubdivision2D(velocityT1, 5, 0.5f);

            //var xSeedData = FieldAnalysis.SomePoints3D(velocity, 200);

            VectorField fffPos = new VectorField(velocity, VectorFieldUnsteady.StableFFF, 3); // (vec, J) => new Vec3(Vec3.Cross(J.Row(0).AsVec3(), J.Row(1).AsVec3())));
            VectorField fffNeg = new VectorField(velocity, VectorFieldUnsteady.StableFFFNegative, 3);
            VectorField.Integrator intVF = new VectorField.IntegratorEuler(fffPos);
            intVF.StepSize = 0.05f;
            intVF.WorldPosition = false;

            cpLinesPos = intVF.Integrate(seedData);

            // Negative FFF integration. Reversed stabilising field.
            //intVF.Direction = Sign.NEGATIVE;
            intVF.Field = fffNeg;
            cpLinesNeg = intVF.Integrate(seedData);
            cpLinesNeg.Color = new Vector3(0.0f, 0.7f, 0.0f);


            //PointSet cp = FieldAnalysis.ComputeCriticalPointsRegularSubdivision23D(velocityT0);
            //PointCloud cpCloud = new PointCloud(redSea, cp);

            // Compute critical points.
            //PointSet<CriticalPoint2D> cpVelocity = FieldAnalysis.ComputeCriticalPointsRegularSubdivision23D(velocityT0, 8);
            //PointCloud pointsCpVelocity = new PointCloud(redSea, cpVelocity);

            //var xSeedData = FieldAnalysis.SomePoints3D(velocity, 200);
            //VectorField.Integrator intVF = new VectorField.IntegratorEuler(velocity);
            //intVF.StepSize = 0.05f;
            //intVF.WorldPosition = false;

            //cpLinesPos = intVF.Integrate(xSeedData);
            //intVF.Direction = Sign.NEGATIVE;
            //cpLinesNeg = intVF.Integrate(xSeedData);

            colorCoded = velocity.ColorCodeArbitrary(cpLinesPos, x => new Vector3(velocity.Sample((Vec3)x, cpLinesPos.WorldPosition).ToVec2().LengthEuclidean() * 10));

            // Trying integrator in 2D.
            //            var randomPos = FieldAnalysis.SomePoints2D(velocityT0, 10);
            //            cpLinesPos = intVF.Integrate(randomPos);
            //            intVF.Direction = Sign.NEGATIVE;
            //            cpLinesNeg = intVF.Integrate(randomPos);

            //allCpsSlices = new CriticalPointSet2D[10];
            //for(int i =0; i < 10; ++i)
            //{
            //    VectorField field = velocity.GetTimeSlice(i);
            //    allCpsSlices[i] = FieldAnalysis.ComputeCriticalPointsRegularSubdivision2D(field, 5, 0.05f);
            //}


            Console.WriteLine("Computed all data necessary.");
        }

        public static void CreateRenderables()
        {
            Plane redSea = new Plane(new Vector3(-10, -5, -5), Vector3.UnitX, Vector3.UnitY, 0.5f, 0.1f);
            FieldPlane velocityPlane = new FieldPlane(redSea, velocityT0, FieldPlane.RenderEffect.LIC);
            FieldPlane velocityPlaneT1 = new FieldPlane(redSea, velocityT1, FieldPlane.RenderEffect.LIC);

            PointCloud<CriticalPoint2D> seeds = new PointCloud<CriticalPoint2D>(redSea, seedData);
            Renderer.Singleton.AddRenderable(seeds);

            //PointCloud<CriticalPoint2D> seedsT0 = new PointCloud<CriticalPoint2D>(redSea, cpT0);
            //Renderer.Singleton.AddRenderable(seedsT0);

            //for(int i =0; i <10; ++i)
            //{
            //    PointCloud< CriticalPoint2D > cp = new PointCloud<CriticalPoint2D>(redSea, allCpsSlices[i]);
            //    Renderer.Singleton.AddRenderable(cp);
            //}

            Renderer.Singleton.AddRenderable(velocityPlane);
            Renderer.Singleton.AddRenderable(velocityPlaneT1);

            PointCloud<Point> colorLine = new PointCloud<Point>(redSea, colorCoded);
            Renderer.Singleton.AddRenderable(colorLine);

            PointCloud<CriticalPoint2D> seedsT1 = new PointCloud<CriticalPoint2D>(redSea, cpT1);
            Renderer.Singleton.AddRenderable(seedsT1);

            //LineBall streamlinesPos = new LineBall(redSea, cpLinesPos);
            //Renderer.Singleton.AddRenderable(streamlinesPos);
            LineBall streamlinesNeg = new LineBall(redSea, cpLinesNeg);
            Renderer.Singleton.AddRenderable(streamlinesNeg);
        }
    }
}
