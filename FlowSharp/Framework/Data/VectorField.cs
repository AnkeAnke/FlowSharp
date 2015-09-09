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
        /// Pun. TODO: Better.
        /// </summary>
        /// <param name="fields"></param>
        public VectorField(ScalarField[] fields)
        {
            Scalars = fields;
            Grid = fields[0].Grid;
        }

        /// <summary>
        /// Access field by scalar index.
        /// </summary>
        public Vector Sample(int index)
        {
            Vector vec = new Vector(Scalars.Length);
            for (int dim = 0; dim < V; ++dim)
                vec[dim] = Scalars[dim].Data[index];

            return vec;
        }

        /// <summary>
        /// Access field by N-dimensional index.
        /// </summary>
        public Vector Sample(Index gridPosition)
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

            return Sample(index);
        }

        public Vector Sample(Vector position)
        {
            return Grid.Sample(this, position);
        }
    }
}

// &PARM04
// ygOrigin = 9.0,
// xgOrigin = 32.0,
// delY   =  210*0.1,
// delX   =  450*0.1,