#region usings
using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

using VVVV.Core.Logging;
#endregion usings

namespace VVVV.Nodes
{
    #region PluginInfo
    [PluginInfo(
        Name = "iOS Touch Translate Scale Rotate",
        Category = "GUI",
        Help = "Handling drag, scaling and rotation",
        Tags = "multitouch",
        Author = "ethermamoth"
    )]
    #endregion PluginInfo
    public class GUITouchTranslateRotateScaleNode : IPluginEvaluate
    {
        #region fields & pins
        [Input("Transform")]
        public ISpread<SlimDX.Matrix> FTransforms;

        [Input("Index / ID")]
        public ISpread<int> FInputIndex;

        [Input("Touch Position")]
        public ISpread<SlimDX.Vector2> FTouchPosition;
        
        [Input("Touch ID")]
        public ISpread<int> FTouchId;

        [Input("Touch Lifetime")]
        public ISpread<int> FTouchLifetime;

        [Input("Time Treshold (ms)", IsSingle = true)]
        public ISpread<int> FTimeThreshold;

        [Input("Scale Limit", DefaultValues=new double[]{0.1, 1.0})]
        public ISpread<Vector2D> FScaleLimit;

        [Input("Init / Reset", IsSingle = true)]
        public ISpread<bool> FReset;

        [Input("Use manual add / remove", IsSingle = true)]
        public ISpread<bool> FUseManual;

        [Input("Add", IsSingle = true)]
        public ISpread<bool> FAddTransform;

        [Input("Remove", IsSingle = true)]
        public ISpread<bool> FRemoveTransform;

        [Input("Delete index")]
        public ISpread<int> FDeleteIndex;

        [Input("Enabled", IsSingle = true)]
        public ISpread<bool> FEnabled;

        [Output("Transform Out")]
        public ISpread<SlimDX.Matrix> FTransformsOut;

        [Output("Index")]
        public ISpread<int> FIndexOut;

        [Output("Translating")]
        public ISpread<bool> FTranslating;

        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        List<TouchObject> touchObjects = new List<TouchObject>();
        List<TouchFinger> touchFingers = new List<TouchFinger>();

        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {
            //reset all
            if (FReset[0])
            {
                touchObjects.Clear();
                touchFingers.Clear();
            }

            if (FUseManual[0])
            {
                if (FAddTransform[0] && FTransforms.SliceCount > 0 && FInputIndex.SliceCount > 0)
                {
                    //add to end
                    int pos = touchObjects.Count;
                    touchObjects.Add(new TouchObject(FInputIndex[0], FTransforms[0]));
                }
                
                if (FRemoveTransform[0])
                {
                    for (int x = 0; x < FDeleteIndex.SliceCount; x++)
                    {
                        if (FDeleteIndex[x] < touchObjects.Count)
                        {
                            try
                            {
                                touchObjects.RemoveAt(FDeleteIndex[x]);
                            }
                            catch (System.ArgumentOutOfRangeException)
                            {
                                FLogger.Log(LogType.Error, "Multitouch error: remove index out of range");
                            }
                        }
                    }
                }
            }

            //init or update objects
            if (touchObjects.Count != FTransforms.SliceCount && !FUseManual[0])
            {
                if (touchObjects.Count < FTransforms.SliceCount)
                {
                    int dif = FTransforms.SliceCount - touchObjects.Count;
                    for (int x = 0; x < dif; x++)
                    {
                        touchObjects.Add(new TouchObject(FInputIndex[FInputIndex.SliceCount - x], FTransforms[FTransforms.SliceCount - x]));
                    }
                }
                else
                {
                    //check which transform to remove
                    for (int i = touchObjects.Count - 1; i >= 0; i--)
                    {
                        int count = 0;
                        for (int x = 0; x < FTransforms.SliceCount; x++)
                        {
                            if (FTransforms[x] == touchObjects[i].initialTransform)
                            {
                                count++;
                            }
                        }

                        if(count > 1 || count == 0)
                            touchObjects.RemoveAt(i);
                    }
                }
            }

            //Only update / check when enabled
            if (FEnabled[0])
            {
                //Remove non existing touches
                //reverse loop to remove elements safely
                List<int> tIDs = FTouchId.ToList();
                for (int i = touchFingers.Count - 1; i >= 0; i--)
                {
                    if (!tIDs.Exists(finger => finger == touchFingers[i].touchId))
                        touchFingers.RemoveAt(i);
                }

                //Check if touch is new or already on the list
                for (int x = 0; x < FTouchId.SliceCount; x++)
                {
                    int foundIndex = touchFingers.FindIndex(finger => finger.touchId == FTouchId[x]);
                    if (foundIndex == -1)
                    {
                        //if not on list add it to it
                        touchFingers.Add(new TouchFinger(FTouchPosition[x], FTouchId[x], FTouchLifetime[x]));
                        foundIndex = touchFingers.Count - 1;
                    }
                    else
                    {
                        //already on list update values
                        touchFingers[foundIndex].UpdateValues(FTouchPosition[x], FTouchLifetime[x]);
                    }

                    //check for hits
                    int hasHitIndex = touchObjects.FindIndex(tobject => tobject.isHit(touchFingers[foundIndex].touchPosition));
                    if (hasHitIndex == -1)
                    {
                        touchFingers[foundIndex].hasHit = false;
                    }
                    else
                    {
                        touchFingers[foundIndex].hasHit = true;
                        //check if the touch is on the objects list and update if not add it
                        int hitIndex = touchObjects[hasHitIndex].getHitIndex(touchFingers[foundIndex].touchId);
                        if (hitIndex == -1)
                        {
                            //only add it if below time thresh
                            if(touchFingers[foundIndex].touchLifeTime < FTimeThreshold[0])
                                touchObjects[hasHitIndex].hitIds.Add(touchFingers[foundIndex]);
                        }
                        else
                        {
                            touchObjects[hasHitIndex].hitIds[hitIndex] = touchFingers[foundIndex];
                        }
                    }
                }

                //update objects
                FTransformsOut.SliceCount = touchObjects.Count;
                FIndexOut.SliceCount = touchObjects.Count;
                FTranslating.SliceCount = touchObjects.Count;

                int index = 0;
                foreach (TouchObject tobj in touchObjects)
                {
                    int transformType = tobj.Update(touchFingers, FScaleLimit[0]);
                    FTransformsOut[index] = tobj.objectTransform;
                    FIndexOut[index] = tobj.sliceIndex;
                    FTranslating[index] = transformType == 1;
                    index++;
                }
            }
        }
    }
}

public class TouchFinger
{ 
    public SlimDX.Vector2 touchPosition;
    public SlimDX.Vector2 lastPosition;
    public SlimDX.Vector2 firstPosition;
    public int touchId;
    public int touchLifeTime;
    public bool hasHit;

    public TouchFinger()
    {
        touchPosition = new SlimDX.Vector2(0f);
        lastPosition = new SlimDX.Vector2(0f);
        firstPosition = new SlimDX.Vector2(0f);
        touchId = 0;
        touchLifeTime = 0;
        hasHit = false;
    }

    public TouchFinger(SlimDX.Vector2 pos, int tid, int tlt)
    {
        touchPosition = pos;
        lastPosition = pos;
        firstPosition = pos;
        touchId = tid;
        touchLifeTime = tlt;
        hasHit = false;
    }

    public void UpdateValues(SlimDX.Vector2 pos, int tlt)
    {
        lastPosition = touchPosition;
        touchPosition = pos;
        touchLifeTime = tlt;
    }
}

public class TouchObject
{
    public int sliceIndex;
    public List<TouchFinger> hitIds;
    public SlimDX.Matrix objectTransform;
    public SlimDX.Matrix initialTransform;

    public TouchObject()
    {
        sliceIndex = 0;
        hitIds = new List<TouchFinger>();
    }

    public TouchObject(int index, SlimDX.Matrix trans)
    {
        sliceIndex = index;
        initialTransform = trans;
        objectTransform = trans;
        hitIds = new List<TouchFinger>();
    }

    public bool isHit(SlimDX.Vector2 touchPosition)
    {
        SlimDX.Vector2 trv = SlimDX.Vector2.TransformCoordinate(touchPosition, SlimDX.Matrix.Invert(objectTransform));
        if (trv.X > -0.5f && trv.Y > -0.5f && trv.X < 0.5f && trv.Y < 0.5f)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public int getHitIndex(int id)
    {
        return hitIds.FindIndex(finger => finger.touchId == id);
    }

    public void cleanList(List<TouchFinger> allFingers)
    {
        //reverse loop to remove elements safely
        for (int i = hitIds.Count - 1; i >= 0; i--)
        {
            if (!allFingers.Exists(finger => finger.touchId == hitIds[i].touchId))
                hitIds.RemoveAt(i);
        }
    }

    public int Update(List<TouchFinger> allFingers, Vector2D scaleLimit)
    { 
        //clean list (check if touches still exist)
        cleanList(allFingers);

        //check how many fingers are still on list and perform actions
        //TRANSLATE
        if (hitIds.Count == 1)
        {
            SlimDX.Vector2 delta = hitIds.First().touchPosition - hitIds.First().lastPosition;
            objectTransform = objectTransform * SlimDX.Matrix.Translation(delta.X, delta.Y, 0);
            return 1;
        }

        //SCALE AND ROTATE
        if (hitIds.Count > 1)
        {
            //SCALE
            float sx = new SlimDX.Vector3(objectTransform.M11, objectTransform.M12, objectTransform.M13).Length();
            float sy = new SlimDX.Vector3(objectTransform.M21, objectTransform.M22, objectTransform.M23).Length();
            //previous distance
            float pd = (float)new Vector2D((hitIds.Last().lastPosition.X - hitIds.First().lastPosition.X),
                       (hitIds.Last().lastPosition.Y - hitIds.First().lastPosition.Y)).Length;
            
            //current distance
            float cd = (float)new Vector2D((hitIds.Last().touchPosition.X - hitIds.First().touchPosition.X),
                       (hitIds.Last().touchPosition.Y - hitIds.First().touchPosition.Y)).Length;

            float scaleFactor = cd - pd;
            float fscale = 1;
            if (scaleLimit.x < sx + scaleFactor && sx + scaleFactor < scaleLimit.y &&
                scaleLimit.x < sy + scaleFactor && sy + scaleFactor < scaleLimit.y)
            {
                fscale += scaleFactor;
            }
            
            //ROTATION
            //previous angle
            double pa = Math.Atan2(hitIds.Last().lastPosition.Y - hitIds.First().lastPosition.Y,
                                      hitIds.Last().lastPosition.X - hitIds.First().lastPosition.X);
            //current angle
            double ca = Math.Atan2(hitIds.Last().touchPosition.Y - hitIds.First().touchPosition.Y, 
                                      hitIds.Last().touchPosition.X - hitIds.First().touchPosition.X);

            double da = ca - pa;
            //Final matrices
            SlimDX.Matrix scale = SlimDX.Matrix.Scaling(fscale, fscale, 1);
            SlimDX.Matrix rot = SlimDX.Matrix.RotationZ((float)da);
            SlimDX.Vector3 srcPos = new SlimDX.Vector3(objectTransform.M41, objectTransform.M42, 0);
            //Translate to origin
            objectTransform.M41 = 0;
            objectTransform.M42 = 0;
            //rotate scale and translate back to srcpos
            objectTransform = objectTransform * rot * scale * SlimDX.Matrix.Translation(srcPos);
            return 2;
        }

        return 0;
    }
}