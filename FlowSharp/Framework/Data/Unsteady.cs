using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SlimDX;

namespace FlowSharp
{
    class VectorFieldUnsteady : VectorField
    {
        public float? TimeOrigin
        {
            get { return Grid.TimeDependant ? (float?)Grid.TimeOrigin : null; }
            set { Grid.TimeOrigin = value; }
        }

        public VectorFieldUnsteady(VectorData data, FieldGrid grid)
        {
            Debug.Assert(grid.TimeDependant);
            Data = data;
            Grid = grid;
        }

        public VectorFieldUnsteady(ScalarFieldUnsteady[] data)
        {
            Debug.Assert(data[0].Grid.TimeDependant);

            VectorData[] raw = new VectorData[data.Length];
            for (int d = 0; d < data.Length; ++d)
                raw[d] = data[d].Data;

            Data = new VectorBuffer(raw);
            Grid = data[0].Grid;
        }

        public VectorFieldUnsteady() { }

        public VectorFieldUnsteady(VectorField field, VFJFunction function, int outputDim, bool needJacobian = true)
            : base(field, function, outputDim, needJacobian) { }
        //public override VectorRef this[int index] { get { return Vector.ToUnsteady(_data[index]); } protected set { _data[index] = value; } }
        /// <summary>
        /// Number of dimensions per vector. Including one time dimension.
        /// </summary>
        public override int NumVectorDimensions { get { return Data.NumVectorDimensions + 1; } }

        public override void ScaleToGrid(Vector scale)
        {
            Debug.Assert(Data.NumVectorDimensions == scale.Length);
            for (int dim = 0; dim < Data.NumVectorDimensions; ++dim)
            {
                ScaleToGrid(scale[dim]);
            }
            SpreadInvalidValue();
        }

        /// <summary>
        /// Access field by scalar index.
        /// </summary>
        //public override Vector Sample(int index)
        //{
        //    Debug.Assert(index >= 0 && index < Size.Product(), "Index out of bounds: " + index + " not within [0, " + Size.Product() + ").");
        //    Vector vec = new Vector(NumVectorDimensions);
        //    for (int dim = 0; dim < _scalarsUnsteady.Length; ++dim)
        //        vec[dim] = Scalars[dim][index];

        //    // Unsteady!
        //    vec[NumVectorDimensions - 1] = 1;

        //    return vec;
        //}

        public VectorField GetTimeSlice(int slice)
        {
            return GetSlice(slice);
        }

        //public override VectorField GetSlice(int slice) { return (VectorField)GetTimeSlice(slice); }

    //    public VectorFieldScalarsUnsteady(VectorFieldScalarsUnsteady field, VFJFunction function, int outputDim)
    //    {
    //        int scalars = outputDim;
    //        FieldGrid gridCopy = field._scalarsUnsteady[0].TimeSlices[0].Grid.Copy();
    //        _scalarsUnsteady = new ScalarFieldUnsteady[outputDim];
            
    //        // Reserve the space.
    //        for (int comp = 0; comp < outputDim; ++comp)
    //        {
    //            ScalarField[] fields = new ScalarField[field.Size.T]; //(field.Grid);

    //            for (int t = 0; t < field.Size.T; ++t)

    //            {
    //                fields[t] = new ScalarField(gridCopy);
    //            }

    //            _scalarsUnsteady[comp] = new ScalarFieldUnsteady(fields);
    //            _scalarsUnsteady[comp].TimeOrigin = field.Scalars[0].TimeOrigin ?? 0;
    //            _scalarsUnsteady[comp].InvalidValue = field.InvalidValue;
    //            _scalarsUnsteady[comp].DoNotScale();
    //        }
            
    //        this.InvalidValue = field.InvalidValue;
    //        this.TimeOrigin = field.TimeOrigin;

    //        Grid = field.Grid.Copy();

    //        // Since the time component is in the grid size as well, we do not need to account for time specially.
    //        GridIndex indexIterator = new GridIndex(field.Size);
    //        foreach (GridIndex index in indexIterator)
    //        {
    //            Vector v = field.Sample((int)index);

    //            if (v[0] == InvalidValue)
    //            {
    //                for (int dim = 0; dim < Scalars.Length; ++dim)
    //                    _scalarsUnsteady[dim][(int)index] = (float)InvalidValue;
    //                continue;
    //            }

    //            SquareMatrix J = field.SampleDerivative(index);
    //            Vector funcValue = function(v, J);

    //            for (int dim = 0; dim < Scalars.Length; ++dim)
    //            {
    //                Scalars[dim][(int)index] = funcValue[dim];
    //            }
    //        }
    //    }

    //    public void DoNotScale()
    //    {
    //        foreach (ScalarFieldUnsteady field in _scalarsUnsteady)
    //            field.DoNotScale();
    //    }

    //    public override VectorFieldScalars GetSlicePlanarVelocity(int timeSlice)
    //    {
    //        ScalarField[] slices = new ScalarField[Size.Length - 1];

    //        // Copy the grid - one dimension smaller!
    //        RectlinearGrid grid = Grid as RectlinearGrid;
    //        Index newSize = new Index(Size.Length - 1);
    //        Array.Copy(Size.Data, newSize.Data, newSize.Length);

    //        FieldGrid sliceGrid = new RectlinearGrid(newSize);
    //        for (int i = 0; i < Size.Length - 1; ++i)
    //        {
    //            slices[i] = this._scalarsUnsteady[i].GetTimeSlice(timeSlice);
                
    //            slices[i].TimeOrigin = timeSlice;
    //        }
    //        return new VectorFieldScalars(slices);
    //    }
    //}

    //class VectorFieldUnsteadyAnalytical : VectorFieldUnsteady
    //{
    //    //delegate Vector Evaluate(Vector inVec);
    //    public delegate Vector Evaluate(Vector inVec, SquareMatrix inJ);

    //    protected Evaluate _evaluate;
    //    //protected int _numVectorDimensions = -1;
    //    protected FieldGrid _outGrid;

    //    public VectorFieldUnsteadyAnalytical(Evaluate func, VectorFieldUnsteady field, FieldGrid outGrid, bool useJacobian = false) : base(field.ScalarsAsSFU)
    //    {
    //        _evaluate = func;
    //       // _numVectorDimensions = outDimensions;
    //    }

    //    public override int NumVectorDimensions
    //    {
    //        get
    //        {
    //            return _outGrid.Size.Length;
    //        }
    //    }

    //    public override void ScaleToGrid(Vector scale)
    //    {
    //        base.ScaleToGrid(scale);
    //    }

    //    public override VectorField GetSlice(int slice)
    //    {
    //        return base.GetSlice(slice);
    //    }

    //    public override Field[] Scalars
    //    {
    //        get
    //        {
    //            return base.Scalars;
    //        }
    //    }

    }


    class ScalarFieldUnsteady : ScalarField
    {
        public new float this[int index]
        {
            get { return (float)Data[index]; }
            set { ((VectorBuffer)Data).Data[index] = value; }
        }

        public new float this[Index gridPosition]
        {
            get
            {
                Debug.Assert(gridPosition < Size && gridPosition.IsPositive());

                int offsetScale = 1;
                int index = 0;

                // Have last dimension running fastest.
                for (int dim = 0; dim < NumVectorDimensions; ++dim)
                {
                    index += offsetScale * gridPosition[dim];
                    offsetScale *= Size[dim];
                }

                return this[index];
            }
            protected set
            {
                Debug.Assert(gridPosition < Size && gridPosition.IsPositive());

                int offsetScale = 1;
                int index = 0;

                // Have last dimension running fastest.
                for (int dim = 0; dim < NumVectorDimensions; ++dim)
                {
                    index += offsetScale * gridPosition[dim];
                    offsetScale *= Size[dim];
                }

                this[index] = value;
            }
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
        //public ScalarFieldUnsteady(FieldGrid grid) : base()
        //{
        //    Grid = grid;
        //    _data = new VectorBuffer(grid.Size.Product(), 1);
        //}

        public ScalarFieldUnsteady() { }
        public ScalarFieldUnsteady(ScalarField[] data, float starttime = 0)
        {
            Grid = data[0].Grid;
            Grid.TimeOrigin = starttime;


            VectorBuffer[] raw = new VectorBuffer[data.Length];
            for (int d = 0; d < data.Length; ++d)
                raw[d] = data[d].BufferData;

            Data = new VectorDataArray<VectorBuffer>(raw);
        }
        /// <summary>
        /// Returns a value from the grid.
        /// </summary>
        /// <param name="gridPosition"></param>
        /// <returns></returns>
        public new float Sample(Index gridPosition)
        {
            return this[gridPosition];
        }

        public new float Sample(Vector position)
        {
            return Grid.Sample(this, position)[0];
        }

        public new Vector SampleDerivative(Vector center)
        {
            Vector gradient = new Vector(0, Size.Length);

            for (int dim = 0; dim < gradient.Length; ++dim)
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
        public new Vector SampleDerivative(Index pos)
        {
            Vector gradient = new Vector(NumDimensions);

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
                        gradient[dim] = Sample(samplePos);
                        samplePos[dim] -= 2;
                        gradient[dim] -= Sample(samplePos);
                        gradient[dim] *= 0.5f;
                        samplePos[dim]++;
                    }
                    else
                    {
                        // Left border.
                        samplePos[dim]++;
                        gradient[dim] = Sample(samplePos);
                        samplePos[dim]--;
                        gradient[dim] -= Sample(samplePos);
                    }
                }
                else
                {
                    if (leftValid)
                    {
                        // Right border.
                        gradient[dim] = Sample(samplePos);
                        samplePos[dim]--;
                        gradient[dim] -= Sample(samplePos);
                        samplePos[dim]++;
                    }
                    else
                    {
                        // Weird case. 
                        gradient[dim] = 0;
                    }
                }
                Debug.Assert(posCpy == samplePos[dim]);
            }

            return gradient;
        }

        ///// <summary>
        ///// Function to compute a new field based on an old one, point wise.
        ///// </summary>
        ///// <param name="v"></param>
        ///// <returns></returns>
        //public delegate float SGFunction(float v, Vector J);

        public ScalarFieldUnsteady(ScalarField field, SGFunction function, bool needJ = true)
        {
            Data = new VectorBuffer(field.Size.Product(), 1);
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


        public ScalarFieldUnsteady(AnalyticalField func, Index size, Vector origin, Vector cellSize)
        {
            Debug.Assert(size.Length == origin.Length && size.Length == cellSize.Length);

            Grid = new RectlinearGrid(size);
            Data = new VectorList(Grid.Size.Product());

            for (int idx = 0; idx < size.Product(); ++idx)
            {
                // Compute the n-dimensional position.
                int index = idx;
                Index pos = new Index(0, size.Length);
                pos[0] = index % size[0];

                for (int dim = 1; dim < size.Length; ++dim)
                {
                    index -= pos[dim - 1];
                    index /= size[dim - 1];
                    pos[dim] = index % size[dim];
                }

                Vector posV = origin + pos * cellSize;
                this[idx] = func(posV);
            }
        }
    }

    //class ScalarFieldUnsteady : VectorFieldUnsteady
    //{
    //    public new float this[int index]
    //    {
    //        get { return (float)_data[index]; }
    //        set { ((VectorBuffer)_data).Data[index] = value; }
    //    }

    //    public new float this[Index gridPosition]
    //    {
    //        get
    //        {
    //            Debug.Assert(gridPosition < Size && gridPosition.IsPositive());

    //            int offsetScale = 1;
    //            int index = 0;

    //            // Have last dimension running fastest.
    //            for (int dim = 0; dim < NumVectorDimensions; ++dim)
    //            {
    //                index += offsetScale * gridPosition[dim];
    //                offsetScale *= Size[dim];
    //            }

    //            return this[index];
    //        }
    //        protected set
    //        {
    //            Debug.Assert(gridPosition < Size && gridPosition.IsPositive());

    //            int offsetScale = 1;
    //            int index = 0;

    //            // Have last dimension running fastest.
    //            for (int dim = 0; dim < NumVectorDimensions; ++dim)
    //            {
    //                index += offsetScale * gridPosition[dim];
    //                offsetScale *= Size[dim];
    //            }

    //            this[index] = value;
    //        }
    //    }

    //    //private float? _timeSlice = null;
    //    //public override float? TimeSlice
    //    //{
    //    //    get
    //    //    {
    //    //        return Grid.TimeOrigin;
    //    //    }

    //    //    set
    //    //    {
    //    //        Grid.TimeOrigin = value;
    //    //    }
    //    //}

    //    /// <summary>
    //    /// Instanciate a new field. The dimension is derived from the fields size.
    //    /// </summary>
    //    /// <param name="fieldSize">Number of grid edges in each dimension.</param>
    //    //public ScalarFieldUnsteady(FieldGrid grid) : base()
    //    //{
    //    //    Grid = grid;
    //    //    _data = new VectorBuffer(grid.Size.Product(), 1);
    //    //}

    //    public ScalarFieldUnsteady() { }
    //    public ScalarFieldUnsteady(ScalarField[] data)
    //    {
    //        Grid = data[0].Grid;


    //        VectorBuffer[] raw = new VectorBuffer[data.Length];
    //        for (int d = 0; d < data.Length; ++d)
    //            raw[d] = data[d].Data;

    //        _data = new VectorDataArray<VectorBuffer>(raw);
    //    }
    //    /// <summary>
    //    /// Returns a value from the grid.
    //    /// </summary>
    //    /// <param name="gridPosition"></param>
    //    /// <returns></returns>
    //    public new float Sample(Index gridPosition)
    //    {
    //        return this[gridPosition];
    //    }

    //    public new float Sample(Vector position)
    //    {
    //        return Grid.Sample(this, position)[0];
    //    }

    //    public new Vector SampleDerivative(Vector center)
    //    {
    //        Vector gradient = new Vector(0, Size.Length);

    //        for (int dim = 0; dim < gradient.Length; ++dim)
    //        {
    //            float stepPos = Math.Min(Size[dim] - 1, center[dim] + 0.5f) - center[dim];
    //            float stepMin = center[dim] - Math.Max(0, center[dim] - 0.5f);
    //            Vector samplePos = new Vector(center);
    //            samplePos[dim] += stepPos;
    //            gradient[dim] = Sample(samplePos);

    //            samplePos[dim] += stepMin - stepPos;
    //            gradient[dim] -= Sample(samplePos);

    //            gradient[dim] /= (stepPos - stepMin);
    //        }

    //        return gradient;
    //    }

    //    /// <summary>
    //    /// Get the derivative at a data point. Not checking for InvalidValue.
    //    /// </summary>
    //    /// <param name="pos"></param>
    //    /// <returns></returns>
    //    public new Vector SampleDerivative(Index pos)
    //    {
    //        Vector gradient = new Vector(NumDimensions);

    //        // For all dimensions, so please reset each time.
    //        Index samplePos = new Index(pos);

    //        for (int dim = 0; dim < NumDimensions; ++dim)
    //        {
    //            // Just to be sure, check thst no value was overwritten.
    //            int posCpy = samplePos[dim];

    //            // See whether a step to the right/left is possible.
    //            samplePos[dim]++;
    //            bool rightValid = (samplePos[dim] < Size[dim]) && Sample(samplePos) != InvalidValue;
    //            samplePos[dim] -= 2;
    //            bool leftValid = (samplePos[dim] >= 0) && Sample(samplePos) != InvalidValue;
    //            samplePos[dim]++;

    //            if (rightValid)
    //            {
    //                if (leftValid)
    //                {
    //                    // Regular case. Interpolate.
    //                    samplePos[dim]++;
    //                    gradient[dim] = Sample(samplePos);
    //                    samplePos[dim] -= 2;
    //                    gradient[dim] -= Sample(samplePos);
    //                    gradient[dim] *= 0.5f;
    //                    samplePos[dim]++;
    //                }
    //                else
    //                {
    //                    // Left border.
    //                    samplePos[dim]++;
    //                    gradient[dim] = Sample(samplePos);
    //                    samplePos[dim]--;
    //                    gradient[dim] -= Sample(samplePos);
    //                }
    //            }
    //            else
    //            {
    //                if (leftValid)
    //                {
    //                    // Right border.
    //                    gradient[dim] = Sample(samplePos);
    //                    samplePos[dim]--;
    //                    gradient[dim] -= Sample(samplePos);
    //                    samplePos[dim]++;
    //                }
    //                else
    //                {
    //                    // Weird case. 
    //                    gradient[dim] = 0;
    //                }
    //            }
    //            Debug.Assert(posCpy == samplePos[dim]);
    //        }

    //        return gradient;
    //    }

    //    /// <summary>
    //    /// Function to compute a new field based on an old one, point wise.
    //    /// </summary>
    //    /// <param name="v"></param>
    //    /// <returns></returns>
    //    public delegate float SGFunction(float v, Vector J);

    //    public ScalarFieldUnsteady(ScalarField field, SGFunction function, bool needJ = true)
    //    {
    //        _data = new VectorBuffer(field.Size.Product(), 1);
    //        Grid = field.Grid;

    //        this.InvalidValue = field.InvalidValue;

    //        GridIndex indexIterator = new GridIndex(field.Size);
    //        foreach (GridIndex index in indexIterator)
    //        {
    //            float s = field[(int)index];

    //            if (s == InvalidValue)
    //            {
    //                this[(int)index] = (float)InvalidValue;
    //            }
    //            else
    //            {
    //                Vector g = needJ ? field.SampleDerivative(index) : new Vec2(0, 0);
    //                this[(int)index] = function(s, g);
    //            }
    //        }
    //    }

    //    public delegate float AnalyticalField(Vector pos);

    //    public static ScalarField FromAnalyticalField(AnalyticalField func, Index size, Vector origin, Vector cellSize)
    //    {
    //        Debug.Assert(size.Length == origin.Length && size.Length == cellSize.Length);

    //        RectlinearGrid grid = new RectlinearGrid(size);
    //        ScalarField field = new ScalarField(grid);

    //        for (int idx = 0; idx < size.Product(); ++idx)
    //        {
    //            // Compute the n-dimensional position.
    //            int index = idx;
    //            Index pos = new Index(0, size.Length);
    //            pos[0] = index % size[0];

    //            for (int dim = 1; dim < size.Length; ++dim)
    //            {
    //                index -= pos[dim - 1];
    //                index /= size[dim - 1];
    //                pos[dim] = index % size[dim];
    //            }

    //            Vector posV = origin + pos * cellSize;
    //            field[idx] = func(posV);
    //        }

    //        return field;
    //    }

    //    public void ComputeStatistics(out float validRegion, out float mean, out float sd)
    //    {
    //        int numValidCells = 0;
    //        mean = 0;
    //        sd = 0;

    //        GridIndex range = new GridIndex(Size);
    //        foreach (GridIndex idx in range)
    //        {
    //            float s = this[(int)idx];
    //            if (s != InvalidValue)
    //            {
    //                numValidCells++;
    //                mean += s;
    //            }
    //        }
    //        validRegion = (float)numValidCells / Size.Product();
    //        mean /= numValidCells;

    //        // Compute standard derivative.
    //        range.Reset();
    //        foreach (GridIndex idx in range)
    //        {
    //            float s = this[(int)idx];
    //            if (s != InvalidValue)
    //            {
    //                float diff = s - mean;
    //                sd += diff * diff;
    //            }
    //        }
    //        sd /= numValidCells;
    //        sd = (float)Math.Sqrt(sd);
    //    }
    //}
}
