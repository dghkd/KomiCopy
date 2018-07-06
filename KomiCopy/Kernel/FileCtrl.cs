using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using KomiCopy.Crypt;

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
        /// <summary>
        /// 識別字字串2
        /// </summary>
        private static readonly String SHIKIBETSU2 = "dghkd_komicopy";
        /// <summary>
        /// 識別字2位元組
        /// </summary>
        private static readonly byte[] SHIKIBETSU2_BYTES = Encoding.ASCII.GetBytes(SHIKIBETSU2);

        /// <summary>
        /// 加解密處理用暫存檔
        /// <para>TEMP_SUB_FILE_NAME = "SubFile.tmp"</para>
        /// </summary>
        private static readonly String TEMP_SUB_FILE_NAME = "SubFile.tmp";
        #endregion


        #region Public Member
        /// <summary>
        /// 加解密控制器
        /// </summary>
        public static CryptCtrl Cryptor = new CryptCtrl();
        #endregion


        #region Public Method
        /// <summary>
        /// 合併檔案
        /// <para>以第一個檔案為基礎，合併第二個以後的檔案</para>
        /// </summary>
        /// <param name="fileList">欲合併之檔案路徑</param>
        /// <param name="isEncrypt">是否使用加密</param>
        /// <param name="anotherPubKeyText">對方公鑰密文(Base58字串)</param>
        public static bool MergeFiles(List<String> fileList,bool isEncrypt,String anotherPubKeyText)
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

            //合併第二個以後的所有檔
            String subTmpFileName = MergeSubFiles(fileList);

            //是否進行加密
            if (isEncrypt)
            {
                //將第二個以後所有檔合併的暫存檔進行加密
                if (!EncryptSubFile(subTmpFileName, anotherPubKeyText))
                {
                    return false;
                }
            }

            //將合併後的暫存檔與複製的第一份檔合併
            byte[] subFileBytes = File.ReadAllBytes(subTmpFileName);
            foreach (byte b in subFileBytes)
            {
                outputFile.WriteByte(b);
            }
            outputFile.Flush();

            //移除暫存檔與關閉最終輸出檔控制
            File.Delete(subTmpFileName);
            outputFile.Close();

            return true;
        }


        /// <summary>
        /// 解析檔案
        /// <para>解析被附加在第一個檔案之後的所有檔案</para>
        /// </summary>
        /// <param name="filePath">欲解析之檔案路徑</param>
        /// <param name="isDecrypt">是否進行解密</param>
        public static bool ResolveFile(String filePath, bool isDecrypt)
        {
            //檢查檔案
            if (!File.Exists(filePath))
            {
                return false;
            }

            //是否進行解密
            if (isDecrypt)
            {
                //將加密檔分離並解密，結束後將依暫存檔路徑產生解密後的暫存檔
                if (DecryptSubFile(filePath))
                {
                    //解密成功，將要解析之檔案路徑替換為暫存檔路徑
                    filePath = TEMP_SUB_FILE_NAME;
                }
                else
                {
                    return false;
                }   
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
                String fileName = Encoding.Unicode.GetString(fileNameBytes);

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

            if (isDecrypt)
            {
                //檔案解析完畢，移除暫存檔
                File.Delete(TEMP_SUB_FILE_NAME);
            }

            return true;
        }
        #endregion


        #region Private Method
        /// <summary>
        /// 合併要附加的檔案，除List的第一個檔案以外
        /// <para>合併完成後回傳暫存檔路徑</para>
        /// </summary>
        /// <param name="fileList">所有要合併的檔案路徑</param>
        /// <returns>合併完成後回傳已合併的暫存檔路徑</returns>
        private static String MergeSubFiles(List<string> fileList)
        {
            FileStream outputSubFile = File.Create(TEMP_SUB_FILE_NAME);

            //合併所有指定檔案
            for (int i = 1; i < fileList.Count; i++)
            {
                String filePath = fileList[i];
                //插入識別字位元組
                foreach (byte b in SHIKIBETSU_BYTES)
                {
                    outputSubFile.WriteByte(b);
                }
                outputSubFile.Flush();

                //插入附加檔檔名
                String fileName = Path.GetFileName(filePath);
                byte[] fileNameBytes = Encoding.Unicode.GetBytes(fileName);
                foreach (byte b in fileNameBytes)
                {
                    outputSubFile.WriteByte(b);
                }
                outputSubFile.Flush();

                //插入識別字位元組
                foreach (byte b in SHIKIBETSU_BYTES)
                {
                    outputSubFile.WriteByte(b);
                }
                outputSubFile.Flush();

                //插入附加檔案
                byte[] appendFileBytes = File.ReadAllBytes(filePath);
                foreach (byte b in appendFileBytes)
                {
                    outputSubFile.WriteByte(b);
                }
                outputSubFile.Flush();
            }
            outputSubFile.Close();
            return TEMP_SUB_FILE_NAME;
        }

        /// <summary>
        /// 加密檔案
        /// </summary>
        /// <param name="filePath">欲加密的檔案路徑</param>
        /// <param name="anotherPubKeyText">對方密文(Base58字串)。可以逗號,區隔多組密文</param>
        private static bool EncryptSubFile(String filePath, String anotherPubKeyText)
        {
            //隨機產生32byte(for AES256)密鑰和16byte向量值.
            byte[] keyBytes = ArrayHelpers.ConcatArrays(Guid.NewGuid().ToByteArray(), Guid.NewGuid().ToByteArray());
            byte[] ivBytes = Guid.NewGuid().ToByteArray();

            //以AES加密法加密檔案
            bool bEncryptFile = FileCtrl.Cryptor.AesEncryptFile(filePath, keyBytes, ivBytes);
            if (!bEncryptFile)
            {
                return false;
            }

            //取加密檔檔案的前8byte當作ECC加密的derivation和encoding參數
            byte[] encryptFileBytes = File.ReadAllBytes(filePath);
            byte[] derivation = ArrayHelpers.SubArray(encryptFileBytes, 0, 8);
            byte[] encoding = derivation.Reverse().ToArray();

            //加密並封裝AES密鑰
            byte[] encryptKeyIV = EncryptAesKeyIV(anotherPubKeyText, keyBytes, ivBytes, derivation, encoding);

            //格式化封裝加密檔案
            List<byte> finalFile = new List<byte>();
            finalFile.AddRange(SHIKIBETSU_BYTES);
            finalFile.AddRange(FileCtrl.Cryptor.PubKeyBytes);
            finalFile.AddRange(SHIKIBETSU_BYTES);
            finalFile.AddRange(encryptKeyIV);
            finalFile.AddRange(SHIKIBETSU_BYTES);
            finalFile.AddRange(encryptFileBytes);
            //輸出最終封裝之加密檔
            File.WriteAllBytes(filePath, finalFile.ToArray());

            return true;
        }

        /// <summary>
        /// 加密封裝AES密鑰
        /// </summary>
        /// <param name="anotherPubKeyText">對方密文(Base58字串)。可以逗號,區隔多組密文<</param>
        /// <param name="keyBytes">AES密鑰key資料</param>
        /// <param name="ivBytes">AES向量值資料</param>
        /// <param name="derivation">ECC加密參數</param>
        /// <param name="encoding">ECC加密參數</param>
        /// <returns>回傳格式化封裝加密後資料</returns>
        private static byte[] EncryptAesKeyIV(String anotherPubKeyText, byte[] keyBytes, byte[] ivBytes, byte[] derivation, byte[] encoding)
        {
            List<byte> result = new List<byte>();
            byte[] aesKeyIVBytes = ArrayHelpers.ConcatArrays(keyBytes, ivBytes);

            String[] pubKeys = anotherPubKeyText.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (String anoPubKey in pubKeys)
            {
                //使用對方公鑰加密AES密鑰資料，AES密鑰資料=密鑰與向量值的串接，最後16byte固定為向量值
                byte[] anotherPubKeyBytes = Base58.Decode(anoPubKey);
                byte[] encryptKeyIV = FileCtrl.Cryptor.EncryptData(aesKeyIVBytes, anotherPubKeyBytes, derivation, encoding);
                //格式化封裝加密資料
                result.AddRange(SHIKIBETSU2_BYTES);
                result.AddRange(encryptKeyIV);
            }

            return result.ToArray();
        }

        /// <summary>
        /// 解密檔案
        /// </summary>
        /// <param name="filePath">欲解密的檔案路徑</param>
        private static bool DecryptSubFile(String filePath)
        {
            //讀取檔案資料
            byte[] inputFileBytes = File.ReadAllBytes(filePath);

            //解析加密資料
            List<byte[]> headerData = ParseEncryptHeader(inputFileBytes);
            //依格式化封裝的加密檔必須含有對方公鑰, 加密密鑰和加密檔三項資料
            if (headerData.Count != 3)
            {
                return false;
            }

            //依序取出 0:對方公鑰, 1:加密密鑰, 2:加密檔
            byte[] anotherPubKeyBytes = headerData.ElementAt(0);
            byte[] encryptKeyIVBytes = headerData.ElementAt(1);
            byte[] encryptFileBytes = headerData.ElementAt(2);
            //取加密檔檔案的前8byte當作ECC加密的derivation和encoding參數
            byte[] derivation = ArrayHelpers.SubArray(encryptFileBytes, 0, 8);
            byte[] encoding = derivation.Reverse().ToArray();

            //解密封裝的AES密鑰
            byte[] aesKeyIV = DecryptAesKeyIV(encryptKeyIVBytes, anotherPubKeyBytes, derivation, encoding);
            if (aesKeyIV == null)
            {
                return false;
            }
            //取出AES密鑰(AES256: key = 32byte)與向量值(iv固定為最後16byte)
            byte[] key = ArrayHelpers.SubArray(aesKeyIV, 0, aesKeyIV.Length - 16);
            byte[] iv = ArrayHelpers.SubArray(aesKeyIV, aesKeyIV.Length - 16, 16);
            
            //使用密鑰解密加密檔案
            File.WriteAllBytes(TEMP_SUB_FILE_NAME, encryptFileBytes);
            bool bDecryptSubFile = FileCtrl.Cryptor.AesDecryptFile(TEMP_SUB_FILE_NAME, key, iv);
            if (!bDecryptSubFile)
            {
                return false;
            }

            //解密成功，解密的檔案為暫存檔，路徑: TEMP_SUB_FILE_NAME
            //後續分解附加檔請從此暫存檔進行分解
            return true;
        }

        /// <summary>
        /// 解密已封裝的AES密鑰資料
        /// </summary>
        /// <param name="encryptKeyIVBytes">欲解密的加密資料。包含封裝多組的加密資料</param>
        /// <param name="anotherPubKeyBytes">對方公鑰資料</param>
        /// <param name="derivation">ECC解密參數</param>
        /// <param name="encoding">ECC解密參數</param>
        /// <returns>回傳解密後的AES密鑰資料，若解密失敗則回傳Null</returns>
        private static byte[] DecryptAesKeyIV(byte[] encryptKeyIVBytes, byte[] anotherPubKeyBytes, byte[] derivation, byte[] encoding)
        {
            byte[] ret = null;

            //拆解封裝的多組資料
            List<byte[]> encryptDataList = ParseEncryptAesKeyIvData(encryptKeyIVBytes);

            //解密各組資料
            foreach (byte[] encryptData in encryptDataList)
            {
                byte[] aesKeyIV = FileCtrl.Cryptor.DecrytpData(encryptData, anotherPubKeyBytes, derivation, encoding);
                if (aesKeyIV != null)
                {
                    ret = aesKeyIV;
                    break;
                }
            }

            return ret;
        }

        /// <summary>
        /// 擷取格式化封裝之加密AES密鑰資料
        /// </summary>
        /// <param name="encryptKeyIVBytes">欲解密的加密資料。包含封裝多組的加密資料</param>
        /// <returns></returns>
        private static List<byte[]> ParseEncryptAesKeyIvData(byte[] encryptKeyIVBytes)
        {
            List<byte[]> ret = new List<byte[]>();
            int startIdx = 0;
            int endIdx = -1;

            while (endIdx != encryptKeyIVBytes.Length)
            {
                //搜尋識別字位元組
                startIdx = IndexShikibetsu2(encryptKeyIVBytes, startIdx);
                endIdx = IndexShikibetsu2(encryptKeyIVBytes, startIdx + SHIKIBETSU2_BYTES.Length);
                endIdx = endIdx == -1 ? encryptKeyIVBytes.Length : endIdx;

                if (startIdx == -1)
                {
                    //已搜不到識別字位元組

                    if (ret.Count == 0)
                    {
                        //無法取出任何區段資料，可能只有一組加密資料而未進行格式化封裝(2.0版行為)
                        //直接擷取整段資料
                        ret.Add(encryptKeyIVBytes);
                    }

                    return ret;
                }

                //取出區段資料
                byte[] data = GetBytes(encryptKeyIVBytes, startIdx + SHIKIBETSU2_BYTES.Length, endIdx);
                ret.Add(data);

                //移動下一次搜尋起始位置
                startIdx = endIdx;
            }

            return ret;
        }

        /// <summary>
        /// 擷取格式化封裝之加密資料
        /// <para>擷取區段以SHIKIBETSU_BYTES為標記</para>
        /// </summary>
        /// <param name="fileBytes">格式化封裝之加密檔資料</param>
        /// <returns>回傳以SHIKIBETSU_BYTES為標記封裝的區段資料集合</returns>
        private static List<byte[]> ParseEncryptHeader(byte[] fileBytes)
        {
            List<byte[]> ret = new List<byte[]>();
            int startIdx = 0;
            int endIdx = -1;

            while (endIdx != fileBytes.Length)
            {
                //搜尋識別字位元組
                startIdx = IndexShikibetsu(fileBytes, startIdx);
                endIdx = IndexShikibetsu(fileBytes, startIdx + SHIKIBETSU_BYTES.Length);
                endIdx = endIdx == -1 ? fileBytes.Length : endIdx;

                if (startIdx == -1)
                {
                    //已搜不到識別字位元組
                    return ret;
                }

                //取出區段資料
                byte[] data = GetBytes(fileBytes, startIdx + SHIKIBETSU_BYTES.Length, endIdx);
                ret.Add(data);

                //移動下一次搜尋起始位置
                startIdx = endIdx;
            }

            return ret;
        }

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
        /// 搜尋識別字2的位元組起始位置
        /// </summary>
        /// <param name="inputFileBytes">檔案資料陣列</param>
        /// <param name="offset">起始搜尋位置</param>
        /// <returns>識別字2的位元組在檔案資料陣列的起始位置，若未搜尋到則回傳值為-1</returns>
        private static int IndexShikibetsu2(byte[] inputFileBytes, int offset)
        {
            int matchIdx = -1;
            for (int i = offset; i < inputFileBytes.Length - FileCtrl.SHIKIBETSU2_BYTES.Length && matchIdx == -1; i++)
            {
                byte[] tmpBytes = ArrayHelpers.SubArray(inputFileBytes, i, FileCtrl.SHIKIBETSU2_BYTES.Length);
                if (FileCtrl.SHIKIBETSU2_BYTES.SequenceEqual(tmpBytes) == true)
                {
                    matchIdx = i;
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
        public static byte[] GetBytes(byte[] inputFileBytes, int startIdx, int endIdx)
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
