
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SlimDX;
using System.Runtime.InteropServices;

namespace FlowSharp
{
    class WallShearMapper : IntegrationMapper
    {
        Mesh _viewGeom;

        ColormapRenderable _test;
        UnstructuredGeometry _geometryWall;
        VectorData _wallShearStressSample, _wallNormalStressSample;
        VectorData _wallShearStressJacobi, _wallNormalStressJacobi;
        VectorData _attributeCurrent;

        VectorField _vectorField;
        List<LineSet> _streamLines;
        List<LineBall> _streamBall;
        UnstructuredGeometry _geometry;

        VectorData _canvasQuant, _canvasAnglePerp, _canvasAngleShear;

        public WallShearMapper(Plane plane) : base()
        {
            Mapping = ShowSide;
            BasePlane = plane;

            var geomLoader = new LoaderVTU(Aneurysm.GeometryPart.Wall);
            _geometryWall = geomLoader.LoadGeometry();

            // Fit plane to data.
            this.BasePlane = Plane.FitToPoints(Vector3.Zero, 4, _geometryWall.Vertices);
            BasePlane.PointSize = 0.1f;

            TIMESTEP = 0;

            LoadOrCreateWallShearStress();
        }

        public List<Renderable> ShowSide()
        {
            bool updateCubes = false;
            // Assemble renderables.
            var wire = new List<Renderable>(5);
            if (_lastSetting == null || GeometryPartChanged)
            {
                LoaderVTU geomLoader = new LoaderVTU(GeometryPart);
                var hexGrid = geomLoader.LoadGeometry();
                if (hexGrid == null)
                    Console.WriteLine("What?");



                _geometry = geomLoader.Grid;

                //update = false;
                updateCubes = true;
            }

            if (_lastSetting == null ||
                GeometryPartChanged ||
                MeasureChanged)
            {
                //if (GeometryPart == Aneurysm.GeometryPart.Wall)
                //{
                //    _attribute = BinaryFile.ReadFile(Aneurysm.Singleton.CustomAttributeFilename("SplatQuant", Aneurysm.GeometryPart.Wall), 1);
                //}
                //if (GeometryPart != Aneurysm.GeometryPart.Wall || _attribute == null)
                //{
                _attributeCurrent = null;

                LoaderEnsight attribLoader = new LoaderEnsight(GeometryPart);

                if (Measure == Aneurysm.Measure.x_wall_shear)
                    _attributeCurrent = _canvasQuant;
                if (Measure == Aneurysm.Measure.y_wall_shear)
                    _attributeCurrent = _canvasAnglePerp;
                if (Measure == Aneurysm.Measure.z_wall_shear)
                    _attributeCurrent = _canvasAngleShear;

                if (_attributeCurrent == null)
                    _attributeCurrent = attribLoader.LoadAttribute((Aneurysm.Variable)(int)Measure, 0);
                //}
                _attributeCurrent.ExtractMinMax();
                updateCubes = true;
            }

            if (updateCubes)
            {
                _viewGeom = new Mesh(BasePlane, _geometry, _attributeCurrent);
            }

            if (_streamLines != null &&
                _streamLines.Count > 0 &&
                (_lastSetting == null ||
                _streamBall == null))
            {
                _streamBall = new List<LineBall>(_streamLines.Count);
                foreach (LineSet lines in _streamLines)
                {
                    _streamBall.Add(new LineBall(BasePlane, lines, LineBall.RenderEffect.HEIGHT));

                    foreach (LineBall ball in _streamBall)
                    {
                        ball.LowerBound = 0;
                    }
                }
            }

            if (_streamBall != null)
                wire.AddRange(_streamBall);

            if (_lastSetting == null ||
                GeometryPartChanged ||
                WindowWidthChanged ||
                WindowStartChanged ||
                ColormapChanged ||
                updateCubes)
            {
                _viewGeom.LowerBound = WindowStart;
                _viewGeom.UpperBound = WindowStart + WindowWidth;
                _viewGeom.UsedMap = Colormap;

                if (_streamBall != null)
                {
                    foreach (LineBall ball in _streamBall)
                    {
                        ball.UsedMap = ColorMapping.GetComplementary(Colormap);
                        ball.LowerBound = WindowStart;
                        ball.UpperBound = WindowStart + WindowWidth;
                    }
                    if (_streamBall.Count > 0)
                        _streamBall[0].UsedMap = Colormap.Red;
                    if (_streamBall.Count > 1)
                        _streamBall[1].UsedMap = Colormap.Green;
                }
            }

            wire.Add(_viewGeom);

            if (_test != null)
                wire.Add(_test);

            var axes = BasePlane.GenerateOriginAxisGlyph();
            wire.AddRange(axes);
            return wire;

        }

        private int[] LoadOrCreateWriteSolidWallMapping(out UnstructuredGeometry geometrySolid, out Octree treeSolid)
        {
            geometrySolid = null;
            treeSolid = null;
            string filename = Aneurysm.Singleton.OctreeFolderFilename + "MapWallToSolid.intarray";

            int[] mapWallToSolid = BinaryFile.ReadFileArray<int>(filename);
            if (mapWallToSolid != null)
                return mapWallToSolid;

            // Load Geometry.
            LoaderVTU loader = new LoaderVTU(Aneurysm.GeometryPart.Solid);
            geometrySolid = loader.LoadGeometry();

            float eps = (geometrySolid.Vertices[1] - geometrySolid.Vertices[0]).LengthEuclidean() * 0.001f;

            // Build Tree.
            treeSolid = Octree.LoadOrComputeWrite(geometrySolid.Vertices, 10, 10, Aneurysm.GeometryPart.Solid, 20, "Vertices");

            // Stab Tree. Map Vertices.
            mapWallToSolid = new int[_geometryWall.Vertices.Length];
            for (int test = 0; test < mapWallToSolid.Length; ++test)
                mapWallToSolid[test] = -1;

            Octree.Node leaf;
            for (int v = 0; v < _geometryWall.Vertices.Length; v++)
            {
                VectorRef pos = _geometryWall.Vertices[v];
                treeSolid.StabCell((Vector3)_geometryWall.Vertices[v], out leaf);
                foreach (int vertSolid in leaf.GetData(treeSolid))
                    if ((geometrySolid.Vertices[vertSolid] - pos).LengthEuclidean() < eps)
                    {
                        mapWallToSolid[v] = vertSolid;
                        break;
                    }
                if (mapWallToSolid[v] < 0)
                    Console.WriteLine($"Did not find wall vertex {v} at position {_geometryWall.Vertices[v]}");
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

        private void LoadOrCreateWallShearStress()
        {
            // Try to load.
            string filenameSample = Aneurysm.Singleton.CustomAttributeFilename($"WallShearStressSample_{TIMESTEP}",  Aneurysm.GeometryPart.Wall);
            string filenameJacobi = Aneurysm.Singleton.CustomAttributeFilename($"WallShearStressJacobi_{TIMESTEP}", Aneurysm.GeometryPart.Wall);
            string filenameSampleNormal = Aneurysm.Singleton.CustomAttributeFilename($"WallNormalStressSample_{TIMESTEP}", Aneurysm.GeometryPart.Wall);
            string filenameJacobiNormal = Aneurysm.Singleton.CustomAttributeFilename($"WallNormalStressJacobi_{TIMESTEP}", Aneurysm.GeometryPart.Wall);
            _wallShearStressSample  = BinaryFile.ReadFile(filenameSample, 3);
            _wallShearStressJacobi  = BinaryFile.ReadFile(filenameJacobi, 3);
            _wallNormalStressSample = BinaryFile.ReadFile(filenameSampleNormal, 3);
            _wallNormalStressJacobi = BinaryFile.ReadFile(filenameJacobiNormal, 3);

            if (_wallShearStressSample != null || _wallShearStressSample != null)
                return;

            // Loading did not work. Compute and save.

            _wallShearStressSample  = new VectorBuffer(_geometryWall.Vertices.Length, 3);
            _wallShearStressJacobi  = new VectorBuffer(_geometryWall.Vertices.Length, 3);
            _wallNormalStressSample = new VectorBuffer(_geometryWall.Vertices.Length, 3);
            _wallNormalStressJacobi = new VectorBuffer(_geometryWall.Vertices.Length, 3);


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
            float eps = (geometrySolid.Vertices[1] - geometrySolid.Vertices[0]).LengthEuclidean() * 0.001f;

            // Stab Octree.
            Vector4 bary;
            Vector pointInside;
            VectorRef normal;

            LoaderEnsight velo = new LoaderEnsight(Aneurysm.GeometryPart.Solid);
            VectorData velocity = velo.LoadAttribute(Aneurysm.Variable.velocity, TIMESTEP);
            for (int v = 0; v < _geometryWall.Vertices.Length; ++v)
            {
                pointInside = _geometryWall.Vertices[v] + normals[v] * eps;
                normal = normals[v];
                List<int> possibleTets = mapVertToTets[mapWallToSolid[v]];

                // Test all tetrahedrons touching the wall vertex.
                foreach (int tet in possibleTets)
                {
                    bool worked = UtilTet.ToBaryCoord(geometrySolid.Vertices, geometrySolid.Primitives, tet, (Vector3)pointInside, out bary);
                    if (worked)
                    {
                        Vector fieldSample = Util.WeightCombine(geometrySolid.Vertices, new Vector(bary), geometrySolid.Primitives[tet]);
                        
                        // We want the wall vertex at the first position to simplyfy computation with the Jacobian.
                        Index indexTet = geometrySolid.Primitives[tet];
                        for (int i = 0; i < 4; ++i)
                            if (indexTet[i] == v)
                            {
                                indexTet[i] = indexTet[0];
                                indexTet[0] = v;
                                break;
                            }

                        // Compute stress by sample inside of cell.
                        Vector wns = VectorRef.Dot(fieldSample, normal) * normal;
                        Vector wss = fieldSample - wns;

                        _wallNormalStressSample[v] = wns / eps;
                        _wallShearStressSample[v]  = wss / eps;

                        // Compute stress using the Jacobian.
                        // TODO: Maybe combine all adjacent Jacobians?
                        SquareMatrix jacobian = UtilTet.Jacobian(geometrySolid.Vertices, velocity, indexTet);

                        fieldSample = jacobian * normal;
                        wns = VectorRef.Dot(fieldSample, normal) * normal;
                        wss = fieldSample - wns;

                        _wallNormalStressJacobi[v] = wns;
                        _wallShearStressJacobi[v]  = wss;
                    }
                }
            }

            BinaryFile.WriteFile(filenameSample, _wallShearStressSample);
            BinaryFile.WriteFile(filenameJacobi, _wallShearStressJacobi);
            BinaryFile.WriteFile(filenameSampleNormal, _wallNormalStressSample);
            BinaryFile.WriteFile(filenameJacobiNormal, _wallNormalStressJacobi);
        }

        //private void ComputeStress(UnstructuredGeometry geometrySolid, int vertexWall, int indexTet)
        //{

        //}

        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.Colormap:
                case Setting.Element.WindowStart:
                case Setting.Element.WindowWidth:
                case Setting.Element.Measure:
                case Setting.Element.GeometryPart:
                    return true;
                default:
                    return false;
            }
        }

        //public override string GetName(Setting.Element element)
        //{
        //    switch (element)
        //    {
        //        case Setting.Element.WindowWidth:
        //            return 0;
        //        case Setting.Element.WindowStart:
        //            return min;
        //        default:
        //            return base.GetMin(element);
        //    }
        //}

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
