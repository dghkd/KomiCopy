using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace KomiCopy.Kernel
{
    public class FileCtrl
    {
        #region Readonly String
        /// <summary>
        /// 識別字字串
        /// </summary>
        private static readonly String SHIKIBETSU = "komicopy_dghkd";
        /// <summary>
        /// 識別字位元組
        /// </summary>
        private static readonly byte[] SHIKIBETSU_BYTES = Encoding.ASCII.GetBytes(SHIKIBETSU);
        #endregion


        #region Public Method
        /// <summary>
        /// 合併檔案
        /// <para>以第一個檔案為基礎，合併第二個以後的檔案</para>
        /// </summary>
        /// <param name="fileList">欲合併之檔案路徑</param>
        public static bool MergeFiles(List<String> fileList)
        {
            //檢查檔案
            if (fileList.Count < 2)
            {
                return false;
            }
            foreach (String path in fileList)
            {
                if (!File.Exists(path))
                {
                    return false;
                }
            }

            //複製第一份檔案
            String mergeFileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + Path.GetExtension(fileList[0]);
            byte[] firstFileBytes = File.ReadAllBytes(fileList[0]);
            FileStream outputFile = File.Create(mergeFileName);
            foreach (byte b in firstFileBytes)
            {
                outputFile.WriteByte(b);
            }
            outputFile.Flush();

            //合併所有指定檔案
            for (int i = 1; i < fileList.Count; i++)
            {
                String filePath = fileList[i];
                //插入識別字位元組
                foreach (byte b in SHIKIBETSU_BYTES)
                {
                    outputFile.WriteByte(b);
                }
                outputFile.Flush();

                //插入附加檔檔名
                String fileName = Path.GetFileName(filePath);
                byte[] fileNameBytes = Encoding.ASCII.GetBytes(fileName);
                foreach (byte b in fileNameBytes)
                {
                    outputFile.WriteByte(b);
                }
                outputFile.Flush();

                //插入識別字位元組
                foreach (byte b in SHIKIBETSU_BYTES)
                {
                    outputFile.WriteByte(b);
                }
                outputFile.Flush();

                //插入附加檔案
                byte[] appendFileBytes = File.ReadAllBytes(filePath);
                foreach (byte b in appendFileBytes)
                {
                    outputFile.WriteByte(b);
                }
                outputFile.Flush();
            }

            outputFile.Close();
            return true;
        }

        /// <summary>
        /// 解析檔案
        /// <para>解析被附加在第一個檔案之後的所有檔案</para>
        /// </summary>
        /// <param name="filePath">與解析之檔案路徑</param>
        public static bool ResolveFile(String filePath)
        {
            //檢查檔案
            if (!File.Exists(filePath))
            {
                return false;
            }

            //初始化資料
            byte[] inputFileBytes = File.ReadAllBytes(filePath);
            int startIdx = 0;
            int endIdx = -1;

            while (endIdx != inputFileBytes.Length)
            {
                //搜尋第一個識別字位元組
                startIdx = IndexShikibetsu(inputFileBytes, startIdx);
                endIdx = IndexShikibetsu(inputFileBytes, startIdx + SHIKIBETSU_BYTES.Length);

                if (startIdx == -1
                    || endIdx == -1)
                {
                    //搜不到識別字位元組
                    return false;
                }

                //取得原檔名
                byte[] fileNameBytes = GetBytes(inputFileBytes, startIdx + SHIKIBETSU_BYTES.Length, endIdx);
                String fileName = Encoding.ASCII.GetString(fileNameBytes);

                //搜尋原檔案結尾
                startIdx = endIdx;
                endIdx = IndexShikibetsu(inputFileBytes, startIdx + SHIKIBETSU_BYTES.Length);
                endIdx = endIdx == -1 ? inputFileBytes.Length : endIdx;

                //取得原檔案
                byte[] fileBytes = GetBytes(inputFileBytes, startIdx + SHIKIBETSU_BYTES.Length, endIdx);
                FileStream outputFS = File.Create(fileName);
                foreach (byte b in fileBytes)
                {
                    outputFS.WriteByte(b);
                }
                outputFS.Flush();
                startIdx = endIdx;

                outputFS.Close();
            }
            return true;
        }
        #endregion


        #region Private Method
        /// <summary>
        /// 搜尋識別字位元組起始位置
        /// </summary>
        /// <param name="inputFileBytes">檔案資料陣列</param>
        /// <param name="offset">起始搜尋位置</param>
        /// <returns>識別字位元組在檔案資料陣列的起始位置，若未搜尋到則回傳值為-1</returns>
        private static int IndexShikibetsu(byte[] inputFileBytes, int offset)
        {
            int matchIdx = -1;
            for (int i = offset; i < inputFileBytes.Length - FileCtrl.SHIKIBETSU_BYTES.Length && matchIdx == -1; i++)
            {
                for (int j = 0; j < FileCtrl.SHIKIBETSU_BYTES.Length; j++)
                {
                    if (inputFileBytes[i + j] != FileCtrl.SHIKIBETSU_BYTES[j])
                    {
                        break;
                    }
                    if (j == FileCtrl.SHIKIBETSU_BYTES.Length - 1)
                    {
                        matchIdx = i;
                    }
                }
            }
            return matchIdx;
        }

        /// <summary>
        /// 擷取資料位元組
        /// </summary>
        /// <param name="inputFileBytes">檔案資料陣列</param>
        /// <param name="startIdx">擷取位元組的起始位置</param>
        /// <param name="endIdx">擷取位元組結束位置</param>
        private static byte[] GetBytes(byte[] inputFileBytes, int startIdx, int endIdx)
        {
            byte[] ret = new byte[100];
            Array.Resize<byte>(ref ret, endIdx - startIdx);

            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = inputFileBytes[startIdx + i];
            }

            return ret;
        }
        #endregion

    }
}
