using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MPE
{
    public class FileSelectionControlBehaviour : MonoBehaviour
    {
        public RectTransform content;
        public GameObject filenamePrefab;
        public TMP_Text actionName;
        public TMP_Text actionButtonName;
        public TMP_InputField inputFileName;
        public RectTransform windowMessage;
        public RectTransform windowConfirm;
        public RectTransform deleteWindow;
        public TMP_Text windowMessageText;
        public RectTransform close;
        MenuItems menuItem;

        public MenuItems MenuItem
        {
            set
            {
                menuItem = value;
              //  actionName.text = menuItem.ToString();
             //   actionButtonName.text = menuItem.ToString();
            }
        }

        List<string> namesList;//names list of the saved configurations

        public List<string> NamesList
        {
            set
            {
                namesList = value;
                FillContent();
            }
        }

        public IFileOperations FileOperations { set; private get; }

        RectTransform prevSelectedFile;

        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
        }

        public void Init()
        {
            inputFileName.text = "";
        }

        public void OnAction()
        {
            if (menuItem == MenuItems.Save)
            {//Save button has been clicked
                string textFieldString = inputFileName.text;
                if (textFieldString.Length < 3 || textFieldString.Contains("*"))
                {
                    windowMessage.gameObject.SetActive(true);
                    if (textFieldString.Length < 3)
                    {
                        windowMessageText.text = "文件名字过短！";
                    }
                    else
                    {
                        windowMessageText.text = "无效字符 *！";
                    }
                }
                else if (namesList.Contains(textFieldString))
                {
                    windowConfirm.gameObject.SetActive(true);
                }
                else
                {
                    namesList.Add(textFieldString);
                    FileOperations.SaveData(true);
                    CloseWindow();
                }
            }
            else if (menuItem == MenuItems.Delete)
            {
                if (inputFileName.text.Length > 0)
                {
                    if (namesList.Remove(inputFileName.text))
                    {
                        FileOperations.DeleteFile(inputFileName.text);
                        inputFileName.text = "";
                    }
                }
                CloseWindow();
            }
            else if (menuItem == MenuItems.Load)
            {
                if (namesList.Contains(inputFileName.text))
                {
                    FileOperations.LoadFile(inputFileName.text);
                    CloseWindow();
                }
                else
                {
                    windowMessage.gameObject.SetActive(true);
                    windowMessageText.text = "文件未找到！";
                }
            }
        }

        public void OnYes()
        {
            windowConfirm.gameObject.SetActive(false);
            FileOperations.SaveData(true);
            CloseWindow();
        }

        public void OnNo()
        {
            windowConfirm.gameObject.SetActive(false);
        }

        public void OpenDeleteWindow()
        {
            deleteWindow.gameObject.SetActive(true);
        }

        public void OnDeleteNo()
        {
            deleteWindow.gameObject.SetActive(false);
        }

        public void OnOk()
        {
            windowMessage.gameObject.SetActive(false);
        }

        public string GetInputFileName()
        {
            return inputFileName.text;
        }

        public void CloseWindow()
        {
            if (windowConfirm.gameObject.activeSelf)
            {
                windowConfirm.gameObject.SetActive(false);
            }
            if (windowMessage.gameObject.activeSelf)
            {
                windowMessage.gameObject.SetActive(false);
            }
            gameObject.SetActive(false);
        }

        public void FileSelect(RectTransform fileName)
        {
            if (prevSelectedFile != fileName)
            {
                inputFileName.text = fileName.name;
                if (prevSelectedFile)
                {
                    prevSelectedFile.GetComponent<TMP_Text>().fontStyle = FontStyles.Normal;
                }
                fileName.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
                prevSelectedFile = fileName;
            }
        }

        void FillContent()
        {
            foreach (RectTransform child in content)
            {
                Destroy(child.gameObject);
            }
            int posY = 0;
            int distY = 25;
            foreach (string name in namesList)
            {
                RectTransform fileName = (Instantiate(filenamePrefab) as GameObject).GetComponent<RectTransform>();
                fileName.Translate(0, posY, 0);
                fileName.SetParent(content, false);
                fileName.GetComponent<TMP_Text>().text = name;
                fileName.name = name;
                posY -= distY;
                fileName.GetComponent<FileBehaviour>().FileSelectionControlBehaviourProp = this;
            }
            content.sizeDelta = new Vector2(content.sizeDelta.x, -posY + distY);
        }
    }

}