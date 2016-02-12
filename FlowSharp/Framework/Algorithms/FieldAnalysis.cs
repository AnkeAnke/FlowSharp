using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SlimDX;

namespace FlowSharp
{
    /// <summary>
    /// Class wrapping a number of methods for vector field processing.
    /// </summary>
    static class FieldAnalysis
    {
        private const float EPS_ZERO = 0.015f;

        /// <summary>
        /// Computes all 0-vectors in a 2D rectlinear vector field.
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        //public static PointSet<Point> ComputeCriticalPointsRegularAnalytical2D(VectorField field, float eps = EPS_ZERO)
        //{
        //    // Only for 2D rectlinear grids.
        //    Debug.Assert(field.Grid as RectlinearGrid != null);
        //    Debug.Assert(field.Size.Length == 2);

        //    List<Point> cpList = new List<Point>(field.Size.Product() / 10); // Rough guess.
        //    // DEBUG
        //    cpList.Add(new Point()
        //    {
        //        Position = new Vector3(0, 0, 0),
        //        Color = new Vector3(1, 1, 0)
        //    });
        //    Vector halfCell = new Vector(0.5f, 2);
        //    //Index numCells = field.Size - new Index(1, field.Size.Length);
        //    for (int x = 0; x < field.Size[0] - 1; ++x)
        //        for (int y = 0; y < field.Size[1] - 1;) // Doing the y++ down at the end.
        //        {
        //            // Get neighbors.
        //            float[] cellWeights;
        //            Vector cellCenter = new Vector(new float[] { 0.5f + x, 0.5f + y });
        //            // Use the neighbor function of the grid data type.
        //            int[] adjacentCells = field.Grid.FindAdjacentIndices(cellCenter, out cellWeights);

        //            for (int dim = 0; dim < 2; ++dim)
        //            {
        //                bool pos = false;
        //                bool neg = false;

        //                for (int neighbor = 0; neighbor < adjacentCells.Length; ++neighbor)
        //                {
        //                    float data = field.Scalars[dim][adjacentCells[neighbor]];
        //                    // Is the cell data valid?
        //                    if (data == field.Scalars[dim].InvalidValue)
        //                        goto NextCell;
        //                    if (data > 0)
        //                        pos = true;
        //                    else if (data < 0)
        //                        neg = true;
        //                    else
        //                    {
        //                        neg = true;
        //                        pos = true;
        //                    }
        //                }
        //                // If no 0 can be achieved in this cell, go on to next cell.
        //                if (!pos || !neg)
        //                {
        //                    goto NextCell;
        //                }
        //            }

        //            //DEBUG:
        //            Vector3 color;

        //            // Now, compute the position. Possible since the function is only quadratic!
        //            Vector p00 = field.Sample(adjacentCells[0]);
        //            Vector p10 = field.Sample(adjacentCells[1]);
        //            Vector p01 = field.Sample(adjacentCells[2]);
        //            Vector p11 = field.Sample(adjacentCells[3]);
        //            Vector a = p10 - p00;
        //            Vector b = p01 - p00;
        //            Vector c = p00 - p01 - p10 + p11;
        //            Vector d = p00;

        //            // The two points that will be found.
        //            float[] valS = new float[2];
        //            float[] valT = new float[2];

        //            // Degenerated linear case?
        //            if (Math.Abs(c[0]) < eps || Math.Abs(c[1]) < eps)
        //            {
        //                // Degenerated double-linear case?
        //                if (Math.Abs(c[0]) < eps && Math.Abs(c[1]) < eps)
        //                {
        //                    float abi = 1 / (a[1] * b[0] - a[0] * b[1]);
        //                    // Only one solution.
        //                    valT = new float[1]; valS = new float[1];
        //                    valT[0] = (a[0] * d[1] - a[1] * d[0]) * abi;
        //                    valS[0] = -(b[0] * valT[0] / a[0] + d[0] / a[0]);
        //                    // DEBUG
        //                    //valT[0] = 0.5f; valS[0] = 0.5f;
        //                    color = new Vector3(1, 0, 0);

        //                    goto WritePoints;
        //                }
        //                // Dimension in which the solution is linear.
        //                int lD = Math.Abs(c[0]) < eps ? 0 : 1;
        //                int qD = 1 - lD;

        //                if (b[lD] == 0)
        //                    goto NextCell;

        //                float cbi = 1 / (c[qD] * b[lD]);
        //                // Values for PQ formula.
        //                float pPQ = d[lD] / b[lD] + a[qD] / c[qD] + a[lD] * b[qD] * cbi;
        //                pPQ /= 2;
        //                float qPQ = d[lD] * (a[qD] + a[lD]) * cbi;

        //                float root = pPQ * pPQ - qPQ;
        //                Debug.Assert(root >= 0);

        //                root = (float)Math.Sqrt(root);

        //                valT[0] = pPQ + root;
        //                valT[1] = pPQ - root;

        //                valS[0] = -(b[lD] * valT[0] / a[lD] + d[lD] / a[lD]);
        //                valS[1] = -(b[lD] * valT[1] / a[lD] + d[lD] / a[lD]);

        //                color = new Vector3(0, 1, 0);

        //                goto WritePoints; // Don't need this. Still here, for better readability.
        //            }
        //            else
        //            {
        //                // Both dimensions are quadratic.
        //                float denom = 1 / (a[0] * c[1] - a[1] * c[0]);
        //                Debug.Assert(denom != 0);
        //                float pPQ = (a[0] * b[1] - a[1] * b[0]) + (c[1] * d[0] - c[0] * d[1]);
        //                pPQ *= denom * 0.5f;
        //                float qPQ = (b[1] * d[0] - b[0] * d[1]) * denom;

        //                float root = pPQ * pPQ - qPQ;
        //                if (root < 0)
        //                    goto NextCell;
        //                root = (float)Math.Sqrt(root);
        //                valS[0] = (pPQ + root);
        //                valS[1] = (pPQ - root);

        //                valT[0] = -(a[0] * valS[0] + d[0]) / (b[0] + c[0] * valS[0]);
        //                valT[1] = -(a[0] * valS[1] + d[0]) / (b[0] + c[0] * valS[1]);

        //                color = new Vector3(0, 0, 1);
        //            }

        //            // Check whether the points lay in the cell. Write those to the point set.
        //            WritePoints:
        //            for (int p = 0; p < valS.Length; ++p)
        //            {
        //                //Continue when not inside.
        //                //if (valS[p] < 0 || valS[p] >= 1 || valT[p] < 0 || valT[p] >= 1)
        //                //    continue;

        //                Point cp = new Point()
        //                {
        //                    Position = new Vector3(origin[0] + (0.5f + x) * cSize[0], origin[1] + (0.5f + y) * cSize[1], 0.0f), //new Vector3(origin[0] + (valS[p] + x) * cSize[0], origin[1] + (valT[p] + y) * cSize[1], 0.0f),
        //                    Color = color, //new SlimDX.Vector3(0.01f, 0.001f, 0.6f), // Debug color. 
        //                    Radius = 0.002f
        //                };
        //                cpList.Add(cp);

        //                //Continue when not inside.
        //                if (valS[p] < 0 || valS[p] >= 1 || valT[p] < 0 || valT[p] >= 1)
        //                    continue;

        //                cp = new Point()
        //                {
        //                    Position = new Vector3(origin[0] + (valS[p] + x) * cSize[0], origin[1] + (valT[p] + y) * cSize[1], 0.0f),
        //                    Color = color, //new SlimDX.Vector3(0.01f, 0.001f, 0.6f), // Debug color. 
        //                    Radius = 0.006f
        //                };
        //                cpList.Add(cp);
        //            }

        //            NextCell:
        //            y++;
        //        }

        //    PointSet<Point> cpSet = new PointSet<Point>(cpList.ToArray());

        //    return cpSet;

        //}
        private static float _epsCriticalPoint;
        /// <summary>
        /// Searches for all 0-vectors in a 2 or 3D rectlinear vector field.
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static CriticalPointSet2D ComputeCriticalPointsRegularSubdivision2D(VectorField field, int numDivisions = 5, float? pointSize = null, float epsCriticalPoint = 0.00000001f)
        {
            // Only for rectlinear grids.
            RectlinearGrid grid = field.Grid as RectlinearGrid;
            Debug.Assert(grid != null);
//            Debug.Assert(grid.Size.Length == 2);
            _epsCriticalPoint = epsCriticalPoint;

            List<Vector> cpList = new List<Vector>(field.Size.Product() / 10); // Rough guess.
            List<Vector> cellList = new List<Vector>(6);
            Vector halfCell = new Vector(0.5f, 2);
            //Index numCells = field.Size - new Index(1, field.Size.Length);
            for (int x = 0; x < field.Size[0] - 1; ++x)
                for (int y = 0; y < field.Size[1] - 1; ++y) // Doing the y++ down at the end.
                {
                    //if (x == 362 && y == 32)
                    //    Console.WriteLine("eiwoe;");
                    SubdivideCell(field, new Vec2(x, y), 0, numDivisions, cellList);

                    // Only take one CP per cell.
                    // TODO: One for each CP type.
                    if (cellList.Count > 0)
                    {
                        Vector center = new Vector(0, 2);
                        foreach (Vector vec in cellList)
                            center += vec;

                        center /= cellList.Count;

                        cpList.Add(center);
                        cellList.Clear();
                    }
                }

            CriticalPoint2D[] points = new CriticalPoint2D[cpList.Count];

            // In a 2D slice, set 3rd value to time value.
            for (int index = 0; index < cpList.Count; ++index)
            {
                Vector3 pos = (Vector3)cpList[index];
                pos.Z += field.TimeSlice ?? 0;

                SquareMatrix J = field.SampleDerivative(cpList[index]);

                points[index] = new CriticalPoint2D(pos, J.ToMat2x2())
                {
                    Radius = pointSize ?? 1
                };
            }
            return new CriticalPointSet2D(points);
        }

        /// <summary>
        /// Searches for all 0-vectors in a 2 or 3D rectlinear vector field.
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static PointSet<Point> ComputeCriticalPointsRegularSubdivision23D(VectorField field, int numDivisions = 5, float? pointSize = null)
        {
            // Only for rectlinear grids.
            RectlinearGrid grid = field.Grid as RectlinearGrid;
            Debug.Assert(grid != null);
            Debug.Assert(grid.Size.Length <= 3);

            List<Vector> cpList = new List<Vector>(field.Size.Product() / 10); // Rough guess.

            Vector halfCell = new Vector(0.5f, 2);
            //Index numCells = field.Size - new Index(1, field.Size.Length);
            for (int x = 0; x < field.Size[0] - 1; ++x)
                for (int y = 0; y < field.Size[1] - 1; ++y) // Doing the y++ down at the end.
                {
                    SubdivideCell(field, new Vector(new float[] { x, y }), 0, numDivisions, cpList);
                }

            Point[] points = new Point[cpList.Count];
            // In a 2D slice, set 3rd value to time value.
            bool attachTimeZ = grid.Size.Length == 2 && field.TimeSlice != 0;
            for (int index = 0; index < cpList.Count; ++index)
            {
                points[index] = new Point()
                {
                    Position = (Vector3)cpList[index],
                    Color = new Vector3(1, 1, 0),
                    Radius = pointSize ?? 1
                };
                if (attachTimeZ)
                    points[index].Position.Z = (float)field.TimeSlice;
            }

            return new PointSet<Point>(points);
        }


        static float _epsZero = 0f;
        /// <summary>
        /// Recursively check each subcell if a 0 can be interpolated. Stop after some level of detail. 
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="level"></param>
        /// <param name="maxLevel"></param>
        /// <param name="cpList">Each found cp will be attached.</param>
        private static void SubdivideCell(VectorField field, Vector origin, int level, int maxLevel, List<Vector> cpList)
        {
            float cellLength = 1.0f / (1 << level);
            // For each dimension, check that a positive and negative value are present.
            for (int dim = 0; dim < field.Scalars.Length; ++dim)
            {
                bool pos = false;
                bool neg = false;

                for (int neighbor = 0; neighbor < (field.Grid as RectlinearGrid).NumAdjacentPoints(); ++neighbor)
                {
                    // Compute the neighbors position.
                    Vector position = new Vector(origin);
                    for (int axis = 0; axis < origin.Length; ++axis)
                    {
                        position[axis] += (neighbor & (1 << axis)) > 0 ? cellLength : 0;
                    }

                    float value = field.Scalars[dim].Sample(position);
                    if (value == field.Scalars[dim].InvalidValue)
                        return;
                    if (value >= -_epsZero)
                        pos = true;
                    if (value <= _epsZero)
                        neg = true;
                }
                if (!pos || !neg)
                    return;
            }
            // 0 can be included. Return or subdivide.
            // If the maximum depth is reached, append the point and return.
            if (level == maxLevel)
            {
                cpList.Add(origin + new Vector(cellLength * 0.5f, origin.Length));
                return;
            }
            //float lengthVecCenter = field.Sample(origin + new Vector(cellLength * 0.5f, origin.Length), false).LengthEuclidean();
            //if (lengthVecCenter < _epsCriticalPoint)
            //{
            //    cpList.Add(origin + new Vector(cellLength * 0.5f, origin.Length));
            //    return;
            //}
            // Subdivide into 2^dim parts.
            for (int part = 0; part < (field.Grid as RectlinearGrid).NumAdjacentPoints(); ++part)
            {
                // Compute the neighbors position.
                Vector position = new Vector(origin);
                for (int axis = 0; axis < origin.Length; ++axis)
                {
                    position[axis] += (part & (1 << axis)) > 0 ? cellLength * 0.5f : 0;
                }
                SubdivideCell(field, position, level + 1, maxLevel, cpList);
            }

        }

        /// <summary>
        /// Outputs all valid cells in the data set as points. Mostly for debugging purposes.
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static PointSet<T> ValidDataPoints<T>(VectorField field) where T :Point, new()
        {
            // Only for 2D rectlinear grids.
            Debug.Assert(field.Grid as RectlinearGrid != null);
            Debug.Assert(field.NumVectorDimensions == 2 || field.NumVectorDimensions == 3);

            List<T> cpList = new List<T>(field.Size.Product());

            //Index numCells = field.Size - new Index(1, field.Size.Length);
            for (int x = 0; x < field.Size[0]; ++x)
                for (int y = 0; y < field.Size[1]; ++y)
                    for (int z = 0; z < ((field.NumVectorDimensions == 3) ? field.Size[2] : 1); ++z)
                    {
                        int[] pos = (field.NumVectorDimensions == 3) ? new int[] { x, y, z } : new int[] { x, y };
                        float data = field.Scalars[0].Sample(new Index(pos));
                        // Is the cell data valid?
                        if (data == field.InvalidValue)
                            continue;

                        T cp = new T()
                        {
                            Position = new SlimDX.Vector3(x, y, z),
                            Color = new SlimDX.Vector3(0.6f, 0.3f, 0.3f), // Debug color. 
                            Radius = 0.1f
                        };
                        cpList.Add(cp);
                    }
            RectlinearGrid rGrid = field.Grid as RectlinearGrid;

            PointSet<T> cpSet = new PointSet<T>(cpList.ToArray());

            return cpSet;
        }

        public static PointSet<T> SomePoints2D<T>(VectorField field, int numPoints, float pointSize = 1) where T : Point, new()
        {
            // Only for 2D rectlinear grids.
            Debug.Assert(field.Grid as RectlinearGrid != null);
            Debug.Assert(field.NumVectorDimensions >= 2);
            Random rnd = new Random();

            T[] cpList = new T[numPoints];
            bool attachTimeZ = field.NumVectorDimensions == 2 && field.TimeSlice != 0;
            //Index numCells = field.Size - new Index(1, field.Size.Length);
            for (int index = 0; index < numPoints; ++index)
            {
                Vector pos = new Vector(0, field.Size.Length);
                pos[0] = (float)rnd.NextDouble() * (field.Size[0]-1);
                pos[1] = (float)rnd.NextDouble() * (field.Size[1]-1);
                float data = field.Scalars[0].Sample(pos);
                if(data == field.InvalidValue)
                {
                    index--;
                    continue;
                }
                T cp = new T()
                {
                    Position = (Vector3)pos,
                    Color = new Vector3((float)index / numPoints, 0.0f, 0.3f), // Debug color. 
                    Radius = pointSize
                };
                if (attachTimeZ)
                    cp.Position.Z = (float)field.TimeSlice;
                cpList[index] = cp;
            }
            RectlinearGrid rGrid = field.Grid as RectlinearGrid;
            PointSet<T> cpSet = new PointSet<T>(cpList);

            return cpSet;
        }

        /// <summary>
        /// Outputs all valid cells in the data set as points. Mostly for debugging purposes.
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static PointSet<Point> SomePoints3D(VectorField field, int numPoints)
        {
            // Only for 2D rectlinear grids.
            Debug.Assert(field.Grid as RectlinearGrid != null);
            Debug.Assert(field.NumVectorDimensions >= 3);
            Random rnd = new Random();

            Point[] cpList = new Point[numPoints];
            for (int index = 0; index < numPoints; ++index)
            {
                Vector pos = new Vector(0, field.NumVectorDimensions);
                pos[0] = (float)rnd.NextDouble() * (field.Size[0] - 1);
                pos[1] = (float)rnd.NextDouble() * (field.Size[1] - 1);
                pos[2] = (float)rnd.NextDouble() * (field.Size[2] - 1);
                float data = field.Scalars[0].Sample(pos);
                if (data == field.InvalidValue)
                {
                    index--;
                    continue;
                }
                Point cp = new Point()
                {
                    Position = (Vector3)pos,
                    Color = new SlimDX.Vector3((float)index / numPoints, 0.0f, 0.3f), // Debug color. 
                    Radius = 1.0f
                };

                cpList[index] = cp;
            }
            RectlinearGrid rGrid = field.Grid as RectlinearGrid;
            PointSet<Point> cpSet = new PointSet<Point>(cpList);

            return cpSet;
        }

        public static float AlphaStableFFF = 0;
        public static Vector StableFFF(Vector v, SquareMatrix J)
        {
            Debug.Assert(v.Length == 3 && J.Length == 3);

            Vec3 f = Vec3.Cross(J.Row(0).AsVec3(), J.Row(1).AsVec3());

            Vec3 fNorm = new Vec3(f);
            fNorm.Normalize();

            // Compute attracting vector field.
            Vec3 d = new Vec3();
            //SquareMatrix dMat = new SquareMatrix(new Vector[] { v.ToVec2(), J[0].ToVec2() });
            //d[0] = dMat.Determinant();
            //dMat[1] = J[1].ToVec2();
            //d[1] = dMat.Determinant();
            //dMat[1] = J[2].ToVec2();
            //d[2] = dMat.Determinant();
            // d = (u · ∇v − v · ∇u)
            d = (v[0] * J.Row(1) - v[1] * J.Row(0)).AsVec3();
            // Add up fields.
            Vec3 g = Vec3.Cross(fNorm, d);

            if (float.IsNaN(f[0]) || float.IsNaN(g[0]))
                Console.WriteLine("NaN NaN?!");
            return f + AlphaStableFFF * g;
        }

        public static Vector StableFFFNegative(Vector v, SquareMatrix J)
        {
            Debug.Assert(v.Length == 3 && J.Length == 3);
            Vec3 f = Vec3.Cross(J.Row(0).AsVec3(), J.Row(1).AsVec3());
            Vec3 fNorm = new Vec3(f);
            fNorm.Normalize();

            // Compute attracting vector field.
            // d = (u · ∇v − v · ∇u)
            Vec3 d = (v[0] * J.Row(1) - v[1] * J.Row(0)).AsVec3();

            // Add up fields.
            Vec3 g = Vec3.Cross(fNorm, d);
            return -f + AlphaStableFFF * g;
        }

        public static Vector PathlineCore(Vector v, SquareMatrix J)
        {
            Debug.Assert(v.Length == 3 && J.Length == 3);
            // FFF
            Vec3 f = Vec3.Cross(J.Row(0).AsVec3(), J.Row(1).AsVec3());

            // Find points where v || f
            Vec3 result =  Vec3.Cross(f*10, v.AsVec3()*10);
            if (float.IsInfinity(result[0]) || float.IsNaN(result[0]))
                Console.WriteLine("NaN NaN?!");

            return result;
        }

        public static Vector Acceleration(Vector v, SquareMatrix J)
        {
            // Theoretically, add v_t. Assume to be zero.
            return J * v; 
        }

        //private static Vector PredictCore(Vector vec, SquareMatrix J)
        //{
        //    AlphaStableFFF = 0;
        //    return StableFFF(vec, J);

        //}

        //private static float CorrectCore(Vector vec, SquareMatrix J, out Vector correction)
        //{
        //    float error = PathlineCore(vec, J).Length;
        //}

        //public static VectorField.IntegratorPredictorCorrector PathlineCoreIntegrator(VectorField field)
        //{
        //    return new VectorField.IntegratorPredictorCorrector(field, PredictCore, CorrectCore, true);
        //}

        public static Vector OkuboWeiss(Vector v, SquareMatrix timeJ)
        {
            SquareMatrix J = timeJ.ToMat2x2();
            SquareMatrix JT = new SquareMatrix(J);
            JT.Transpose();
            SquareMatrix S = (J + JT) * 0.5f;
            SquareMatrix W = (J - JT) * 0.5f;

            float Q = (S.EuclideanNormSquared() - W.EuclideanNormSquared()) * 0.5f;

            return (Vector)Q;
        }

        public static Vector VFLength(Vector v, SquareMatrix J)
        {
            return (Vector)v.LengthEuclidean();
        }

        public static Vector Divergence(Vector v, SquareMatrix J)
        {
            float sum = 0;
            for (int dim = 0; dim < v.Length; ++dim)
                sum += J[dim][dim];
            return (Vector)sum;
        }

        public static Vector DivX(Vector v, SquareMatrix J)
        {
            return (Vector)J[0][0];
        }

        public static Vector DivY(Vector v, SquareMatrix J)
        {
            return (Vector)J[1][1];
        }
        public static Vector Div2D(Vector v, SquareMatrix J)
        {
            return new Vec2(J[0][0], J[1][1]);
        }

        public static Vector Vorticity(Vector v, SquareMatrix J)
        {
            Debug.Assert(v.Length == 2); // If not, write other formula.
            return (Vector)(J.Vx - J.Uy);
        }

        public static Vector Shear(Vector v, SquareMatrix J)
        {
            Debug.Assert(v.Length == 2); // If not, write other formula.
            return (Vector)(J.Vx + J.Uy);
        }

        public static Renderable BuildGraph<P>(Plane basePlane, PointSet<P> positions, float[] values, float scaleUp, RedSea.DisplayLines lineSetting, Colormap colormap = Colormap.Parula) where P : Point
        {
            Debug.Assert(positions.Length == values.Length);
            Renderable result;
            switch (lineSetting)
            {
                case RedSea.DisplayLines.LINE:
                    Vector3[] pos = new Vector3[values.Length];
                    for (int p = 0; p < pos.Length; ++p)
                    {
                        pos[p] = positions.Points[p].Position + Vector3.UnitZ * values[p] * scaleUp;
                    }
                    LineSet lines = new LineSet(new Line[] { new Line() { Positions = pos } });
                    result = new LineBall(basePlane, lines, LineBall.RenderEffect.HEIGHT, colormap);
                    break;
                default:
                    Point[] point = new Point[positions.Length];
                    for (int p = 0; p < positions.Length; ++p)
                    {
                        point[p] = new Point() { Position = positions.Points[p].Position + Vector3.UnitZ * values[p] * scaleUp, Color = new Vector3(values[p], values[p], values[p]) };
                    }
                    PointSet<Point> pointSet = new PointSet<Point>(point);
                    result = new PointCloud(basePlane, pointSet);
                    break;
            }

            return result;
        }

        public static Renderable BuildGraph(Plane basePlane, Vector3[] positions, float[] values, float scaleUp, RedSea.DisplayLines lineSetting, Colormap colormap = Colormap.Parula)
        {
            Debug.Assert(positions.Length == values.Length);
            Renderable result;
            switch (lineSetting)
            {
                case RedSea.DisplayLines.LINE:
                    Vector3[] pos = new Vector3[values.Length];
                    for (int p = 0; p < pos.Length; ++p)
                    {
                        pos[p] = positions[p] + Vector3.UnitZ * values[p] * scaleUp;
                    }
                    LineSet lines = new LineSet(new Line[] { new Line() { Positions = pos } });
                    result = new LineBall(basePlane, lines, LineBall.RenderEffect.HEIGHT, colormap);
                    break;
                default:
                    Point[] point = new Point[positions.Length];
                    for (int p = 0; p < positions.Length; ++p)
                    {
                        point[p] = new Point() { Position = positions[p] + Vector3.UnitZ * values[p] * scaleUp, Color = new Vector3(values[p], values[p], values[p]) };
                    }
                    PointSet<Point> pointSet = new PointSet<Point>(point);
                    result = new PointCloud(basePlane, pointSet);
                    break;
            }

            return result;
        }

        public static List<Renderable> BuildGraph(Plane basePlane, LineSet positions, float[] values, float scaleUp, RedSea.DisplayLines lineSetting, Colormap colormap = Colormap.Parula)
        {
            Debug.Assert(positions.NumExistentPoints == values.Length);
            List<Renderable> result = new List<Renderable>(positions.Lines.Length);


            int count = 0;

            switch (lineSetting)
            {
                case RedSea.DisplayLines.LINE:
                    foreach(Line l in positions.Lines)
                    {
                        Vector3[] pos = new Vector3[l.Length];
                        for (int p = 0; p < l.Length; ++p)
                            pos[p] = l.Positions[p] + Vector3.UnitZ * values[count++] * scaleUp;

                        LineSet lines = new LineSet(new Line[] { new Line() { Positions = pos } }) {Color = positions.Color};
                        result.Add(new LineBall(basePlane, lines, LineBall.RenderEffect.HEIGHT, colormap));
                    }
                    
                    break;
                default:
                    Point[] point = new Point[positions.NumExistentPoints];
                    foreach (Line l in positions.Lines)
                    {
                        for (int p = 0; p < l.Length; ++p)
                            point[count++] = new Point() { Position = l.Positions[p] + Vector3.UnitZ * values[p] * scaleUp, Color = new Vector3(values[p], values[p], values[p]) };
                    }

                    PointSet<Point> pointSet = new PointSet<Point>(point);
                    result.Add(new PointCloud(basePlane, pointSet));
                    break;
            }

            return result;
        }

        public static LineSet BuildGraphLines(LineSet positions, float[] values, float scaleUp = 1.0f)
        {
            Debug.Assert(positions.NumExistentPoints == values.Length);
            List<Renderable> result = new List<Renderable>(positions.Lines.Length);


            int count = 0;
            List<Line> lines = new List<Line>(positions.Length);
            foreach (Line l in positions.Lines)
            {
                Vector3[] pos = new Vector3[l.Length];
                for (int p = 0; p < l.Length; ++p)
                    pos[p] = l.Positions[p] + Vector3.UnitZ * values[count++] * scaleUp;

                lines.Add(new Line() { Positions = pos });
            }


            return new LineSet(lines.ToArray());
        }

        public static Line FindBoundaryFromDistanceDonut(Line[] distances)
        {
            int[] tmp;
            return FindBoundaryFromDistanceDonut(distances, out tmp);
        }

        public static Line[] GetGraph(Line core, Vector2 center, LineSet lines, float stepSize, int everyNthTimeStep, bool distance = true)
        {
            Line[] starLines = new Line[lines.Lines.Length];

            int count = 0;
            float[] values = new float[lines.NumExistentPoints];

            for (int l = 0; l < lines.Lines.Length; ++l)
            {
                Line line = lines.Lines[l];
                // Write star coordinates here.
                Vector3[] starPos = new Vector3[line.Length];
                if (line.Length > 0)
                {
                    // Outgoing direction.
                    Vector3 start = line.Positions[0];
                    start.Z = 0;
                    Vector3 dir = line.Positions[0] - new Vector3(center/*new Vector2(core[0].X, core[0].Y)*/, line.Positions[0].Z); ; dir.Normalize();

                    // Scale such that step size does not scale the statistics.
                    dir *= 100.0f / core.Length;

                    for (int p = 0; p < line.Length; ++p)
                    {
                        if (distance)
                            values[count++] = core.DistanceToPointInZ(line.Positions[p]); // Core selected. Take distance to the core, in the respective time slice.
                        else
                        {
                            // Cos(angle)!!!
                            Vector3 nearestCenter;
                            core.DistanceToPointInZ(line[p], out nearestCenter);
                            Vector3 rad = (line[p] - nearestCenter);
                            rad.Normalize();
                            values[count++] = 1 + (float)Math.Cos(Math.PI * Vector3.Dot(Vector3.UnitX, rad));
                        }
                        starPos[p] = start + p * dir;
                    }
                }
                starLines[l] = new Line() { Positions = starPos };
                lines.Lines[l].Attribute = new float[line.Length];
                Array.Copy(values, count - line.Length, lines.Lines[l].Attribute, 0, line.Length);
            }
            return FieldAnalysis.BuildGraphLines(new LineSet(starLines), values).Lines;
        }

        public static Line FindBoundaryFromDistanceAngleDonut(Line[] distances, Line[] angles, out int[] indices)
        {
            Vector3[] boundary = new Vector3[distances.Length + 1];
            indices = new int[distances.Length + 1];

            int numBoundaryPoints = 0;
            // Vars.
            float eps = 0.5f;

            for (int l = 0; l < distances.Length; ++l)
            {
                int lastBoundExtremum = 0;
                float lastBoundHit = 1; // Exactly in the middle. This leaves the first bound extremum either positive or negative.
                int numBoundHits = 0;

                Line line = angles[l];
                for (int p = 1; p < line.Length - 1; ++p)
                {
                    // Are we at a maximum near the the bound?
                    float slopeLeft = line[p].Z - line[p - 1].Z;
                    float slopeRight = line[p + 1].Z - line[p].Z;

                    // Extremum?
                    if (slopeLeft * slopeRight < 0)
                    {
                        // Maximum (1 + 1) or minimum (-1 + 1)?
                        float bound = slopeLeft > 0 ? 2 : 0;

                        // Near the bound?
                        if (Math.Abs(line[p].Z - bound) < eps)
                        {
                            // Different bound than last time?
                            if ((lastBoundHit - 1) * (line[p].Z - 1) <= 0)
                            {
                                lastBoundHit = line[p].Z;
                                numBoundHits++;
                                lastBoundExtremum = p;
                            }
                        }
                    }
                }

                // Now, look at distance values and find highst slope.
                int avgStepsBetweenExtrema = (int)((float)lastBoundExtremum / numBoundHits + 0.5f);

                line = distances[l];
                int cut = lastBoundExtremum;
                float maxSlope = 0;
                for (int p = Math.Max(0, lastBoundExtremum - avgStepsBetweenExtrema); p < Math.Min(line.Length - 2, lastBoundExtremum + avgStepsBetweenExtrema); ++p)
                {
                    float slope = line[p + 2].Z - line[p + 1].Z;
                    if (slope > maxSlope)
                    {
                        cut = p;
                        maxSlope = slope;
                    }
                }
                
                    indices[l] = angles[l].Length > 0 ? cut : -1;
                if (angles[l].Length > 0)
                    boundary[numBoundaryPoints++] = angles[l][cut];
            }

            if (numBoundaryPoints + 1 < boundary.Length)
                Array.Resize(ref boundary, numBoundaryPoints + 1);

            // Close circle.
            boundary[boundary.Length - 1] = boundary[0];
            indices[indices.Length - 1] = indices[0];


            return new Line() { Positions = boundary };
        }
        public static Line FindBoundaryFromDistanceDonut(Line[] distances, out int[] indices)
        {
            Vector3[] boundary = new Vector3[distances.Length + 1];
            indices = new int[distances.Length + 1];

            // Vars.
            float eps = 0.5f;

            for(int l =0; l < distances.Length; ++l)
            {
                int lastBoundExtremum = 0;
                float lastBoundHit = 1; // Exactly in the middle. This leaves the first bound extremum either positive or negative.
                //int cut = 0;

                Line line = distances[l];
                for(int p = 1; p < line.Length - 1; ++p)
                {
                    // Are we at a maximum near the the bound?
                    float slopeLeft = line[p].Z - line[p - 1].Z;
                    float slopeRight = line[p + 1].Z - line[p].Z;

                    // Extremum?
                    if(slopeLeft * slopeRight < 0)
                    {
                        // Maximum (1 + 1) or minimum (-1 + 1)?
                        float bound = slopeLeft > 0 ? 2 : 0;

                        // Near the bound?
                        if(Math.Abs(line[p].Z - bound) < eps)
                        {
                            // Different bound than last time?
                            if ((lastBoundHit - 1) * (line[p].Z - 1) <= 0)
                            {
                                lastBoundHit = line[p].Z;
                                lastBoundExtremum = p;
                            }
                        }
                    }
                }
                indices[l] = lastBoundExtremum;

                boundary[l] = line[lastBoundExtremum];
            }

            // Close circle.
            boundary[boundary.Length - 1] = boundary[0];
            indices[indices.Length - 1] = indices[0];


            return new Line() { Positions = boundary };
        }


        public static Line FindBoundaryFromDistanceDonutAngle(Line[] distances, out int[] indices)
        {
            Vector3[] boundary = new Vector3[distances.Length + 1];
            indices = new int[distances.Length + 1];

            // Vars.
            float eps = 0.5f;

            for(int l =0; l < distances.Length; ++l)
            {
                int lastBoundExtremum = 0;
                float lastBoundHit = 1; // Exactly in the middle. This leaves the first bound extremum either positive or negative.
                //int cut = 0;

                Line line = distances[l];
                for(int p = 1; p < line.Length - 1; ++p)
                {
                    // Are we at a maximum near the the bound?
                    float slopeLeft = line[p].Z - line[p - 1].Z;
                    float slopeRight = line[p + 1].Z - line[p].Z;

                    // Extremum?
                    if(slopeLeft * slopeRight < 0)
                    {
                        // Maximum (1 + 1) or minimum (-1 + 1)?
                        float bound = slopeLeft > 0 ? 2 : 0;

                        // Near the bound?
                        if(Math.Abs(line[p].Z - bound) < eps)
                        {
                            // Different bound than last time?
                            if ((lastBoundHit - 1) * (line[p].Z - 1) <= 0)
                            {
                                lastBoundHit = line[p].Z;
                                lastBoundExtremum = p;
                            }
                        }
                    }
                }
                indices[l] = lastBoundExtremum;

                boundary[l] = line[lastBoundExtremum];
            }

            // Close circle.
            boundary[boundary.Length - 1] = boundary[0];
            indices[indices.Length - 1] = indices[0];


            return new Line() { Positions = boundary };
        }
        public static Line FindBoundaryFromDistanceDonutTwoLineFitSlopeDiff(Line[] distances, out int[] indices)
        {
            Vector3[] boundary = new Vector3[distances.Length + 1];
            indices = new int[distances.Length + 1];

            int start = 10;
            for (int l = 0; l < indices.Length - 1; ++l)
            {
                Line line = distances[l];

                int cut = 0;
                float slopeDiff = 0;
                for (int p = start; p < line.Length - start; ++p)
                {
                    StraightLine first = FieldAnalysis.FitLine(line, 0, p);
                    StraightLine second = FieldAnalysis.FitLine(line, p);
                    float diff = second.Slope - first.Slope;

                    if (diff > slopeDiff)
                    {
                        slopeDiff = diff;
                        cut = p;
                    }
                }
                indices[l] = cut;

                boundary[l] = line[cut];
            }

            // Close circle.
            boundary[boundary.Length - 1] = boundary[0];
            indices[indices.Length - 1] = indices[0];


            return new Line() { Positions = boundary };
        }
        public static Line FindBoundaryFromDistanceDonutCuttingLine(Line[] distances, out int[] indices)
        {
            Vector3[] boundary = new Vector3[distances.Length * 3 + 1];
            indices = new int[distances.Length + 1];

            int numMinima = 3;

            for (int l = 0; l < distances.Length; ++l)
            {
                Line line = distances[l];
                StraightLine straight = null;

                int minimaLeft = numMinima;
                int minimaUsed = 0;
                int lastMin = 0;

                for(int p = 1; p < line.Length - 1 && minimaLeft > 0; ++ p)
                {
                    float leftSlope = line[p].Z - line[p - 1].Z;
                    float rightSlope = line[p + 1].Z - line[p].Z;
                    if(leftSlope < 0 && rightSlope > 0)
                    {
                        minimaLeft--;
                        minimaUsed++;
                        lastMin = p;

                        if(minimaLeft == 0)
                        {
                            straight = FieldAnalysis.FitLine(line, 0, lastMin);

                            // Slope facing downwards. Add more minima.
                            if (straight.Slope < 0)
                                minimaLeft++;
                        }
                    }
                }

                float averageDiffMinima = lastMin / minimaUsed;

                if (straight == null)
                {
                    lastMin = line.Length - 1;
                    straight = FieldAnalysis.FitLine(line, 0, lastMin);
                }

                float avgDistSquared = 0;
                for(int p = 0; p < lastMin; ++p)
                {
                    float dist = line[p].Z - straight[p];
                    avgDistSquared += dist * dist;
                }
                avgDistSquared /= lastMin;

                float cutGraph = straight.CutLine2D(line, lastMin);
                float lineEnd = lastMin;
                while(cutGraph > 0)
                {
                    // Compute average error. If it is not too big compared to the beginning, take the new end.
                    float avgDistSquaredCut = 0;
                    for (int p = lastMin; p < cutGraph; ++p)
                    {
                        float dist = line[p].Z - straight[p];
                        avgDistSquaredCut += dist * dist;
                    }
                    avgDistSquaredCut /= ((int)(cutGraph + 0.5f) - lastMin);

                    // We allow 4 times the error that was measured in the beginning line.
                    if (cutGraph - lastMin > averageDiffMinima * 3 || avgDistSquaredCut > avgDistSquared * 4)
                        break;
                    //if (cutGraph - lastMin > averageDiffMinima * 3)
                    //    break;

                    // We cut the graph again in reasonable time. Take this intersection as next possible border.
                    lastMin = (int)Math.Ceiling(cutGraph);
                    lineEnd = cutGraph;
                    straight = FieldAnalysis.FitLine(line, 0, lastMin);
                    cutGraph = straight.CutLine2D(line, lastMin);
                }

                indices [l] = lastMin;

                boundary[l * 3] = line[0];
                boundary[l * 3].Z = straight.YOffset;

                boundary[l*3 + 1] = line.Value(lineEnd);
                boundary[l*3 + 1].Z = straight[lineEnd];

                boundary[l * 3 + 2] = boundary[l * 3];
            }
            // Close circle.
            boundary[boundary.Length - 1] = boundary[0];
            indices[indices.Length - 1] = indices[0];


            return new Line() { Positions = boundary };
        }
        public static Line FindBoundaryFromDistanceDonutQuarters(Line[] distances, out int[] indices)
        {
            Vector3[] boundary = new Vector3[distances.Length + 1];
            indices = new int[boundary.Length];

            int startLength = 5;

            for (int l = 0; l < distances.Length; ++l)
            {
                Line line = distances[l];

                int cut = 0;
                float maxHeightTillCut = -1;
                int p = 0;

                // We need a few steps to get a maximum that makes sense.
                for(; p < startLength; ++p )
                {
                    if(line[p].Z > maxHeightTillCut)
                    {
                        cut = p;
                        maxHeightTillCut = line[p].Z;
                    }
                }

                LookForNewCut: // Find the next maximum
                p = cut;
                for (; p < line.Length - 1; ++p)
                {
                    if(line[p].Z > maxHeightTillCut)
                    {
                        // We found a new cut position. Save values.
                        cut = p;
                        maxHeightTillCut = line[p].Z;
                        // Break cut search and try validating.
                        break;
                    }
                }

                // Validate this maximum: run over cut/2 and see if the cut value is minimal.
                int max = Math.Min(line.Length - 1, (int)(1.5f * Math.Max(cut, startLength) + 0.5f));
                for(; p < max; ++p)
                {
                    if (line[p].Z < maxHeightTillCut)
                        goto LookForNewCut;
                }
                // We made it out of the loop. We either found an appropriate cut, or hit the end.
                indices[l] = cut;
                boundary[l] = line[indices[l]];
            }
            // Close circle.
            boundary[distances.Length] = boundary[0];


            return new Line() { Positions = boundary };
        }

        //public static Line FindBoundaryFromDistanceDonut(Line[] distances, out int[] indices)
        //{
        //    Vector3[] boundary = new Vector3[distances.Length + 1];
        //    indices = new int[boundary.Length];
        //    int countPoints = 0;

        //    int hack = 19;

        //    for (int l = 0; l < distances.Length; ++l)
        //    {
        //        Line line = distances[l];
        //        indices[l] = Math.Min(line.Length - 1, hack);
        //        boundary[l] = line[indices[l]];
        //    }
        //    boundary[distances.Length] = boundary[0];
        //    return new Line() { Positions = boundary };
        //}
        public class StraightLine
        {
            public float YOffset;
            public float Slope;
            public float this[float pos] { get { return YOffset + Slope * pos; } }
            /// <summary>
            /// Returns the first index after cutting the line.
            /// </summary>
            /// <param name="line"></param>
            /// <param name="startPos"></param>
            /// <returns></returns>
            public float CutLine2D(Line line, int startPos = 0)
            {
                float diff = 0;
                for(int point = startPos; point < line.Length; ++point)
                {
                    float locDiff = line[point].Z - YOffset - (Slope * point);
                    if (diff * locDiff < 0)
                    {
                        float slope = line[point].Z - line[point - 1].Z;
                        return point - 1 + (this[point-1] - line[point - 1].Z) / (slope - Slope);
                    }
                    
                    diff = locDiff;
                }
                return -1;
            }
        }
        /// <summary>
        /// Simple linear Regression.
        /// </summary>
        public static StraightLine FitLine(Line line, int offset = 0, int length = -1)
        {
            float sumX = 0;
            float sumY = 0;
            float sumXY = 0;
            float sumXX = 0;

            int end = (length >= 0) ? (offset + length) : line.Length;
            int N = end - offset;
            Debug.Assert(end <= line.Length);

            for(int point = offset; point < end; ++point)
            {
                sumY += line[point].Z;
                sumXY += line[point].Z * point;

                // We could analytically compute these. Not doing that for now because of lazyness.
                sumX += point;
                sumXX += point * point;
            }

            StraightLine straight = new StraightLine();

            // Slope(b) = (NΣXY - (ΣX)(ΣY)) / (NΣXX - (ΣX*ΣX))
            straight.Slope = (N * sumXY - sumX * sumY) / (N * sumXX - sumX * sumX);

            // Intercept(a) = (ΣY - b(ΣX)) / N
            straight.YOffset = (sumY - straight.Slope * sumX) / N;

            return straight;
        }

        public static LineSet PlotLines2D(LineSet lines, float? xScale = null, float? yScale = null)
        {
            float xMult = xScale ?? lines.Thickness;
            float yMult = yScale ?? ((float)lines.Length / lines[0].Length) * lines.Thickness;
            LineSet set = new LineSet(lines);
            for(int x = 0; x < set.Length; ++x)
            {
                for(int y = 0; y < set[x].Length; ++y)
                {
                    Vector3 pos = new Vector3();
                    pos.X = x * xMult;
                    pos.Y = y * yMult;
                    pos.Z = set[x][y].Z;
                    set[x][y] = pos;
                }           
            }
            return set;
        }

        public static LineSet CutLength(LineSet lines, int length)
        {
            Line[] shorties = new Line[lines.Length];

            for(int s = 0; s < shorties.Length; ++s)
            {
                Vector3[] points = new Vector3[Math.Min(lines[s].Length, length)];
                Array.Copy(lines[s].Positions, points, points.Length);
                shorties[s] = new Line() { Positions = points };
            }
            return new LineSet(shorties);
        }

        //public static float FindFirstOccurenceZ(Line line, float value)
        //{
        //    for(int p = 0; p < line.Length-1; ++p)
        //    {
        //        if((line[p].Z - value) * 
        //    }
        //}
    }
}
