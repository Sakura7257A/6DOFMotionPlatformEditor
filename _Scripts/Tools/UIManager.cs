/*************************************************************************
 *  Copyright © 2023-2030 LPH CO.,LTD. All rights reserved.
 *------------------------------------------------------------------------
 *  公司：SWEET
 *  项目：MotionPlatformEditor
 *  文件：UIManager.cs
 *  作者：LeonLiu
 *  日期：2026/6/16 14:25:27
 *  功能：Nothing
*************************************************************************/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MPE
{
    public class UIManager : MonoBehaviour
    {

        public GameObject exitConfirmPanel;

        public GameObject mainPageConfirmPanel;

        public GameObject serverConfirmPanel;

        public GameObject platformTypePanel;

        public GameObject videoTypePanel;

        public GameObject video360;

        public GameObject videoHuMu;

        public GameObject helpPanel;

        public static UIManager _ins;

        private void Awake()
        {
            _ins = this;
        }

        public void OpenConfirmPanel()
        {
            exitConfirmPanel.SetActive(true);
        }

        public void CloseConfirmPanel()
        {
            exitConfirmPanel.SetActive(false);
        }


        public void OnExitButton()
        {
            Application.Quit();
        }

        public void OpenServerConfirmPanel()
        {
            serverConfirmPanel.SetActive(true);
        }

        public void OpenVideoTypePanel()
        {
            platformTypePanel.gameObject.SetActive(false);

            videoTypePanel.gameObject.SetActive(true);
        }

        public void OnSelect360Video()
        {
            video360.SetActive(true);
            videoHuMu.SetActive(false);
            videoTypePanel.SetActive(false);
        }

        public void OnSelectHuMuVideo()
        {
            videoHuMu.SetActive(true);
            video360.SetActive(false);
            videoTypePanel.SetActive(false);
        }

        public void OpenHelpPanel()
        {
            if (helpPanel != null)
            {
                if(helpPanel.activeSelf)
                {
                    helpPanel.SetActive(false);
                }
                else
                {
                    helpPanel.SetActive(true);
                }
            }
        }


        public void OpenMainPageConfirmPanel()
        {
            mainPageConfirmPanel.SetActive(true);
        }

        public void CloseMainPageConfirmPanel()
        {
            mainPageConfirmPanel.SetActive(false);
        }

        public void BackMainPage()
        {
            SceneManager.LoadScene(0);
        }
    }
}


