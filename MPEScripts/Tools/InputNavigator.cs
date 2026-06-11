/*************************************************************************
 *  Copyright © 2026-2030 SWEET CO.,LTD. All rights reserved.
 *------------------------------------------------------------------------
 *  公司：SWEET
 *  项目：MotionPlatformEditor
 *  文件：InputNavigator.cs
 *  作者：LeonLiu
 *  日期：2026/3/6 23:49:53
 *  功能：多个输入框Tab切换
*************************************************************************/

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MPE
{
    public class InputNavigator : MonoBehaviour
    {

        public List<TMP_InputField> inputFields = new List<TMP_InputField>();
        private int currentIndex = 0;

        void Start()
        {
            // 确保初始焦点在第一个输入框
            if (inputFields.Count > 0)
            {
                inputFields[0].ActivateInputField();
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                // 如果按下Tab键且没有按下Shift键，则向下移动焦点
                if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                {
                    MoveFocus(1);
                }
                // 如果按下Tab键且按下Shift键，则向上移动焦点
                else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    MoveFocus(-1);
                }
            }
        }

        void MoveFocus(int direction)
        {
            inputFields[currentIndex].DeactivateInputField(); // 失去焦点当前输入框
            currentIndex = (currentIndex + direction + inputFields.Count) % inputFields.Count; // 计算下一个索引，并循环回到开始或结束处
            inputFields[currentIndex].ActivateInputField(); // 获取焦点下一个输入框
        }
    }
}


