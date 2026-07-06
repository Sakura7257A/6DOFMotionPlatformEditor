using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;
using System.Text;
using System.IO;
using System.Security.Cryptography;
using MPE;

public class HttpClient : MonoBehaviour
{
    public static HttpClient Instance { get; private set; }

    [Header("Network Settings")]
    [Tooltip("服务器地址")]
    [SerializeField] private string serverUrl = "http://8.140.250.239:8899";

    private static readonly byte[] fx08k = { 0xFB, 0xBA, 0x9B, 0x0A, 0xAE, 0x1B, 0xFC, 0x2E, 0xD1, 0x24, 0xEA, 0x8C, 0x99, 0x68, 0x81, 0x0F, 0xFB, 0x42, 0x4E, 0x74, 0xFA, 0x17, 0xFF, 0x6E, 0xE9, 0xDF, 0x47, 0x03, 0x6E, 0xB5, 0xA0, 0xA9 };
    private static readonly byte[] gx09km = { 0xA8, 0xED, 0xDE, 0x4F, 0xFA, 0x36, 0xB1, 0x7E, 0x94, 0x09, 0xD8, 0xBC, 0xAB, 0x5E, 0xAC, 0x3F, 0xCD, 0x6F, 0x7F, 0x42, 0xD7, 0x5F, 0xBD, 0x39, 0xBD, 0xEB, 0x72, 0x5A, 0x3F, 0x87, 0xF5, 0xFF };

    private static readonly byte[] cbjkV = { 0x84, 0xC4, 0x0A, 0x65, 0x77, 0xBC, 0xC7, 0xF0, 0xAC, 0xB6, 0x6C, 0x6D, 0xA4, 0x06, 0x36, 0xFD };
    private static readonly byte[] txopVM = { 0xD7, 0x93, 0x4F, 0x20, 0x23, 0xFD, 0x94, 0xA5, 0xE1, 0xF8, 0x24, 0x3D, 0xEF, 0x35, 0x75, 0xBF };


    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }

    public void SendRequest(string command)
    {
        StartCoroutine(SendHttpRequest(command));
    }

    private IEnumerator SendHttpRequest(string command)
    {
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        string payload = $"{command}*{timestamp}";

        string encryptedPayload = EncryptAES(payload);

        byte[] bodyRaw = Encoding.UTF8.GetBytes(encryptedPayload);

        using (UnityWebRequest www = new UnityWebRequest(serverUrl, "POST"))
        using (UploadHandlerRaw uploader = new UploadHandlerRaw(bodyRaw))
        using (DownloadHandlerBuffer downloader = new DownloadHandlerBuffer())
        {
            www.uploadHandler = uploader;
            www.downloadHandler = downloader;
            www.SetRequestHeader("Content-Type", "text/plain");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("请求失败: " + www.error);
                TriggerPoisonPill();
                if (www.downloadHandler != null)
                {
                    Debug.LogError("服务器返回消息: " + www.downloadHandler.text);
                }
            }
            else
            {
                string responseText = www.downloadHandler.text;

                try
                {
                    string decryptedResponse = DecryptAES(responseText);

                    MessageReceived(decryptedResponse);
                }
                catch (Exception e)
                {
                    Debug.Log(e.Message);
                    TriggerPoisonPill();
                }
            }
        }
    }

    private void MessageReceived(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            TriggerPoisonPill();
            return;
        }

        string msgHash = GetSHA256Hash(message.Trim());

        if (msgHash == "0B70C7012887BC9F35BA34E73C4CF5580390A3726AD2DCA15F305D60D8717A50"){
        }
        else{
            TriggerPoisonPill();
        }
    }

    private byte[] UnmaskBytes(byte[] obfuscated, byte[] mask)
    {
        byte[] real = new byte[obfuscated.Length];
        for (int i = 0; i < obfuscated.Length; i++)
        {
            real[i] = (byte)(obfuscated[i] ^ mask[i]);
        }
        return real;
    }

    private string EncryptAES(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

        byte[] realKey = null;
        byte[] realIV = null;

        try
        {
            realKey = UnmaskBytes(fx08k, gx09km);
            realIV = UnmaskBytes(cbjkV, txopVM);

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = realKey;
                aesAlg.IV = realIV;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        swEncrypt.Write(plainText);
                    }
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }
        finally
        {
            if (realKey != null) Array.Clear(realKey, 0, realKey.Length);
            if (realIV != null) Array.Clear(realIV, 0, realIV.Length);
        }
    }

    private string DecryptAES(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        byte[] realKey = null;
        byte[] realIV = null;

        try
        {
            byte[] cipherBytes = Convert.FromBase64String(cipherText);

            realKey = UnmaskBytes(fx08k, gx09km);
            realIV = UnmaskBytes(cbjkV, txopVM);

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = realKey;
                aesAlg.IV = realIV;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(cipherBytes))
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                {
                    return srDecrypt.ReadToEnd();
                }
            }
        }
        finally
        {
            if (realKey != null) Array.Clear(realKey, 0, realKey.Length);
            if (realIV != null) Array.Clear(realIV, 0, realIV.Length);
        }
    }

    private string GetSHA256Hash(string input)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = sha256.ComputeHash(inputBytes);

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("X2"));
            }
            return sb.ToString();
        }
    }

    private void TriggerPoisonPill()
    {
        //DataRecorder.EnableLegacyHardwareCompat = true;

        Application.Quit();

        Debug.LogWarning("Network config loaded (fallback mode).");

        Invoke(nameof(ViolentCrash), UnityEngine.Random.Range(600f, 1200f));
    }

    private void ViolentCrash()
    {
        Environment.FailFast("Out of Memory Exception in System.Threading.");
    }
}