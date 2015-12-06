using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SlimDX;
using System.Runtime.InteropServices;

namespace FlowSharp
{
    class FlowMapMapper : SelectionMapper
    {
        private FieldPlane _currentState;
        private FlowMapUncertain _flowMap;
        private VectorFieldUnsteady _velocity;

        public FlowMapMapper(Loader.SliceRange[] uv, Plane plane, VectorFieldUnsteady velocity) : base(plane, velocity.Size.ToInt2())
        {
            _flowMap = new FlowMapUncertain(_startPoint, uv, 0, 9);
            _algorithm = _flowMap;
            Plane = plane;
            _subrangePlane = Plane;
            Mapping = GetCurrentMap;
            _velocity = velocity;
            _maxPlane = velocity.Size.ToInt2();
        }

        /// <summary>
        /// If different planes were chosen, load new fields.
        /// </summary>
        /// <returns></returns>
        public List<Renderable> GetCurrentMap()
        {
            if (_lastSetting == null ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain)
            {
                if (_lastSetting == null || _currentSetting.SliceTimeMain < _flowMap.CurrentTime)
                {
                    _flowMap.SetupPoint(_startPoint, _currentSetting.SliceTimeMain);
                }

                // Integrate to the desired time step.
                while (_flowMap.CurrentTime < _currentSetting.SliceTimeMain)
                    _flowMap.Step(_currentSetting.StepSize);

                //.GetPlane(Plane);
                //_currentState = _flowMap.GetPlane(Plane);

            }
            if (_lastSetting == null ||
                _currentSetting.StepSize != _lastSetting.StepSize)
            {
            }
            if (_lastSetting == null ||
                _currentSetting.Colormap != _lastSetting.Colormap ||
                _currentSetting.Shader != _lastSetting.Shader ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain)
            {
                RefreshPlane();
                _currentState.LowerBound = 0;
                _currentState.UpperBound = _currentSetting.WindowWidth;
            }
            if (_lastSetting == null ||
                _lastSetting.WindowWidth != _currentSetting.WindowWidth)
            {
                _currentState.LowerBound = 0;
                _currentState.UpperBound = _currentSetting.WindowWidth;
            }
            List<Renderable> list = new List<Renderable>(1);
            list.Add(_currentState);
            return list;
        }

        private void RefreshPlane()
        {
            switch (_currentSetting.Shader)
            {
                case FieldPlane.RenderEffect.LIC:
                case FieldPlane.RenderEffect.LIC_LENGTH:
                    var tmp = _velocity.GetTimeSlice(_currentSetting.SliceTimeMain);
                    tmp.TimeSlice = null;
                    _currentState = new FieldPlane(_subrangePlane, tmp, _currentSetting.Shader, _currentSetting.Colormap);
                    _currentState.AddScalar(_flowMap.FlowMap);
                    break;
                default:
                    _currentState = _flowMap.GetPlane(_subrangePlane);
                    _currentState.UsedMap = _currentSetting.Colormap;
                    _currentState.SetRenderEffect(_currentSetting.Shader);
                    break;
            }
        }

        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.Colormap:
                case Setting.Element.WindowWidth:
                //return _currentSetting.Shader == FieldPlane.RenderEffect.COLORMAP || _currentSetting.Shader == FieldPlane.RenderEffect.DEFAULT;
                case Setting.Element.SliceTimeMain:
                case Setting.Element.Shader:
                case Setting.Element.StepSize:
                    return true;
                default:
                    return false;
            }
        }

        public override void EndSelection(Vector2[] points)
        {
            base.EndSelection(points);

            _velocity = _flowMap.LoadMeanField();
            RefreshPlane();

            int size = _velocity.Size.ToInt2().Product();
            float fill, mean, sd;
            _velocity.ScalarsAsSFU[0].GetTimeSlice(0).ComputeStatistics(out fill, out mean, out sd);
            Console.WriteLine("Region:\n\tRegion: " + size + "\n\tValid Part: " + fill + "\n\tValid Cells: " + fill * size);

        }

        public override void ClickSelection(Vector2 point)
        {
            base.ClickSelection(point);
            _flowMap.SetupPoint(_startPoint, _currentSetting.SliceTimeMain);
            RefreshPlane();
        }
    }
    class DiffusionMapper : SelectionMapper
    {
        private FieldPlane[] _dataMap;
        private Plane _scaledPlane;
        private CutDiffusion _diffusionMap;
        private VectorFieldUnsteady _velocity;
        private int _cellToSeedRatio = 2;

        public DiffusionMapper(VectorFieldUnsteady velocity, Plane plane) : base(plane, velocity.Size.ToInt2())
        {
            _diffusionMap = new CutDiffusion(velocity, 0, 0.3f);
            _algorithm = _diffusionMap;
            Plane = plane;
            _subrangePlane = Plane;
            Mapping = GetCurrentMap;
            _velocity = velocity;
            _maxPlane = velocity.Size.ToInt2();
            _scaledPlane = new Plane(_subrangePlane.Origin, _subrangePlane.XAxis, _subrangePlane.YAxis, _subrangePlane.ZAxis, 1.0f/_cellToSeedRatio, _subrangePlane.PointSize);
        }

        /// <summary>
        /// If different seed or parameters were chosen, update.
        /// </summary>
        /// <returns></returns>
        public List<Renderable> GetCurrentMap()
        {
            if (_lastSetting == null ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain ||
                _currentSetting.AlphaStable != _lastSetting.AlphaStable ||
                _currentSetting.StepSize != _lastSetting.StepSize ||
                _currentSetting.IntegrationTime != _lastSetting.IntegrationTime)
            {
                _diffusionMap.SetupMap(_startPoint, _currentSetting.SliceTimeMain, _currentSetting.IntegrationTime, _cellToSeedRatio);

                // Integrate to the desired time step.
                if (_diffusionMap.CurrentTime < _diffusionMap.EndTime)
                    _diffusionMap.Advect(_currentSetting.StepSize, _currentSetting.AlphaStable, _startPoint);

                RefreshPlane();
            }
            if (_lastSetting == null ||
                _currentSetting.StepSize != _lastSetting.StepSize)
            {
            }
            if (_lastSetting == null ||
                _currentSetting.Colormap != _lastSetting.Colormap ||
                _currentSetting.Shader != _lastSetting.Shader ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain)
            {
                RefreshPlane();
            }
            if (_lastSetting == null ||
                _lastSetting.WindowWidth != _currentSetting.WindowWidth ||
                _currentSetting.WindowStart != _lastSetting.WindowStart)
            {
                RefreshBoundsPlanes();
                //if(_currentSetting.Shader == FieldPlane.RenderEffect.LIC_LENGTH)
                //    _dataMap[1].UpperBound = 0;
            }
            List<Renderable> list = _dataMap.ToList<Renderable>();
            return list;
        }

        private void RefreshPlane()
        {
            _dataMap = new FieldPlane[1];
            switch (_currentSetting.Shader)
            {
                case FieldPlane.RenderEffect.OVERLAY:
                    {
                        _dataMap = new FieldPlane[2];
                        _dataMap[1] = new FieldPlane(_scaledPlane, _diffusionMap.ReferenceMap, (_velocity.Size * _cellToSeedRatio).ToInt2(), 0, 0, FieldPlane.RenderEffect.OVERLAY, ColorMapping.GetComplementary(_currentSetting.Colormap));
                        var tmp = _velocity.GetTimeSlice(_currentSetting.SliceTimeMain);
                        tmp.TimeSlice = null;
                        _dataMap[0] = new FieldPlane(_subrangePlane, tmp, FieldPlane.RenderEffect.LIC, _currentSetting.Colormap);
                        _dataMap[0].AddScalar(_diffusionMap.CutMap);
                        RefreshBoundsPlanes();
                        break;
                    }
                case FieldPlane.RenderEffect.LIC:
                case FieldPlane.RenderEffect.LIC_LENGTH:
                    {
                        _dataMap = new FieldPlane[2];
                        var tmp = _velocity.GetTimeSlice(_currentSetting.SliceTimeMain);
                        tmp.TimeSlice = null;
                        _dataMap[0] = new FieldPlane(_subrangePlane, tmp, _currentSetting.Shader, ColorMapping.GetComplementary( _currentSetting.Colormap));
                        _dataMap[1] = new FieldPlane(_scaledPlane, _diffusionMap.CutMap, (_velocity.Size * _cellToSeedRatio).ToInt2(), 0, 0, FieldPlane.RenderEffect.OVERLAY, _currentSetting.Colormap);
                        RefreshBoundsPlanes();
                        //if (_currentSetting.Shader == FieldPlane.RenderEffect.LIC)
                        //    _dataMap[0].AddScalar(_diffusionMap.ReferenceMap);
                        break;
                    }
                default:
                    _dataMap[0] = _diffusionMap.GetPlane(_subrangePlane);
                    _dataMap[0].UsedMap = _currentSetting.Colormap;
                    _dataMap[0].SetRenderEffect(_currentSetting.Shader);
                    RefreshBoundsPlanes();
                    break;
            }
        }

        private void RefreshBoundsPlanes()
        {
            //foreach (FieldPlane plane in _dataMap)
            //{
                _dataMap[0].LowerBound = 0;
                _dataMap[0].UpperBound = _currentSetting.WindowWidth;
            if (_dataMap.Length > 1)
            {
                _dataMap[1].LowerBound = 0;
                _dataMap[1].UpperBound = _currentSetting.WindowStart;
            }
            //}
        }

        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.Colormap:
                case Setting.Element.WindowWidth:
                //return _currentSetting.Shader == FieldPlane.RenderEffect.COLORMAP || _currentSetting.Shader == FieldPlane.RenderEffect.DEFAULT;
                case Setting.Element.SliceTimeMain:
                case Setting.Element.Shader:
                case Setting.Element.StepSize:
                case Setting.Element.IntegrationTime:
                case Setting.Element.AlphaStable:
                    return true;
                case Setting.Element.WindowStart:
                    return _currentSetting.Shader == FieldPlane.RenderEffect.LIC || _currentSetting.Shader == FieldPlane.RenderEffect.LIC_LENGTH || _currentSetting.Shader == FieldPlane.RenderEffect.OVERLAY;
                default:
                    return false;
            }
        }

        public override void EndSelection(Vector2[] points)
        {
            //base.EndSelection(points);

            //_velocity = _velocity.GetTimeSlice()
            //RefreshPlane();

            //int size = _velocity.Size.ToInt2().Product();
            //float fill, mean, sd;
            //_velocity.ScalarsAsSFU[0].GetTimeSlice(0).ComputeStatistics(out fill, out mean, out sd);
            //Console.WriteLine("Region:\n\tRegion: " + size + "\n\tValid Part: " + fill + "\n\tValid Cells: " + fill * size);
        }

        public override void ClickSelection(Vector2 point)
        {
            base.ClickSelection(point);
            //_flowMap.SetupPoint(_startPoint, _currentSetting.SliceTimeMain);
            //RefreshPlane();
            _diffusionMap.SetupMap(_startPoint, _currentSetting.SliceTimeMain, _currentSetting.IntegrationTime, _cellToSeedRatio);

            // Integrate to the desired time step.
            if (_diffusionMap.CurrentTime < _diffusionMap.EndTime)
                _diffusionMap.Advect(_currentSetting.StepSize, _currentSetting.AlphaStable, _startPoint);
            Console.WriteLine("Clicked: " + _startPoint.ToString());
            RefreshPlane();
        }
    }

    class LocalDiffusionMapper : SelectionMapper
    {
        private FieldPlane[] _dataMap;
        private Plane _scaledPlane;
        private LocalDiffusion _diffusionMap;
        private VectorFieldUnsteady _velocity;

        public LocalDiffusionMapper(VectorFieldUnsteady velocity, Plane plane) : base(plane, velocity.Size.ToInt2())
        {
            _diffusionMap = new LocalDiffusion(velocity, 0, 0.3f);
            _algorithm = _diffusionMap;
            Plane = plane;
            _subrangePlane = Plane;
            Mapping = GetCurrentMap;
            _velocity = velocity;
            _maxPlane = velocity.Size.ToInt2();
        }

        /// <summary>
        /// If different seed or parameters were chosen, update.
        /// </summary>
        /// <returns></returns>
        public List<Renderable> GetCurrentMap()
        {
            if (_lastSetting == null ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain ||
                _currentSetting.AlphaStable != _lastSetting.AlphaStable ||
                _currentSetting.StepSize != _lastSetting.StepSize ||
                _currentSetting.IntegrationTime != _lastSetting.IntegrationTime)
            {
                _diffusionMap.SetupMap(_startPoint, _currentSetting.SliceTimeMain, _currentSetting.IntegrationTime);

                // Integrate to the desired time step.
                if (_diffusionMap.CurrentTime < _diffusionMap.EndTime)
                    _diffusionMap.Advect(_currentSetting.StepSize, _currentSetting.AlphaStable, _startPoint);

                RefreshPlane();
            }
            if (_lastSetting == null ||
                _currentSetting.StepSize != _lastSetting.StepSize)
            {
            }
            if (_lastSetting == null ||
                _currentSetting.Colormap != _lastSetting.Colormap ||
                _currentSetting.Shader != _lastSetting.Shader ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain)
            {
                RefreshPlane();
            }
            if (_lastSetting == null ||
                _lastSetting.WindowWidth != _currentSetting.WindowWidth ||
                _currentSetting.WindowStart != _lastSetting.WindowStart)
            {
                RefreshBoundsPlanes();
                if (_currentSetting.Shader == FieldPlane.RenderEffect.LIC_LENGTH)
                    _dataMap[1].UpperBound = 0;
            }
            List<Renderable> list = _dataMap.ToList<Renderable>();
            return list;
        }

        private void RefreshPlane()
        {
            _dataMap = new FieldPlane[1];
            switch (_currentSetting.Shader)
            {
                //case FieldPlane.RenderEffect.OVERLAY:
                //    {
                //        _dataMap = new FieldPlane[2];
                //        _dataMap[1] = new FieldPlane(_scaledPlane, _diffusionMap.ReferenceMap, (_velocity.Size * _cellToSeedRatio).ToInt2(), 0, 0, FieldPlane.RenderEffect.OVERLAY, ColorMapping.GetComplementary(_currentSetting.Colormap));
                //        var tmp = _velocity.GetTimeSlice(_currentSetting.SliceTimeMain);
                //        tmp.TimeSlice = null;
                //        _dataMap[0] = new FieldPlane(_subrangePlane, tmp, FieldPlane.RenderEffect.LIC, _currentSetting.Colormap);
                //        _dataMap[0].AddScalar(_diffusionMap.CutMap);
                //        RefreshBoundsPlanes();
                //        break;
                //    }
                //case FieldPlane.RenderEffect.LIC:
                //case FieldPlane.RenderEffect.LIC_LENGTH:
                //    {
                //        _dataMap = new FieldPlane[2];
                //        var tmp = _velocity.GetTimeSlice(_currentSetting.SliceTimeMain);
                //        tmp.TimeSlice = null;
                //        _dataMap[0] = new FieldPlane(_subrangePlane, tmp, _currentSetting.Shader, ColorMapping.GetComplementary(_currentSetting.Colormap));
                //        _dataMap[0].LowerBound = 0;
                //        _dataMap[0].UpperBound = _currentSetting.WindowWidth;
                //        _dataMap[1] = new FieldPlane(_scaledPlane, _diffusionMap.ReferenceMap, (_velocity.Size * _cellToSeedRatio).ToInt2(), 0, 0, FieldPlane.RenderEffect.OVERLAY, _currentSetting.Colormap);
                //        _dataMap[1].LowerBound = 0;
                //        _dataMap[1].UpperBound = 0;
                //        //if (_currentSetting.Shader == FieldPlane.RenderEffect.LIC)
                //        //    _dataMap[0].AddScalar(_diffusionMap.ReferenceMap);
                //        break;
                //    }
                default:
                    _dataMap[0] = _diffusionMap.GetPlane(_subrangePlane);
                    _dataMap[0].UsedMap = _currentSetting.Colormap;
                    _dataMap[0].SetRenderEffect(_currentSetting.Shader);
                    RefreshBoundsPlanes();
                    break;
            }
        }

        private void RefreshBoundsPlanes()
        {
            foreach (FieldPlane plane in _dataMap)
            {
                plane.LowerBound = 0;
                plane.UpperBound = _currentSetting.WindowWidth;
            }
        }

        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.Colormap:
                case Setting.Element.WindowWidth:
                //return _currentSetting.Shader == FieldPlane.RenderEffect.COLORMAP || _currentSetting.Shader == FieldPlane.RenderEffect.DEFAULT;
                case Setting.Element.SliceTimeMain:
                case Setting.Element.Shader:
                case Setting.Element.StepSize:
                case Setting.Element.IntegrationTime:
                case Setting.Element.AlphaStable:
                    return true;
                default:
                    return false;
            }
        }

        public override void EndSelection(Vector2[] points)
        {
            //base.EndSelection(points);

            //_velocity = _velocity.GetTimeSlice()
            //RefreshPlane();

            //int size = _velocity.Size.ToInt2().Product();
            //float fill, mean, sd;
            //_velocity.ScalarsAsSFU[0].GetTimeSlice(0).ComputeStatistics(out fill, out mean, out sd);
            //Console.WriteLine("Region:\n\tRegion: " + size + "\n\tValid Part: " + fill + "\n\tValid Cells: " + fill * size);
        }

        public override void ClickSelection(Vector2 point)
        {
            //base.ClickSelection(point);
            ////_flowMap.SetupPoint(_startPoint, _currentSetting.SliceTimeMain);
            ////RefreshPlane();
            //_diffusionMap.SetupMap(_startPoint, _currentSetting.SliceTimeMain, _currentSetting.IntegrationTime, _cellToSeedRatio);
            //Console.WriteLine("Clicked: " + _startPoint.ToString());

            //// Integrate to the desired time step.
            //if (_diffusionMap.CurrentTime < _diffusionMap.EndTime)
            //    _diffusionMap.Advect(_currentSetting.StepSize, _currentSetting.AlphaStable, _startPoint);

            //RefreshPlane();
        }
    }
}
