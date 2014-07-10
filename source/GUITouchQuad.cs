#region usings
using System;
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
        Name = "iOS Touch Quad",
        Category = "GUI",
        Help = "Simple Quad Point Button",
        Tags = ""
    )]
    #endregion PluginInfo
    public class GUITouchQuadNode : IPluginEvaluate
    {
        #region fields & pins
        [Input("Transform")]
        public ISpread<SlimDX.Matrix> FTransforms;

        [Input("Touch Position")]
        public ISpread<SlimDX.Vector2> FTouchPosition;

        [Input("Is new", IsBang = true)]
        public IDiffSpread<bool> FTouchNew;

        [Input("Touch ID")]
        public ISpread<int> FTouchId;

        [Input("Enabled", IsSingle = true)]
        public ISpread<bool> FEnabled;

        [Output("Over")]
        public ISpread<bool> FOver;

        [Output("Input Index")]
        public ISpread<int> FInputIndex;

        [Output("Clicked", IsBang = true)]
        public ISpread<bool> FClicked;

        [Import()]
        public ILogger FLogger;
        #endregion fields & pins

        private bool dirty = false;

        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
        {
            FOver.SliceCount = FTransforms.SliceCount;
            FInputIndex.SliceCount = FTransforms.SliceCount;
            FClicked.SliceCount = FTransforms.SliceCount;

            if (dirty)
            {
                for (int y = 0; y < FTransforms.SliceCount; y++)
                {
                    FOver[y] = false;
                    FClicked[y] = false;
                    FInputIndex[y] = 0;
                }
                dirty = false;
            }
            
            if (FEnabled[0])
            {
                for (int y = 0; y < FTransforms.SliceCount; y++)
                {
                    for (int x = 0; x < FTouchPosition.SliceCount; x++)
                    {
                        SlimDX.Vector2 trv = SlimDX.Vector2.TransformCoordinate(FTouchPosition[x], SlimDX.Matrix.Invert(FTransforms[y]));
                        if (trv.X > -0.5f && trv.Y > -0.5f && trv.X < 0.5f && trv.Y < 0.5f)
                        {
                            dirty = true;
                            FOver[y] = true;
                            FInputIndex[y] = FTouchId[x];
                            if (FTouchNew[x])
                            {
                                FClicked[y] = true;
                            }
                        }
                    }
                }
            }
        }
    }
}