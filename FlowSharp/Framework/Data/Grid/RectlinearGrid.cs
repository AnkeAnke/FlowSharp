using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    /// <summary>
    /// Cells: N-dimensional boxes, constant edge length.
    /// </summary>
    class RectlinearGrid : FieldGrid
    {
        //   private float? _timeOrigin;
        // private bool _timeDependent = false;
        //public override float? TimeOrigin
        //{
        //    get
        //    {
        //        return _timeOrigin;
        //    }

        //    set
        //    {
        //        //Debug.Assert((_timeDependent) == (value != null), "Please don't mess with time!");
        //        _timeOrigin = value;// ?? 0;
        //        Origin.T = value ?? Origin.T;
        //    }
        //}

        /// <summary>
        /// Create a new rectlinear grid descriptor.
        /// </summary>
        /// <param name="size">Number of cells in each dimension.</param>
        /// <param name="cellSize">Size of one cell, only used for rendering.</param>
        public RectlinearGrid(Index size, Vector origin = null, float? timeOrigin = null)
        {
            Size = new Index(size);
            Origin = origin ?? new Vector(0, size.Length);
            TimeDependant = timeOrigin != null;
            Origin.T = timeOrigin ?? Origin.T;
            //_timeDependent = (timeOrigin != null);
            //TimeOrigin = timeOrigin;
        }
        public override FieldGrid Copy()
        {
            return new RectlinearGrid(new Index(Size), new Vector(Origin), TimeOrigin);
        }

        public override int NumAdjacentPoints()
        {
            return 1 << Size.Length; // 2 to the power of N 
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
            Vector origin = Origin.ToVec(Origin.Length + 1);
            RectlinearGrid timeGrid = new RectlinearGrid(timeSize, origin, timeStart);
            //_timeDependent = true;
            //TimeOrigin = timeStart;
            return timeGrid;
        }

        /// <summary>
        /// Returns the adjacent grid point indices.
        /// Indices in ascending order.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="indices"></param>
        /// <param name="weights"></param>
        public override int[] FindAdjacentIndices(Vector pos, out float[] weights)
        {
            int numPoints = NumAdjacentPoints();
            int[] indices = new int[numPoints];
            weights = new float[numPoints];

            Vector position = new Vector(pos);

            Debug.Assert(InGrid(position) || ((Vector)Size + Origin - pos).Min() == 0);

            Index gridPos = Index.Min((Index)(position - Origin), Size - new Index(1, Size.Length));
            Vector relativePos = position - Origin - (Vector)gridPos;

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

        public override bool InGrid(Vector position)
        {
            Debug.Assert(position.Length == Size.Length, "Trying to access " + Size.Length + "D field with " + position.Length + "D index.");
            //int dims = _timeOrigin == null ? Size.Length : Size.Length - 1;
            //Vector relPos = position - Origin.ToVec(dims);
            //for (int dim = 0; dim < dims; ++dim)
            //{
            //    if (relPos[dim] < 0 || relPos[dim] > Size[dim] - 1)
            //        return false;
            //}
            //if (_timeOrigin != null && (position.T < _timeOrigin || position.T > _timeOrigin + Size.T - 1))
            //    return false;
            for (int dim = 0; dim < Size.Length; ++dim)
                if (position[dim] < Origin[dim] || position[dim] >= Origin[dim] + Size[dim])
                    return false;
            return true;
        }
    }
}
