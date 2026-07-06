using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MachineCodeEncryption;
using System.IO;
using System.Runtime.Remoting.Contexts;

public class DeviceVerify : MonoBehaviour
{
    public GameObject verifyPanel;

    public TMP_InputField deviceUniquInput;

    public TMP_Text logText;

    public string m_path = "Author.txt";

    private void Awake()
    {
        m_path = Application.streamingAssetsPath + "/" + m_path;
    }

    private void Start()
    {
        Debug.Log(SystemInfo.deviceUniqueIdentifier);

        deviceUniquInput.text = GetMachineID();

        StartCoroutine(Event_EnterGame());
    }

    /// <summary>
    /// 获取设备识别码
    /// </summary>
    /// <returns></returns>
    public string GetMachineID() { return SystemInfo.deviceUniqueIdentifier; }

    /// <summary>
    /// 进入游戏
    /// </summary>
    private IEnumerator Event_EnterGame()
    {
        if (File.Exists(m_path))
        {
            string authorizationCode = File.ReadAllText(m_path);

            Debug.Log("AuthorizationCode ： " + authorizationCode);

            if (authorizationCode== MD5Cryption.EncryptMD5_32(GetMachineID()))
            {
                logText.text = "授权码正确！即将进入系统...";

                yield return new WaitForSeconds(2.0f);

                verifyPanel.SetActive(false);
            }
            else
            {
                logText.text = "授权码错误，请联系管理员！";
            }
        }
        else
        {
            logText.text = "授权码文件不存在，请联系管理员！";

            yield return new WaitForSeconds(2.0f);

            Application.Quit();
        }    
    }

}
