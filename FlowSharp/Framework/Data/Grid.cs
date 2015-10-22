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
        public abstract Vector Scale { get; }
        public abstract float? TimeOrigin { get; set; }

        public abstract int NumAdjacentPoints();
        public virtual Vector Sample(VectorField field, Vector position, bool worldSpace = true)
        {
            // Query relevant edges and their weights. Reault varies with different grid types.
            int numCells = NumAdjacentPoints();
            float[] weights;
            int[] indices = FindAdjacentIndices(position, out weights, worldSpace);

            Debug.Assert(indices.Length == weights.Length);

            Vector result = new Vector(0, field.NumVectorDimensions);
            // Add the other weightes grid points.
            for (int dim = 0; dim < indices.Length; ++dim)
            {
                Vector add = field.Sample(indices[dim]);
                if(add[0] == field.InvalidValue)
                {
                    return new Vector(field.InvalidValue??float.MaxValue, field.NumVectorDimensions);
                }
                result += add * weights[dim];
            }

            return result;
        }

        public virtual float Sample(ScalarField field, Vector position, bool worldSpace = true)
        {
            // Query relevant edges and their weights. Reault varies with different grid types.
            int numCells = NumAdjacentPoints();
            float[] weights;
            int[] indices = FindAdjacentIndices(position, out weights, worldSpace);

            Debug.Assert(indices.Length == weights.Length);

            // Start with the first grid point.
            float result = field[indices[0]];
            if (result == field.InvalidValue)
                return (float)field.InvalidValue;
            result *= weights[0];

            // Add the other weightes grid points.
            for (int dim = 1; dim < indices.Length; ++dim)
            {
                if (field[indices[dim]] == field.InvalidValue)
                    return (float)field.InvalidValue;
                result += weights[dim] * field[indices[dim]];
            }

            return result;
        }

        public abstract FieldGrid Copy();
        public abstract Vector ToGridPosition(Vector worldPos);
        public abstract Vector ToWorldPosition(Vector griddPos);
        public abstract FieldGrid GetAsTimeGrid(int numTimeSlices, float timeStart, float timeStep);
        public abstract bool InGrid(Vector pos, bool worldSpace = true);

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
        public override Vector Scale { get { return CellSize; } }

        public Vector Extent { get { return (Size - new Index(1, Size.Length)) * CellSize; } }
        public Vector Maximum { get { return Origin + Extent; } }
        private bool _timeDependent = false;
        public override float? TimeOrigin
        {
            get
            {
                return _timeDependent ? (float?)Origin.T : null;
            }

            set
            {
                Debug.Assert((_timeDependent) == (value != null), "Please don't mess with time!");
                Origin.T = value ?? 0;
            }
        }

        /// <summary>
        /// Create a new rectlinear grid descriptor.
        /// </summary>
        /// <param name="size">Number of cells in each dimension.</param>
        /// <param name="origin">Minimum value in each direction.</param>
        /// <param name="cellSize">Size of one cell.</param>
        public RectlinearGrid(Index size, Vector origin, Vector cellSize, float? timeOrigin = null)
        {
            Size = new Index(size);
            Origin = new Vector(origin);
            CellSize = new Vector(cellSize);
            _timeDependent = (timeOrigin != null);
            TimeOrigin = timeOrigin;
        }
        public override FieldGrid Copy()
        {
            return new RectlinearGrid(new Index(Size), new Vector(Origin), new Vector(CellSize), TimeOrigin);
        }

        public Vector GetWorldPosition(Index index)
        {
            return Origin + index * CellSize;
        }

        public override int NumAdjacentPoints()
        {
            return 1 << Size.Length; // 2 to the power of N 
        }

        public override Vector ToWorldPosition(Vector gridPos)
        {
            Vector ret = new Vector(Origin);
            ret += CellSize * gridPos;
            return ret;
        }

        public override Vector ToGridPosition(Vector worldPos)
        {
            Vector ret = new Vector(worldPos);
            ret -= Origin;
            ret /= CellSize;
            return ret;
        }

        /// <summary>
        /// Append a time dimension.
        /// </summary>
        /// <param name="numTimeSlices"></param>
        /// <param name="timeStart"></param>
        /// <param name="timeStep"></param>
        /// <returns></returns>
        public override FieldGrid GetAsTimeGrid(int numTimeSlices, float timeStart, float timeStep)
        {
            Index timeSize = new Index(Size.Length + 1);
            Array.Copy(Size.Data, timeSize.Data, Size.Length);
            timeSize[Size.Length] = numTimeSlices;

            Vector timeOrigin = new Vector(Size.Length + 1);
            Array.Copy(Origin.Data, timeOrigin.Data, Size.Length);
            timeOrigin[Size.Length] = timeStart;

            Vector timeCell = new Vector(Size.Length + 1);
            Array.Copy(CellSize.Data, timeCell.Data, Size.Length);
            timeCell[Size.Length] = timeStep;

            RectlinearGrid timeGrid = new RectlinearGrid(timeSize, timeOrigin, timeCell);
            _timeDependent = true;
            return timeGrid;
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
            int numPoints = NumAdjacentPoints();
            int[] indices = new int[numPoints];
            weights = new float[numPoints];

            Vector position = new Vector(pos);

            if (worldSpace)
            {
                // Find minimum grid point.
                position -= Origin;
                position /= CellSize;
            }

            Debug.Assert(InGrid(position, false));

            Index gridPos = Index.Min((Index)position, Size-new Index(1, Size.Length));
            Vector relativePos = position - (Vector)gridPos;

            // Convert to int.
            int offsetScale = 1;
            int index = 0;

            // Have last dimension running fastest. Compute 1D index.
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
                        // Extremum case: The value is on the outmost border. Clip that position.
                        if (gridPos[dim] < Size[dim] - 1)
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

        public override bool InGrid(Vector pos, bool worldSpace = true)
        {
            Vector position = new Vector(pos);

            if (worldSpace)
            {
                // Find minimum grid point.
                position -= Origin;
                position /= CellSize;
            }
            for (int dim = 0; dim < Size.Length; ++dim)
            {
                if (position[dim] < 0 || position[dim] > Size[dim] - 1)
                    return false;
            }
            return true;
        }
    }
}