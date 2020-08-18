﻿using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Normal.Realtime;
using UnityEngine;
using static OVRSkeleton;
    
/*
    This version of this file inspired by the GitHub project SpeakGeek-Normcore-Quest-Hand-Tracking
    https://github.com/dylanholshausen/SpeakGeek-Normcore-Quest-Hand-Tracking
    Thanks, Dylan Holshausen! Especially for the Skeletal Serialization, which I cribbed heavily from. Click that link and buy Dylan a beer.
*/

namespace absurdjoy
{
    public class QuestSkeletonSerializer : RealtimeComponent<GenericStringModel>, IAssignSkeleton
    {
        private SkinnedMeshRenderer skinnedMeshRenderer;
        private List<Transform> allBones = new List<Transform>();

        private IOVRSkeletonDataProvider ovrSkeletonDataProvider;

        // outgoing data cache
        private StringBuilder stringBuilder;
        // incoming data cache
        private string[] incomingDataArray;

        protected override void OnRealtimeModelReplaced(GenericStringModel previousModel, GenericStringModel currentModel)
        {
            base.OnRealtimeModelReplaced(previousModel, currentModel);
            if (previousModel != null)
            {
                previousModel.stringValueDidChange -= ReceivedData;
            }

            if (currentModel != null)
            {
                if (!currentModel.isFreshModel)
                {
                    ReceivedData(currentModel, model.stringValue);
                }

                currentModel.stringValueDidChange += ReceivedData;
            }
        }

        private void ReceivedData(GenericStringModel model, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            DeserializeSkeletalData(value);
        }

        public void SendData(string data)
        {
            model.stringValue = data;
        }        
        
        private void OnEnable()
        {
            skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
            
            stringBuilder = new StringBuilder();

            transform.eulerAngles = Vector3.zero;
            transform.localPosition = Vector3.zero;
            transform.localScale = Vector3.one;
        }

        /// <summary>
        /// (only called on locally controlled avatars) Assign a skeleton to this script to harvest data from.  
        /// </summary>
        public void AssignLocalSkeleton(OVRSkeleton ovrSkeleton)
        {
            this.ovrSkeletonDataProvider = ovrSkeleton.GetComponent<IOVRSkeletonDataProvider>();
        }

        private bool IsOnline()
        {
            return realtimeView != null && realtimeView.realtime != null && realtimeView.realtime.connected;
        }
        
        private void Update()
        {
            if (!IsOnline())
            {
                return;
            }

            if (realtimeView.isOwnedLocallyInHierarchy)
            {
                LocalUpdate();
                // RemoteUpdate is called by changes to the model
            }
        }
        
        /// <summary>
        /// Only called on the avatar representing your local hand.
        /// </summary>
        private void LocalUpdate()
        {
            SendData( SerializeSkeletalData() );
        }

        private string SerializeSkeletalData()
        { 
            var data = ovrSkeletonDataProvider.GetSkeletonPoseData();
            stringBuilder.Clear();

            if (!data.IsDataValid || !data.IsDataHighConfidence)
            {
                // Data is invalid or low confidence; we don't want to transmit garbage data to the remote machine
                // Hide the renderer.
                skinnedMeshRenderer.enabled = false;
                stringBuilder.Append("0|");
            }
            else
            {
                // Data is valid.
                // Show the renderer.
                skinnedMeshRenderer.enabled = true;

                stringBuilder.Append("1|");

                //Set bone transform from SkeletonPoseData
                for (var i = 0; i < allBones.Count; ++i)
                {
                    allBones[i].transform.localRotation = data.BoneRotations[i].FromFlippedZQuatf();

                    stringBuilder.Append(allBones[i].transform.localEulerAngles.x);
                    stringBuilder.Append("|");
                    stringBuilder.Append(allBones[i].transform.localEulerAngles.y);
                    stringBuilder.Append("|");
                    stringBuilder.Append(allBones[i].transform.localEulerAngles.z);
                    stringBuilder.Append("|");
                }
            }

            return stringBuilder.ToString();
        }
        
        /// <summary>
        /// Called when new network data arrives.
        /// </summary>
        public void DeserializeSkeletalData(string netHandData)
        {
            if (string.IsNullOrEmpty(netHandData) || !IsOnline() || realtimeView.isOwnedLocallyInHierarchy)
            {
                // Invalid data or we ar the local player. No need to update.
                return;
            }

            incomingDataArray = netHandData.Split('|');

            if (incomingDataArray[0] == "0")
            {
                // Hand was turned off.
                skinnedMeshRenderer.enabled = false;

                return;
            }
            else if (incomingDataArray[0] == "1")
            {
                // Hand was turned on.
                skinnedMeshRenderer.enabled = true;
            }

            int startIndex = 1;
            for (var i = 0; i < allBones.Count; ++i)
            {
                int tmpBoneCount = i * 3;

                allBones[i].transform.localEulerAngles = new Vector3(
                    float.Parse(incomingDataArray[startIndex + tmpBoneCount], CultureInfo.InvariantCulture),
                    float.Parse(incomingDataArray[startIndex + 1 + tmpBoneCount], CultureInfo.InvariantCulture), 
                    float.Parse(incomingDataArray[startIndex + 2 + tmpBoneCount], CultureInfo.InvariantCulture));
            }
        }
    }
}