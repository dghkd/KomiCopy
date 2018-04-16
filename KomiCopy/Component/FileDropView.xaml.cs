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

using KomiCopy.ViewModel;

namespace KomiCopy.Component
{
    /// <summary>
    /// FileDropView.xaml 的互動邏輯
    /// </summary>
    public partial class FileDropView : UserControl
    {
        private CopyFileVM _vm;
        public FileDropView()
        {
            InitializeComponent();
        }

        private void On_Border_Drop(object sender, DragEventArgs e)
        {
            _vm = this.DataContext as CopyFileVM;
            if (_vm == null)
            {
                return;
            }

            String[] dropFiles = (String[])e.Data.GetData(DataFormats.FileDrop);
            _vm.FilePath = dropFiles[0];
            try
            {
                _vm.ImageObject = new BitmapImage(new Uri(_vm.FilePath));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(String.Format("Set image source fail:{0}", ex.Message));
                String fileExt = System.IO.Path.GetExtension(_vm.FilePath);
                _vm.FileDesc = String.Format("{0} file", fileExt);
            }
        }
    }
}
