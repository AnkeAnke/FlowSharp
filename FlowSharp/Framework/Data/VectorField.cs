using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace FlowSharp
{
    /// <summary>
    /// Class for N dimensional vectorV fields, consisting of V=Length dimensional scalar fields.
    /// </summary>
    class VectorField
    {
        public ScalarField[] Scalars { get; protected set; }
        public FieldGrid Grid { get; protected set; }
        public Index Size { get { return Grid.Size; } }

        /// <summary>
        /// Number of dimensions per vector.
        /// </summary>
        public int V { get { return Scalars.Length; } }

        /// <summary>
        /// Access field by scalar index.
        /// </summary>
        public void Sample(int index, out Vector vec)
        {
            vec = new Vector(Scalars.Length);
            for (int dim = 0; dim < V; ++dim)
                vec[dim] = Scalars[dim].Data[index];
        }

        /// <summary>
        /// Access field by N-dimensional index.
        /// </summary>
        public void Sample(Index gridPosition, out Vector vec)
        {
            Debug.Assert(gridPosition < Size && gridPosition.IsPositive());

            int offsetScale = 1;
            int index = 0;

            // Have last dimension running fastest.
            for (int dim = 0; dim < V; ++dim)
            {
                index += offsetScale * gridPosition[dim];
                offsetScale *= Size[dim];
            }

            Sample(index, out vec);
        }

        public void Sample(Vector position, out Vector result)
        {
            Grid.Sample(this, position, out result);
        }
    }

    /// <summary>
    /// Defines how a field is saved and accessed.
    /// </summary>
    abstract class FieldGrid
    {
        public Index Size;

        public abstract int NumAdjecentPoints();
        public virtual void Sample(VectorField field, Vector position, out Vector result)
        {
            // Query relevant edges and their weights. Reault varies with different grid types.
            int numCells = NumAdjecentPoints();
            int[] indices = new int[numCells];
            float[] weights = new float[numCells];
            FindAdjacentIndices(position, out indices, out weights);

            Debug.Assert(indices.Length == weights.Length);

            // Start with the first grid point.
            field.Sample(indices[0], out result);
            result *= weights[0];

            // Add the other weightes grid points.
            for (int dim = 1; dim < indices.Length; ++dim)
            {
                Vector add;
                field.Sample(indices[0], out add);
                result += add * weights[dim];
            }
        }

// TODO
//        public virtual void Sample(ScalarField field, Vector position, out float result);

        protected abstract void FindAdjacentIndices(Vector position, out int[] result, out float[] weights);
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

        public override int NumAdjecentPoints()
        {
            return 1 << Size.Length; // 2 to the power of N 
        }

        protected override void FindAdjacentIndices(Vector pos, out int[] indices, out float[] weights)
        {
            int numPoints = NumAdjecentPoints();
            indices = new int[numPoints];
            weights = new float[numPoints];

            Vector position = new Vector(pos);

            // Find minimum grid point.
            position -= Origin;
            position /= CellSize;

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
        }
    }
}

// &PARM04
// ygOrigin = 9.0,
// xgOrigin = 32.0,
// delY   =  210*0.1,
// delX   =  450*0.1,