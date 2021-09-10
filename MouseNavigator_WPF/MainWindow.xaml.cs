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

namespace MouseNavigator_WPF
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private int hMouseHook;
        private MouseMonitor mouseMonitor;
        //private MouseState state;
        private int mouseState;

        public MainWindow()
        {
            InitializeComponent();
            this.buttonUp.DragOver += ButtonUp_DragOver;

            mouseMonitor = new MouseMonitor();
            //state = new MouseState();

            formStartHook();
            this.formStartHook();
        }

        public void floatingWindow()
        {


        }
        private void formStartHook()
        {
            this.hMouseHook = mouseMonitor.MouseHookStart(onMouseProc);

        }

        private void formStopHook()
        {
            if (this.hMouseHook != 0)
            {
                WinApi.UnhookWindowsHookEx(this.hMouseHook);
                //this.state.saveAction(DateTime.Today);


            }
        }

        public int onMouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            switch (wParam.ToInt32())
            {
                case MouseMessage.WM_LBUTTONDOWN:
                    //this.leftClickCount++;
                    
                    floatingWindow();
                    break;
                case MouseMessage.WM_RBUTTONDOWN:
                    //this.rightClickCount++;
                    break;
                case MouseMessage.WM_MBUTTONDOWN:
                    //this.middleClickCount++;
                    mouseState += 1;
                    Application.Current.MainWindow.Show();
                    Application.Current.MainWindow.Activate();

                    break;
            }

            this.MouseStateLabel.Content = this.mouseState.ToString();

            return WinApi.CallNextHookEx(this.hMouseHook, nCode, wParam, lParam);
        }

        private void ButtonUp_DragOver(object sender, DragEventArgs e)
        {
            MessageBox.Show("drag\n!");
            throw new NotImplementedException();
        }

        private void ButtonUp_Click(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show("clicked\n!");
            Application.Current.MainWindow.Hide();

        }

    }
}
