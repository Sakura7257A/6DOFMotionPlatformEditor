/*************************************************************************
 *  Copyright © 2026-2030 SWEET CO.,LTD. All rights reserved.
 *------------------------------------------------------------------------
 *  公司：SWEET
 *  项目：MotionPlatformEditor
 *  文件：VideoManager.cs
 *  作者：LeonLiu
 *  日期：2026/2/4 16:52:4
 *  功能：视频管理模块
*************************************************************************/

using RenderHeads.Media.AVProVideo;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MPE
{
    public class VideoManager : MonoBehaviour
    {
        public MediaPlayer _mediaPlayer;
        public Slider _sliderTime;

        private bool _wasPlayingBeforeTimelineDrag;
        private bool _isHoveringOverTimeline;


        private void Start()
        {
            CreateTimelineDragEvents();       
        }

        private void OnTimeSliderBeginDrag()
        {
            if (_mediaPlayer && _mediaPlayer.Control != null)
            {
                _wasPlayingBeforeTimelineDrag = _mediaPlayer.Control.IsPlaying();
                if (_wasPlayingBeforeTimelineDrag)
                {
                    _mediaPlayer.Pause();
                }
                OnTimeSliderDrag();
            }
        }

        private void OnTimeSliderDrag()
        {
            if (_mediaPlayer && _mediaPlayer.Control != null)
            {
                TimeRange timelineRange = GetTimelineRange();               
                double time = timelineRange.startTime + (_sliderTime.value);
                _mediaPlayer.Control.Seek(time);
                _isHoveringOverTimeline = true;
            }
        }

        private void OnTimeSliderEndDrag()
        {
            if (_mediaPlayer && _mediaPlayer.Control != null)
            {
                if (_wasPlayingBeforeTimelineDrag)
                {
                    _mediaPlayer.Play();
                    _wasPlayingBeforeTimelineDrag = false;
                }
            }
        }

        private void OnTimelineBeginHover(PointerEventData eventData)
        {
            if (eventData.pointerCurrentRaycast.gameObject != null)
            {
                _isHoveringOverTimeline = true;
                //_sliderTime.transform.localScale = new Vector3(1f, 2.5f, 1f);
            }
        }

        private void OnTimelineEndHover(PointerEventData eventData)
        {
            _isHoveringOverTimeline = false;
            //_sliderTime.transform.localScale = new Vector3(1f, 1f, 1f);
        }

        private TimeRange GetTimelineRange()
        {
            if (_mediaPlayer.Info != null)
            {
                return Helper.GetTimelineRange(_mediaPlayer.Info.GetDuration(), _mediaPlayer.Control.GetSeekableTimes());
            }
            return new TimeRange();
        }

        private void CreateTimelineDragEvents()
        {
            EventTrigger trigger = _sliderTime.gameObject.GetComponent<EventTrigger>();
            if (trigger != null)
            {
                EventTrigger.Entry entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerDown;
                entry.callback.AddListener((data) => { OnTimeSliderBeginDrag(); });
                trigger.triggers.Add(entry);

                entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.Drag;
                entry.callback.AddListener((data) => { OnTimeSliderDrag(); });
                trigger.triggers.Add(entry);

                entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerUp;
                entry.callback.AddListener((data) => { OnTimeSliderEndDrag(); });
                trigger.triggers.Add(entry);

                entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerEnter;
                entry.callback.AddListener((data) => { OnTimelineBeginHover((PointerEventData)data); });
                trigger.triggers.Add(entry);

                entry = new EventTrigger.Entry();
                entry.eventID = EventTriggerType.PointerExit;
                entry.callback.AddListener((data) => { OnTimelineEndHover((PointerEventData)data); });
                trigger.triggers.Add(entry);
            }
        }
    }
}


