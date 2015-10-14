using SlimDX;
using System;

namespace FlowSharp
{
    class FSMain
    {
        static VectorFieldUnsteady velocity;
        //static VectorField velocityT0;
        //static VectorField velocityT1;
        ////static VectorField fff;
        //static CriticalPointSet2D seedData;
        //static CriticalPointSet2D cpT0;
        //static CriticalPointSet2D cpT1;
        //static LineSet cpLinesPos, cpLinesNeg;

        static PointSet<Point> colorCoded;

        //static CriticalPointSet2D[] allCpSlices;
        //static VectorField[] allTimeSlices;
        //static LineSet[] allCPLines;
        //static PointSet<Point>[] allCPLinesPoints;

        static DisplaySet cpSet;
        static CriticalPointSet2D[] completeCPSets;

        public static void LoadData()
        {
            // Playground.
            //SquareMatrix J = new SquareMatrix(new Vec3[] { new Vec3(-74, 3, 7), new Vec3(2, -61, 81), new Vec3(3, 6, -40) });
            //float ux, uy, ut, vx, vy, vt;
            //ux = J[0][0]; uy = J[1][0]; ut = J[2][0];
            //vx = J[0][1]; vy = J[1][1]; vt = J[2][1];
            //Vec3 correct = new Vec3(uy * vt - ut * vy, ut * vx - ux * vt, ux * vy - uy * vx);
            //Vec3 det = new Vec3(new SquareMatrix(new Vec2[] { J[1].ToVec2(), J[2].ToVec2() }).Determinant(),
            //         new SquareMatrix(new Vec2[] { J[2].ToVec2(), J[0].ToVec2() }).Determinant(),
            //         new SquareMatrix(new Vec2[] { J[0].ToVec2(), J[1].ToVec2() }).Determinant());
            //Vec3 cross = Vec3.Cross(J.Row(0).AsVec3(), J.Row(1).AsVec3());
            //Console.WriteLine("Correct: " + correct);
            //Console.WriteLine("det: " + det);
            //Console.WriteLine("Cross: " + cross);




            int numTimeSlices = 3;
            Loader ncFile = new Loader("E:/Anke/Dev/Data/First/s1/Posterior_Diag.nc");
            ScalarField[] u = new ScalarField[numTimeSlices];
            Loader.SliceRange sliceU = new Loader.SliceRange(ncFile, RedSea.Variable.VELOCITY_X);
            sliceU.SetOffset(RedSea.Dimension.MEMBER, 0); // Average
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

            completeCPSets = new CriticalPointSet2D[numTimeSlices];
            DisplaySet.FieldData[] sets = new DisplaySet.FieldData[numTimeSlices];

            // Compute some 3D values.
            VectorField fffPos = new VectorField(velocity, VectorFieldUnsteady.StableFFF, 3); // (vec, J) => new Vec3(Vec3.Cross(J.Row(0).AsVec3(), J.Row(1).AsVec3())));
            VectorField fffNeg = new VectorField(velocity, VectorFieldUnsteady.StableFFFNegative, 3);

            for (int time = 0; time < numTimeSlices; ++time)
            {
                DisplaySet.FieldData set = new DisplaySet.FieldData(velocity.GetTimeSlice(time));

                // Points: CP subset
                //                velocityT1 = velocity.GetTimeSlice(8);

                completeCPSets[time] = FieldAnalysis.ComputeCriticalPointsRegularSubdivision2D(set.Field, 5, 0.3f);

                var seedData = completeCPSets[time].SelectTypes(new CriticalPoint2D.TypeCP[] { CriticalPoint2D.TypeCP.ATTRACTING_FOCUS, CriticalPoint2D.TypeCP.REPELLING_FOCUS });
                set.Points = new CriticalPointSet2D[] { seedData };
                // Critical points.
                
                //cpT0 = seedData.SelectTypes(new CriticalPoint2D.TypeCP[] { CriticalPoint2D.TypeCP.ATTRACTING_NODE, CriticalPoint2D.TypeCP.REPELLING_NODE, CriticalPoint2D.TypeCP.SADDLE });
                //cpT1 = FieldAnalysis.ComputeCriticalPointsRegularSubdivision2D(velocityT1, 5, 0.5f);

                //var xSeedData = FieldAnalysis.SomePoints3D(velocity, 200);

                VectorField.Integrator intVF = new VectorField.IntegratorEuler(fffPos);
                intVF.MaxNumSteps = 10000;
                intVF.StepSize = 0.02f;
                intVF.WorldPosition = false;

                var cpLinesPos = intVF.Integrate(seedData);

                // Negative FFF integration. Reversed stabilising field.
                //intVF.Direction = Sign.NEGATIVE;
                intVF.Field = fffNeg;
                var cpLinesNeg = intVF.Integrate(seedData);
                cpLinesNeg.Color = new Vector3(0.0f, 0.7f, 0.0f);
                set.Lines = new LineSet[] { cpLinesNeg, cpLinesPos };

                sets[time] = set;

                Console.WriteLine("Completed processing step " + time + '.');
            }

            Plane redSea = new Plane(new Vector3(-10, -5, -5), Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ * 3, 0.5f, 0.1f);
            cpSet = new DisplaySet(sets, redSea, velocity);

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

            //            colorCoded = velocity.ColorCodeArbitrary(cpLinesPos, RedSea.DisplayLineFunctions[(int)RedSea.DisplayLines.POINTS_2D_LENGTH]); // (f, w, x) => new Vector3(f.Sample((Vec3)x, w.WorldPosition).ToVec2().LengthEuclidean() * 10));

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

            RedSea.Singleton.SetPresets(new DisplaySet[] { null, cpSet });
//            FieldPlane velocityPlane = new FieldPlane(redSea, velocityT0, FieldPlane.RenderEffect.LIC);
//            FieldPlane velocityPlaneT1 = new FieldPlane(redSea, velocityT1, FieldPlane.RenderEffect.LIC);
//
//            PointCloud<CriticalPoint2D> seeds = new PointCloud<CriticalPoint2D>(redSea, seedData);
//            Renderer.Singleton.AddRenderable(seeds);
//
//            //PointCloud<CriticalPoint2D> seedsT0 = new PointCloud<CriticalPoint2D>(redSea, cpT0);
//            //Renderer.Singleton.AddRenderable(seedsT0);
//
//            //for(int i =0; i <10; ++i)
//            //{
//            //    PointCloud< CriticalPoint2D > cp = new PointCloud<CriticalPoint2D>(redSea, allCpsSlices[i]);
//            //    Renderer.Singleton.AddRenderable(cp);
//            //}
//
//            Renderer.Singleton.AddRenderable(velocityPlane);
//            Renderer.Singleton.AddRenderable(velocityPlaneT1);
//
//            PointCloud<Point> colorLine = new PointCloud<Point>(redSea, colorCoded);
//            Renderer.Singleton.AddRenderable(colorLine);
//
//            PointCloud<CriticalPoint2D> seedsT1 = new PointCloud<CriticalPoint2D>(redSea, cpT1);
//            Renderer.Singleton.AddRenderable(seedsT1);
//
//            //LineBall streamlinesPos = new LineBall(redSea, cpLinesPos);
//            //Renderer.Singleton.AddRenderable(streamlinesPos);
//            LineBall streamlinesNeg = new LineBall(redSea, cpLinesNeg);
//            Renderer.Singleton.AddRenderable(streamlinesNeg);
        }
    }
}
