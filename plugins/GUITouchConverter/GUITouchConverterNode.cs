#region usings
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;
using VVVV.Utils.Win32;

using VVVV.Core.Logging;
#endregion usings

namespace VVVV.Nodes
{
	
	public class TouchPoint
	{
		public int status;
		public int startTime;		
		public Vector2D firstPosition;
		public Vector2D currentPosition;
	}
	
	#region PluginInfo
	[PluginInfo(Name = "TouchConverter", Category = "GUI", Help = "Basic template with one value in/out", Tags = "")]
	#endregion PluginInfo
	public class GUITouchConverterNode : IPluginEvaluate
	{
		#region fields & pins
		[Input("Touch Id", DefaultValue = 0)]
		public ISpread<int> FTouchId;
		
		[Input("Touch Position")]
		public ISpread<Vector2D> FTouchPosition;
		
		[Input("Movement Threshold", DefaultValue = 0.4, IsSingle=true)]
		public ISpread<Vector2D> FMovementThreshold;
		
		[Input("Time Threshold", DefaultValue = 100, IsSingle=true)]
		public ISpread<int> FTimeThreshold;
		
		[Input("Enable Movement Threshold", IsSingle=true)]
		public ISpread<bool> FMoveEnabled;
		
		[Input("Enable Time Threshold", IsSingle=true)]
		public ISpread<bool> FTimeEnabled;

		[Output("Touch Id")]
		public ISpread<int> FTouchIdOut;
		
		[Output("Touch Position")]
		public ISpread<Vector2D> FTouchPositionOut;
		
		[Output("Is New", IsBang=true)]
		public ISpread<bool> FTouchNewOut;

		[Import()]
		public ILogger FLogger;
		#endregion fields & pins
		
		private Dictionary<int, TouchPoint> candidateList = new Dictionary<int, TouchPoint>();
		private List<int> removeList = new List<int>();
		private bool running = false;
		private Stopwatch ftimer = new Stopwatch();

		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			if(FTimeEnabled[0])
			{
				if(!running)
				{
					ftimer.Reset();
					ftimer.Start();
					running = true;
					candidateList.Clear();
				}
			}else{
				if(running)
				{
					running = false;
					ftimer.Stop();
				}
			}
			
			if(FMoveEnabled[0])
			{
				for(int x=0; x<FTouchId.SliceCount; x++)
				{
					if(candidateList.ContainsKey(FTouchId[x]))
					{
						candidateList[FTouchId[x]].currentPosition = FTouchPosition[x];
						if(candidateList[FTouchId[x]].status == 1)
						{
							Vector2D distance = candidateList[FTouchId[x]].firstPosition - candidateList[FTouchId[x]].currentPosition;
							if( Math.Abs(distance.x) > FMovementThreshold[0].x ||
								Math.Abs(distance.y) > FMovementThreshold[0].y )
							{
								candidateList[FTouchId[x]].status = 0;
							}
							
							if(running)
							{
								if((int)ftimer.ElapsedMilliseconds - candidateList[FTouchId[x]].startTime > FTimeThreshold[0])
								{
									candidateList[FTouchId[x]].status = 0;
								}
							}
						}					
						
					}else{
						TouchPoint tp = new TouchPoint();
						tp.firstPosition = FTouchPosition[x];
						tp.currentPosition = FTouchPosition[x];
						tp.status = 1;
						if(running)
						{
							tp.startTime = (int)ftimer.ElapsedMilliseconds;
						}
						
						candidateList.Add(FTouchId[x], tp);
					}
				}
				
				//OUTPUT
				FTouchIdOut.SliceCount = candidateList.Count;
				FTouchPositionOut.SliceCount = candidateList.Count;
				FTouchNewOut.SliceCount = candidateList.Count;
				
				int m = 0;
				foreach(KeyValuePair<int, TouchPoint> entry in candidateList)
				{
					bool onList = false;
					for(int x=0; x<FTouchId.SliceCount; x++)
					{
						if(FTouchId[x] == entry.Key)
							onList = true;
					}
					
					if(entry.Value.status == 1 && !onList)
						entry.Value.status = 2;
					
					if(entry.Value.status == 0 && !onList)
						removeList.Add(entry.Key);
					
					if(entry.Value.status == 3)
						removeList.Add(entry.Key);
					
					FTouchIdOut[m] = entry.Key;
					FTouchPositionOut[m] = entry.Value.currentPosition;
					if(entry.Value.status == 2)
					{
						FTouchNewOut[m] = true;
						entry.Value.status = 3;
					}else{
						FTouchNewOut[m] = false;
					}
					m++;
				}
				
				if(removeList.Count > 0)
				{
					for(int x=0; x<removeList.Count; x++)
					{
						candidateList.Remove(removeList[x]);
					}
					removeList.Clear();
				}
			}
		}
	}
}
