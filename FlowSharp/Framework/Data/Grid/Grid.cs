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
        public int VectorLength { get { return Size.Length; } }
        public virtual bool TimeDependant { get; set; }
        public Vector Origin;
        public virtual float? TimeOrigin{
            get { return TimeDependant? (float?)Origin.T : null; }
            set {
                if (value != null)
                {
                    Origin.T = (float)value;
                    TimeDependant = true;
                }
                else
                {
                    TimeDependant = false;
                }
            } }

        public abstract int NumAdjacentPoints();
        public virtual Vector Sample(VectorField field, Vector position)
        {
            // Query relevant edges and their weights. Result varies with different grid types.
            int numCells = NumAdjacentPoints();
            VectorRef weights;
            Index indices = FindAdjacentIndices(position, out weights);

            if (indices == null)
                return null;

            Debug.Assert(indices.Length == weights.Length);

            Vector result = new Vector(0, field.NumDimensions);
            // Add the other weighted grid points.
            for (int dim = 0; dim < indices.Length; ++dim)
            {
                VectorRef add = field.Sample(indices[dim]);
                if(add[0] == field.InvalidValue)
                {
                    new Vector(field.InvalidValue??float.MaxValue, field.NumDimensions);
                }
                result += add * weights[dim];
            }

            return result;
        }

        //public virtual float Sample(ScalarField field, Vector position)
        //{
        //    // Query relevant edges and their weights. Reault varies with different grid types.
        //    int numCells = NumAdjacentPoints();
        //    float[] weights;
        //    int[] indices = FindAdjacentIndices(position, out weights);

        //    Debug.Assert(indices.Length == weights.Length);

        //    // Start with the first grid point.
        //    float result = field[indices[0]];
        //    if (result == field.InvalidValue)
        //        return (float)field.InvalidValue;
        //    result *= weights[0];

        //    // Add the other weightes grid points.
        //    for (int dim = 1; dim < indices.Length; ++dim)
        //    {
        //        if (field[indices[dim]] == field.InvalidValue)
        //            return (float)field.InvalidValue;
        //        result += weights[dim] * field[indices[dim]];
        //    }

        //    return result;
        //}

        public abstract FieldGrid Copy();
        public abstract FieldGrid GetAsTimeGrid(int numTimeSlices, float timeStart);
        public abstract bool InGrid(Vector pos);

        /// <summary>
        /// Returns all cell points necessary to interpolate a value at the position.
        /// </summary>
        /// <param name="position">The position in either world or grid space.</param>
        /// <param name="weights">The weights used for linear interpolation.</param>
        /// <returns>Scalar indices for acessing the grid.</returns>
        public abstract Index FindAdjacentIndices(VectorRef position, out VectorRef weights);

        public abstract Vector CutToBorder(VectorField field, VectorRef pos, VectorRef dir);
    }

    public enum CellType
    {
        Hexa = 8,
        Tetra = 4
    }
}