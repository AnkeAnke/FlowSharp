using SlimDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    static class UtilTet
    {
        public static bool ToBaryCoord(VectorData vertices, IndexData cells, int cell, Vector3 worldPos, out Vector4 bary)
        {
            return ToBaryCoord(vertices, cells[cell], worldPos, out bary);
        }

        public static bool ToBaryCoord(VectorData vertices, Index cell, Vector3 worldPos, out Vector4 bary)
        {
            Matrix tet = new Matrix();
            for (int i = 0; i < 4; ++i)
            {
                tet.set_Columns(i, new Vector4(vertices[cell[i]][0], vertices[cell[i]][1], vertices[cell[i]][2], 1));
            }

            bary = Vector4.Zero;
            float d0 = tet.Determinant();

            // Go over all corner points and exchange them with the sample position.
            // If sign of determinant is the same as of the original, cube, the point is on the same side.
            for (int i = 0; i < 4; ++i)
            {
                Matrix mi = tet;
                mi.set_Columns(i, new Vector4((Vector3)worldPos, 1));
                bary[i] = mi.Determinant() / d0;
                if (bary[i] <= 0)
                {
                    //PROF_BARY.Stop();
                    return false;
                }
            }
            float barySum = bary.Sum();
            return true;
        }

        public static SquareMatrix Jacobian(VectorData vertices, VectorData vertexValues, IndexData cells, int cell)
        {
            VectorRef originFunction = vertexValues[cells[cell][0]];
            VectorRef originPosition = vertices[cells[cell][0]];

            SquareMatrix function = new SquareMatrix(new Vector[] {
                vertexValues[cells[cell][1]] - originFunction,
                vertexValues[cells[cell][2]] - originFunction,
                vertexValues[cells[cell][3]] - originFunction });

            SquareMatrix direction = new SquareMatrix(new Vector[] {
                vertices[cells[cell][1]] - originPosition,
                vertices[cells[cell][2]] - originPosition,
                vertices[cells[cell][3]] - originPosition });

            return function * direction.Inverse();
        }

        public static SquareMatrix Jacobian(VectorData vertices, VectorData vertexValues, Index cell)
        {
            VectorRef originFunction = vertexValues[cell[0]];
            VectorRef originPosition = vertices[cell[0]];

            SquareMatrix function = new SquareMatrix(new Vector[] {
                vertexValues[cell[1]] - originFunction,
                vertexValues[cell[2]] - originFunction,
                vertexValues[cell[3]] - originFunction });

            SquareMatrix direction = new SquareMatrix(new Vector[] {
                vertices[cell[1]] - originPosition,
                vertices[cell[2]] - originPosition,
                vertices[cell[3]] - originPosition });

            SquareMatrix result =  function * direction.Inverse();

            //Console.WriteLine("Index " + cell);
            //Console.WriteLine("Function:\n" + function);
            //Console.WriteLine("\nDirections:\n" + direction);
            //Console.WriteLine("\nInverse Direction:\n" + direction.Inverse());

            //Console.WriteLine("\nControl Identity:\n" + direction * direction.Inverse());
            //Console.WriteLine("\nResult:\n" + result);
            return result;
        }
    }
}
