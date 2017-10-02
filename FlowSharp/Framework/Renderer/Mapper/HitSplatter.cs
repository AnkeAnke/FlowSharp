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



            SplatToAttribute(treeWall, normals, endPoints, treeWall.Extent.Max() * 0.05f, 3);

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

            VectorData[] canvasQuant      = new VectorData[Aneurysm.Singleton.NumSteps];
            VectorData[] canvasAnglePerp  = new VectorData[Aneurysm.Singleton.NumSteps];
            VectorData[] canvasAngleShear = new VectorData[Aneurysm.Singleton.NumSteps];

            // Create a canvas for each measure/time.

            LoaderEnsight.LoadGridSizes();

            for (int t = 0; t < canvasQuant.Length; ++t)
            {

                canvasQuant[t] = 
                    new VectorBuffer(LoaderEnsight.NumVerticesPerPart[(int)Aneurysm.GeometryPart.Wall], 1, 0);

                canvasAnglePerp[t] = 
                    new VectorBuffer(LoaderEnsight.NumVerticesPerPart[(int)Aneurysm.GeometryPart.Wall], 1, 0);

                canvasAngleShear[t] = 
                    new VectorBuffer(LoaderEnsight.NumVerticesPerPart[(int)Aneurysm.GeometryPart.Wall], 1, 0);
            }

            // Splat the points.

            Stopwatch watchSplat = new Stopwatch();
            watchSplat.Start();

            //Parallel.ForEach(points, pointVec =>
            for (int p = 0; p < points.Length; ++p)
            {
                VectorRef pointVec = points[p];
                //if (p % (points.Length/100) == 0)
                //    Console.WriteLine($"\tSplatted {(p * 100) / points.Length}%");

                if (p % 100 == 0)
                    Console.WriteLine($"Splatted {p} / {points.Length}");

                DirectionPoint point = new DirectionPoint(pointVec);
                int firstTimeSlice = (int)Math.Ceiling(pointVec.T / Aneurysm.Singleton.TimeScale - radiusTime + Aneurysm.Singleton.NumSteps) %
                                        Aneurysm.Singleton.NumSteps;

                Dictionary<int, float> verts = attributeTree.FindWithinRadius((Vector3)pointVec, radiusSpatial);
                foreach (var v in verts)
                {
                    float weightSpatial = 1.0f - radiusSpatial / v.Value;

                    Vector incident = new Vector(point.Direction);
                    incident.Normalize();
                    float angle = VectorRef.Dot(normals[v.Key], incident);
                    angle = (float)(Math.Cosh(Math.Abs(angle)) / Math.PI);

                    // Go over all time slices in time radius.
                    for (int timeSlice = firstTimeSlice; timeSlice < firstTimeSlice + radiusTime * 2 + 1; ++timeSlice)
                    {
                        float weightTime = radiusTime / Math.Abs(timeSlice * Aneurysm.Singleton.TimeScale - pointVec.T);
                        float weightTotal = Math.Max(0, weightSpatial - weightTime);
                        canvasQuant[timeSlice % Aneurysm.Singleton.NumSteps][v.Key] += (Vector)weightTotal;

                        canvasAnglePerp[timeSlice % Aneurysm.Singleton.NumSteps][v.Key] += (Vector)(weightTotal * angle);
                        canvasAngleShear[timeSlice % Aneurysm.Singleton.NumSteps][v.Key] += (Vector)(weightTotal * (1.0f - angle));
                    }
                }
            } //);

            watchSplat.Stop();
            Console.WriteLine($"===== Time to splat: {watchSplat.Elapsed}");

            // Write results to files.

            Stopwatch watchWrite = new Stopwatch();
            watchWrite.Start();

            for (int t = 0; t < canvasQuant.Length; ++t)
            {
                BinaryFile.WriteFile(
                    Aneurysm.Singleton.CustomAttributeFilename($"SplatQuantity_{t}", Aneurysm.GeometryPart.Wall),
                    canvasQuant[t]);

                BinaryFile.WriteFile(
                    Aneurysm.Singleton.CustomAttributeFilename($"SplatPerpendicular_{t}", Aneurysm.GeometryPart.Wall),
                    canvasAnglePerp[t]);

                BinaryFile.WriteFile(
                    Aneurysm.Singleton.CustomAttributeFilename($"SplatShear_{t}", Aneurysm.GeometryPart.Wall),
                    canvasAngleShear[t]);
            }

            watchWrite.Stop();
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
