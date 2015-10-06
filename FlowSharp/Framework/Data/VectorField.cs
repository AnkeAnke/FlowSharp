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

        public Vector Sample(Vector position, bool worldPosition = true)
        {
            return Grid.Sample(this, position, worldPosition);
        }


        /// <summary>
        /// Function to compute a new field based on an old one, point wise.
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public delegate Vector VFFunction(Vector v);

        public delegate Vector VFJFunction(Vector v, Vector du, Vector dv);

        public VectorField(VectorField field, VFJFunction function)
        {
            int scalars = function(field.Sample(0), field.Sample(0), field.Sample(0)).Length;
            this.Grid = field.Grid;
            this.Scalars = new ScalarField[scalars];
            for(int dim = 0; dim < scalars; ++dim)
            {
                Scalars[dim] = new ScalarField(Grid);
            }

            // Let's assume the field is always 2D... 
            //TODO: Make nD
            for(int x = 0; x < Size[0]; ++x)
                for(int y = 0; y < Size[1]; ++y)
                {
                    Vector v = field.Sample(y * Size[0] + x);
                    Vector pos = new Vec2(x, y);

                }
        }
    }
}

// &PARM04
// ygOrigin = 9.0,
// xgOrigin = 32.0,
// delY   =  210*0.1,
// delX   =  450*0.1,