﻿using SlimDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using PointSet = FlowSharp.PointSet<FlowSharp.Point>;

namespace FlowSharp
{
    class RedSea
    {
        /// <summary>
        /// Data folder name, in a fashion that only a number has to be added for the respective time slice.
        /// </summary>
        public string DataFolder;
        /// <summary>
        /// File name. Should contain intial '/'.
        /// </summary>
        public string FileName;
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

        public enum Display : int
        {
            NONE,
            CP_TRACKING,
            PATHLINE_CORES,
            MEMBER_COMPARISON,
            OKUBO_WEISS,
            FLOW_MAP_UNCERTAIN
        }

        public enum DisplayLines : int
        {
            LINE,
            POINTS_2D_LENGTH
        }
        public static VectorField.PositionToColor[] DisplayLineFunctions = new VectorField.PositionToColor[]
        {
            null,
            (f, world, x) => new Vector3(f.Sample((Vec3)x, world).ToVec2().LengthEuclidean() * 10)
        };

        private static RedSea _instance;
        public static RedSea Singleton {
            get
            {
                if (_instance == null)
                    _instance = new RedSea();
                return _instance;
            } }

        private DataMapper[] _mappers;
        private Display _currentMapper = Display.NONE;

        private RedSea()
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

        //    // Depending on display and slice0 setting.
        //    protected DisplaySet[] _displayPresets;
        //    protected Renderable[] _slice1;
        //    protected Display _preset;

        //    public void SetPresets(DisplaySet[] presets)
        //    {
        //        _displayPresets = presets;
        //    }

        //    //protected List<Renderable> _currentDisplay;


        //    public void SetPreset(Display preset, int slice0, DisplayLines lineSetting)
        //    {
        //        Debug.Assert(Renderer.Singleton.Initialized);
        //        Renderer.Singleton.ClearRenderables();
        //        // Check if the renderables have been created yet.
        //        if (_displayPresets[(int)preset] != null)
        //        {
        //            Renderer.Singleton.AddRenderables(_displayPresets[(int)preset].CreateRenderablesSelected(slice0, lineSetting).ToList());
        //        }

        //        if (_slice1 != null)
        //            Renderer.Singleton.AddRenderables(_slice1.ToList());
        //        _preset = preset;
        //    }
        //    public void SetPreset(Display preset, int slice1)
        //    {
        //        if (_slice1 != null)
        //            foreach (Renderable obj in _slice1)
        //                Renderer.Singleton.Remove(obj);
        //        if (_displayPresets[(int)preset] == null)
        //            return;
        //        _slice1 = _displayPresets[(int)preset].CreateRenderablesReference(slice1);
        //        Renderer.Singleton.AddRenderables(_slice1.ToList());
        //    }
        //}

        //class DisplaySet
        //{
        //    public class FieldData
        //    {
        //        public VectorField Field;
        //        /// <summary>
        //        /// Those points will only be displayed when all properties are shown.
        //        /// </summary>
        //        public PointSet[] SelectedPoints;
        //        /// <summary>
        //        /// These points will be shown when the instance is displayed, but not selected.
        //        /// </summary>
        //        public PointSet[] ReferencePoints;
        //        public LineSet[] Lines;

        //        public FieldData(VectorField field, PointSet<Point>[] selectedPoints, PointSet[] staticPoints, LineSet[] lines)
        //        {
        //            Field = field;
        //            SelectedPoints = selectedPoints;
        //            ReferencePoints = staticPoints;
        //            Lines = lines;
        //        }

        //        public FieldData(VectorField field)
        //        {
        //            Field = field;
        //        }
        //    }
        //    private Renderable[][] _staticRenderables; // Containing field plane etc.
        //    private FieldData[] _rawData;
        //    public Plane Plane;
        //    public VectorField Field;

        //    public VectorField GetField(int index) { return _rawData[Math.Min(_rawData.Length-1, index)].Field; }

        //    public DisplaySet(FieldData[] data, Plane plane, VectorField field)
        //    {
        //        _rawData = data;
        //        _staticRenderables = new Renderable[data.Length][];
        //        Plane = plane;
        //        Field = field;
        //    }
        //    protected void GenerateStaticRenderables()
        //    {
        //        if (_staticRenderables[0] != null)
        //            return;

        //        for (int f = 0; f < _staticRenderables.Length; f++)
        //        {
        //            _staticRenderables[f] = new Renderable[1];
        //            _staticRenderables[f][0] = new FieldPlane(Plane, _rawData[f].Field, FieldPlane.RenderEffect.LIC);
        //        }
        //    }
        //    public Renderable[] CreateRenderablesSelected(int field, RedSea.DisplayLines lineSetting, FieldPlane.RenderEffect effect = FieldPlane.RenderEffect.LIC)
        //    {
        //        GenerateStaticRenderables();

        //        FieldData data = _rawData[field];
        //        Renderable[] allObjects = new Renderable[_staticRenderables[field].Length + data.Lines.Length + data.SelectedPoints.Length];

        //        for(int p = 0; p < data.SelectedPoints.Length; ++p)
        //        {
        //            allObjects[p] = new PointCloud(Plane, data.SelectedPoints[p]);
        //        }

        //        // Depending on Settings, create a different renderable.
        //        for(int l = 0; l < data.Lines.Length; ++l)
        //        {
        //            Renderable line;
        //            if(lineSetting != RedSea.DisplayLines.LINE)
        //            {
        //                PointSet<Point> points = Field.ColorCodeArbitrary(data.Lines[l], RedSea.DisplayLineFunctions[(int)lineSetting]);
        //                line = new PointCloud(Plane, points);
        //            }
        //            else
        //            {
        //                line = new LineBall(Plane, data.Lines[l]);
        //            }
        //            allObjects[data.SelectedPoints.Length + l] = line;
        //        }

        //        // Simply copy static renderables.
        //        Array.Copy(_staticRenderables[field], 0, allObjects, data.SelectedPoints.Length + data.Lines.Length, _staticRenderables[field].Length);

        //        //allObjects[allObjects.Length - 1] = new FieldPlane(Plane, data.Field, effect);

        //        return allObjects;

        //    }

        //    public Renderable[] CreateRenderablesReference(int field, FieldPlane.RenderEffect effect = FieldPlane.RenderEffect.LIC)
        //    {
        //        GenerateStaticRenderables();
        //        // Set the Point size to be smaller.
        //        float pointSize = Plane.PointSize;
        //        Plane.PointSize *= 0.5f;

        //        FieldData data = _rawData[field];
        //        Renderable[] allObjects = new Renderable[_staticRenderables[field].Length + data.ReferencePoints.Length];

        //        for (int p = 0; p < data.ReferencePoints.Length; ++p)
        //        {
        //            allObjects[p] = new PointCloud(Plane, data.ReferencePoints[p]);
        //        }

        //        // Simply copy static renderables.
        //        Array.Copy(_staticRenderables[field], 0, allObjects, data.ReferencePoints.Length, _staticRenderables[field].Length);
        //        Plane.PointSize = pointSize;

        //        return allObjects;
        //    }
    }
}
