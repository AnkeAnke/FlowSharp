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

        public int NumSteps = 200;

        public float DomainScale = 1.0f; //2.593f / 15;
        public float TimeScale { get { return 1.0f/DomainScale; } }
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
            "Wall Shear Stress X",
            "Wall Shear Stress Y",
            "Wall Shear Stress Z"
        };

        public string VariableName(Variable variable)
        {
            return Names[(int)variable];
        }

        //private static Dictionary<Variable, string> _variableShort = new Dictionary<Variable, string>()
        //{
        //    {Variable.TIME, "T" },
        //    {Variable.GRID_X,"GX"},
        //    {Variable.CENTER_X,"CX"},
        //    {Variable.GRID_Y,"GY"},
        //    {Variable.CENTER_Y,"CY"},
        //    {Variable.GRID_Z,"GZ"},
        //    {Variable.CENTER_Z,"CZ"},
        //    {Variable.SALINITY, "S"},
        //    {Variable.TEMPERATURE, "T"},
        //    {Variable.VELOCITY_X, "U"},
        //    {Variable.VELOCITY_Y, "V"},
        //    {Variable.VELOCITY_Z, "W"},
        //    {Variable.SURFACE_HEIGHT, "Eta"}
        //};

        //public static string GetShortName(Variable var)
        //{
        //    return _variableShort[var];
        //}

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
            (f, x) => new Vector3(f.Sample((Vec3)x).ToVec2().LengthEuclidean() * 10)
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
            string filename = "0" + (401 + slice) + '.';
            if ((int)variable > 0)
                filename += "scl" + (int)variable;
            else
                filename += "vel";
            return filename;   
        }
        public string EnsightVariableFileName(Variable variable, int slice)
        {
                return EnsightFolderFilename + EnsightVariableFilename(variable, slice);
                //if (_mappers[(int)_currentMapper] == null)
                //    return "Default";
                //string name = _currentMapper.ToString();
                //name += '_' + _mappers[(int)_currentMapper].CurrentSetting.GetFilename();
                //return name;
        }

        public string VtuCompleteFilename(int timestep, GeometryPart geom)
        {
            return VtuFolderFilename + timestep + '/' + VtuDataFilename + timestep + '_' + (((int)geom)-1) + "_0.vtu";
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
