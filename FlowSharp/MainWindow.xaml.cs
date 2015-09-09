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
        public MainWindow()
        {
            InitializeComponent();

            DX11Display.Scene = Renderer.Singleton;

            FSMain.LoadData();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            DataTypesDropDown.ItemsSource = Enum.GetValues(typeof(RedSea.Variable)).Cast<RedSea.Variable>();
        }
    }
}
