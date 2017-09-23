using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    static class HitSplatter
    {
        public static void SplatHits(/*float radiusSpatial, float radiusTime*/)
        {
            LoaderVTU loader = new LoaderVTU(Aneurysm.GeometryPart.Wall);
            UnstructuredGeometry wallGrid = loader.LoadGeometry();
            Octree wallTree = Octree.LoadOrComputeWrite(wallGrid.Vertices, 10, 10, Aneurysm.GeometryPart.Wall, float.MaxValue);

            // Load Time Steps
            string filenameHits = Aneurysm.Singleton.CustomAttributeFilename("ParticleHits", Aneurysm.GeometryPart.Wall);
            float[] points = BinaryFile.ReadAllFileArrays<float>(filenameHits);
            VectorBuffer endPoints = new VectorBuffer(points, 7);

            //VectorBuffer normals = wallGrid.ComputeNormals();

            //// Create or load all attribute canvases.
            //VectorData canvasQuant = LoadOrCreateEmptyWallCanvas($"SplatQuantity", TIMESTEP);
            //VectorData canvasAnglePerp = LoadOrCreateEmptyWallCanvas($"SplatPerpendicular", TIMESTEP);
            //VectorData canvasAngleShear = LoadOrCreateEmptyWallCanvas($"SplatShear", TIMESTEP);



            //SplatToAttribute(wallTree, normals, endPoints, wallTree.Extent.Max() * 0.02f, 2);

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

        private static void SplatToAttribute(
            Octree attributeTree,
            VectorData normals,
            VectorData points,
            float radiusSpatial, float radiusTime)
        {



            //Parallel.ForEach(points, p =>
            //{
            //    Dictionary<int, float> verts = attributeTree.FindWithinRadius(Util.Convert(p.Position), radius);
            //    foreach (var v in verts)
            //    {
            //        float weight = radiusSpatial / v.Value - 1;
            //        canvasQuant[v.Key] += (Vector)weight;

            //        Vector incident = new Vector(p.Direction);
            //        incident.Normalize();

            //        float angle = VectorRef.Dot(normals[v.Key], incident);
            //        angle = (float)Math.Cosh(Math.Abs(angle));
            //        canvasPerp[v.Key] += weight / angle;
            //        canvasShear[v.Key] += angle * weight;
            //    }
            //});
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
