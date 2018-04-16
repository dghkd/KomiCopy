using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.ComponentModel;

namespace KomiCopy.ViewModel
{
    public class CopyFileVM : INotifyPropertyChanged
    {
        #region Private Member
        private String _filePath;
        private BitmapImage _img;
        private String _fileDesc;
        #endregion


        #region Constructro
        public CopyFileVM()
        {
            _filePath = "";
            _fileDesc = "拖放檔案到這裡";
        }
        #endregion


        #region Public Member
        public String FilePath
        {
            get { return _filePath; }
            set { _filePath = value; OnPropertyChanged("FilePath"); }
        }

        public BitmapImage ImageObject
        {
            get { return _img; }
            set { _img = value; OnPropertyChanged("ImageObject"); }
        }

        public String FileDesc
        {
            get { return _fileDesc; }
            set { _fileDesc = value; OnPropertyChanged("FileDesc"); }
        }
        #endregion


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string prop)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
    }
}
