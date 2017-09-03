using SlimDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using PointSet = FlowSharp.PointSet<FlowSharp.Point>;

namespace FlowSharp
{
    class Aneurysm : DataContextVariant
    {
        //public delegate string FilenameBuilder(int step, Aneurysm.Variable var = Variable.VELOCITY);

        //public FilenameBuilder GetFilename;
        public string EnsightFolderFilename;
        public string EnsightGeoFilename;
        public string EnsightFilename;
        public string SnapFileName;

        public string VtuFolderFilename;
        public string VtuDataFilename;

        public string OctreeFolderFilename;

        public int NumSteps = 200;
        public float TimeScale { get { return 0.005f; } }
        /// <summary>
        /// Relevant variables of Read Sea file.
        /// </summary>
        public enum Variable : int
        {
            pressure = 1,
            wall_shear = 2,
            x_wall_shear = 3,
            y_wall_shear = 4,
            z_wall_shear = 5,
            velocity = 0
        }

        private string[] Names =
        {
            "Velocity",
            "Static Pressure",
            "Wall Shear Stress",
            "X-Wall Shear Stress",
            "Y-Wall Shear Stress",
            "Z-Wall Shear Stress"
        };

        public string VariableName(Variable variable)
        {
            return Names[(int)variable];
        }

        public enum Dimension : int
        {
            TIME = 0
        }

        public enum Display : int
        {
            NONE,
            VIEW_TETRAHEDRONS,
            VIEW_HEXAHEDRONS
            //MEMBER_COMPARISON,
            ////SUBSTEP_VIEWER,
            //CP_TRACKING,
            //PATHLINE_CORES,            
            //OKUBO_WEISS,
            //FLOW_MAP_UNCERTAIN,
            //PATHLINE_LENGTH,
            //CUT_DIFFUSION_MAP,
            //LOCAL_DIFFUSION_MAP,
            //PATHLINE_RADIUS,
            //LINE_STATISTICS,
            //SUBSTEP_VIEWER,
            //DONUT_ANALYSIS,
            //CORE_DISTANCE,
            //PREDICTOR_CORE_ANGLE,
            //CONCENTRIC_DISTANCE,
            //FTLE_CONCENTRIC,
            //PATHLINE_DISTANCE,
            //CONCENTRIC_TUBE,
            //PLAYGROUND
        }

        public enum DisplayLines : int
        {
            LINE,
            POINTS_2D_LENGTH,
            INVALIDATE_LINE,
            STOP_LINE
        }

        public enum DisplayTracking : int
        {
            FIELD,
            POINTS,
            LINE,
            LINE_POINTS,
            LINE_SELECTION
        }
        public enum Measure : int
        {
            pressure = 1,
            wall_shear = 2,
            x_wall_shear = 3,
            y_wall_shear = 4,
            z_wall_shear = 5,
            velocity = 0
        }

        public enum GeometryPart : int
        {
            Solid   = 1,
            Wall    = 2,
            Inlet   = 3,
            Outlet1 = 4,
            Outlet2 = 5,
            Outlet3 = 6
        }
        public MainWindow WPFWindow { get; set; }

        public static VectorField.PositionToColor[] DisplayLineFunctions = new VectorField.PositionToColor[]
        {
            null,
            (f, x) => new Vector3(f.Sample((Vec4)x).ToVec2().LengthEuclidean() * 10)
        };

        private static Aneurysm _instance;
        public static Aneurysm Singleton {
            get
            {
                if (_instance == null)
                    _instance = new Aneurysm();
                return _instance;
            } }

        private int _numTImeSlices = 10;
        public int NumTimeSlices
        {
            get { return _numTImeSlices; }
            set { _numTImeSlices = value; WPFWindow?.UpdateNumTimeSlices(); }
        }


        private DataMapper[] _mappers;
        private Display _currentMapper = Display.NONE;
        public Display Mapper { get { return _currentMapper; } }

        public string GridFilename { get
            {
                return EnsightFolderFilename + EnsightGeoFilename;
                //if (_mappers[(int)_currentMapper] == null)
                //    return "Default";
                //string name = _currentMapper.ToString();
                //name += '_' + _mappers[(int)_currentMapper].CurrentSetting.GetFilename();
                //return name;
                    } }
        private string EnsightVariableFilename(Variable variable, int slice)
        {
            string filename = $"ruptured ({slice+1}).";
            if (variable == Variable.velocity)
                filename += "vel";
            else
                filename += "scl" + (int)variable;
            return filename;   
        }
        public string EnsightVariableFileName(Variable variable, int slice)
        {
                return EnsightFolderFilename + EnsightVariableFilename(variable, slice);
        }

        public string VtuCompleteFilename(int timestep, GeometryPart geom)
        {
            return VtuFolderFilename + VtuDataFilename + (((int)geom)-1) + "_0.vtu";
        }

        public string OctreeFilename(int maxVerts, int maxLevels, GeometryPart part)
        {
            return OctreeFolderFilename + part + "_vert" + maxVerts + "_lvl" + maxLevels + ".octree";
        }

        public string CustomAttributeFilename(string custom, GeometryPart part)
        {
            return OctreeFolderFilename + "Attribute/" + custom + "_" + part + ".attribute";
        }

        private Aneurysm()
        {
            _mappers = new DataMapper[Enum.GetValues(typeof(Display)).Length];
            _mappers[0] = new EmptyMapper();
        }

        public void SetMapper(Display preset, DataMapper mapper)
        {
            _mappers[(int)preset] = mapper;
        }

        public DataMapper SetMapper(Display preset)
        {
            _currentMapper = preset;
            return _mappers[(int)_currentMapper];
        }

        public void Update()
        {
            Renderer.Singleton.ClearRenderables();
            _mappers[(int)_currentMapper].UpdateMapping();
        }

        public void UpdateSelection()
        {
            _mappers[(int)_currentMapper].UpdateSelection();
        }

        public void EndSelection()
        {
            _mappers[(int)_currentMapper].OnRelease();
            Update();
        }
    }
}
