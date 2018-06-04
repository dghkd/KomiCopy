# KomiCopy
檔案合併、解析工具

![](https://raw.githubusercontent.com/dghkd/KomiCopy/master/preview1.png)

操作方法:  
合併時  
1. 左上角選擇數量  
2. 將要合併的檔案拖放到欄位中  
3. 點擊合併    
合併的檔案將以左邊第一個欄位的檔案為基礎進行合併  
輸出檔名為當下時間

解析時  
1. 點選解析頁面  
2. 拖放要解析的檔案到欄位中  
3. 點擊分解  
解析成功後，即可在同一資料夾內看到原本被附加合併的檔案  

##加密功能
###加密方
若要使用加密功能需要**先取得對方密文**(Base58編碼字串)  
並在合併時勾選"使用專屬加密"後將對方密文貼入下方空白欄位中  
再點擊合併，即可產生只有對方可以解密的檔案  

###解密方
接收方/解密者 需要先公布自己密文讓對方取得  
切換至 密文設定 頁面即可看到自己的密文  
當對方使用你的密文進行專屬加密後，就只有產生此密文的你可以解密那份檔案  
**因此請勿在未完成解密前重新建立新的密文**  
這樣會導致你再也無法解密對方用你前一次密文加密的檔案  
若你確定沒有要解密的檔案，就可任意重建新密文，但程式只會記錄最新的一組  

###加密方法
加密功能採用ECC + AES混合加密  
ECC使用secp128r2定義橢圓(OID:1.3.132.0.29)
AES使用隨機產生2組GUID(32byte)作為KEY和1組隨機產生GUID(16byte)作為IV

1. 先合併除第一個檔案外的所有附加檔  
2. 以AES對合併後的所有附加檔進行加密  
	2-1. 隨機產生3組GUID  
	2-2. 前2組串接為32byte資料作為AES加密密鑰(Key: 256bit)  
	2-3. 第3組作為AES加密向量值(IV: 128bit)  
3. 以ECC對AES的密鑰與向量值進行加密  
	3-1. 取出AES加密檔案的前8個byte當作ECC加密的derivation參數  
	3-2. 將derivation參數反轉後當作ECC加密的encoding參數  
	3-3. 將AES的密鑰與向量值串接成32byte + 16byte資料  
	3-4. 以對方公鑰密文資料和3-1, 3-2步驟的derivation、encoding為參數，對AES的密鑰與向量值資料進行加密  
4. 依序寫入資料產生封裝檔  
	4-1. 加密者的ECC公鑰(供對方解密時金鑰交換使用)  
	4-2. 以ECC加密過的AES密鑰和向量值資料  
	4-3. 以AES加密過的合併附加檔  
※寫入每個資料前皆會插入識別字以區隔資料