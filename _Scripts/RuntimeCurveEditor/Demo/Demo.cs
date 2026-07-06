using UnityEngine;
using UnityEngine.UI;
using RuntimeCurveEditor;
using TMPro;
using MPE;


namespace MPE
{
   // public enum MenuItems { New, Save, SaveAs, Load, Delete, Exit };

    public class Demo : MonoBehaviour, IFileOperations
    {
        public RTAnimationCurve rtAnimationCurve;//class through which we'll access the Runtime Curve Editor core

        // 1. 添加 MPEManager 引用，确保你在Inspector中把场景里的 MPEManager 拖拽给它
        public MPEManager mpeManager;

        public RectTransform menuList;
        public RectTransform fileSelectionControl;

        public TMP_Text filenameText;

        const string DEFAULT_NAME = "Unnamed";

        private bool showFileMenu;//true when the file's vertical menu is visible

        public void OnFileButton()
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
                if (rtAnimationCurve.DataAltered())
                {
                    SaveData(false);
                }
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
            if (!saveAs && filenameText.text.Contains("*"))
            {
                filenameText.text = filenameText.text.Substring(0, filenameText.text.Length - 1);
            }
            else
            {
                filenameText.text = fileSelectionControl.GetComponent<FileSelectionControlBehaviour>().GetInputFileName();
            }
            // 修改这里：将 demoAnimationCurves 替换为 mpeManager
            rtAnimationCurve.SaveData(filenameText.text, mpeManager);
        }

        public void LoadFile(string fileName)
        {
            // 修改这里：将 demoAnimationCurves 替换为 mpeManager
            rtAnimationCurve.LoadData(fileName, mpeManager);

            // 你的业务代码...
            filenameText.text = fileName;
        }


        void ShowFileSelection(MenuItems menuItem)
        {
            fileSelectionControl.gameObject.SetActive(true);
            FileSelectionControlBehaviour fileSelectionControlBehaviour = fileSelectionControl.GetComponent<FileSelectionControlBehaviour>();
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