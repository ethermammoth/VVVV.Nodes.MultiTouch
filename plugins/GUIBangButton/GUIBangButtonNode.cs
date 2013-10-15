#region usings
using System;
using System.ComponentModel.Composition;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;
using VVVV.Utils.SlimDX;

using VVVV.Core.Logging;
#endregion usings

namespace VVVV.Nodes
{
	#region PluginInfo
	[PluginInfo(Name = "BangButton", 
	Category = "GUI", 
	Help = "Basic template with one value in/out", 
	Tags = "")]
	#endregion PluginInfo
	public class GUIBangButtonNode : IPluginEvaluate
	{
		#region fields & pins
		[Input("Transform")]
		public ISpread<SlimDX.Matrix> FTransforms;
		
		[Input("Touch Position")]
		public ISpread<SlimDX.Vector2> FTouchPosition;
		
		[Input("Is new", IsBang=true)]
		public IDiffSpread<bool> FTouchNew;
		
		[Input("Enabled", IsSingle=true)]
		public ISpread<bool> FEnabled;
		
		[Output("Over")]
		public ISpread<bool> FOver;

		[Output("Clicked", IsBang=true)]
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
			
			if(FEnabled[0])
			{
				if(dirty)
				{
					for(int y=0; y<FTransforms.SliceCount; y++)
					{
						FOver[y] = false;
						FClicked[y] = false;
					}
					dirty = false;
				}
				
				
				for(int y=0; y<FTransforms.SliceCount; y++)
				{
					for(int x=0; x<FTouchPosition.SliceCount; x++)
					{
						SlimDX.Vector2 trv = SlimDX.Vector2.TransformCoordinate(FTouchPosition[x], SlimDX.Matrix.Invert(FTransforms[y]));
						if (trv.X > -0.5f && trv.Y > -0.5f && trv.X < 0.5f && trv.Y < 0.5f) 
						{
							dirty = true;
							FOver[y] = true;
							if(FTouchNew[x])
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
