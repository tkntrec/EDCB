using System;
using System.Windows;
using System.Windows.Controls;

namespace EpgTimer.Setting
{
    /// <summary>
    /// SetOtherAppView.xaml の相互作用ロジック
    /// </summary>
    public partial class SetOtherAppView : UserControl
    {
        public SetOtherAppView()
        {
            InitializeComponent();

            button_exe.Click += ViewUtil.OpenFileNameDialog(textBox_exe, false, "", ".exe");
            button_wplayExe.Click += ViewUtil.OpenFileNameDialog(textBox_wplayExe, false, "", ".exe");
            button_playExe.Click += ViewUtil.OpenFileNameDialog(textBox_playExe, false, "", ".exe");

            //エスケープキャンセルだけは常に有効にする。
            var bx = new BoxExchangeEdit.BoxExchangeEditor(null, this.listBox_bon, true);
            if (CommonManager.Instance.NWMode == false)
            {
                bx.AllowDragDrop();
                bx.AllowKeyAction();
                button_up.Click += bx.button_Up_Click;
                button_down.Click += bx.button_Down_Click;
                button_del.Click += bx.button_Delete_Click;
                button_add.Click += (sender, e) => ViewUtil.ListBox_TextCheckAdd(listBox_bon, comboBox_bon.Text);
            }
            else
            {
                label3.IsEnabled = false;
                panel_bonButtons.IsEnabled = false;
                button_add.IsEnabled = false;
            }
        }

        public void LoadSetting()
        {
            comboBox_bon.ItemsSource = CommonManager.GetBonFileList();
            comboBox_bon.SelectedIndex = 0;

            listBox_bon.Items.Clear();
            int num = IniFileHandler.GetPrivateProfileInt("TVTEST", "Num", 0, SettingPath.TimerSrvIniPath);
            for (uint i = 0; i < num; i++)
            {
                string item = IniFileHandler.GetPrivateProfileString("TVTEST", i.ToString(), "", SettingPath.TimerSrvIniPath);
                if (item.Length > 0) listBox_bon.Items.Add(item);
            }
        }
        public void SaveSetting()
        {
            IniFileHandler.WritePrivateProfileString("TVTEST", "Num", listBox_bon.Items.Count, SettingPath.TimerSrvIniPath);
            IniFileHandler.DeletePrivateProfileNumberKeys("TVTEST", SettingPath.TimerSrvIniPath);
            for (int i = 0; i < listBox_bon.Items.Count; i++)
            {
                IniFileHandler.WritePrivateProfileString("TVTEST", i.ToString(), listBox_bon.Items[i], SettingPath.TimerSrvIniPath);
            }
        }
        private void replaceTest_TextChanged(object sender, TextChangedEventArgs e)
        {
            textBox_replaceTestResult.Text =
                CommonManager.ReplaceText(textBox_replaceTest.Text, CommonManager.CreateReplaceDictionary(textBox_replacePattern.Text));
        }
    }
}
