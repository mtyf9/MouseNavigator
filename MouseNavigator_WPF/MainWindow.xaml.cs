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
        private MouseHook mouseHook;

        public MainWindow()
        {
            InitializeComponent();
            //this.Closing += new System.ComponentModel.CancelEventHandler(MainWindow_Closing);

            this.buttonUp.DragOver += ButtonUp_DragOver;


            mouseHook = new MouseHook();
            mouseHook.InitHook();
            mouseHook.FormStartHook();
            mouseHook.ContentRender += SetContent;
        }
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("确定是退出吗？", "询问", MessageBoxButton.YesNo, MessageBoxImage.Question);

            //关闭窗口
            if (result == MessageBoxResult.Yes)
                e.Cancel = false;

            //不关闭窗口
            if (result == MessageBoxResult.No)
                e.Cancel = true;

            mouseHook.FormStopHook();

        }
        public void FloatingWindow()
        {
            FloatingWindow floatingWindow = new FloatingWindow();
            floatingWindow.Show();

        }

        public void SetContent(string value1)
        {
            this.MouseStateLabel.Content = mouseHook.mouseState.ToString();

        }
        //private void formStartHook()
        //{
        //    this.hMouseHook = mouseMonitor.MouseHookStart(onMouseProc);

        //}

        //private void formStopHook()
        //{
        //    if (this.hMouseHook != 0)
        //    {
        //        WinApi.UnhookWindowsHookEx(this.hMouseHook);
        //        //this.state.saveAction(DateTime.Today);


        //    }
        //}

        //public int OnMouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        //{
        //    switch (wParam.ToInt32())
        //    {
        //        case MouseMessage.WM_LBUTTONDOWN:
        //            //this.leftClickCount++;

        //            floatingWindow();
        //            break;
        //        case MouseMessage.WM_RBUTTONDOWN:
        //            //this.rightClickCount++;
        //            break;
        //        case MouseMessage.WM_MBUTTONDOWN:
        //            //this.middleClickCount++;
        //            mouseState += 1;
        //            Application.Current.MainWindow.Show();
        //            Application.Current.MainWindow.Activate();

        //            break;
        //    }

        //    this.MouseStateLabel.Content = this.mouseState.ToString();

        //    return WinApi.CallNextHookEx(this.hMouseHook, nCode, wParam, lParam);
        //}

        private void ButtonUp_DragOver(object sender, DragEventArgs e)
        {
            MessageBox.Show("drag\n!");
            throw new NotImplementedException();
        }

        private void ButtonUp_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("clicked\n!");

            //Application.Current.MainWindow.Hide();

        }



    }
}
