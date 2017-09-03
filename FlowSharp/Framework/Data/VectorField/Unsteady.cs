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
        public float TimeScale = 1.0f;
        public virtual float TimeEnd { get { return (TimeOrigin ?? 0) + Size.T * TimeScale; } }
        public float? TimeOrigin
        {
            get { return Grid.TimeDependant ? (float?)Grid.TimeOrigin : null; }
            set { Grid.TimeOrigin = value; }
        }

        public VectorFieldUnsteady(VectorData data, FieldGrid grid, int numSlices, float timeOrigin = 0)
        {
            //TimeScale = timeScale;
            Debug.Assert(grid.TimeDependant);
            Data = data;
            Grid = grid.GetAsTimeGrid(numSlices, timeOrigin);
        }

        public VectorFieldUnsteady(VectorField field, float timeScale = 1.0f)
        {
            TimeScale = timeScale;
            Debug.Assert(field.Grid.TimeDependant);
            Data = field.Data;
            Grid = field.Grid;
        }

        public VectorFieldUnsteady(ScalarFieldUnsteady[] data, float timeScale = 1.0f)
        {
            TimeScale = timeScale;
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
        public override int NumVectorDimensions { get { return Data.VectorLength + 1; } }

        public override void ScaleToGrid(Vector scale)
        {
            Debug.Assert(Data.VectorLength == scale.Length);
            for (int dim = 0; dim < Data.VectorLength; ++dim)
            {
                ScaleToGrid(scale[dim]);
            }
            SpreadInvalidValue();
        }

        public VectorField GetTimeSlice(int slice)
        {
            return GetSlice(slice);
        }
    }

    class VectorFieldInertialUnsteady : VectorFieldUnsteady
    {
        public VectorFieldInertial[] TimeSteps;

        public override float TimeEnd
        {
            get
            {
                return (TimeOrigin ?? 0) + TimeSteps.Length * TimeScale;
            }
        }

        public VectorFieldInertialUnsteady(VectorFieldInertial[] fields, float timeScale = 1.0f)
        {
            TimeScale = timeScale;
            Data = null;
            TimeSteps = fields;
            Grid = fields[0].Grid.GetAsTimeGrid(fields.Length, fields[0].Grid.TimeOrigin ?? 0);
        }
        public VectorFieldInertialUnsteady(VectorData[] data, float inertia, FieldGrid grid, float timeOrigin = 0, float timeScale = 1.0f)
        {
            TimeScale = timeScale;
            Data = null;
            TimeSteps = new VectorFieldInertial[data.Length];
            for (int f = 0; f < data.Length; ++f)
                TimeSteps[f] = new VectorFieldInertial(data[f], grid, inertia);
            Grid = grid.GetAsTimeGrid(data.Length, timeOrigin);
        }


        public override int NumVectorDimensions { get { return TimeSteps[0].NumVectorDimensions + 1; } }

        public override void ScaleToGrid(Vector scale)
        {
            Debug.Assert(Data.VectorLength == scale.Length);
            for (int dim = 0; dim < Data.VectorLength; ++dim)
            {
                ScaleToGrid(scale[dim]);
            }
            SpreadInvalidValue();
        }

        public VectorField GetTimeSlice(int slice)
        {
            return TimeSteps[slice];
        }

        public override VectorRef Sample(Index gridPosition)
        {
            return Vector.ToUnsteady(TimeSteps[gridPosition.T].Sample(gridPosition.ToIntX(gridPosition.Length - 1)));
        }

        public override Vector Sample(Vector position, Vector lastDirection)
        {
            if (position == null) Console.WriteLine(position);

            float time = position.T - (float)TimeOrigin;
            time /= TimeScale;
            int timeStep = (int)time;

            if (timeStep < 0 || timeStep >= TimeSteps.Length - 1)
            {
                Console.WriteLine($"{time} not within [0, {TimeSteps.Length})");
                Console.WriteLine($"\tsince {position.T} not within [TimeOrigin, {TimeEnd})");
                return null;
            }

            time = time - timeStep;
            Vector spatial = position.ToVec(position.Length - 1);

            Vector sample0 = TimeSteps[timeStep].Sample(spatial, lastDirection);
            Vector sample1 = TimeSteps[timeStep + 1].Sample(spatial, lastDirection);
            if (sample0 == null || sample1 == null)
                return null;

            return Vector.ToUnsteady((1f - time) * sample0 + time * sample1);
        }

        public override bool IsUnsteady()
        {
            return true;
        }

        public override void ScaleToGrid(float dimwiseScale)
        {
            foreach (var field in TimeSteps)
                field.ScaleToGrid(dimwiseScale);
        }
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
