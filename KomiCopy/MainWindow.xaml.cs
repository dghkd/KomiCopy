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
using System.Diagnostics;
using System.Collections.ObjectModel;

using KomiCopy.ViewModel;
using KomiCopy.Kernel;
using KomiCopy.Crypt;

namespace KomiCopy
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<CopyFileVM> _fileColle = new ObservableCollection<CopyFileVM>();
        private CopyFileVM _resolveFile = new CopyFileVM();
        CryptCtrl _ctrl = new CryptCtrl();

        public MainWindow()
        {
            InitializeComponent();

            FileCtrl.Cryptor.LoadLocalHostKeyPair();

            LB_Files.ItemsSource = _fileColle;
            UC_ResolveFile.DataContext = _resolveFile;
            TXTBOX_LocalPubKey.Text = FileCtrl.Cryptor.GetPublicKeyText();
        }

        private void On_CMB_FileCount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int selCount = CMB_FileCount.SelectedIndex + 2;

            while (_fileColle.Count > selCount)
            {
                _fileColle.RemoveAt(selCount);
            }

            while (_fileColle.Count < selCount)
            {
                CopyFileVM addFile = new CopyFileVM();
                _fileColle.Add(addFile);
            }
        }

        private void On_BTN_Merge_Click(object sender, RoutedEventArgs e)
        {
            BTN_Merge.IsEnabled = false;
            List<String> fileList = new List<String>();
            foreach (CopyFileVM vm in _fileColle)
            {
                fileList.Add(vm.FilePath);
            }

            bool bMerged = FileCtrl.MergeFiles(fileList, Convert.ToBoolean(CB_IsEncrypt.IsChecked), TXTBOX_AnotherPubKey.Text);

            if (bMerged)
            {
                MessageBox.Show("合併成功!");
            }
            else
            {
                MessageBox.Show("合併失敗!");
            }

            BTN_Merge.IsEnabled = true;
        }

        private void On_BTN_Resolve_Click(object sender, RoutedEventArgs e)
        {
            bool bResolved = FileCtrl.ResolveFile(_resolveFile.FilePath, Convert.ToBoolean(CB_IsDecrypt.IsChecked));

            if (bResolved)
            {
                MessageBox.Show("解析成功!");
            }
            else
            {
                MessageBox.Show("解析失敗!");
            }
        }

        private void On_BTN_CreateCryptKey_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult ret = MessageBox.Show("你確定要重新建立新的密文嗎?", "重建密文", MessageBoxButton.YesNo);
            if (ret == MessageBoxResult.Yes)
            {
                FileCtrl.Cryptor.CreateLocalHostKeyPair();
                TXTBOX_LocalPubKey.Text = FileCtrl.Cryptor.GetPublicKeyText();
            }
        }

        private void On_BTN_CopyKeyText_Click(object sender, RoutedEventArgs e)
        {
            bool bCopied = false;
            for (int i = 0; i < 5 && !bCopied; i++)
            {
                try
                {
                    String text = TXTBOX_LocalPubKey.Text;
                    Clipboard.SetDataObject(text);
                    TXTBOX_LocalPubKey.Focus();
                    TXTBOX_LocalPubKey.SelectAll();
                    bCopied = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(String.Format("[On_BTN_CopyKeyText_Click] Copy to Clipboard fail:{0}", ex.Message));
                    System.Threading.SpinWait.SpinUntil(() => false, 100);
                }
            }
        }
    }
}
