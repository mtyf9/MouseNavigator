using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MouseNavigator_WPF
{
    class MouseHook
    {
        private int hMouseHook;
        private MouseMonitor mouseMonitor;
        public int mouseState;

        public delegate void Dele2Main(string value1);
        public event Dele2Main ContentRender;

        public void InitHook()
        {
            mouseMonitor = new MouseMonitor();

        }
        public int OnMouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            ContentRender(null);
            switch (wParam.ToInt32())
            {
                case MouseMessage.WM_LBUTTONDOWN:
                    //this.leftClickCount++;
                    
                    //floatingWindow();
                    break;
                case MouseMessage.WM_RBUTTONDOWN:
                    //this.rightClickCount++;
                    Application.Current.MainWindow.Hide();
                    break;
                case MouseMessage.WM_MBUTTONDOWN:
                    //this.middleClickCount++;
                    mouseState += 1;
                    //Application.Current.FloatingWindow.Show();
                    Application.Current.MainWindow.Show();
                    Application.Current.MainWindow.Activate();

                    break;
            }
            //MainWindow.MouseStateLabel.Content = this.mouseState.ToString();

            //this.state.recordAction(wParam.ToInt32());
            return WinApi.CallNextHookEx(this.hMouseHook, nCode, wParam, lParam);
        }

        public void FormStartHook()
        {
            this.hMouseHook = mouseMonitor.MouseHookStart(OnMouseProc);

        }

        public void FormStopHook()
        {
            if (this.hMouseHook != 0)
            {
                WinApi.UnhookWindowsHookEx(this.hMouseHook);
                //this.state.saveAction(DateTime.Today);

            }
        }


    }
}
