/*************************************************************************
 * Copyright © 2026-2030 SWEET CO.,LTD. All rights reserved.
 *------------------------------------------------------------------------
 * 公司：SWEET
 * 项目：MotionPlatformEditor
 * 文件：FileSaveManager.cs
 * 作者：LeonLiu
 * 日期：2026/3/8 1:22:39
 * 功能：曲线保存
*************************************************************************/

using RuntimeCurveEditor;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace MPE
{
    public enum MenuItems { New, Save, SaveAs, Load, Delete, Exit };
    public class FileSaveManager : MonoBehaviour, IFileOperations
    {

        public RTAnimationCurve rtAnimationCurve;//class through which we'll access the Runtime Curve Editor core

        // 1. 添加 MPEManager 引用，确保你在Inspector中把场景里的 MPEManager 拖拽给它
        public MPEManager mpeManager;

        public RectTransform menuList;
        public RectTransform fileSelectionControl;

        public TMP_Text filenameText;

        const string DEFAULT_NAME = "Unnamed";

        private bool showFileMenu;//true when the file's vertical menu is visible

        public void OnMenuButton()
        {
            showFileMenu = !showFileMenu;
            menuList.gameObject.SetActive(showFileMenu);
        }

        public void OnFileNewButton()
        {
            NewWindow();
        }

        public void OnFileSaveButton()
        {
            if (DEFAULT_NAME != filenameText.text)
            {
                // 修复2：不再严格判断 DataAltered，只要用户点击了Save，就无条件保存，防止漏存
                SaveData(false);
            }
            else
            {
                ShowFileSelection(MenuItems.Save);
            }
        }

        public void OnFileSaveAsButton()
        {
            ShowFileSelection(MenuItems.Save);
        }

        public void OnFileLoadButton()
        {
            ShowFileSelection(MenuItems.Load);
        }

        public void OnFileDeleteButton()
        {
            ShowFileSelection(MenuItems.Delete);
        }

        public void OnFileExitButton()
        {
            Application.Quit();
        }

        public void DeleteFile(string fileName)
        {
            string temp = filenameText.text.Replace("*", "");
            if (temp == fileName)
            {//this is the case, we're deleting the current file
                NewWindow();
            }
            rtAnimationCurve.DeleteFile(fileName);
        }

        public void SaveData(bool saveAs)
        {
            // === 核心修复1：彻底分离另存为和普通保存的名称获取逻辑 ===
            if (saveAs)
            {
                // 如果是另存为，或者新建后首次通过面板保存，从面板的输入框中获取名字
                filenameText.text = fileSelectionControl.GetComponent<FileSelectionControlBehaviour>().GetInputFileName();
            }
            else
            {
                // 如果是普通保存，直接用标题栏现有的名字，并且强制剔除所有的星号 '*'
                filenameText.text = filenameText.text.Replace("*", "");
            }

            // 修改这里：将 demoAnimationCurves 替换为 mpeManager
            rtAnimationCurve.SaveData(filenameText.text, mpeManager);
        }

        public void LoadFile(string fileName)
        {
            // 修改这里：将 demoAnimationCurves 替换为 mpeManager
            rtAnimationCurve.LoadData(fileName, mpeManager);

            filenameText.text = fileName;
        }

        void ShowFileSelection(MenuItems menuItem)
        {


            fileSelectionControl.gameObject.SetActive(true);
            FileSelectionControlBehaviour fileSelectionControlBehaviour = fileSelectionControl.GetComponent<FileSelectionControlBehaviour>();
            if (menuItem == MenuItems.Save)
            {
                fileSelectionControlBehaviour.actionName.text = "保存文件";
                fileSelectionControlBehaviour.actionButtonName.text = "保存";
            }
            else if (menuItem == MenuItems.Delete)
            {
                fileSelectionControlBehaviour.actionName.text = "删除文件";
                fileSelectionControlBehaviour.actionButtonName.text = "删除";
            }
            else if (menuItem == MenuItems.Load)
            {
                fileSelectionControlBehaviour.actionName.text = "加载文件";
                fileSelectionControlBehaviour.actionButtonName.text = "加载";
            }
            fileSelectionControlBehaviour.MenuItem = menuItem;
            fileSelectionControlBehaviour.NamesList = rtAnimationCurve.GetNamesList();
            fileSelectionControlBehaviour.FileOperations = this;
            fileSelectionControlBehaviour.Init();
        }

        void NewWindow()
        {
            rtAnimationCurve.NewWindow();
            filenameText.text = DEFAULT_NAME;
        }



        void OnDataAlter()
        {
            if ((filenameText.text != DEFAULT_NAME) && (filenameText.text[filenameText.text.Length - 1] != '*') && rtAnimationCurve.DataAltered())
            {
                filenameText.text += "*";
            }
        }
    }
}