/*************************************************************************
 *  Copyright © 2026-2030 SWEET CO.,LTD. All rights reserved.
 *------------------------------------------------------------------------
 *  公司：SWEET
 *  项目：MotionPlatformEditor
 *  文件：Global.cs
 *  作者：LeonLiu
 *  日期：2026/2/7 21:1:55
 *  功能：全局变量
*************************************************************************/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MPE
{
    public class Global : MonoBehaviour
    {
        public static Global Instance { get; private set; }

        /// <summary>
        /// 液压缸长
        /// </summary>
        public float stroke;

        /// <summary>
        /// 最大行程
        /// </summary>
        public float maxStroke;

        /// <summary>
        /// 最大角度
        /// </summary>
        public float maxAngle;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }


    }
}


