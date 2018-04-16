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

namespace KomiCopy
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<CopyFileVM> _fileColle = new ObservableCollection<CopyFileVM>();
        private CopyFileVM _resolveFile = new CopyFileVM();

        public MainWindow()
        {
            InitializeComponent();
            
            LB_Files.ItemsSource = _fileColle;
            UC_ResolveFile.DataContext = _resolveFile;
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

            bool bMerged = FileCtrl.MergeFiles(fileList);

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
            bool bResolved = FileCtrl.ResolveFile(_resolveFile.FilePath);

            if (bResolved)
            {
                MessageBox.Show("解析成功!");
            }
            else
            {
                MessageBox.Show("解析失敗!");
            }
        }

    }
}
