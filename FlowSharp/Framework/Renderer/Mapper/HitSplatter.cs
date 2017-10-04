using SlimDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    static class HitSplatter
    {
        public static void SplatHits(/*float radiusSpatial, float radiusTime*/)
        {
            LoaderVTU loaderWall = new LoaderVTU(Aneurysm.GeometryPart.Wall);
            UnstructuredGeometry gridWall = loaderWall.LoadGeometry();
            Octree treeWall = Octree.LoadOrComputeWrite(gridWall.Vertices, 10, 10, Aneurysm.GeometryPart.Wall, float.MaxValue);

            // Load Time Steps
            string filenameHits = Aneurysm.Singleton.CustomAttributeFilename("ParticleHits", Aneurysm.GeometryPart.Wall);
            float[] points = BinaryFile.ReadAllFileArrays<float>(filenameHits);
            VectorBuffer endPoints = new VectorBuffer(points, 7);

            Console.WriteLine($"===== # End Points {endPoints.Length}");

            VectorBuffer normals = gridWall.ComputeNormals();

            //// Create or load all attribute canvases.
            //VectorData canvasQuant = LoadOrCreateEmptyWallCanvas($"SplatQuantity", TIMESTEP);
            //VectorData canvasAnglePerp = LoadOrCreateEmptyWallCanvas($"SplatPerpendicular", TIMESTEP);
            //VectorData canvasAngleShear = LoadOrCreateEmptyWallCanvas($"SplatShear", TIMESTEP);



            SplatToAttribute(treeWall, normals, endPoints, treeWall.Extent.Max() * 0.02f, 3);

            //timeWrite.Start();
            //BinaryFile.WriteFile(
            //    Aneurysm.Singleton.CustomAttributeFilename($"SplatQuantity_{TIMESTEP}", Aneurysm.GeometryPart.Wall),
            //    _canvasQuant);
            //BinaryFile.WriteFile(
            //    Aneurysm.Singleton.CustomAttributeFilename($"SplatPerpendicular_{TIMESTEP}", Aneurysm.GeometryPart.Wall),
            //    _canvasAnglePerp);
            //BinaryFile.WriteFile(
            //    Aneurysm.Singleton.CustomAttributeFilename($"SplatShear_{TIMESTEP}", Aneurysm.GeometryPart.Wall),
            //    _canvasAngleShear);
        }

        /// <summary>
        /// SPlats the given particles to the wall. If the result file does already exist, return.
        /// </summary>
        /// <param name="attributeTree">Tree of the wall geometry</param>
        /// <param name="normals">Normals of the wall geometry</param>
        /// <param name="points">Point set to splat</param>
        /// <param name="radiusSpatial">Maximal spatial radius (in 3D euclidean distance)</param>
        /// <param name="radiusTime">Maximal distance in time (in time scale)</param>
        private static void SplatToAttribute(
            Octree attributeTree,
            VectorData normals,
            VectorData points,
            float radiusSpatial, float radiusTime)
        {

            //int numPoints = points.Length;

            //// Concat filename strings for existence test. 42 is just an arbitrary number.
            //string filenameQuant42 = Aneurysm.Singleton.CustomAttributeFilename($"SplatQuantity_{42}_{numPoints}", Aneurysm.GeometryPart.Wall);

            //if (File.Exists(filenameQuant42))
            //{
            //    return;
            //}

            // Create a canvas for each measure.
            LoaderEnsight.LoadGridSizes();
            VectorData canvasQuant = new VectorBuffer(LoaderEnsight.NumVerticesPerPart[(int)Aneurysm.GeometryPart.Wall], 1, 0);
            VectorData canvasAnglePerp = new VectorBuffer(LoaderEnsight.NumVerticesPerPart[(int)Aneurysm.GeometryPart.Wall], 1, 0);
            VectorData canvasAngleShear = new VectorBuffer(LoaderEnsight.NumVerticesPerPart[(int)Aneurysm.GeometryPart.Wall], 1, 0);
            VectorData canvasVelocity = new VectorBuffer(LoaderEnsight.NumVerticesPerPart[(int)Aneurysm.GeometryPart.Wall], 1, 0);



            // Splat the points.

            Stopwatch watchSplat = new Stopwatch();
            watchSplat.Start();

            Stopwatch watchWrite = new Stopwatch();

            //Parallel.ForEach(points, pointVec =>
            //for (int p = 0; p < points.Length/100; ++p)
            for (int r  = 0; r < 100; ++r)
            { 
                Parallel.For(r * points.Length / 100,  Math.Min(points.Length, points.Length * (r+1)/100), p =>
                {
                    VectorRef pointVec = points[p];
                    //if (p % (points.Length/100) == 0)
                    //    Console.WriteLine($"\tSplatted {(p * 100) / points.Length}%");

                    //if (p % 100 == 0)
                    //Console.WriteLine($"Splatted {p} / {points.Length}");

                    DirectionPoint point = new DirectionPoint(pointVec);

                    //Console.WriteLine($"{p}:\n\tT = {pointVec.T}\n\tt = {pointVec.T / Aneurysm.Singleton.TimeScale}\n\ts = {firstTimeSlice}\n\tS = {firstTimeSlice + radiusTime * 2 + 1}");


                    Dictionary<int, float> verts = attributeTree.FindWithinRadius((Vector3)pointVec, radiusSpatial);
                    foreach (var v in verts)
                    {
                        float weightSpatial = v.Value / radiusSpatial;
                        weightSpatial = Math.Max(0, 1.0f - weightSpatial * weightSpatial);

                        Vector incident = new Vector(point.Direction);
                        incident.Normalize();
                        float angle = Math.Abs(VectorRef.Dot(normals[v.Key], incident));
                        angle = (float)(Math.Cosh(Math.Abs(angle)) / Math.PI);


                        //Console.WriteLine($"\tslice = {timeSlice}\n\ttdist = {Math.Abs(timeSlice - pointVec.T / Aneurysm.Singleton.TimeScale)}\n\tweigh = {weightTime}\n\ttotal = {weightTotal}\n");
                        canvasQuant[v.Key] += (Vector)weightSpatial;

                        canvasAnglePerp[v.Key] += (Vector)(weightSpatial * angle);
                        canvasAngleShear[v.Key] += (Vector)(weightSpatial * (1.0f - angle));
                        canvasVelocity[v.Key] += (Vector)point.Direction.Length();
                    }
                } );

                watchSplat.Stop();
                Console.WriteLine($"===== Time to splat {r}/100: {watchSplat.Elapsed}");

                // Write results to files.

                watchWrite.Start();


                BinaryFile.WriteFile(
                    Aneurysm.Singleton.CustomAttributeFilename($"SplatQuantity", Aneurysm.GeometryPart.Wall),
                    canvasQuant);

                BinaryFile.WriteFile(
                    Aneurysm.Singleton.CustomAttributeFilename($"SplatPerpendicular", Aneurysm.GeometryPart.Wall),
                    canvasAnglePerp);

                BinaryFile.WriteFile(
                    Aneurysm.Singleton.CustomAttributeFilename($"SplatShear", Aneurysm.GeometryPart.Wall),
                    canvasAngleShear);

                BinaryFile.WriteFile(
                        Aneurysm.Singleton.CustomAttributeFilename($"SplatVelocity", Aneurysm.GeometryPart.Wall),
                        canvasVelocity);

                watchWrite.Stop();
            }
            Console.WriteLine($"===== Time to write: {watchWrite.Elapsed}");
        }

        private static VectorBuffer LoadOrCreateEmptyWallCanvas(string name, int step)
        {
            VectorBuffer buff = BinaryFile.ReadFile(Aneurysm.Singleton.CustomAttributeFilename(name + $"_{step}", Aneurysm.GeometryPart.Wall), 1);
            if (buff == null)
                buff = new VectorBuffer(LoaderEnsight.NumVerticesPerPart[(int)Aneurysm.GeometryPart.Wall], 1, 0);

            return buff;
        }

    }
}
