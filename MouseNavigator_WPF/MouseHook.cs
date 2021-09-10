using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MouseNavigator_WPF
{
    class MouseHook
    {
        private int hMouseHook;
        private MouseMonitor mouseMonitor;


        public void initHook()
        {

        }
        public int onMouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            /*
            switch (wparam.toint32())
            {
                case mousemessage.wm_lbuttondown:
                    //this.leftclickcount++;
                    application.current.mainwindow.show();

                    break;
                case mousemessage.wm_rbuttondown:
                    //this.rightclickcount++;
                    break;
                case mousemessage.wm_mbuttondown:
                    //this.middleclickcount++;
                    mousestate += 1;
                    application.current.mainwindow.activate();

                    break;
            }
            */

            //this.state.recordAction(wParam.ToInt32());
            return WinApi.CallNextHookEx(this.hMouseHook, nCode, wParam, lParam);
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


    }
}
