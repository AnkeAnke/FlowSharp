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
        public static PointSet<Point> ComputeCriticalPointsRegularAnalytical2D(VectorField field, float eps = EPS_ZERO)
        {
            // Only for 2D rectlinear grids.
            Debug.Assert(field.Grid as RectlinearGrid != null);
            Debug.Assert(field.Size.Length == 2);

            List<Point> cpList = new List<Point>(field.Size.Product() / 10); // Rough guess.
            // DEBUG
            cpList.Add(new Point()
            {
                Position = new Vector3(0, 0, 0),
                Color = new Vector3(1, 1, 0)
            });
            Vector halfCell = new Vector(0.5f, 2);
            Vector cSize = (field.Grid as RectlinearGrid).CellSize;
            Vector origin = (field.Grid as RectlinearGrid).Origin;
            //Index numCells = field.Size - new Index(1, field.Size.Length);
            for (int x = 0; x < field.Size[0] - 1; ++x)
                for (int y = 0; y < field.Size[1] - 1;) // Doing the y++ down at the end.
                {
                    // Get neighbors.
                    float[] cellWeights;
                    Vector cellCenter = new Vector(new float[] { 0.5f + x, 0.5f + y });
                    // Use the neighbor function of the grid data type.
                    int[] adjacentCells = field.Grid.FindAdjacentIndices(cellCenter, out cellWeights, false);

                    for (int dim = 0; dim < 2; ++dim)
                    {
                        bool pos = false;
                        bool neg = false;

                        for (int neighbor = 0; neighbor < adjacentCells.Length; ++neighbor)
                        {
                            float data = field.Scalars[dim][adjacentCells[neighbor]];
                            // Is the cell data valid?
                            if (data == field.Scalars[dim].InvalidValue)
                                goto NextCell;
                            if (data > 0)
                                pos = true;
                            else if (data < 0)
                                neg = true;
                            else
                            {
                                neg = true;
                                pos = true;
                            }
                        }
                        // If no 0 can be achieved in this cell, go on to next cell.
                        if (!pos || !neg)
                        {
                            goto NextCell;
                        }
                    }

                    //DEBUG:
                    Vector3 color;

                    // Now, compute the position. Possible since the function is only quadratic!
                    Vector p00 = field.Sample(adjacentCells[0]);
                    Vector p10 = field.Sample(adjacentCells[1]);
                    Vector p01 = field.Sample(adjacentCells[2]);
                    Vector p11 = field.Sample(adjacentCells[3]);
                    Vector a = p10 - p00;
                    Vector b = p01 - p00;
                    Vector c = p00 - p01 - p10 + p11;
                    Vector d = p00;

                    // The two points that will be found.
                    float[] valS = new float[2];
                    float[] valT = new float[2];

                    // Degenerated linear case?
                    if (Math.Abs(c[0]) < eps || Math.Abs(c[1]) < eps)
                    {
                        // Degenerated double-linear case?
                        if (Math.Abs(c[0]) < eps && Math.Abs(c[1]) < eps)
                        {
                            float abi = 1 / (a[1] * b[0] - a[0] * b[1]);
                            // Only one solution.
                            valT = new float[1]; valS = new float[1];
                            valT[0] = (a[0] * d[1] - a[1] * d[0]) * abi;
                            valS[0] = -(b[0] * valT[0] / a[0] + d[0] / a[0]);
                            // DEBUG
                            //valT[0] = 0.5f; valS[0] = 0.5f;
                            color = new Vector3(1, 0, 0);

                            goto WritePoints;
                        }
                        // Dimension in which the solution is linear.
                        int lD = Math.Abs(c[0]) < eps ? 0 : 1;
                        int qD = 1 - lD;

                        if (b[lD] == 0)
                            goto NextCell;

                        float cbi = 1 / (c[qD] * b[lD]);
                        // Values for PQ formula.
                        float pPQ = d[lD] / b[lD] + a[qD] / c[qD] + a[lD] * b[qD] * cbi;
                        pPQ /= 2;
                        float qPQ = d[lD] * (a[qD] + a[lD]) * cbi;

                        float root = pPQ * pPQ - qPQ;
                        Debug.Assert(root >= 0);

                        root = (float)Math.Sqrt(root);

                        valT[0] = pPQ + root;
                        valT[1] = pPQ - root;

                        valS[0] = -(b[lD] * valT[0] / a[lD] + d[lD] / a[lD]);
                        valS[1] = -(b[lD] * valT[1] / a[lD] + d[lD] / a[lD]);

                        color = new Vector3(0, 1, 0);

                        goto WritePoints; // Don't need this. Still here, for better readability.
                    }
                    else
                    {
                        // Both dimensions are quadratic.
                        float denom = 1 / (a[0] * c[1] - a[1] * c[0]);
                        Debug.Assert(denom != 0);
                        float pPQ = (a[0] * b[1] - a[1] * b[0]) + (c[1] * d[0] - c[0] * d[1]);
                        pPQ *= denom * 0.5f;
                        float qPQ = (b[1] * d[0] - b[0] * d[1]) * denom;

                        float root = pPQ * pPQ - qPQ;
                        if (root < 0)
                            goto NextCell;
                        root = (float)Math.Sqrt(root);
                        valS[0] = (pPQ + root);
                        valS[1] = (pPQ - root);

                        valT[0] = -(a[0] * valS[0] + d[0]) / (b[0] + c[0] * valS[0]);
                        valT[1] = -(a[0] * valS[1] + d[0]) / (b[0] + c[0] * valS[1]);

                        color = new Vector3(0, 0, 1);
                    }

                    // Check whether the points lay in the cell. Write those to the point set.
                    WritePoints:
                    for (int p = 0; p < valS.Length; ++p)
                    {
                        //Continue when not inside.
                        //if (valS[p] < 0 || valS[p] >= 1 || valT[p] < 0 || valT[p] >= 1)
                        //    continue;

                        Point cp = new Point()
                        {
                            Position = new Vector3(origin[0] + (0.5f + x) * cSize[0], origin[1] + (0.5f + y) * cSize[1], 0.0f), //new Vector3(origin[0] + (valS[p] + x) * cSize[0], origin[1] + (valT[p] + y) * cSize[1], 0.0f),
                            Color = color, //new SlimDX.Vector3(0.01f, 0.001f, 0.6f), // Debug color. 
                            Radius = 0.002f
                        };
                        cpList.Add(cp);

                        //Continue when not inside.
                        if (valS[p] < 0 || valS[p] >= 1 || valT[p] < 0 || valT[p] >= 1)
                            continue;

                        cp = new Point()
                        {
                            Position = new Vector3(origin[0] + (valS[p] + x) * cSize[0], origin[1] + (valT[p] + y) * cSize[1], 0.0f),
                            Color = color, //new SlimDX.Vector3(0.01f, 0.001f, 0.6f), // Debug color. 
                            Radius = 0.006f
                        };
                        cpList.Add(cp);
                    }

                    NextCell:
                    y++;
                }

            PointSet<Point> cpSet = new PointSet<Point>(cpList.ToArray());

            return cpSet;

        }
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
            Debug.Assert(grid.Size.Length == 2);
            _epsCriticalPoint = epsCriticalPoint;

            List<Vector> cpList = new List<Vector>(field.Size.Product() / 10); // Rough guess.

            Vector halfCell = new Vector(0.5f, 2);
            Vector cSize = (field.Grid as RectlinearGrid).CellSize;
            Vector origin = (field.Grid as RectlinearGrid).Origin;
            //Index numCells = field.Size - new Index(1, field.Size.Length);
            for (int x = 0; x < field.Size[0] - 1; ++x)
                for (int y = 0; y < field.Size[1] - 1; ++y) // Doing the y++ down at the end.
                {
                    SubdivideCell(field, new Vec2(x, y), 0, numDivisions, cpList);
                }

            CriticalPoint2D[] points = new CriticalPoint2D[cpList.Count];

            // In a 2D slice, set 3rd value to time value.
            bool attachTimeZ = grid.Size.Length == 2 && field.TimeSlice != 0;
            for (int index = 0; index < cpList.Count; ++index)
            {
                Vector3 pos = (Vector3)cpList[index];
                if (attachTimeZ)
                    pos.Z = (float)field.TimeSlice;

                SquareMatrix J = field.SampleDerivative(cpList[index], false);

                points[index] = new CriticalPoint2D(pos, J)
                {
                    Radius = pointSize ?? 1
                };
            }

            return new CriticalPointSet2D(points, new Vector3((Vector2)grid.CellSize, 1.0f), (Vector3)grid.Origin);
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
            Vector cSize = (field.Grid as RectlinearGrid).CellSize;
            Vector origin = (field.Grid as RectlinearGrid).Origin;
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

            return new PointSet<Point>(points, new Vector3((Vector2)grid.CellSize, 1.0f), (Vector3)grid.Origin);
        }

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

                    float value = field.Scalars[dim].Sample(position, false);
                    if (value == field.Scalars[dim].InvalidValue)
                        return;
                    if (value >= 0)
                        pos = true;
                    if (value <= 0)
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
        public static PointSet<Point> ValidDataPoints(VectorField field)
        {
            // Only for 2D rectlinear grids.
            Debug.Assert(field.Grid as RectlinearGrid != null);
            Debug.Assert(field.NumVectorDimensions == 2 || field.NumVectorDimensions == 3);

            List<Point> cpList = new List<Point>(field.Size.Product());

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

                        Point cp = new Point()
                        {
                            Position = new SlimDX.Vector3(x, y, z),
                            Color = new SlimDX.Vector3(0.6f, 0.3f, 0.3f), // Debug color. 
                            Radius = 0.1f
                        };
                        cpList.Add(cp);
                    }
            RectlinearGrid rGrid = field.Grid as RectlinearGrid;

            PointSet<Point> cpSet = new PointSet<Point>(cpList.ToArray(), (Vector3)rGrid.CellSize, (Vector3)rGrid.Origin);

            return cpSet;
        }

        public static PointSet<Point> SomePoints2D(VectorField field, int numPoints, float pointSize = 1)
        {
            // Only for 2D rectlinear grids.
            Debug.Assert(field.Grid as RectlinearGrid != null);
            Debug.Assert(field.NumVectorDimensions >= 2);
            Random rnd = new Random();

            Point[] cpList = new Point[numPoints];
            bool attachTimeZ = field.NumVectorDimensions == 2 && field.TimeSlice != 0;
            //Index numCells = field.Size - new Index(1, field.Size.Length);
            for (int index = 0; index < numPoints; ++index)
            {
                Vector pos = new Vector(0, field.Size.Length);
                pos[0] = (float)rnd.NextDouble() * (field.Size[0]-1);
                pos[1] = (float)rnd.NextDouble() * (field.Size[1]-1);
                float data = field.Scalars[0].Sample(pos, false);
                if(data == field.InvalidValue)
                {
                    index--;
                    continue;
                }
                Point cp = new Point()
                {
                    Position = (Vector3)pos,
                    Color = new SlimDX.Vector3((float)index / numPoints, 0.0f, 0.3f), // Debug color. 
                    Radius = pointSize
                };
                if (attachTimeZ)
                    cp.Position.Z = (float)field.TimeSlice;
                cpList[index] = cp;
            }
            RectlinearGrid rGrid = field.Grid as RectlinearGrid;
            PointSet<Point> cpSet = new PointSet<Point>(cpList, new Vector3((Vector2)rGrid.CellSize, 1.0f), (Vector3)rGrid.Origin);

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
                float data = field.Scalars[0].Sample(pos, false);
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
            PointSet<Point> cpSet = new PointSet<Point>(cpList, new Vector3((Vector2)rGrid.CellSize, 1.0f), (Vector3)rGrid.Origin);

            return cpSet;
        }

        public static float AlphaStableFFF = 20;
        public static Vector StableFFF(Vector v, SquareMatrix J)
        {
            //return J[0];
            Debug.Assert(v.Length == 3 && J.Length == 3);
            Vector x = J.Row(0).AsVec3();
            x = J.Row(1).AsVec3();
            Vec3 f = Vec3.Cross(J.Row(0).AsVec3(), J.Row(1).AsVec3());
            //f *= f.T > 0 ? 1 : -1;
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
            //return J[0].AsVec3();
            return f + AlphaStableFFF * g;
        }

        public static Vector StableFFFNegative(Vector v, SquareMatrix J)
        {
            Debug.Assert(v.Length == 3 && J.Length == 3);
            Vec3 f = Vec3.Cross(J.Row(0).AsVec3(), J.Row(1).AsVec3());
            Vec3 fNorm = new Vec3(f);
            fNorm.Normalize();

            // Compute attracting vector field.
            Vec3 d = new Vec3();
            SquareMatrix dMat = new SquareMatrix(new Vector[] { v.ToVec2(), J[0].ToVec2() });
            d[0] = dMat.Determinant();
            dMat[1] = J[1].ToVec2();
            d[1] = dMat.Determinant();
            dMat[1] = J[2].ToVec2();
            d[2] = dMat.Determinant();

            // Add up fields.
            Vec3 g = Vec3.Cross(fNorm, d);
            return -f + AlphaStableFFF * g;
        }
    }
}
