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
using System.Windows.Shapes;

using KomiCopy.Kernel;

namespace KomiCopy.Dialog
{
    /// <summary>
    /// MultiPubKeyEditorDialog.xaml 的互動邏輯
    /// </summary>
    public partial class MultiPubKeyEditorDialog : Window
    {
        public String PubKeysResult { get; set; }

        public MultiPubKeyEditorDialog()
        {
            InitializeComponent();
            this.PubKeysResult = "";
        }

        private void On_BTN_OK_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < TXTBOX_PubKeys.LineCount; i++)
            {
                String line = TXTBOX_PubKeys.GetLineText(i).Replace("\r\n", "");
                this.PubKeysResult += line + ",";
            }
            this.DialogResult = true;
        }

        private void On_BTN_Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void On_TXTBOX_PubKeys_TextChanged(object sender, TextChangedEventArgs e)
        {
            int validCount = 0;

            for (int i = 0; i < TXTBOX_PubKeys.LineCount; i++)
            {
                String pubKeyText = TXTBOX_PubKeys.GetLineText(i).Replace("\r\n", "");
                if (FileCtrl.Cryptor.IsVaildPublicKeyText(pubKeyText))
                {
                    validCount++;
                }
            }

            TXT_TotalCount.Text = Convert.ToString(TXTBOX_PubKeys.LineCount);
            TXT_ValidCount.Text = Convert.ToString(validCount);
        }
    }
}
