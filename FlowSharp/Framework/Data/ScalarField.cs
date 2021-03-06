﻿using System;
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

        public virtual FieldGrid Grid { get; set; }
        /// <summary>
        /// Number of grid edges in every dimension.
        /// </summary>
        public Index Size { get { return Grid.Size; } }

        // Not Grid.TimeOrigin, because a steady field should not have a time origin in the grid (for sampling logic).
        private float? _timeSlice = null;
        public virtual float? TimeOrigin //{ get { return Grid.TimeOrigin; } set { Grid.TimeOrigin = value; } }
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

        public abstract float Sample(Vector position);

        public abstract Vector SampleDerivative(Vector position);

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

        public virtual bool IsValid(Vector pos)
        {
            float[] weights;
            int[] neighbors = this.Grid.FindAdjacentIndices(pos, out weights);
            foreach (int neighbor in neighbors)
                if (this[neighbor] == InvalidValue)
                    return false;
            return true;
        }

        public abstract void ChangeEndian();

        public static void FlipEndian(float[] data)
        {
            int length = data.Length;
            // Rewrite the data in memory.
            unsafe
            {
                fixed (float* floatPtr = data)
                {
                    byte* byteData = (byte*)floatPtr;

                    // Swap beginning and ending float after multiplication.
                    for (int i = 0; i < length; ++i)
                    {
                        byte tmp = byteData[i * 4];
                        byteData[i * 4] = byteData[i * 4 + 3];
                        byteData[i * 4 + 3] = tmp;

                        tmp = byteData[i * 4 + 1];
                        byteData[i * 4 + 1] = byteData[i * 4 + 2];
                        byteData[i * 4 + 2] = tmp;

                    }
                }
            }
        }
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

        //private float? _timeSlice = null;
        //public override float? TimeSlice
        //{
        //    get
        //    {
        //        return Grid.TimeOrigin;
        //    }

        //    set
        //    {
        //        Grid.TimeOrigin = value;
        //    }
        //}

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

        public override float Sample(Vector position)
        {
            return Grid.Sample(this, position);
        }

        public override Vector SampleDerivative(Vector center)
        {
            Vector gradient = new Vector(0, Size.Length);

            for(int dim = 0; dim <gradient.Length; ++dim)
            {
                float stepPos = Math.Min(Size[dim] - 1, center[dim] + 0.5f) - center[dim];
                float stepMin = center[dim] - Math.Max(0, center[dim] - 0.5f);
                Vector samplePos = new Vector(center);
                samplePos[dim] += stepPos;
                gradient[dim] = Sample(samplePos);

                samplePos[dim] += stepMin - stepPos;
                gradient[dim] -= Sample(samplePos);

                gradient[dim] /= (stepPos - stepMin);
            }

            return gradient;
        }

        /// <summary>
        /// Get the derivative at a data point. Not checking for InvalidValue.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public Vector SampleDerivative(Index pos)
        {
            Debug.Assert(NumDimensions == Size.Length);
            Vector jacobian = new Vector(NumDimensions);

            // For all dimensions, so please reset each time.
            Index samplePos = new Index(pos);

            for (int dim = 0; dim < NumDimensions; ++dim)
            {
                // Just to be sure, check thst no value was overwritten.
                int posCpy = samplePos[dim];

                // See whether a step to the right/left is possible.
                samplePos[dim]++;
                bool rightValid = (samplePos[dim] < Size[dim]) && Sample(samplePos) != InvalidValue;
                samplePos[dim] -= 2;
                bool leftValid = (samplePos[dim] >= 0) && Sample(samplePos) != InvalidValue;
                samplePos[dim]++;

                if (rightValid)
                {
                    if (leftValid)
                    {
                        // Regular case. Interpolate.
                        samplePos[dim]++;
                        jacobian[dim] = Sample(samplePos);
                        samplePos[dim] -= 2;
                        jacobian[dim] -= Sample(samplePos);
                        jacobian[dim] *= 0.5f;
                        samplePos[dim]++;
                    }
                    else
                    {
                        // Left border.
                        samplePos[dim]++;
                        jacobian[dim] = Sample(samplePos);
                        samplePos[dim]--;
                        jacobian[dim] -= Sample(samplePos);
                    }
                }
                else
                {
                    if (leftValid)
                    {
                        // Right border.
                        jacobian[dim] = Sample(samplePos);
                        samplePos[dim]--;
                        jacobian[dim] -= Sample(samplePos);
                        samplePos[dim]++;
                    }
                    else
                    {
                        // Weird case. 
                        jacobian[dim] = 0;
                    }
                }
                Debug.Assert(posCpy == samplePos[dim]);
            }

            return jacobian;
        }

        /// <summary>
        /// Function to compute a new field based on an old one, point wise.
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public delegate float SGFunction(float v, Vector J);

        public ScalarField(ScalarField field, SGFunction function, bool needJ = true)
        {
            _data = new float[field.Size.Product()];
            Grid = field.Grid;

            this.InvalidValue = field.InvalidValue;

            GridIndex indexIterator = new GridIndex(field.Size);
            foreach (GridIndex index in indexIterator)
            {
                float s = field[(int)index];

                if (s == InvalidValue)
                {
                    this[(int)index] = (float)InvalidValue;
                }
                else
                {
                    Vector g = needJ ? field.SampleDerivative(index) : new Vec2(0, 0);
                    this[(int)index] = function(s, g);
                }
            }
        }

        public delegate float AnalyticalField(Vector pos);

        public static ScalarField FromAnalyticalField(AnalyticalField func, Index size, Vector origin, Vector cellSize)
        {
            Debug.Assert(size.Length == origin.Length && size.Length == cellSize.Length);

            RectlinearGrid grid = new RectlinearGrid(size);
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

        public void ComputeStatistics(out float validRegion, out float mean, out float sd)
        {
            int numValidCells = 0;
            mean = 0;
            sd = 0;

            GridIndex range = new GridIndex(Size);
            foreach(GridIndex idx in range)
            {
                float s = this[(int)idx];
                if(s != InvalidValue)
                {
                    numValidCells++;
                    mean += s;
                }
            }
            validRegion = (float)numValidCells / Size.Product();
            mean /= numValidCells;

            // Compute standard derivative.
            range.Reset();
            foreach (GridIndex idx in range)
            {
                float s = this[(int)idx];
                if (s != InvalidValue)
                {
                    float diff = s - mean;
                    sd += diff * diff;
                }
            }
            sd /= numValidCells;
            sd = (float)Math.Sqrt(sd);
        }

        public override void ChangeEndian()
        {
            FlipEndian(Data);
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
