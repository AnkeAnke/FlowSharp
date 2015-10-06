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
        /// Returns a value from the grid.
        /// </summary>
        /// <param name="index">Scalar index.</param>
        /// <returns></returns>
        public float Sample(int index)
        {
            Debug.Assert(index < Size.Product() && index >= 0);

            return _data[index];
        }

        public float Sample(Vector position, bool worldSpace = true)
        {
            return Grid.Sample(this, position, worldSpace);
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


        public delegate float AnalyticalField(Vector pos);

        public static ScalarField FromAnalyticalField(AnalyticalField func, Index size, Vector origin, Vector cellSize)
        {
            Debug.Assert(size.Length == origin.Length && size.Length == cellSize.Length);

            RectlinearGrid grid = new RectlinearGrid(size, origin, cellSize);
            ScalarField field = new ScalarField(grid);

            for(int idx = 0; idx < size.Product(); ++idx)
            {
                // Compute the n-dimensional position.
                int index = idx;
                Index pos = new Index(0, size.Length);
                pos[0] = index % size[0];

                for(int dim = 1; dim < size.Length; ++dim)
                {
                    index -= pos[dim - 1];
                    index /= size[dim - 1];
                    pos[dim] = index % size[dim];
                }

                Vector posV = origin + pos * cellSize;
                field.Data[idx] = func(posV);
            }

            return field;
        }
    }
}
