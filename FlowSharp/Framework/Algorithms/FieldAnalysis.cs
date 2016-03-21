using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SlimDX;
using Integrator = FlowSharp.VectorField.Integrator;

namespace FlowSharp
{
    /// <summary>
    /// Class wrapping a number of methods for vector field processing.
    /// </summary>
    static class FieldAnalysis
    {
        #region DomainPoints
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
        public static PointSet<T> ValidDataPoints<T>(VectorField field) where T : Point, new()
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
                pos[0] = (float)rnd.NextDouble() * (field.Size[0] - 1);
                pos[1] = (float)rnd.NextDouble() * (field.Size[1] - 1);
                float data = field.Scalars[0].Sample(pos);
                if (data == field.InvalidValue)
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
        #endregion DomainPoints

        #region FieldFunctions

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

            //if (float.IsNaN(f[0]) || float.IsNaN(g[0]))
            //    Console.WriteLine("NaN NaN?!");
            Vector res = f + AlphaStableFFF * g;
            //    res /= res.T; // Normalize time length.
            return res;
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
            Vec3 result = Vec3.Cross(f * 10, v.AsVec3() * 10);
            //if (float.IsInfinity(result[0]) || float.IsNaN(result[0]))
            //    Console.WriteLine("NaN NaN?!");

            return result;
        }

        public static Vector PathlineCoreLength(Vector v, SquareMatrix J)
        {
            Debug.Assert(v.Length == 3 && J.Length == 3);
            // FFF
            Vec3 f = Vec3.Cross(J.Row(0).AsVec3(), J.Row(1).AsVec3());

            // Find points where v || f
            Vec3 result = Vec3.Cross(f * 10, v.AsVec3() * 10);
            if (float.IsInfinity(result[0]) || float.IsNaN(result[0]))
                Console.WriteLine("NaN NaN?!");

            return (Vector)result.LengthEuclidean();
        }


        public static Vector Acceleration(Vector v, SquareMatrix J)
        {
            // Theoretically, add v_t. Assume to be zero.
            Vector vec = J * v;
            //vec /= vec.T;
            return vec.ToVec2();
        }

        public static Vector AccelerationLength(Vector v, SquareMatrix J)
        {
            // Theoretically, add v_t. Assume to be zero.
            return (Vector)(J * v).LengthEuclidean();
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

        public static Vector NegativeGradient(Vector v, SquareMatrix J)
        {
            // Debug.Assert(v.Length == 1); // If not, write other formula.
            return new Vec3((-J.Row(0)).ToVec2(), 0);
        }

        public static VectorField.IntegratorPredictorCorrector PathlineCoreIntegrator(VectorFieldUnsteady field, float stepsize)
        {
            VectorFieldUnsteady pathlineLength = new VectorFieldUnsteady(field, AccelerationLength, 1);
            VectorField correctorF = new VectorField(pathlineLength, NegativeGradient, 3);

            VectorFieldUnsteady acceleration = new VectorFieldUnsteady(field, Acceleration, 2);
            VectorField predictorF = new VectorField(acceleration, StableFFF, 3);

            Integrator predictor = new VectorField.IntegratorRK4(predictorF)
            {
                StepSize = stepsize,
                Direction = Sign.POSITIVE,
                EpsCriticalPoint = 0.00000000001f,
                NormalizeField = true
            };
            Integrator corrector = new VectorField.IntegratorEuler(correctorF)
            {
                StepSize = stepsize / 10.0f,
                NormalizeField = true,
                MaxNumSteps = 10000,
                Direction = Sign.POSITIVE,
                EpsCriticalPoint = 0.00000000001f
            };

            return new VectorField.IntegratorPredictorCorrector(predictor, corrector);
        }

        public static VectorField.IntegratorPredictorCorrector StreamlineCoreIntegrator(VectorFieldUnsteady field, float stepsize)
        {
            VectorFieldUnsteady pathlineLength = new VectorFieldUnsteady(field, (v, J) => (Vector)v.LengthEuclidean(), 1);
            VectorField correctorF = new VectorField(pathlineLength, NegativeGradient, 3);

            VectorFieldUnsteady acceleration = new VectorFieldUnsteady(field, (v, J) => v.ToVec2(), 2);
            VectorField predictorF = new VectorField(acceleration, StableFFF, 3);

            Integrator predictor = new VectorField.IntegratorRK4(predictorF)
            {
                StepSize = stepsize,
                Direction = Sign.POSITIVE,
                EpsCriticalPoint = 0.00000000001f,
                NormalizeField = true
            };
            Integrator corrector = new VectorField.IntegratorEuler(correctorF)
            {
                StepSize = stepsize / 10.0f,
                NormalizeField = true,
                MaxNumSteps = 10000,
                Direction = Sign.POSITIVE,
                EpsCriticalPoint = 0.00000000001f
            };

            return new VectorField.IntegratorPredictorCorrector(predictor, corrector);
        }
        #endregion FieldFunctions

        #region Graphs
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
                    foreach (Line l in positions.Lines)
                    {
                        Vector3[] pos = new Vector3[l.Length];
                        for (int p = 0; p < l.Length; ++p)
                            pos[p] = l.Positions[p] + Vector3.UnitZ * values[count++] * scaleUp;

                        LineSet lines = new LineSet(new Line[] { new Line() { Positions = pos } }) { Color = positions.Color };
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

        public static LineSet WriteGraphToSun(Graph2D[] values, Vector3 center)
        {
            float angleDiff = (float)(Math.PI * 2 / values.Length);
            Line[] lines = new Line[values.Length];
            for (int l = 0; l < values.Length; ++l)
            {
                Vector3 dir = new Vector3((float)Math.Sin(l * angleDiff + PiH), (float)Math.Cos(l * angleDiff + PiH), 0);
                lines[l] = values[l].SetLineHeightStraight(center, dir, Vector3.UnitZ);
            }
            return new LineSet(lines);
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
                    dir *= 100.0f / core.Positions.Last().Z * stepSize;

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
                            values[count++] = 1 + /*(float)Math.Cos(2 * Math.Acos(*/Vector3.Dot(Vector3.UnitX, rad);
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

        #region ConcentricDistance
        private static float Pi2 = (float)Math.PI * 2;
        private static float PiH = (float)(Math.PI * 0.5);
        private static float Pi3H = (float)(Math.PI * 1.5);
        private delegate float AngleFunc(Vector3 vec);

        public static Graph2D[] GetDistanceToAngle(Line core, Vector2 center, LineSet lines)
        {
            int count = 0;
            float[][] angles = new float[lines.Length][];
            float[][] distances = new float[lines.Length][];
            Graph2D[] result = new Graph2D[lines.Length];
            AngleFunc angleFunc = Angle2D;

            for (int l = 0; l < lines.Lines.Length; ++l)
            {
                Line line = lines.Lines[l];
                // Write star coordinates here.
                Vector3[] starPos = new Vector3[line.Length];
                if (line.Length > 0)
                {
                    // TODO: Count turns!
                    float turnAdd = 0;
                    int numCrossings = 0;

                    angles[l] = new float[line.Length];
                    distances[l] = new float[line.Length];
                    for (int p = 0; p < line.Length; ++p)
                    {
                        distances[l][p] = core.DistanceToPointInZ(line.Positions[p]); // Core selected. Take distance to the core, in the respective time slice.

                        // Cos(angle)!!!
                        Vector3 nearestCenter;
                        core.DistanceToPointInZ(line[p], out nearestCenter);
                        Vector3 rad = (line[p] - nearestCenter);
                        //rad.Z = 0;
                        //rad.Normalize();
                        float angle = angleFunc(rad);
                        if (p > 0)
                        {
                            // Crossing the 360 degree border?
                            if (angle < PiH && angles[l][p - 1] - turnAdd > Pi3H)
                            {
                                turnAdd += Pi2;
                            }
                            // Crossing the 360 degree border backwards?
                            else if (angle > Pi3H && angles[l][p - 1] - turnAdd < PiH)
                            {
                                turnAdd -= Pi2;
                            }
                        }
                        angles[l][p] = angle + turnAdd;
                    }

                    result[l] = new Graph2D(angles[l], distances[l]);
                }
                else
                {
                    result[l] = new Graph2D(new float[0], new float[0]) { Offset = 2 * result[l - 1].Offset - result[l - 2].Offset };
                }

            }

            return result;
        }

        public static Graph2D[][] GetErrorsToTime(LineSet lines, int angles, float[] radii)
        {
            Debug.Assert(lines.Length == angles * radii.Length);
            int count = 0;
            Graph2D[][] result = new Graph2D[angles][];

            for (int a = 0; a < angles; ++a)
            {
                result[a] = new Graph2D[radii.Length - 1];
                for (int r = 0; r < radii.Length-1; ++r)
                {
                    int idx = a * radii.Length + r;
                    result[a][r] = DiffZ(lines[idx], lines[idx + 1]);
                    result[a][r].Offset = radii[r];
                }
            }

            return result;
        }

        public delegate float LineFunc(Vector3 a, Vector3 b);
        public static Graph2D OperateZ(Line l0, Line l1, LineFunc func)
        {
            if (l0.Length == 0 || l1.Length == 0)
            {
                return new Graph2D(new float[0], new float[0]) { Offset = 0 };
            }
            float[] x = new float[l0.Length + l1.Length];
            float[] fx = new float[x.Length];

            int p0 = 0; int p1 = 0; int pCount = 0;
            if (l0[0].Z < l1[0].Z)
            {
                p0 = l0.GetLastBelowZ(l0[0].Z) + 1;
            }
            if (l1[0].Z < l0[0].Z)
            {
                p1 = l1.GetLastBelowZ(l0[0].Z) + 1;
            }

            float maxX = Math.Min(l0[l0.Length - 1].Z, l1[l1.Length - 1].Z);
            // Interleave
            while (p0 < l0.Length && p1 < l1.Length)
            {
                float v0 = p0 < l0.Length ? l0[p0].Z : float.MaxValue;
                float v1 = p1 < l1.Length ? l1[p1].Z : float.MaxValue;

                if (v0 < v1)
                {
                    x[pCount] = v0;
                    fx[pCount] = func((Vector3)l1.SampleZ(v0), l0[p0]);
                    p0++;
                }
                if (v0 > v1)
                {
                    x[pCount] = v1;
                    fx[pCount] = func(l1[p1], (Vector3)l0.SampleZ(v1));
                    p1++;
                }
                if (v0 == v1)
                {
                    x[pCount] = v0;
                    fx[pCount] = func(l1[p1], l0[p0]);
                    p0++; p1++;
                }

                ++pCount;
            }
            if (pCount < x.Length)
            {
                Array.Resize(ref x, pCount);
                Array.Resize(ref fx, pCount);
            }
            return new Graph2D(x, fx);
        }

        public static Graph2D DiffZ(Line l0, Line l1)
        {
            return OperateZ(l0, l1, (a, b) => { return (a - b).Length(); });
        }

        public static float Angle2D(Vector3 vec)
        {
            float angle = (float)Math.Atan2(vec.Y, vec.X);
            if (angle < 0)
                angle += Pi2;
            return angle;
        }

        public static Vector3 SphericalPosition(Vector3 center, float part, float rad)
        {
            float x = (float)(Math.Sin(part * Pi2 + PiH));
            float y = (float)(Math.Cos(part * Pi2 + PiH));
            return center + new Vector3(x * rad, y * rad, 0);
        }

        public static Line WriteGraphToCircle(Graph2D graph, Vector3 center, float radius)
        {
            Graph2D circle = new Graph2D(graph);
            circle.CutGraph(graph.Offset + Pi2);
            Line obj = new Line() { Positions = new Vector3[circle.Length] };
            for (int c = 0; c < circle.Length; ++c)
            {
                obj[c] = new Vector3((float)Math.Cos(circle.X[c]) * radius, (float)Math.Sin(circle.X[c]) * radius, circle.Fx[c]) + center;
            }

            return obj;
        }

        public static LineSet WriteGraphsToCircles(Graph2D[] graphs, Vector3 center)
        {
            Line[] lines = new Line[graphs.Length];
            for (int g = 0; g < graphs.Length; ++g)
                lines[g] = WriteGraphToCircle(graphs[g], center, graphs[g].Offset);

            return new LineSet(lines);
        }

        public static Graph2D[] GraphDifferenceForward(Graph2D[] vals)
        {
            Graph2D[] diff = new Graph2D[vals.Length - 1];
            for (int v = 0; v < vals.Length - 1; ++v)
            {
                // Make sure that the radius is still correct.
                diff[v] = Graph2D.Distance(vals[v + 1], vals[v]);
                //if (diff[v].Length > 0 && float.IsNaN(diff[v].Fx[0]))
                //    Console.WriteLine("NaN NaN NaN NaN NaN Batman!");
                diff[v].Offset = vals[v].Offset;
            }

            return diff;
        }

        public static LineSet FindBoundaryInErrors3(Graph2D[] errors, Vector3 center, /*float time,*/ float rangeForTracking = 4f)
        {
            float thresh = (errors[0].X[1] - errors[0].X[0]);
            int maxDistR = (int)Math.Ceiling(rangeForTracking / thresh);
            thresh *= 430;
            Console.WriteLine("Threshold for boundary: {0}", thresh);

            int numAngles = errors.Length;
            //int stuckPoints = 0;
            //bool[] stuck = new bool[errors.Length]; // Initial false.
            int[] circleR = new int[numAngles];

            bool somethingChanged = true;
            int sign = 1;
            // There is still points moving.
            while (somethingChanged)
            {
                somethingChanged = false;
                for (int a = 0; a < numAngles && a > -numAngles; a += sign)
                {
                    int angle = (a + numAngles) % numAngles;
                    int current = circleR[angle];

                    if (current < errors[angle].Length - 1 && errors[angle].Fx[current + 1] < thresh)
                    {
                        // We could o forward. What about our neighbors?
                        current++;
                        if (current - circleR[(angle - 1 + numAngles) % numAngles] < maxDistR &&
                           current - circleR[(angle + 1) % numAngles] < maxDistR)
                        {
                            circleR[angle]++;
                            somethingChanged = true;
                        }
                    }
                }
                sign = -sign;
            }

            Line bound = new Line(numAngles + 1);
            for(int angle = 0; angle < numAngles; ++angle)
            {
                bound[angle] = SphericalPosition(center, (float)angle / numAngles, errors[angle].X[circleR[angle]]);
            }
            bound[bound.Length - 1] = bound[0];

            return new LineSet(new Line[] { bound });
        }
        public static LineSet FindBoundaryInErrors2(Graph2D[] errors, Vector3 center, /*float time,*/ float rangeForTracking = 1f)
        {
            float threshold = (errors[0].X[1] - errors[0].X[0]) * 3; // The average point on the pathlines is 3 times further away than the seeds.
   //         Line thresholded = new Line() { Positions = new Vector3[errors.Length + 1] };

            // Find all switch points.
            List<int>[] edges = new List<int>[errors.Length];
            bool[][] edgeUsed = new bool[edges.Length][];
            int unusedEdges = 0;
            for (int err = 0; err < errors.Length; ++err)
            {
                edges[err] = errors[err].ThresholdFronts(threshold);
                edgeUsed[err] = new bool[edges[err].Count];
                unusedEdges += edges[err].Count;
            }
            List<List<Int2>> bows = new List<List<Int2>>(32);

            // Connect fronts.
            int a = 0; int e = 0;
            while (unusedEdges > 0)
            {
                // Find first unused front.
                for(; a < edges.Length; ++a)
                {
                    for(e = 0; e < edges[a].Count; ++e)
                    {
                        if(!edgeUsed[a][e])
                        {
                            edgeUsed[a][e] = true;
                            unusedEdges--;
                            goto Connect;
                        }
                    }
                }

                Connect:
                var currentBow = new List<Int2>(errors.Length / 2);
                
                // Left and right turning.
                for(int sign = -1; sign <= 1; sign += 2)
                {
                    // Okay.
                    // a is angle (index between 0 and numSeeds),
                    // e is index within edges (bewtween 0 and num edges on line)
                    // x is index on error graph, radius
                    int lastA = a; int lastE = e;
                    while(true) // We will we will break you!
                    {
                        int dist = int.MaxValue;
                        int nextX = -1;
                        int lastX = edges[lastA][lastE];
                        int nextA = (lastA + sign + errors.Length) % errors.Length;
                        
                        int nextE = 0;
                        // Connect next edge.
                        for (; nextE < edges[nextA].Count; ++nextE)
                        {
                            int thisDist = Math.Abs(edges[nextA][nextE] - edges[lastA][lastE]);
                            if (thisDist < dist)
                            {
                                dist = thisDist;
                                nextX = nextE;
                            }
                        }
                        
                        nextE = nextX;
                        nextX = edges[nextA][nextE];
                        // Close enought to consider?
                        // Compute diagonal of trapeze.
                        //float r0 = errors[a].X[edges[a][e]];
                        //float r1 = errors[nextA].X[nearestE];
                        //float dr = r1 - r0;
                        //float s = (r1 - r0) * 0.5f;
                        //float diag = (float)Math.Sqrt((r0+ s)*(r0+ s) + dr*dr - s * s);
// THis is the real distance
//                        float diag = (SphericalPosition(center, (float)nextA / errors.Length, errors[nextA].X[nextX]) - SphericalPosition(center, (float)lastA / errors.Length, errors[lastA].X[lastX])).Length();
                        float diag = errors[nextA].X[nextX] - errors[lastA].X[lastX];
                        // Connect to line?
                        if (diag > rangeForTracking)
                        {
                            break;
                        }

                        currentBow.Add(new Int2(nextA, nextX));
                        if (edgeUsed[nextA][nextE] == false)
                        {
                            edgeUsed[nextA][nextE] = true;
                            unusedEdges--;
                        }


                        lastA = nextA;
                        lastE = nextE;
                    }

                    // Reverse, so elements will be ordered increasing by a.
                    if(sign == -1)
                    {
                        currentBow.Reverse();
                    }
                }

                if(currentBow.Count > errors.Length / 50)
                    bows.Add(currentBow);

                Console.WriteLine("Edges not connected yet: {0}", unusedEdges);
            }

            // Now, we have the bows. Connect them in a useful manner.

            // T  O  D  O

            // Make circular.
            //    thresholded[thresholded.Length - 1] = thresholded[0];

            //    return new LineSet(new Line[] { thresholded });
            Line[] lines = new Line[bows.Count];
            for(int b = 0; b < lines.Length; ++b)
            {
                lines[b] = new Line() { Positions = new Vector3[bows[b].Count] };
                for(int p = 0; p < lines[b].Length; ++p)
                {
                    Int2 spherePos = bows[b][p];
                    lines[b][p] = SphericalPosition(center, (float)spherePos.X / errors.Length, errors[spherePos.X].X[spherePos.Y]);
                }
            }
            return new LineSet(lines);
        }

        public static LineSet FindBoundaryInErrors(Graph2D[] errors, Vector3 center, /*float time,*/ float rangeForTracking = 0.5f)
        {
            float threshold = 100f/errors[0].Length; // 
            Line thresholded = new Line() { Positions = new Vector3[errors.Length + 1] };
            int tLast = 0;
            for(int e = 0; e < errors.Length; ++e)
            {
                int t = errors[e].Threshold(threshold);

                if (e > 0 && Math.Abs(t - tLast) > 10)
                {
                    t = errors[e].ThresholdRange(tLast - 20, tLast + 20, threshold*0.75f);
                }

                float angle = (float)e * Pi2 / errors.Length;
                thresholded[e] = SphericalPosition(center, (float)e / errors.Length, errors[e].X[t]);
                tLast = t;
            }
            thresholded[thresholded.Length - 1] = thresholded[0];

            return new LineSet(new Line[] { thresholded });
    /*        float debugOffset = 0.01f;


            // Save lines.
            List<Line> bounds = new List<Line>(16);

            {
                // Take intial direction. Find all maxima.
                List<int> maxima = errors[0].Maxima();
                float time = 0;

                // Initalize lines for all extrema.
                foreach (int max in maxima)
                {
                    Line l = new Line() { Positions = new Vector3[errors.Length + 1], Attribute = new float[errors.Length + 1] };
                    l[0] = center + new Vector3(errors[0].X[max], 0, time);
                    l.Attribute[0] = errors[0].Curvature(max);
                    //TODO: DEBUG
                    time += debugOffset;

                    bounds.Add(l);
                }
            }
            // Follow maxima.
            for (int e = 1; e < errors.Length; ++e)
            {
                float angle = ((float)e * Pi2 / errors.Length);
                float x = (float)(Math.Sin(angle + Math.PI / 2));
                float y = (float)(Math.Cos(angle + Math.PI / 2));

                int numLines = bounds.Count;
                for(int l = 0; l < numLines; ++ l)
                {
                    Line line = bounds[l];
                    // Line ended already?
                    if (line.Length < errors.Length + 1)
                        continue;
                    float lastRad = (float)Math.Sqrt(line[e - 1].X * line[e - 1].X + line[e - 1].Y * line[e - 1].Y);
                    List<int> maxima = errors[e].MaximaSides(lastRad, rangeForTracking);
                    int maxCount = maxima.Count;

                    // Cannot connect?
                    if(maxCount < 1)
                    {
                        line.Resize(e);
                        continue;
                    }

                    if(maxCount > 2)
                    {
                        Console.WriteLine("So many maxima!");
                    }

                    line[e] = center + new Vector3(x * errors[e].X[maxima[0]], y * errors[e].X[maxima[0]], line[e - 1].Z);
                    line.Attribute[e] = -errors[e].Curvature(maxima[0]);

                    //// If multiple possible maxima appear, add new line. Copy points so far.
                    //for (int m = 1; m < maxCount; ++m)
                    //{
                    //    Line cpy = new Line(line);
                    //    cpy[e] = new Vector3(x * errors[e].X[maxima[m]], y * errors[e].X[maxima[m]], line[e - 1].Z + debugOffset);
                    //    bounds.Add(cpy);
                    //}
                }
            }

            foreach(Line l in bounds)
            {
                Console.WriteLine("Length line: {0}", l.Length);
                if (l.Length == errors.Length + 1)
                {
                    l[l.Length - 1] = l[0];
                    l.Attribute[l.Length - 1] = l.Attribute[0];
                }
            }

            return new LineSet(bounds.ToArray());
            */

        }

        public static int/*[]*/ FindBoundaryInError(Graph2D error)
        {
            /*            float threshold = 0.2f;
                        //List<int> spikes = new List<int>(8);
                        int bestMax = 0; float bestAspect = 0;
                        /// Last index where left was turning to right (left bound of hill).
                        int lastLeft = -1;
                        /// Last index where right was turning to left (right bound of hill).
                        int lastRight = -1;
                        /// Last maximum.
                        int lastMax = -1;
                        bool currentlyRight = true; // Makes more sense than false here.
                        for (int e = 1; e < error.Length-1; ++e)
                        {
                            if (error.Fx[e] > threshold)
                                break;
                            float leftSlope = error.Fx[e] - error.Fx[e - 1];
                            float rightSlope = error.Fx[e + 1] - error.X[e];
                            leftSlope /= error.X[e] - error.X[e - 1];
                            rightSlope /= error.X[e + 1] - error.X[e];

                            // Maximum?
                            if(rightSlope < 0 && leftSlope > 0)
                            {
                                lastMax = e;
                            }

                            // Switched to right curving?
                            if (rightSlope < leftSlope && !currentlyRight)
                            {
                                currentlyRight = true;
                                lastLeft = e;
                            }
                            // Switched to left curving?
                            if (rightSlope > leftSlope && currentlyRight)
                            {
                                currentlyRight = false;
                                // lastRight = e;
                                if(lastLeft >= 0 && lastMax - lastLeft > 0) // Includes lastMax >= 0 :D
                                {
                                    float aspect = error.Fx[lastMax] - Math.Min(error.Fx[lastLeft], error.Fx[e]);
                                    aspect /= error.X[e] - error.X[lastLeft];
                                    if(aspect > bestAspect)//1)
                                    {
                                        bestAspect = aspect;
                                        bestMax = lastMax;
                                        //spikes.Add(lastMax);
                                    }
                                }
                            }

                        }

                        return bestMax; // spikes.ToArray();
                        */
            float tolerance = 0.1f;
            for (int e = 0; e < error.Length; ++e)
                if (error.Fx[e] > tolerance)
                    return e;
            return -1;
        }
        #endregion ConcentricDistance

        public static void WriteFxToLinesetAttribute(LineSet lines, Graph2D[] graphs)
        {
            for (int l = 0; l < lines.Length; ++l)
            {
                lines[l].Attribute = graphs[l].Fx;
            }
        }

        public static void WriteXToLinesetAttribute(LineSet lines, Graph2D[] graphs)
        {
            Console.WriteLine("Num Lines: {0}", lines.Length);
            for (int l = 0; l < lines.Length; ++l)
            {
                lines[l].Attribute = graphs[l].X;
            }
        }
        #endregion Graphs

        #region Boundaries
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
                for (int p = Math.Max(0, lastBoundExtremum - avgStepsBetweenExtrema / 2); p < Math.Min(line.Length - 2, lastBoundExtremum + avgStepsBetweenExtrema); ++p)
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

            for (int l = 0; l < distances.Length; ++l)
            {
                int lastBoundExtremum = 0;
                float lastBoundHit = 1; // Exactly in the middle. This leaves the first bound extremum either positive or negative.
                //int cut = 0;

                Line line = distances[l];
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

            for (int l = 0; l < distances.Length; ++l)
            {
                int lastBoundExtremum = 0;
                float lastBoundHit = 1; // Exactly in the middle. This leaves the first bound extremum either positive or negative.
                //int cut = 0;

                Line line = distances[l];
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

                for (int p = 1; p < line.Length - 1 && minimaLeft > 0; ++p)
                {
                    float leftSlope = line[p].Z - line[p - 1].Z;
                    float rightSlope = line[p + 1].Z - line[p].Z;
                    if (leftSlope < 0 && rightSlope > 0)
                    {
                        minimaLeft--;
                        minimaUsed++;
                        lastMin = p;

                        if (minimaLeft == 0)
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
                for (int p = 0; p < lastMin; ++p)
                {
                    float dist = line[p].Z - straight[p];
                    avgDistSquared += dist * dist;
                }
                avgDistSquared /= lastMin;

                float cutGraph = straight.CutLine2D(line, lastMin);
                float lineEnd = lastMin;
                while (cutGraph > 0)
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

                indices[l] = lastMin;

                boundary[l * 3] = line[0];
                boundary[l * 3].Z = straight.YOffset;

                boundary[l * 3 + 1] = line.Value(lineEnd);
                boundary[l * 3 + 1].Z = straight[lineEnd];

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
                for (; p < startLength; ++p)
                {
                    if (line[p].Z > maxHeightTillCut)
                    {
                        cut = p;
                        maxHeightTillCut = line[p].Z;
                    }
                }

            LookForNewCut: // Find the next maximum
                p = cut;
                for (; p < line.Length - 1; ++p)
                {
                    if (line[p].Z > maxHeightTillCut)
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
                for (; p < max; ++p)
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
        #endregion Boundaries

        #region Regression
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
                for (int point = startPos; point < line.Length; ++point)
                {
                    float locDiff = line[point].Z - YOffset - (Slope * point);
                    if (diff * locDiff < 0)
                    {
                        float slope = line[point].Z - line[point - 1].Z;
                        return point - 1 + (this[point - 1] - line[point - 1].Z) / (slope - Slope);
                    }

                    diff = locDiff;
                }
                return -1;
            }

            public float SquaredError(float x, float fx)
            {
                float diff = this[x] - fx;
                return diff * diff;
            }
        }

        /// <summary>
        /// Simple linear Regression.
        /// </summary>
        public static StraightLine FitLine(Graph2D graph, int length = -1)
        {
            float sumX = 0;
            float sumY = 0;
            float sumXY = 0;
            float sumXX = 0;

            int end = (length >= 0) ? length : graph.Length;
            Debug.Assert(end <= graph.Length);

            for (int x = 0; x < end; ++x)
            {
                sumY += graph.Fx[x];
                sumXY += graph.Fx[x] * graph.X[x];


                sumX += graph.X[x];
                sumXX += graph.X[x] * graph.X[x];
            }

            StraightLine straight = new StraightLine();

            // Slope(b) = (NΣXY - (ΣX)(ΣY)) / (NΣXX - (ΣX*ΣX))
            straight.Slope = (end * sumXY - sumX * sumY) / (end * sumXX - sumX * sumX);

            // Intercept(a) = (ΣY - b(ΣX)) / N
            straight.YOffset = (sumY - straight.Slope * sumX) / end;

            return straight;
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

            for (int point = offset; point < end; ++point)
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
        #endregion Regression

        public static LineSet PlotLines2D(LineSet lines, float? xScale = null, float? yScale = null)
        {
            float xMult = xScale ?? lines.Thickness;
            float yMult = yScale ?? ((float)lines.Length / lines[0].Length) * lines.Thickness;
            LineSet set = new LineSet(lines);
            for (int x = 0; x < set.Length; ++x)
            {
                for (int y = 0; y < set[x].Length; ++y)
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

            for (int s = 0; s < shorties.Length; ++s)
            {
                Vector3[] points = new Vector3[Math.Min(lines[s].Length, length)];
                Array.Copy(lines[s].Positions, points, points.Length);
                shorties[s] = new Line() { Positions = points };
            }
            return new LineSet(shorties);
        }

        /// <summary>
        /// Compute FTLE values from a given set of pathlines. Assumed: Lines given as x+, x-, y+, y-
        /// </summary>
        /// <param name="pathlines"></param>
        /// <returns></returns>
        public static Graph2D[] ComputeFTLE2D(LineSet pathlines, Vector3 origin, float[] angles, float[] radii, float integrationStart, float integrationTime)
        {
            Debug.Assert(pathlines.Length % 4 == 0); // Redundant with next line.
            Debug.Assert(pathlines.Length == angles.Length * radii.Length * 4);

            float doubleDist = pathlines[0][0].X - pathlines[1][0].X;
            float endTime = integrationStart + integrationTime;

            float min = float.MaxValue;
            float max = float.MinValue;

            Graph2D[] graph = new Graph2D[angles.Length];
            
            // Go over all angles, each angle being one graph.
            for (int a = 0; a < angles.Length; ++a)
            {
                float[] x = new float[radii.Length];
                Array.Copy(radii, x, x.Length);
                float[] fx = new float[radii.Length];

                for (int r = 0; r < radii.Length; ++r)
                {
                    SquareMatrix stress = new SquareMatrix(2);

                    int idx = (a * radii.Length + r ) * 4;
                    if (pathlines[idx].Length < 1 || pathlines[idx + 1].Length < 1 || pathlines[idx + 2].Length < 1 || pathlines[idx + 3].Length < 1 ||
                        pathlines[idx].Last.Z < endTime || pathlines[idx + 1].Last.Z < endTime || pathlines[idx + 2].Last.Z < endTime || pathlines[idx + 3].Last.Z < endTime)
                    {
                        fx[r] = 0;// float.MaxValue;
                        continue;
                    }

                    Vector3 diffX = (Vector3)pathlines[idx + 0].SampleZ(endTime) - (Vector3)pathlines[idx + 1].SampleZ(endTime);
                    Vector3 diffY = (Vector3)pathlines[idx + 2].SampleZ(endTime) - (Vector3)pathlines[idx + 3].SampleZ(endTime);

                    if (Math.Abs(diffX.Z) > EPS_ZERO || Math.Abs(diffY.Z) > EPS_ZERO)
                    {
                        fx[r] = 0;
                        continue;
                    }

                    stress.m00 = diffX.X / doubleDist;
                    stress.m10 = diffY.X / doubleDist;
                    stress.m01 = diffX.Y / doubleDist;
                    stress.m11 = diffY.Y / doubleDist;

                    SquareMatrix cauchy = stress.Transposed() * stress;

                    Vector lambdas = cauchy.EigenvaluesReal();
                    float lMax = lambdas.Max();
                    fx[r] = (float)(Math.Log(Math.Sqrt(lMax)) / integrationTime);// pathlines[idx].Length > 0 ? (pathlines[idx][0]- origin).Length()  : 0;
                    min = Math.Min(min, fx[r]);
                    max = Math.Max(max, fx[r]);
                }
                graph[a] = new Graph2D(x, fx);
            }
            Console.WriteLine("FTLE values for time {0} to {1}: [{2}, {3}]", integrationStart, integrationTime + integrationStart, min, max);
            return graph;
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
