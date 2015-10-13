using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX;
using System.Diagnostics;

namespace FlowSharp
{
    abstract class Field
    {
        /// <summary>
        /// Number of spacial dimensions.
        /// </summary>
        public virtual int NumDimensions { get { return Size.Length; } }

        public FieldGrid Grid { get; set; }
        /// <summary>
        /// Number of grid edges in every dimension.
        /// </summary>
        public Index Size { get { return Grid.Size; } }
        private float? _timeSlice = null;
        public virtual float? TimeSlice
        {
            get { return _timeSlice; }
            set { _timeSlice = value; }
        }

        public abstract float this[int index]
        { get; set; }


        private float? _invalid = null;
        public virtual float? InvalidValue
        {
            get { return _invalid; }
            set { _invalid = value; }
        }

        /// <summary>
        /// Returns a value from the grid.
        /// </summary>
        /// <param name="gridPosition"></param>
        /// <returns></returns>
        public abstract float Sample(Index gridPosition);

        public abstract float Sample(Vector position, bool worldSpace = true);

        public abstract Vector SampleDerivative(Vector position, bool worldPosition = true);

        public abstract DataStream GetDataStream();
        /// <summary>
        /// Compare the value to the defined invalid value.
        /// </summary>
        /// <param name="gridPosition"></param>
        /// <returns></returns>
        public bool IsValid(Index gridPosition)
        {
            return (InvalidValue == null || Sample(gridPosition) != InvalidValue);
        }

        public abstract bool IsUnsteady();
    }
    /// <summary>
    /// Class for N dimensional float fields.
    /// </summary>
    class ScalarField : Field
    {
        private float[] _data;

        public float[] Data
        {
            get { return _data; }
            protected set { _data = value; }
        }

        public override float this[int index]
        {
            get { return _data[index]; }
            set { _data[index] = value; }
        }

        /// <summary>
        /// Instanciate a new field. The dimension is derived from the fields size.
        /// </summary>
        /// <param name="fieldSize">Number of grid edges in each dimension.</param>
        public ScalarField(FieldGrid grid)
        {
            Grid = grid;
            _data = new float[Size.Product()];
        }

        protected ScalarField() { }

        /// <summary>
        /// Returns a value from the grid.
        /// </summary>
        /// <param name="gridPosition"></param>
        /// <returns></returns>
        public override float Sample(Index gridPosition)
        {
            Debug.Assert(gridPosition < Size && gridPosition.IsPositive());

            int offsetScale = 1;
            int index = 0;

            // Have last dimension running fastest.
            for(int dim = 0; dim < NumDimensions; ++dim)
            {
                index += offsetScale * gridPosition[dim];
                offsetScale *= Size[dim];
            }

            return _data[index];
        }

        public override float Sample(Vector position, bool worldSpace = true)
        {
            return Grid.Sample(this, position, worldSpace);
        }

        public override Vector SampleDerivative(Vector position, bool worldPosition = true)
        {

            Vector center = worldPosition ? Grid.ToGridPosition(position) : new Vector(position);
            Vector gradient = new Vector(0, Size.Length);

            for(int dim = 0; dim <gradient.Length; ++dim)
            {
                float stepPos = Math.Min(Size[dim], center[dim] + 0.5f) - center[dim];
                float stepMin = center[dim] - Math.Max(0, center[dim] - 0.5f);
                Vector samplePos = new Vector(center);
                samplePos[dim] += stepPos;
                gradient[dim] = Sample(samplePos, false);

                samplePos[dim] += stepMin - stepPos;
                gradient[dim] -= Sample(samplePos, false);

                gradient[dim] /= (stepPos - stepMin);
            }

            return gradient;
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
                field[idx] = func(posV);
            }

            return field;
        }

        public override DataStream GetDataStream()
        {
            return new DataStream(Data, true, false);
        }

        public override bool IsUnsteady()
        {
            return false;
        }

        //class SliceRange
        //{
        //    /// <summary>
        //    /// Offset vector. A negative value means that the whole dimension is to be included.
        //    /// </summary>
        //    public Vector OffsetGridSpace { get; protected set; }
        //    protected Index _fieldSize;

        //    public SliceRange(Index fieldSize)
        //    {
        //        _fieldSize = fieldSize;
        //        OffsetGridSpace = new Vector(-1, fieldSize.Length);
        //    }

        //    public void SetOffset(int dimension, float value)
        //    {
        //        Debug.Assert(value > 0 && value < _fieldSize[dimension]);
        //        OffsetGridSpace[dimension] = value;
        //    }

        //    public bool IsDataSubspace
        //}
    }
}
