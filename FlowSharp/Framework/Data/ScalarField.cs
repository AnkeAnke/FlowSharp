using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX;
using System.Diagnostics;

namespace FlowSharp
{
    /// <summary>
    /// Class for N dimensional float fields.
    /// </summary>
    class ScalarField
    {
        /// <summary>
        /// Number of dimensions. Cannot be set as generic parameter, this has to do.
        /// </summary>
        public int N { get; protected set; }

        public FieldGrid Grid { get; protected set; }
        /// <summary>
        /// Number of grid edges in every dimension.
        /// </summary>
        public Index Size { get { return Grid.Size; } }

        protected float[] _data;

        public float[] Data
        {
            get { return _data; }
            protected set { _data = value; }
        }

        //protected bool _containsInvalidValue = false;
        //protected float _invalidValue;
        ///// <summary>
        ///// Value assigned to nonexistant values.
        ///// </summary>
        //public float InvalidValue
        //{
        //    get { return _invalidValue; }
        //    set { _containsInvalidValue = true; _invalidValue = value; }
        //}

        public float? InvalidValue = null;

        /// <summary>
        /// Instanciate a new field. The dimension is derived from the fields size.
        /// </summary>
        /// <param name="fieldSize">Number of grid edges in each dimension.</param>
        public ScalarField(FieldGrid grid)
        {
            N = grid.Size.Length;
            Grid = grid;
            _data = new float[Size.Product()];
        }

        /// <summary>
        /// Returns a value from the grid.
        /// </summary>
        /// <param name="gridPosition"></param>
        /// <returns></returns>
        public float Sample(Index gridPosition)
        {
            Debug.Assert(gridPosition < Size && gridPosition.IsPositive());

            int offsetScale = 1;
            int index = 0;

            // Have last dimension running fastest.
            for(int dim = 0; dim < N; ++dim)
            {
                index += offsetScale * gridPosition[dim];
                offsetScale *= Size[dim];
            }

            return _data[index];
        }

        /// <summary>
        /// Compare the value to the defined invalid value.
        /// </summary>
        /// <param name="gridPosition"></param>
        /// <returns></returns>
        public bool IsValid(Index gridPosition)
        {
            return (InvalidValue == null || Sample(gridPosition) != InvalidValue);
        }
    }
}
