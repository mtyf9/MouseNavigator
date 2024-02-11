using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static MouseNavigator_WPF.MouseHook;

namespace MouseNavigator_WPF
{
    /// <summary>
    /// FloatingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class FloatingWindow : Window
    {
        public delegate void Action();
        public Action[] MyActions = new Action[13];

        public FloatingWindow()
        {
            InitializeComponent();

            //this.Background = "Transparent";
            this.WindowStyle = WindowStyle.None;

            // 初始化代理数组
            //MyActions[1] = Button1Action;
            //MyActions[2] = Button2Action;
            MyActions[3] = Button3Action;
            //MyActions[4] = Button4Action;
            //MyActions[5] = Button5Action;
            MyActions[6] = Button6Action;
            //MyActions[7] = Button7Action;
            //MyActions[8] = Button8Action;
            MyActions[9] = Button9Action;
            //MyActions[10] = Button10Action;
            //MyActions[11] = Button11Action;
            MyActions[12] = Button12Action;

        }



        private void Button3Action()
        {
            throw new NotImplementedException();
        }

        private void Button6Action()
        {
            KeyboardSimulator.SimulateShortcut(KeyboardSimulator.VK_LWIN, KeyboardSimulator.VK_D);
        }

        private void Button9Action()
        {
            throw new NotImplementedException();
        }

        private void Button12Action()
        {
            KeyboardSimulator.SimulateShortcut(KeyboardSimulator.VK_ALT, KeyboardSimulator.VK_TAB);


        }

        internal void PerformButtonAction(string buttonName)
        {
            if (buttonName == null) return;
            // 从按钮名称中提取数字, 按钮名称格式为 "buttonX"
            int index = int.Parse(buttonName.Substring(6));

            // 检查索引有效性
            if (index >= 1 && index < MyActions.Length && MyActions[index] != null)
            {
                MyActions[index].Invoke();
            }
            else
            {
                MessageBox.Show($"Action for {buttonName} is not defined.");
            }
        }
    }

}
