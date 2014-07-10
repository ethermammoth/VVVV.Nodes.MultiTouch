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
        Name = "iOS Touch Intersect Quad",
        Category = "GUI",
        Help = "Quad Line Intersection",
        Tags = ""
    )]
    #endregion PluginInfo
    public class GUITouchIntersectQuadNode : IPluginEvaluate
    {
        #region fields & pins
        [Input("Transform Quad")]
        public ISpread<SlimDX.Matrix> FTransforms;

        [Input("Transform Line")]
        public ISpread<SlimDX.Matrix> FTouchLine;

        [Input("Is new", IsBang = true)]
        public IDiffSpread<bool> FTouchNew;

        [Input("Enabled", IsSingle = true)]
        public ISpread<bool> FEnabled;

        [Output("Over")]
        public ISpread<bool> FOver;

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
            FClicked.SliceCount = FTransforms.SliceCount;

            if (dirty)
            {
                for (int y = 0; y < FTransforms.SliceCount; y++)
                {
                    FOver[y] = false;
                    FClicked[y] = false;
                }
                dirty = false;
            }

            if (FEnabled[0])
            {
                for (int y = 0; y < FTransforms.SliceCount; y++)
                {
                    for (int x = 0; x < FTouchLine.SliceCount; x++)
                    {
                        //intersection code here
                        SlimDX.Vector3 PQuad = new SlimDX.Vector3(0, 0, 0);
                        SlimDX.Vector3 nQuad = new SlimDX.Vector3(0, 0, 1);
                        SlimDX.Vector3 PLine1 = new SlimDX.Vector3(0, 0, -0.5f);
                        SlimDX.Vector3 PLine2 = new SlimDX.Vector3(0, 0, 0.5f);

                        //transform line
                        PLine1 = SlimDX.Vector3.TransformCoordinate(PLine1, FTouchLine[x]);
                        PLine2 = SlimDX.Vector3.TransformCoordinate(PLine2, FTouchLine[x]);

                        //transform line into object space of the quad
                        SlimDX.Matrix InvQuadTransform = new SlimDX.Matrix();
                        InvQuadTransform.M44 = 1;
                        InvQuadTransform = SlimDX.Matrix.Invert(FTransforms[y]);
                        PLine1 = SlimDX.Vector3.TransformCoordinate(PLine1, InvQuadTransform);
                        PLine2 = SlimDX.Vector3.TransformCoordinate(PLine2, InvQuadTransform);

                        //get line direction vector
                        SlimDX.Vector3 rLine = SlimDX.Vector3.Subtract(PLine2, PLine1);

                        //check attitude of line and plane
                        float denom = nQuad.X * rLine.X + nQuad.Y * rLine.Y + nQuad.Z * rLine.Z;

                        //if they are not parallel it intersect infinite plane
                        if (denom != 0)
                        {
                            //get intersection position on line
                            float LineAlpha = (nQuad.X * (PQuad.X - PLine1.X) + nQuad.Y * (PQuad.Y - PLine1.Y) +
                                               nQuad.Z * (PQuad.Z - PLine1.Z)) / denom;

                            //calculate intersection point
                            SlimDX.Vector3 IntPoint = new SlimDX.Vector3( PLine1.X + LineAlpha * rLine.X,
                                                                          PLine1.Y + LineAlpha * rLine.Y,
                                                                          PLine1.Z + LineAlpha * rLine.Z );
                            //intersection outputs could go here
                            //if there is an intersection
                            if ((IntPoint.X <= 0.5f) && (IntPoint.X >= -0.5f) &&
                                (IntPoint.Y <= 0.5f) && (IntPoint.Y >= -0.5f))
                            {
                                dirty = true;
                                FOver[y] = true;
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
}