using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class RedSea
    {
        /// <summary>
        /// Relevant variables of Read Sea file.
        /// </summary>
        public enum Variable : int
        {
            TIME = 3,
            GRID_X = 5,
            CENTER_X = 6,
            GRID_Y = 7,
            CENTER_Y = 8,
            GRID_Z = 9,
            CENTER_Z = 10,
            SALINITY = 11,
            TEMPERATURE = 12,
            VELOCITY_X = 13,
            VELOCITY_Y = 14,
            SURFACE_HEIGHT = 15
        }

        public enum Dimension : int
        {
            MEMBER = 2,
            TIME = 3,
            GRID_X = 8,
            CENTER_X = 9,
            GRID_Y = 10,
            CENTER_Y = 11,
            GRID_Z = 12,
            CENTER_Z = 13
        }
    }
}
