using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    /// <summary>
    /// Defines how a field is saved and accessed. Contains methods for sampling data.
    /// </summary>
    abstract class FieldGrid
    {
        public Index Size;

        public abstract int NumAdjecentPoints();
        public virtual Vector Sample(VectorField field, Vector position)
        {
            // Query relevant edges and their weights. Reault varies with different grid types.
            int numCells = NumAdjecentPoints();
            float[] weights;
            int[] indices = FindAdjacentIndices(position, out weights);

            Debug.Assert(indices.Length == weights.Length);

            // Start with the first grid point.
            Vector result = field.Sample(indices[0]);
            result *= weights[0];

            // Add the other weightes grid points.
            for (int dim = 1; dim < indices.Length; ++dim)
            {
                Vector add = field.Sample(indices[dim]);
                result += add * weights[dim];
            }

            return result;
        }

        public virtual float Sample(ScalarField field, Vector position)
        {
            // Query relevant edges and their weights. Reault varies with different grid types.
            int numCells = NumAdjecentPoints();
            float[] weights;
            int[] indices = FindAdjacentIndices(position, out weights);

            Debug.Assert(indices.Length == weights.Length);

            // Start with the first grid point.
            float result = field.Sample(indices[0]);
            result *= weights[0];

            // Add the other weightes grid points.
            for (int dim = 1; dim < indices.Length; ++dim)
            {
                result += weights[dim] * field.Sample(indices[dim]);
            }

            return result;
        }

        /// <summary>
        /// Returns all cell points necessary to interpolate a value at the position.
        /// </summary>
        /// <param name="position">The position in either world or grid space.</param>
        /// <param name="weights">The weights used for linear interpolation.</param>
        /// <param name="worldSpace">Is the position given in world space (origin + size * cellSize) or grid space (size)?</param>
        /// <returns>Scalar indices for acessing the grid.</returns>
        public abstract int[] FindAdjacentIndices(Vector position, out float[] weights, bool worldSpace = true);
    }

    /// <summary>
    /// Cells: N-dimensional boxes, constant edge length.
    /// </summary>
    class RectlinearGrid : FieldGrid
    {
        public Vector Origin, CellSize;

        public Vector Extent { get { return Size * CellSize; } }
        public Vector Maximum { get { return Origin + Extent; } }

        /// <summary>
        /// Create a new rectlinear grid descriptor.
        /// </summary>
        /// <param name="size">Number of cells in each dimension.</param>
        /// <param name="origin">Minimum value in each direction.</param>
        /// <param name="cellSize">Size of one cell.</param>
        public RectlinearGrid(Index size, Vector origin, Vector cellSize)
        {
            Size = new Index(size);
            Origin = new Vector(origin);
            CellSize = new Vector(cellSize);
        }

        public Vector GetWorldPosition(Index index)
        {
            return Origin + index * CellSize;
        }

        public override int NumAdjecentPoints()
        {
            return 1 << Size.Length; // 2 to the power of N 
        }

        /// <summary>
        /// Returns the adjacent grid point indices.
        /// Indices in ascending order.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="indices"></param>
        /// <param name="weights"></param>
        public override int[] FindAdjacentIndices(Vector pos, out float[] weights, bool worldSpace = true)
        {
            int numPoints = NumAdjecentPoints();
            int[] indices = new int[numPoints];
            weights = new float[numPoints];

            Vector position = new Vector(pos);

            if (worldSpace)
            {
                // Find minimum grid point.
                position -= Origin;
                position /= CellSize;
            }

            Index gridPos = (Index)position;
            Vector relativePos = position - (Vector)gridPos;


            // Convert to int.
            int offsetScale = 1;
            int index = 0;

            // Have last dimension running fastest.
            for (int dim = 0; dim < Size.Length; ++dim)
            {
                index += offsetScale * gridPos[dim];
                offsetScale *= Size[dim];
            }

            // Linear interpolation in N dimensions.
            for (int point = 0; point < numPoints; ++point)
            {
                int stepDim = 1;
                int pointIndex = index;
                float pointWeight = 1.0f;

                // Compute the one dimensional index of each point.
                for (int dim = 0; dim < Size.Length; ++dim)
                {
                    // Is the dimth bit set?
                    if ((point & (1 << dim)) > 0)
                    {
                        pointIndex += stepDim;
                        pointWeight *= relativePos[dim];
                    }
                    else
                    {
                        pointWeight *= (1 - relativePos[dim]);
                    }
                    stepDim *= Size[dim];
                }

                // Accumulated all dimensions. Save value to output arrays.
                indices[point] = pointIndex;
                weights[point] = pointWeight;
            }

            return indices;
        }
    }
}