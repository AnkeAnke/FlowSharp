
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SlimDX;
using System.Runtime.InteropServices;
using System.Collections;
using System.IO;

namespace FlowSharp
{
    class WallShearMapper : IntegrationMapper
    {
        public enum ShearMeasure
        {
            ShearStress_OscillatoryIndex,
            ShearStress_Given,
            ShearStress_Sample,
            NormalStress_Sample,
            ShearStress_Jacobi,
            NormalStress_Jacobi,
            //NormalStress_TimeDerivative,
        }

        Mesh _wall;

        ColormapRenderable _test;
        UnstructuredGeometry _geometryWall;
        //VectorData _wallShearStressSample, _wallNormalStressSample;
        //VectorData _wallShearStressJacobi, _wallNormalStressJacobi;
        VectorData[] _attributesStress;
        VectorData _attributeCurrent{
            get { return _attributesStress[Custom]; }
            set { _attributesStress[Custom] = value; } }

        VectorField _vectorField;
        List<LineSet> _streamLines;
        List<LineBall> _streamBall;
        UnstructuredGeometry _geometry;

        VectorData _canvasQuant, _canvasAnglePerp, _canvasAngleShear;

        public WallShearMapper(Plane plane) : base()
        {
            Mapping = ShowWall;
            BasePlane = plane;

            var geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Wall);
            _geometryWall = geomLoader.LoadGeometry();

            // Fit plane to data.
            this.BasePlane = Plane.FitToPoints(Vector3.Zero, 4, _geometryWall.Vertices);
            BasePlane.PointSize = 0.1f;



            _attributesStress = new VectorData[Enum.GetValues(typeof(ShearMeasure)).Length];

            LoadOrCreateOSI();
        }

        public List<Renderable> ShowWall()
        {
            List<Renderable> renderables = new List<Renderable>(16);

            if (_lastSetting == null || CustomChanged || LineXChanged)
            {
                TIMESTEP = LineX;
                if (Custom != (int)ShearMeasure.ShearStress_OscillatoryIndex)
                    LoadOrCreateWallShearStress();

                _attributeCurrent.ExtractMinMax();

                _wall = new Mesh(
                    BasePlane,
                    _geometryWall,
                    _attributeCurrent,
                    Mesh.RenderEffect.DEFAULT,
                    Colormap);
            }

            if (_lastSetting == null ||
                ColormapChanged ||
                WindowStartChanged ||
                WindowWidthChanged ||
                CustomChanged ||
                LineXChanged)
            {
                _wall.LowerBound = WindowStart;
                _wall.UpperBound = WindowStart + WindowWidth;
                _wall.UsedMap = Colormap;
            }

            renderables.Add(_wall);

            Plane cpy = new Plane(BasePlane);
            cpy.PointSize *= 10;
            var axes = cpy.GenerateOriginAxisGlyph();
            renderables.AddRange(axes);

            return renderables;
        }

        private int[] LoadOrCreateWriteSolidWallMapping(out UnstructuredGeometry geometrySolid, out Octree treeSolid)
        {
            // Load Geometry.
            LoaderVTU loader = new LoaderVTU(Aneurysm.GeometryPart.Solid);
            geometrySolid = loader.LoadGeometry();

            float eps = (geometrySolid.Vertices[1] - geometrySolid.Vertices[0]).LengthEuclidean() * 0.001f;

            // Build Tree.
            treeSolid = Octree.LoadOrComputeWrite(geometrySolid.Vertices, 10, 7, Aneurysm.GeometryPart.Solid, 20, "Vertices");

            string filename = Path.Combine(Aneurysm.Singleton.OctreeFolderFilename, "MapWallToSolid.intarray");

            int[] mapWallToSolid = BinaryFile.ReadFileArray<int>(filename);
            if (mapWallToSolid != null)
                return mapWallToSolid;

            // Stab Tree. Map Vertices.
            mapWallToSolid = new int[_geometryWall.Vertices.Length];
            for (int test = 0; test < mapWallToSolid.Length; ++test)
                mapWallToSolid[test] = -1;

            Octree.Node leaf;
            for (int v = 0; v < _geometryWall.Vertices.Length; v++)
            {
                VectorRef pos = _geometryWall.Vertices[v];
                //treeSolid.StabCell((Vector3)_geometryWall.Vertices[v], out leaf);
                var possibleVerts = treeSolid.FindWithinRadius((Vector3)pos, eps);
                if (possibleVerts.Count < 1)
                    Console.WriteLine("Whut");

                var vertsList = possibleVerts.ToList();
                vertsList.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));

                mapWallToSolid[v] = vertsList[0].Key;

                if (mapWallToSolid[v] < 0)
                {
                    Console.WriteLine($"Did not find wall vertex {v} at position {_geometryWall.Vertices[v]}");
                    throw new Exception();
                }
            }

            BinaryFile.WriteFileArray(filename, mapWallToSolid);

            return mapWallToSolid;
        }

        private VectorData LoadOrCreateWriteWallNormals()
        {
            string filename = Aneurysm.Singleton.CustomAttributeFilename("normals", Aneurysm.GeometryPart.Wall);

            VectorBuffer normals = BinaryFile.ReadFile(filename, 3);
            if (normals != null)
                return normals;

            normals = _geometryWall.ComputeNormals();

            BinaryFile.WriteFile(filename, normals);

            return normals;
        }

        private void LoadOrCreateOSI()
        {
            string filenameTime = Aneurysm.Singleton.CustomAttributeFilename($"WallShearStressTime", Aneurysm.GeometryPart.Wall);
            //string filenameTimeNormal = Aneurysm.Singleton.CustomAttributeFilename($"WallNormalStressTime", Aneurysm.GeometryPart.Wall);

            _attributesStress[(int)ShearMeasure.ShearStress_OscillatoryIndex] = BinaryFile.ReadFile(filenameTime, 1);
            //_attributesStress[(int)ShearMeasure.NormalStress_TimeDerivative] = BinaryFile.ReadFile(filenameTime, 1);

            if (_attributesStress[(int)ShearMeasure.ShearStress_OscillatoryIndex] != null)
                //&& _attributesStress[(int)ShearMeasure.NormalStress_TimeDerivative] != null)
                return;

            Console.WriteLine("===== Computing Oscillatory Shear Index");
            //_attributesStress[(int)ShearMeasure.ShearStress_TimeDerivative]  = new VectorBuffer(_geometryWall.Vertices.Length, 1);
            //_attributesStress[(int)ShearMeasure.NormalStress_TimeDerivative] = new VectorBuffer(_geometryWall.Vertices.Length, 1);
            var denomShearOSI = new VectorBuffer(_geometryWall.Vertices.Length, 1);
            //var denomNormalOSI = new VectorBuffer(_geometryWall.Vertices.Length, 1);
            var nomShearOSI = new VectorBuffer(_geometryWall.Vertices.Length, 3);
            //var nomNormalOSI = new VectorBuffer(_geometryWall.Vertices.Length, 1);

            VectorData.LocalFunction addFunc = (x, y) => { return x + y; };
            VectorData.LocalFunction addNormFunc = (x, y) => { return x + y.LengthEuclidean(); };

            for (int t = 0; t < Aneurysm.Singleton.NumSteps; ++t)
            {
                LoaderEnsight velo = new LoaderEnsight(Aneurysm.GeometryPart.Wall);
                //VectorData givenShear = new VectorDataArray<VectorChannels>(
                //    new VectorChannels[] {
                //    velo.LoadAttribute(Aneurysm.Variable.x_wall_shear, t),
                //    velo.LoadAttribute(Aneurysm.Variable.y_wall_shear, t),
                //    velo.LoadAttribute(Aneurysm.Variable.z_wall_shear, t) });

                VectorData givenShear = new VectorChannels(
                   new float[][] {
                    velo.LoadAttribute(Aneurysm.Variable.x_wall_shear, t).GetChannel(0),
                    velo.LoadAttribute(Aneurysm.Variable.y_wall_shear, t).GetChannel(0),
                    velo.LoadAttribute(Aneurysm.Variable.z_wall_shear, t).GetChannel(0) });
                nomShearOSI.ApplyTo(addFunc, givenShear);
                denomShearOSI.ApplyTo(addNormFunc, givenShear);
            }

            denomShearOSI.ApplyTo((x, y) => { return (Vector)(0.5f * (1.0f - y.LengthEuclidean() / x[0])); }, nomShearOSI);

            _attributesStress[(int)ShearMeasure.ShearStress_OscillatoryIndex] = denomShearOSI;

            BinaryFile.WriteFile(filenameTime, _attributesStress[(int)ShearMeasure.ShearStress_OscillatoryIndex]);
        }

        private void LoadOrCreateWallShearStress()
        {
            // Try to load.
            string filenameSample = Aneurysm.Singleton.CustomAttributeFilename($"WallShearStressSample_{TIMESTEP}",  Aneurysm.GeometryPart.Wall);
            string filenameJacobi = Aneurysm.Singleton.CustomAttributeFilename($"WallShearStressJacobi_{TIMESTEP}", Aneurysm.GeometryPart.Wall);

            string filenameSampleNormal = Aneurysm.Singleton.CustomAttributeFilename($"WallNormalStressSample_{TIMESTEP}", Aneurysm.GeometryPart.Wall);
            string filenameJacobiNormal = Aneurysm.Singleton.CustomAttributeFilename($"WallNormalStressJacobi_{TIMESTEP}", Aneurysm.GeometryPart.Wall);

            _attributesStress[(int)ShearMeasure.ShearStress_Sample] =
                BinaryFile.ReadFile(filenameSample, 1);

            _attributesStress[(int)ShearMeasure.ShearStress_Jacobi] =
                BinaryFile.ReadFile(filenameJacobi, 1);

            _attributesStress[(int)ShearMeasure.NormalStress_Sample] =
                BinaryFile.ReadFile(filenameSampleNormal, 1);

            _attributesStress[(int)ShearMeasure.NormalStress_Jacobi] =
                BinaryFile.ReadFile(filenameJacobiNormal, 1);

            // Load the given WSS.
            LoaderEnsight loader = new LoaderEnsight(Aneurysm.GeometryPart.Wall);
            _attributesStress[(int)ShearMeasure.ShearStress_Given] =
                loader.LoadAttribute(Aneurysm.Variable.wall_shear, TIMESTEP);

            bool compute = false;
            foreach (var field in _attributesStress)
                if (field == null)
                    compute = true;
            if (!compute)
                return;

            // Loading did not work. Compute and save.

            _attributesStress[(int)ShearMeasure.ShearStress_Sample] =
                new VectorBuffer(_geometryWall.Vertices.Length, 1);

            _attributesStress[(int)ShearMeasure.ShearStress_Jacobi] =
                new VectorBuffer(_geometryWall.Vertices.Length, 1);



            _attributesStress[(int)ShearMeasure.NormalStress_Sample] =
                new VectorBuffer(_geometryWall.Vertices.Length, 1);

            _attributesStress[(int)ShearMeasure.NormalStress_Jacobi] =
                new VectorBuffer(_geometryWall.Vertices.Length, 1);



            // Map tets to vertices.
            UnstructuredGeometry geometrySolid;
            Octree treeSolid;
            int[] mapWallToSolid = LoadOrCreateWriteSolidWallMapping(out geometrySolid, out treeSolid);            

            List<int>[] mapVertToTets = new List<int>[geometrySolid.Vertices.Length];
            for (int m = 0; m < mapVertToTets.Length; ++m)
                mapVertToTets[m] = new List<int>();

            for (int t = 0; t < geometrySolid.NumCells; ++t)
            {
                for (int v = 0; v < 4; ++v)
                    mapVertToTets[geometrySolid.Primitives[t][v]].Add(t);
            }

            // Read/Comute Normals.
            VectorData normals = LoadOrCreateWriteWallNormals();
            float eps = (geometrySolid.Vertices[1] - geometrySolid.Vertices[0]).LengthEuclidean() * 0.01f;

            // Stab Octree.
            Vector4 bary;
            Vector pointInside;
            VectorRef normal;

            LoaderEnsight velo = new LoaderEnsight(Aneurysm.GeometryPart.Solid);
            VectorData velocitySolid = velo.LoadAttribute(Aneurysm.Variable.velocity, TIMESTEP);
            VectorData givenShear = velo.LoadAttribute(Aneurysm.Variable.wall_shear, TIMESTEP);
            for (int v = 0; v < _geometryWall.Vertices.Length; ++v)
            {
                
                normal = normals[v];
                List<int> possibleTets = mapVertToTets[mapWallToSolid[v]];

                // Test all tetrahedrons touching the wall vertex.
                bool anyWorked = false;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    if (anyWorked)
                        break;
                    
                    foreach (int tet in possibleTets)
                    {
                        // Swizzle wall vertex to index 0.
                        // We want the wall vertex at the first position to simplyfy computation with the Jacobian.
                        Index indexTet = geometrySolid.Primitives[tet];
                        for (int i = 0; i < 4; ++i)
                            if (indexTet[i] == v)
                            {
                                indexTet[i] = indexTet[0];
                                indexTet[0] = v;
                                break;
                            }

                        //float eps = (geometrySolid.Vertices[indexTet[1]] + geometrySolid.Vertices[indexTet[2]] + geometrySolid.Vertices[indexTet[3]] - geometrySolid.Vertices[v]*3).LengthEuclidean() / 6;
                        pointInside = _geometryWall.Vertices[v] + normals[v] * sign * eps;

                        bool worked = UtilTet.ToBaryCoord(geometrySolid.Vertices, geometrySolid.Primitives, tet, (Vector3)pointInside, out bary);
                        if (!worked)
                            continue;
                        
                        anyWorked = true;
                        Vector fieldSample = Util.WeightCombine(velocitySolid, new Vector(bary), geometrySolid.Primitives[tet]);

                        
                        // Compute stress by sample inside of cell.
                        float wns = VectorRef.Dot(fieldSample, normal);
                        Vector wss = fieldSample - wns * normal;

                        //_attributesStress[(int)ShearMeasure.WNS_Sample][v] = (Vector)wns;
                        _attributesStress[(int)ShearMeasure.NormalStress_Sample][v] = (Vector)(wns * normal).LengthEuclidean();
                        _attributesStress[(int)ShearMeasure.ShearStress_Sample][v] = (Vector)wss.LengthEuclidean();


                        // Compute stress using the Jacobian.
                        // TODO: Maybe combine all adjacent Jacobians?
                        SquareMatrix jacobian = UtilTet.Jacobian(geometrySolid.Vertices, velocitySolid, indexTet);

                        fieldSample = jacobian * normal;
                        wns = VectorRef.Dot(fieldSample, normal);
                        wss = fieldSample - wns * normal;

                        _attributesStress[(int)ShearMeasure.NormalStress_Jacobi][v] = (Vector)wns;
                        _attributesStress[(int)ShearMeasure.ShearStress_Jacobi][v] = (Vector)wss.LengthEuclidean();

                        // TESTS
                        //_attributesStress[(int)ShearMeasure.ShearStress_Jacobi][v] = (Vector)wns;
                        //_attributesStress[(int)ShearMeasure.NormalStress_Jacobi][v] = (Vector)jacobian[0].LengthEuclidean();
                        break;
                        
                    }
                }
            }

            Console.WriteLine("Computed Stress Measures.");
            BinaryFile.WriteFile(filenameSample, _attributesStress[(int)ShearMeasure.ShearStress_Sample]);
            BinaryFile.WriteFile(filenameJacobi, _attributesStress[(int)ShearMeasure.ShearStress_Jacobi]);
            BinaryFile.WriteFile(filenameSampleNormal, _attributesStress[(int)ShearMeasure.NormalStress_Sample]);
            BinaryFile.WriteFile(filenameJacobiNormal, _attributesStress[(int)ShearMeasure.NormalStress_Jacobi]);
        }


        public override IEnumerable GetCustomAttribute()
        {
            return Enum.GetValues(typeof(ShearMeasure)).Cast<ShearMeasure>();
        }

        public override string GetName(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.LineX:
                    return "Time Step";
                default:
                    return base.GetName(element);
            }
        }

        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.Colormap:
                case Setting.Element.WindowStart:
                case Setting.Element.WindowWidth:
                case Setting.Element.Measure:
                case Setting.Element.GeometryPart:
                case Setting.Element.Custom:
                case Setting.Element.LineX:
                    return true;
                default:
                    return false;
            }
        }

        public override float? GetMin(Setting.Element element)
        {
            _attributeCurrent.ExtractMinMax();
            float min = _attributeCurrent?.MinValue?[0] ?? -500f;

            switch (element)
            {
                case Setting.Element.WindowWidth:
                    return 0;
                case Setting.Element.WindowStart:
                    return min;
                default:
                    return base.GetMin(element);
            }
        }

        public override float? GetMax(Setting.Element element)
        {
            _attributeCurrent.ExtractMinMax();
            float min = _attributeCurrent?.MinValue?[0] ?? -500f;
            float max = _attributeCurrent?.MaxValue?[0] ?? 500;
            if (max == min)
                max = min + 0.001f;
            //Console.WriteLine("Attribute {2}:\n\tAttribute min {0}\n\tAttribute max {1}", min, max, element.ToString());
            switch (element)
            {
                case Setting.Element.WindowWidth:
                    return max - min;
                case Setting.Element.WindowStart:
                    return max;
                default:
                    return base.GetMax(element);
            }
        }
    }
}
