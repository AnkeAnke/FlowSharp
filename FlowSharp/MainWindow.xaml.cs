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
        private RedSea.Display _display;
        private RedSea.DisplayLines _displayLines;
        private int _slice0, _slice1;
        //private FieldPlane _slice1Plane;

        public MainWindow()
        {
            InitializeComponent();

            DX11Display.Scene = Renderer.Singleton;

            FSMain.LoadData();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
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
            //DropDownSlice1.SelectedIndex = 1;
        }

        private void OnChangeDisplay(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _display = (RedSea.Display)(comboBox.SelectedItem as RedSea.Display?);
            UpdateRenderer();
        }

        private void OnChangeDisplayLines(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _displayLines = (RedSea.DisplayLines)(comboBox.SelectedItem as RedSea.DisplayLines?);
            UpdateRenderer();
        }

        private void OnChangeSlice0(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _slice0 = (int)(comboBox.SelectedItem as int?);
            UpdateRenderer();
        }

        private void OnChangeSlice1(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _slice1 = (int)(comboBox.SelectedItem as int?);
            //            _slice1Plane = new...
            if (Renderer.Singleton.Initialized)
                RedSea.Singleton.SetPreset(_display, _slice1);
        }

        private void UpdateRenderer()
        {
            if(Renderer.Singleton.Initialized)
                RedSea.Singleton.SetPreset(_display, _slice0, _displayLines);
        }
    }
}
