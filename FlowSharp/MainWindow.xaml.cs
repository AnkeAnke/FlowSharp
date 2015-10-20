using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FlowSharp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DataMapper _mapper;
        //private RedSea.Display _display;
        //private RedSea.DisplayLines _displayLines;
        //private int _slice0, _slice1;
        //private FieldPlane _slice1Plane;

        public MainWindow()
        {
            InitializeComponent();

            DX11Display.Scene = Renderer.Singleton;
            _mapper = new EmptyMapper();

            FSMain.LoadData();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            //DX11Display.MouseEnter += DX11Display_MouseEnter;
            //DropDownDisplay.ItemsSource = Enum.GetValues(typeof(RedSea.Display)).Cast<RedSea.Display>();
            //DropDownDisplayLines.ItemsSource = Enum.GetValues(typeof(RedSea.DisplayLines)).Cast<RedSea.DisplayLines>();
            //DropDownSlice0.ItemsSource = Enumerable.Range(0, 10);
            //DropDownSlice1.ItemsSource = Enumerable.Range(0, 10);
            //DropDownDisplay.SelectedIndex = 0;
            //DropDownDisplayLines.SelectedIndex = 0;
            //DropDownSlice0.SelectedIndex = 0;
            //DropDownSlice1.SelectedIndex = 0;
        }

        private void LoadDisplay(object sender, RoutedEventArgs e)
        {
            DropDownDisplay.ItemsSource = Enum.GetValues(typeof(RedSea.Display)).Cast<RedSea.Display>();
            DropDownDisplay.SelectedIndex = 0;
        }

        private void LoadDisplayLines(object sender, RoutedEventArgs e)
        {
            DropDownDisplayLines.ItemsSource = Enum.GetValues(typeof(RedSea.DisplayLines)).Cast<RedSea.DisplayLines>();
            DropDownDisplayLines.SelectedIndex = 0;
        }

        private void LoadSlice0(object sender, RoutedEventArgs e)
        {
            DropDownSlice0.ItemsSource = Enumerable.Range(0, 10);          
            DropDownSlice0.SelectedIndex = 0;
        }

        private void LoadSlice1(object sender, RoutedEventArgs e)
        {
            DropDownSlice1.ItemsSource = Enumerable.Range(0, 10);
            DropDownSlice1.SelectedIndex = 1;
        }
        private void LoadIntegrator(object sender, RoutedEventArgs e)
        {
            DropDownIntegrator.ItemsSource = Enum.GetValues(typeof(VectorField.Integrator.Type)).Cast<VectorField.Integrator.Type>();
            DropDownIntegrator.SelectedIndex = (int)VectorField.Integrator.Type.EULER;
        }

        private void LoadStepSize(object sender, RoutedEventArgs e)
        {
            //(sender as Slider).Minimum = 0.01;
            (sender as Slider).Value = 0.01;
        }

        private void OnChangeDisplay(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper = RedSea.Singleton.SetMapper((RedSea.Display)(comboBox.SelectedItem as RedSea.Display?));
            _mapper.CurrentSetting.AlphaStable = (float)alpha.Value;
            _mapper.CurrentSetting.IntegrationType = (VectorField.Integrator.Type)DropDownIntegrator.SelectedIndex;
            _mapper.CurrentSetting.LineSetting = (RedSea.DisplayLines)DropDownDisplayLines.SelectedIndex;
            _mapper.CurrentSetting.SlicePositionMain = DropDownSlice0.SelectedIndex;
            _mapper.CurrentSetting.SlicePositionReference = DropDownSlice1.SelectedIndex;
            _mapper.CurrentSetting.StepSize = (float)step.Value / 10f;
            //_display = (RedSea.Display)(comboBox.SelectedItem as RedSea.Display?);
            UpdateRenderer();
        }

        private void OnChangeDisplayLines(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            DataMapper.Setting cpy = new DataMapper.Setting(_mapper.CurrentSetting);
            _mapper.CurrentSetting.LineSetting = (RedSea.DisplayLines)(comboBox.SelectedItem as RedSea.DisplayLines?);
            UpdateRenderer();
        }

        private void OnChangeSlice0(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.SlicePositionMain = (int)(comboBox.SelectedItem as int?);
            UpdateRenderer();
        }

        private void OnChangeSlice1(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.SlicePositionReference = (int)(comboBox.SelectedItem as int?);
            UpdateRenderer();
        }

        private void OnChangeIntegrator(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.IntegrationType = (VectorField.Integrator.Type)(comboBox.SelectedItem as VectorField.Integrator.Type?);
            UpdateRenderer();
        }

        // ~~~~~~~~~~~ Sliders ~~~~~~~~~~~~~ \\
        private void OnChangeStepSize(object sender, RoutedEventArgs e)
        {
            var slider = sender as Slider;
            _mapper.CurrentSetting.StepSize = (float)(slider.Value as double?)/10f;
            UpdateRenderer();
        }

        private void OnChangeAlphaFFF(object sender, RoutedEventArgs e)
        {
            var slider = sender as Slider;
            _mapper.CurrentSetting.AlphaStable = (float)(slider.Value as double?);
            UpdateRenderer();
        }

        private void UpdateRenderer()
        {
            if (Renderer.Singleton.Initialized && _mapper != null)
                RedSea.Singleton.Update();
        }

        private void ActivateCamera(object sender, MouseEventArgs e)
        {
            Renderer.Singleton.Camera.Active = true;
        }

        private void DeactivateCamera(object sender, MouseEventArgs e)
        {
            Renderer.Singleton.Camera.Active = false;
        }

        //public bool EnableCamera()
        //{
        //    Renderer.Singleton.Camera.
        //    return DX11Display.Focus();
        //}
        ///// <summary>
        ///// The parameter should be of type RedSea.Display. Accessibility problems...
        ///// </summary>
        ///// <param name="disp">RedSea.Display, please!</param>
        //public void SetToMapper(int disp)
        //{
        //    DropDownDisplay.SelectedIndex = (int)disp;
        //    _mapper = RedSea.Singleton.SetMapper((RedSea.Display)disp);
        //    UpdateRenderer();
        //}
    }
}
