using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace KomiCopy.Crypt
{
    public class CryptCtrl
    {
        #region Const Values
        private const String Curve_OID_secp112r1 = "1.3.132.0.6";
        private const String Curve_OID_secp112r2 = "1.3.132.0.7";
        private const String Curve_OID_secp128r1 = "1.3.132.0.28";
        /// <summary>
        /// Curve_OID_secp128r2 = "1.3.132.0.29"
        /// <para>Reference: http://oidref.com/1.3.132.0 </para>
        /// </summary>
        private const String Curve_OID_secp128r2 = "1.3.132.0.29";

        /// <summary>
        /// PublicKey_Header_Size = 24
        /// </summary>
        private const int PublicKey_Header_Size = 24;

        /// <summary>
        /// KeyPair_FileName = @"LocalHostKeyPair"
        /// </summary>
        public const String KeyPair_FileName = @"LocalHostKeyPair";
        /// <summary>
        /// KeyPair_FilePath = @"C:\Users\[user account]\AppData\Roaming\[KeyPair_FileName]
        /// </summary>
        public static readonly String KeyPair_FilePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + KeyPair_FileName;
        #endregion


        #region Private Member
        private ECKeyPairGenerator _ecKeyGenerator;
        private AsymmetricCipherKeyPair _ecLocalKeyPair;
        private int _keyFieldSize = 0;

        private byte[] _pubKeyHeaderBytes = null;
        private byte[] _pubKeyInfoBytes = null;
        private byte[] _pubKeyBytes = null;
        #endregion


        #region Constructor
        public CryptCtrl()
        {
            _ecKeyGenerator = new ECKeyPairGenerator();
            _ecKeyGenerator.Init(new ECKeyGenerationParameters(new DerObjectIdentifier(Curve_OID_secp128r2), new SecureRandom()));

            _ecLocalKeyPair = _ecKeyGenerator.GenerateKeyPair();
            _keyFieldSize = this.GetKeyFieldSize();
        }
        #endregion


        #region Public Method
        /// <summary>
        /// 建立本機公私密鑰並儲存
        /// </summary>
        /// <param name="path">儲存檔案路徑,若為空值則採用預設路徑(See: KeyPair_FilePath)</param>
        public void CreateLocalHostKeyPair(String path = "")
        {
            //儲存檔路徑若為空值則採用預設路徑
            path = (path == "" || path == null) ? KeyPair_FilePath : path;

            //建立儲存檔路徑之父資料夾
            if (!Directory.Exists(Path.GetDirectoryName(path)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }

            //建立公鑰與私鑰
            _ecLocalKeyPair = _ecKeyGenerator.GenerateKeyPair();
            ECPublicKeyParameters pubKey = _ecLocalKeyPair.Public as ECPublicKeyParameters;
            ECPrivateKeyParameters privKey = _ecLocalKeyPair.Private as ECPrivateKeyParameters;
            byte[] pubKeyBytes = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(pubKey).GetEncoded();
            byte[] privKeyBytes = PrivateKeyInfoFactory.CreatePrivateKeyInfo(privKey).GetEncoded();

            //儲存至指定檔案
            FileStream ecKeyFs = File.Create(path);
            //寫入公鑰資訊
            foreach (byte b in pubKeyBytes)
            {
                ecKeyFs.WriteByte(b);
            }
            //寫入私鑰資訊
            foreach (byte b in privKeyBytes)
            {
                ecKeyFs.WriteByte(b);
            }

            ecKeyFs.Flush();
            ecKeyFs.Close();

            //清除舊資料
            _pubKeyHeaderBytes = null;
            _pubKeyInfoBytes = null;
            _pubKeyBytes = null;
        }

        /// <summary>
        /// 讀取本機公私密鑰資訊
        /// </summary>
        /// <param name="path">指定檔案路徑,若為空值則採用預設路徑(See: KeyPair_FilePath)</param>
        /// <returns></returns>
        public bool LoadLocalHostKeyPair(String path = "")
        {
            //指定檔路徑若為空值則採用預設路徑
            path = (path == "" || path == null) ? KeyPair_FilePath : path;

            //若檔案不存在則重新建立
            if (!File.Exists(path))
            {
                CreateLocalHostKeyPair();
            }

            //讀取檔案取得公私鑰資訊
            byte[] allBytes = File.ReadAllBytes(path);
            int pubKeyInfoSize = this.GetKeyFieldSize() / 8 * 2 + PublicKey_Header_Size;

            byte[] pubKeyBytes = ArrayHelpers.SubArray(allBytes, 0, pubKeyInfoSize);
            byte[] privKeyBytes = ArrayHelpers.SubArray(allBytes, pubKeyInfoSize, allBytes.Length - pubKeyInfoSize);

            try
            {
                ECPublicKeyParameters pubKey = PublicKeyFactory.CreateKey(pubKeyBytes) as ECPublicKeyParameters;
                ECPrivateKeyParameters privKey = PrivateKeyFactory.CreateKey(privKeyBytes) as ECPrivateKeyParameters;

                _ecLocalKeyPair = new AsymmetricCipherKeyPair(pubKey, privKey);
            }
            catch (Exception e)
            {
                Debug.WriteLine(String.Format("[LoadLocalHostKeyPair] Create key fail:{0}", e.Message));
                return false;
            }

            _pubKeyHeaderBytes = null;
            _pubKeyInfoBytes = null;
            _pubKeyBytes = null;

            return true;
        }

        /// <summary>
        /// 以ECC演算法加密資料
        /// </summary>
        /// <param name="originalData">原始資料</param>
        /// <param name="anotherPubKey">解密者使用的公鑰資料</param>
        /// <param name="derivation">The derivation parameter for the KDF function.</param>
        /// <param name="encoding">The encoding parameter for the KDF function.</param>
        public byte[] EncryptData(byte[] originalData, byte[] anotherPubKey, byte[] derivation, byte[] encoding)
        {
            if (_ecLocalKeyPair == null)
            {
                Debug.WriteLine(String.Format("[EncryptData] Local key pair not create."));
                return null;
            }

            byte[] ret = null;

            //ECPublicKeyParameters = public key header(24byte) + another public key(key field size - 24byte,)
            byte[] anoPubKeyInfoBytes = ArrayHelpers.ConcatArrays(this.PubKeyHeaderBytes, anotherPubKey);
            ECPublicKeyParameters anoPubKeyParam = PublicKeyFactory.CreateKey(anoPubKeyInfoBytes) as ECPublicKeyParameters;

            IesEngine ies = new IesEngine(
                new ECDHBasicAgreement(),
                new Kdf2BytesGenerator(new Sha256Digest()),
                new HMac(new Sha256Digest()));
            IesParameters iesParam = new IesParameters(derivation, encoding, 256);

            try
            {
                ies.Init(true, _ecLocalKeyPair.Private, anoPubKeyParam, iesParam);
                ret = ies.ProcessBlock(originalData, 0, originalData.Length);
            }
            catch (Exception e)
            {
                Debug.WriteLine(String.Format("[EncryptData] Init IES Enging fail:{0}", e.Message));
                return null;
            }

            return ret;
        }

        /// <summary>
        /// 以ECC演算法解密資料
        /// </summary>
        /// <param name="encryptData">被加密的資料</param>
        /// <param name="anotherPubKey">加密者使用的公鑰資料</param>
        /// <param name="derivation">The derivation parameter for the KDF function.</param>
        /// <param name="encoding">The encoding parameter for the KDF function.</param>
        public byte[] DecrytpData(byte[] encryptData, byte[] anotherPubKey, byte[] derivation, byte[] encoding)
        {
            if (_ecLocalKeyPair == null)
            {
                Debug.WriteLine(String.Format("[DecrytpData] Local key pair not create."));
                return null;
            }

            byte[] ret = null;

            //ECPublicKeyParameters = public key header(24byte) + another public key(key field size - 24byte,)
            List<byte> anoPubKeyByteList = new List<byte>();
            anoPubKeyByteList.AddRange(this.PubKeyHeaderBytes);
            anoPubKeyByteList.AddRange(anotherPubKey);
            byte[] anoPubKeyInfoBytes = anoPubKeyByteList.ToArray();
            ECPublicKeyParameters anoPubKeyParam = PublicKeyFactory.CreateKey(anoPubKeyInfoBytes) as ECPublicKeyParameters;

            IesEngine ies = new IesEngine(
                new ECDHBasicAgreement(),
                new Kdf2BytesGenerator(new Sha256Digest()),
                new HMac(new Sha256Digest()));
            IesParameters iesParam = new IesParameters(derivation, encoding, 256);

            try
            {
                ies.Init(false, _ecLocalKeyPair.Private, anoPubKeyParam, iesParam);
                ret = ies.ProcessBlock(encryptData, 0, encryptData.Length);
            }
            catch (Exception e)
            {
                Debug.WriteLine(String.Format("[EncryptData] IES Enging fail:{0}", e.Message));
                return null;
            }

            return ret;
        }

        /// <summary>
        /// 以AES加密檔案
        /// <para>注意:加密後的檔案會直接覆蓋原檔</para>
        /// </summary>
        /// <param name="filePath">欲加密之檔案路徑</param>
        /// <param name="key">密鑰值，必須為16、24、32byte其中一個長度，分別對應AES128、192、256加密</param>
        /// <param name="iv">向量值，必須為16byte</param>
        public bool AesEncryptFile(String filePath, byte[] key, byte[] iv)
        {
            try
            {
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = key;
                    aesAlg.IV = iv;
                    ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                    byte[] fileBytes = File.ReadAllBytes(filePath);
                    byte[] encryptedBytes = encryptor.TransformFinalBlock(fileBytes, 0, fileBytes.Length);

                    File.WriteAllBytes(filePath, encryptedBytes);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(String.Format("[AesEncryptFile] Encrypt file fail:{0}", e.Message));
                return false;
            }

            return true;
        }

        /// <summary>
        /// 以AES解密檔案
        /// <para>注意:解密後的檔案會直接覆蓋原檔</para>
        /// </summary>
        /// <param name="filePath">欲解密之檔案路徑</param>
        /// <param name="key">密鑰值，必須為16、24、32byte其中一個長度，分別對應AES128、192、256加密</param>
        /// <param name="iv">向量值，必須為16byte</param>
        public bool AesDecryptFile(String filePath, byte[] key, byte[] iv)
        {
            try
            {
                using (Aes aesAlg = Aes.Create())
                {
                    aesAlg.Key = key;
                    aesAlg.IV = iv;
                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                    byte[] encryptBytes = File.ReadAllBytes(filePath);
                    byte[] decryptBytes = decryptor.TransformFinalBlock(encryptBytes, 0, encryptBytes.Length);

                    File.WriteAllBytes(filePath, decryptBytes);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(String.Format("[AesDecryptFile] Decrypt file fail:{0}", e.Message));
                return false;
            }

            return true;
        }

        /// <summary>
        /// 判斷是否為合法公鑰密文
        /// </summary>
        /// <param name="pubKeyText">公鑰密文(Base58字串)</param>
        public bool IsVaildPublicKeyText(String pubKeyText)
        {
            byte[] pubKeyBytes;
            try
            {
                pubKeyBytes = Base58.Decode(pubKeyText);
            }
            catch (Exception e)
            {
                Debug.WriteLine(String.Format("[IsVaildPublicKeyText] Decode Base58 fail:{0}", e.Message));
                return false;
            }

            //ECPublicKeyParameters = public key header(24byte) + another public key(key field size - 24byte,)
            List<byte> anoPubKeyByteList = new List<byte>();
            anoPubKeyByteList.AddRange(this.PubKeyHeaderBytes);
            anoPubKeyByteList.AddRange(pubKeyBytes);
            byte[] anoPubKeyInfoBytes = anoPubKeyByteList.ToArray();

            //Try create public key parameter.
            try
            {
                ECPublicKeyParameters anoPubKeyParam = PublicKeyFactory.CreateKey(anoPubKeyInfoBytes) as ECPublicKeyParameters;
            }
            catch (Exception e)
            {
                Debug.WriteLine(String.Format("[IsVaildPublicKeyText] Create Public Key fail:{0}", e.Message));
                return false;
            }

            return true;
        }
        #endregion


        #region Public Key Field
        /// <summary>
        /// 取得本機公鑰全文(含固定標頭)資料
        /// </summary>
        public byte[] PubKeyInfoBytes
        {
            get
            {
                if (_pubKeyInfoBytes == null)
                {
                    ECPublicKeyParameters pubKey = _ecLocalKeyPair.Public as ECPublicKeyParameters;
                    _pubKeyInfoBytes = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(pubKey).GetEncoded();
                }
                return _pubKeyInfoBytes;
            }
        }

        /// <summary>
        /// 取得本機公鑰資料
        /// </summary>
        public byte[] PubKeyBytes
        {
            get
            {
                if (_pubKeyBytes == null)
                {
                    _pubKeyBytes = ArrayHelpers.SubArray(this.PubKeyInfoBytes, PublicKey_Header_Size, this.GetPubKeyByteSize());
                }
                return _pubKeyBytes;
            }
        }

        /// <summary>
        /// 取得公鑰標頭資料
        /// <para>若採用不同的橢圓或定義域，則會產生不同的標頭資料</para>
        /// </summary>
        public byte[] PubKeyHeaderBytes
        {
            get
            {
                if (_pubKeyHeaderBytes == null)
                {
                    _pubKeyHeaderBytes = ArrayHelpers.SubArray(this.PubKeyInfoBytes, 0, PublicKey_Header_Size);
                }
                return _pubKeyHeaderBytes;
            }
        }

        /// <summary>
        /// 取得本機公鑰密文字串
        /// </summary>
        /// <returns>回傳Base58編碼字串</returns>
        public String GetPublicKeyText()
        {
            ECPublicKeyParameters pubKey = _ecLocalKeyPair.Public as ECPublicKeyParameters;
            byte[] pubKeyInfoBytes = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(pubKey).GetEncoded();
            byte[] pubKeyBytes = ArrayHelpers.SubArray(pubKeyInfoBytes, PublicKey_Header_Size, this.GetPubKeyByteSize());

            return Base58.Encode(pubKeyBytes);
        }

        /// <summary>
        /// 取得公鑰byte大小
        /// <para>Ex: secp128r2 key field siz = 128bit = 16bytes</para>
        /// <para>  Private key size = 16byte, Public key size = 32byte </para>
        /// </summary>
        public int GetPubKeyByteSize()
        {
            return this.GetKeyFieldSize() / 8 * 2;
        }

        /// <summary>
        /// 取得密鑰長度
        /// </summary>
        /// <para>Ex: secp128r2 key field siz = 128</para>
        /// <example>
        /// secp112r2 key field siz = 112
        /// secp128r2 key field siz = 128
        /// </example>
        /// <returns>回傳所使用的密鑰長度，以bit為單位</returns>
        public int GetKeyFieldSize()
        {
            if (_keyFieldSize == 0)
            {
                AsymmetricCipherKeyPair tmpKeyPair = _ecKeyGenerator.GenerateKeyPair();
                ECPublicKeyParameters tmpPubKey = tmpKeyPair.Public as ECPublicKeyParameters;
                _keyFieldSize = tmpPubKey.Parameters.Curve.FieldSize;
            }
            return _keyFieldSize;
        }

        #endregion
    }
}
