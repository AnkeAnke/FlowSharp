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
        /// <summary>
        /// Searches for all 0-vectors in a 2D rectlinear vector field.
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        public static PointSet ComputeCriticalPointsRectlinear2D(VectorField field)
        {
            // Only for 2D rectlinear grids.
            Debug.Assert(field.Grid as RectlinearGrid != null);
            Debug.Assert(field.Size.Length == 2);

            List<Point> cpList = new List<Point>(field.Size.Product() / 10); // Rough guess.
            Vector halfCell = new Vector(0.5f, 2);
            //Index numCells = field.Size - new Index(1, field.Size.Length);
            for (int x = 0; x < field.Size[0] - 1; ++x)
                for (int y = 0; y < field.Size[1] - 1; ) // Doing the y++ down at the end.
                {
                    // Get neighbors.
                    float[] cellWeights;
                    Vector cellCenter = new Vector(new float[] { 0.5f + x, 0.5f + y });
                    // Use the neighbor function of the grid data type.
                    int[] adjacentCells = field.Grid.FindAdjacentIndices(cellCenter, out cellWeights, false);

                    bool pos = false;
                    bool neg = false;
                    bool containsZero = true;
                    for (int dim = 0; dim < 2; ++dim)
                    {
                        for (int neighbor = 0; neighbor < adjacentCells.Length; ++neighbor)
                        {
                            float data = field.Scalars[dim].Data[adjacentCells[neighbor]];
                            // Is the cell data valid?
                            if (data == field.Scalars[dim].InvalidValue)
                                goto NextCell;
                            if (data >= 0)
                                pos = true;
                            else
                                neg = true;
                        }
                        // If no 0 can be achieved in this cell, go on to next cell.
                        if (!pos || !neg)
                        {
                            containsZero = false;
                            break;
                        }

                        if (!containsZero)
                            goto NextCell;

                    }
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
                    if (c[0] == 0 || c[1] == 0)
                    {
                        // Degenerated double-linear case?
                        if (c[0] == 0 && c[1] == 0)
                        {
                            float abi = 1 / (a[1] * b[0] - a[0] * b[1]);
                            // Only one solution.
                            valT = new float[1]; valS = new float[1];
                            valT[0] = (a[0] * d[1] - a[1] * d[0]) * abi;
                            valS[0] = -(b[0] * valT[0] / a[0] + d[0] / a[0]);

                            goto WritePoints;
                        }
                        // Dimension in which the solution is linear.
                        int lD = c[0] == 0 ? 0 : 1;
                        int qD = 1 - lD;

                        Debug.Assert(b[lD] != 0 && c[qD] != 0);
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
                        goto WritePoints; // Don't need this. Still here, for better readability.
                    }
                    else
                    {
                        // Both dimensions are quadratic.
                        float denom = 1 / (a[0] * c[1] - a[1] * c[0]);
                        Debug.Assert(denom != 0);
                        float pPQ = (a[0] * b[1] - a[1] * b[0]) + (c[1] * d[0] - c[0] * d[1]);
                        pPQ *= denom / 2;
                        float qPQ = 4 * (b[1] * d[0] - b[0] * d[1]) * denom;

                        float root = pPQ * pPQ - qPQ;
                        if (root < 0)
                            goto NextCell;
                        root = (float)Math.Sqrt(root);
                        valS[0] = (pPQ + root);
                        valS[1] = (pPQ - root);

                        valT[0] = -(a[0] * valS[0] + d[0]) / (b[0] + c[0] * valS[0]);
                        valT[1] = -(a[0] * valS[1] + d[0]) / (b[0] + c[0] * valS[1]);
                    }

                    // Check whether the points lay in the cell. Write those to the point set.
                WritePoints:
                    for (int p = 0; p < valS.Length; ++p)
                    {
                        // Continue when not inside.
                        if (valS[p] < 0 || valS[p] > 1 || valT[p] < 0 || valT[p] > 1)
                            continue;

                        Vector cSize = (field.Grid as RectlinearGrid).CellSize;
                        Point cp = new Point()
                        {
                            Position = new SlimDX.Vector3((valS[p] + x) * cSize[0], (valT[p] + y) * cSize[1], 0.0f),
                            Color = new SlimDX.Vector3(0.01f, 0.001f, 0.6f), // Debug color. 
                            Radius = 0.01f
                        };
                        cpList.Add(cp);
                    }

                NextCell:
                    y++;
                }



            PointSet cpSet = new PointSet(cpList.ToArray());

            return cpSet;

        }

        public static PointSet ValidCells(VectorField field)
        {
            // Only for 2D rectlinear grids.
            Debug.Assert(field.Grid as RectlinearGrid != null);
            Debug.Assert(field.Size.Length == 2);

            List<Point> cpList = new List<Point>(field.Size.Product()); // Rough guess.

            //Index numCells = field.Size - new Index(1, field.Size.Length);
            for (int x = 0; x < field.Size[0] - 1; ++x)
                for (int y = 0; y < field.Size[1] - 1; ++y)
                {
                    float data = field.Scalars[0].Sample(new Index(new int[] { x, y }));
                    // Is the cell data valid?
                    if (data == field.Scalars[0].InvalidValue)
                        continue;


                    Point cp = new Point()
                    {
                        Position = new SlimDX.Vector3(x, y, 0.0f),
                        Color = new SlimDX.Vector3(1.0f, 0.0f, 1.0f), // Debug color. 
                        Radius = 0.005f
                    };
                    cpList.Add(cp);
                }
            RectlinearGrid rGrid = field.Grid as RectlinearGrid;
            Vector3 cellSize = new Vector3(rGrid.CellSize[0], rGrid.CellSize[1], 0.0f);
            Vector3 origin = new Vector3(rGrid.Origin[0], rGrid.Origin[1], 0.0f);

            PointSet cpSetL = new PointSet(cpList.ToArray(), cellSize, origin);

            return cpSetL;
        }

        /// <summary>
        /// Searches for all 0-vectors in a vector field.
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        //public static PointSet ComputeCriticalPointsRectlinear(VectorField field)
        //{
        //    // Only for rectlinear grids.
        //    Debug.Assert(field.Grid as RectlinearGrid != null);

        //    List<Point> cpList = new List<Point>(field.Size.Product() / 10);

        //    Index numCells = field.Size - new Index(1, field.Size.Length);
        //    for(int cell = 0; cell < numCells.Product(); ++cell)
        //    {

        //    }



        //    PointSet cpSet = new PointSet()
        //    {
        //        Points = cpList.ToArray()
        //    };

        //    return cpSet;

        //}
    }
}
